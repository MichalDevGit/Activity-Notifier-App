using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Tracking;

namespace ActivityNotifier.Core.Tests.Concurrency;

/// <summary>
/// One thread records minutes (the minute timer) while others read snapshots (the rules timer).
/// Without the tracker's lock, a reader could see a half-updated state, or the history queue
/// could change while it is being copied (InvalidOperationException).
/// </summary>
public sealed class ActivityTrackerConcurrencyTests
{
    private const int Minutes = 2_000;
    private const int Readers = 4;

    private static readonly DateTimeOffset T0 = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetSnapshot_WhileAnotherThreadRecordsMinutes_AlwaysReturnsConsistentSnapshot()
    {
        var tracker = new ActivityTracker(new TrackingOptions());

        // Single writer: minutes must arrive in time order, exactly like the real minute timer.
        // Every 10th minute is inactive, so sessions keep going (5 idle minutes are needed to end one).
        Task writer = Task.Run(() =>
        {
            for (int m = 0; m < Minutes; m++)
            {
                int keys = m % 10 == 0 ? 0 : 50;
                tracker.RecordMinute(new ActivityMinute(T0.AddMinutes(m), keys, 0, 0));
            }
        }, TestContext.Current.CancellationToken);

        // Several readers check the snapshot's own rules while the writer is busy.
        Task[] readers = [.. Enumerable.Range(0, Readers).Select(_ => Task.Run(() =>
        {
            while (!writer.IsCompleted)
            {
                ActivitySnapshot snapshot = tracker.GetSnapshot();

                Assert.True(snapshot.RecentMinutes.Count <= 60);
                if (snapshot.SessionStartUtc is { } start && snapshot.LastActiveUtc is { } lastActive)
                {
                    // Start and last activity must come from the same moment, not from two different updates.
                    Assert.Equal(lastActive - start, snapshot.ContinuousDuration);
                }

                for (int i = 1; i < snapshot.RecentMinutes.Count; i++)
                {
                    Assert.True(snapshot.RecentMinutes[i - 1].MinuteStartUtc < snapshot.RecentMinutes[i].MinuteStartUtc);
                }
            }
        }, TestContext.Current.CancellationToken))];

        // A failed Assert inside a task makes that task fail; awaiting rethrows the exception here.
        await writer;
        await Task.WhenAll(readers);

        Assert.Equal(60, tracker.GetSnapshot().RecentMinutes.Count);
    }
}
