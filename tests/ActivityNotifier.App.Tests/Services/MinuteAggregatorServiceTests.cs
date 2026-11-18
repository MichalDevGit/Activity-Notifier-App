using System.Threading.Channels;
using ActivityNotifier.App.Services;
using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Tracking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ActivityNotifier.App.Tests.Services;

/// <summary>
/// Runs the real service loop on a fake clock. The loop runs on a thread-pool thread, so after moving the clock
/// the test waits for a tracker event (a signal) before checking anything. No sleeping, no guessing.
/// </summary>
public sealed class MinuteAggregatorServiceTests : IDisposable
{
    // Only a safety net so a broken test fails instead of hanging. Normally the signal arrives in microseconds.
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(10);

    private static readonly DateTimeOffset NineOClock = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(NineOClock.AddSeconds(30)); // the app starts at 09:00:30
    private readonly InputCounters _counters = new();
    private readonly ActivityTracker _tracker;
    private readonly MinuteAggregatorService _service;

    // Every tracker event lands here, so the test can wait for "the next thing that happened".
    private readonly Channel<EventArgs> _trackerEvents = Channel.CreateUnbounded<EventArgs>();

    public MinuteAggregatorServiceTests()
    {
        // Idle threshold 1 minute: a single quiet minute ends the session, so every minute gives an event to wait for.
        _tracker = new ActivityTracker(new TrackingOptions { IdleThresholdMinutes = 1 });
        _tracker.SessionStarted += (_, e) => _trackerEvents.Writer.TryWrite(e);
        _tracker.SessionEnded += (_, e) => _trackerEvents.Writer.TryWrite(e);

        _service = new MinuteAggregatorService(_counters, _tracker, _time, NullLogger<MinuteAggregatorService>.Instance);
    }

    public void Dispose() => _service.Dispose();

    [Fact]
    public async Task ExecuteAsync_ClockReachesNextWholeMinute_RecordsTheStartedMinute()
    {
        await _service.StartAsync(TestContext.Current.CancellationToken);
        _counters.AddKeystroke();
        _counters.AddKeystroke();
        _counters.AddKeystroke();

        _time.Advance(TimeSpan.FromSeconds(30)); // 09:01:00, the first whole minute after the start

        var started = Assert.IsType<SessionStartedEventArgs>(await NextTrackerEventAsync());
        Assert.Equal(NineOClock, started.StartUtc); // the bucket is the whole minute 09:00, not 09:00:30
        var minute = Assert.Single(_tracker.GetSnapshot().RecentMinutes);
        Assert.Equal(3, minute.Keystrokes);
    }

    [Fact]
    public async Task ExecuteAsync_SeveralMinutes_RecordsOneBucketPerWholeMinute()
    {
        await _service.StartAsync(TestContext.Current.CancellationToken);

        // 09:00 active → session starts at 09:00
        _counters.AddKeystroke();
        _time.Advance(TimeSpan.FromSeconds(30)); // 09:01:00
        var first = Assert.IsType<SessionStartedEventArgs>(await NextTrackerEventAsync());
        Assert.Equal(NineOClock, first.StartUtc);

        // 09:01 quiet → with a 1-minute idle threshold the session ends at its last activity (09:01)
        _time.Advance(TimeSpan.FromMinutes(1)); // 09:02:00
        var ended = Assert.IsType<SessionEndedEventArgs>(await NextTrackerEventAsync());
        Assert.Equal(NineOClock.AddMinutes(1), ended.EndUtc);

        // 09:02 active → a new session starts at 09:02
        _counters.AddClick();
        _time.Advance(TimeSpan.FromMinutes(1)); // 09:03:00
        var second = Assert.IsType<SessionStartedEventArgs>(await NextTrackerEventAsync());
        Assert.Equal(NineOClock.AddMinutes(2), second.StartUtc);

        Assert.Equal(3, _tracker.GetSnapshot().RecentMinutes.Count); // 09:00, 09:01, 09:02: none skipped or doubled
    }

    [Fact]
    public async Task StopAsync_InTheMiddleOfAMinute_RecordsTheUnfinishedMinute()
    {
        await _service.StartAsync(TestContext.Current.CancellationToken);
        _counters.AddKeystroke();
        _time.Advance(TimeSpan.FromSeconds(30)); // 09:01:00: minute 09:00 recorded
        await NextTrackerEventAsync();

        _counters.AddKeystroke();
        _counters.AddKeystroke();
        _time.Advance(TimeSpan.FromSeconds(20)); // 09:01:20: the app exits mid-minute

        // StopAsync cancels the loop and waits until ExecuteAsync has finished, including the flush.
        await _service.StopAsync(TestContext.Current.CancellationToken);

        var last = _tracker.GetSnapshot().RecentMinutes[^1];
        Assert.Equal(NineOClock.AddMinutes(1), last.MinuteStartUtc);
        Assert.Equal(2, last.Keystrokes);
    }

    private async Task<EventArgs> NextTrackerEventAsync() =>
        await _trackerEvents.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
}
