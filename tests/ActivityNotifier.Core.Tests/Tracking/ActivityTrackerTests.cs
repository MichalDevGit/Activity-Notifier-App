using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Tracking;

namespace ActivityNotifier.Core.Tests.Tracking;

public sealed class ActivityTrackerTests
{
    // Minute 0 = 09:00 UTC. Idle threshold = 5 minutes (the default).
    private static readonly DateTimeOffset T0 = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly ActivityTracker _tracker = new(new TrackingOptions { IdleThresholdMinutes = 5 });
    private readonly List<SessionStartedEventArgs> _started = [];
    private readonly List<SessionEndedEventArgs> _ended = [];

    public ActivityTrackerTests()
    {
        _tracker.SessionStarted += (_, e) => _started.Add(e);
        _tracker.SessionEnded += (_, e) => _ended.Add(e);
    }

    [Fact]
    public void RecordMinute_FirstActiveMinute_StartsSessionAndRaisesSessionStarted()
    {
        _tracker.RecordMinute(Active(0));

        var e = Assert.Single(_started);
        Assert.Equal(T0, e.StartUtc);
        Assert.True(_tracker.GetSnapshot().IsInSession);
    }

    [Fact]
    public void RecordMinute_InactiveMinuteWithoutSession_DoesNothing()
    {
        _tracker.RecordMinute(Inactive(0));

        Assert.Empty(_started);
        Assert.Empty(_ended);
        Assert.False(_tracker.GetSnapshot().IsInSession);
    }

    [Fact]
    public void RecordMinute_IdleBelowThreshold_KeepsSession()
    {
        _tracker.RecordMinute(Active(0));   // last activity: 09:01
        RecordInactive(1, 4);               // 09:01–09:05 empty = 4 idle minutes

        Assert.Empty(_ended);
        Assert.True(_tracker.GetSnapshot().IsInSession);
    }

    [Fact]
    public void RecordMinute_IdleReachesThreshold_EndsSessionWithIdleReason()
    {
        _tracker.RecordMinute(Active(0));   // last activity: 09:01
        RecordInactive(1, 5);               // 09:01–09:06 empty = 5 idle minutes

        var e = Assert.Single(_ended);
        Assert.Equal(T0, e.StartUtc);
        Assert.Equal(T0.AddMinutes(1), e.EndUtc); // when it really ended, not when we noticed
        Assert.Equal(SessionEndReason.Idle, e.Reason);
        Assert.False(_tracker.GetSnapshot().IsInSession);
    }

    [Fact]
    public void RecordMinute_MoreInactiveMinutesAfterEnd_DoesNotEndAgain()
    {
        _tracker.RecordMinute(Active(0));
        RecordInactive(1, 20);

        Assert.Single(_ended);
    }

    [Fact]
    public void RecordMinute_ActiveAfterIdleEnd_StartsNewSession()
    {
        _tracker.RecordMinute(Active(0));
        RecordInactive(1, 9);
        _tracker.RecordMinute(Active(10));

        Assert.Equal(2, _started.Count);
        Assert.Equal(T0.AddMinutes(10), _started[1].StartUtc);
        Assert.Equal(T0.AddMinutes(10), _tracker.GetSnapshot().SessionStartUtc);
    }

    [Fact]
    public void RecordMinute_ActiveMinuteAfterGap_EndsOldSessionAndStartsNew()
    {
        var order = new List<string>();
        _tracker.SessionEnded += (_, _) => order.Add("ended");
        _tracker.SessionStarted += (_, _) => order.Add("started");

        _tracker.RecordMinute(Active(0));
        order.Clear();
        _tracker.RecordMinute(Active(30)); // no minutes in between, e.g. the computer was asleep

        var e = Assert.Single(_ended);
        Assert.Equal(T0, e.StartUtc);
        Assert.Equal(T0.AddMinutes(1), e.EndUtc);
        Assert.Equal(T0.AddMinutes(30), _started[1].StartUtc);
        Assert.Equal(["ended", "started"], order);
    }

    [Fact]
    public void RecordMinute_ActiveMinuteAfterShortGap_KeepsSession()
    {
        _tracker.RecordMinute(Active(0));
        _tracker.RecordMinute(Active(3)); // gap of 2 minutes < 5

        Assert.Empty(_ended);
        Assert.Equal(TimeSpan.FromMinutes(4), _tracker.GetSnapshot().ContinuousDuration);
    }

    [Fact]
    public void InterruptSession_ActiveSession_EndsWithGivenReason()
    {
        RecordActive(0, 9); // 09:00–09:10

        _tracker.InterruptSession(SessionEndReason.Sleep);

        var e = Assert.Single(_ended);
        Assert.Equal(T0, e.StartUtc);
        Assert.Equal(T0.AddMinutes(10), e.EndUtc);
        Assert.Equal(SessionEndReason.Sleep, e.Reason);
        Assert.False(_tracker.GetSnapshot().IsInSession);
    }

    [Fact]
    public void InterruptSession_NoSession_DoesNotRaiseEvent()
    {
        _tracker.InterruptSession(SessionEndReason.Shutdown);

        Assert.Empty(_ended);
    }

    [Fact]
    public void GetSnapshot_InSession_ReturnsContinuousDuration()
    {
        RecordActive(0, 49); // 50 active minutes

        var snapshot = _tracker.GetSnapshot();

        Assert.Equal(T0, snapshot.SessionStartUtc);
        Assert.Equal(T0.AddMinutes(50), snapshot.LastActiveUtc);
        Assert.Equal(TimeSpan.FromMinutes(50), snapshot.ContinuousDuration);
    }

    [Fact]
    public void GetSnapshot_NoSession_DurationIsZeroAndLastActiveIsKept()
    {
        _tracker.RecordMinute(Active(0));
        RecordInactive(1, 5); // session ended

        var snapshot = _tracker.GetSnapshot();

        Assert.Equal(TimeSpan.Zero, snapshot.ContinuousDuration);
        Assert.Equal(T0.AddMinutes(1), snapshot.LastActiveUtc);
    }

    [Fact]
    public void GetSnapshot_MinutesOlderThanWindow_AreDropped()
    {
        RecordActive(0, 60); // 61 minutes

        var minutes = _tracker.GetSnapshot().RecentMinutes;

        Assert.Equal(60, minutes.Count);
        Assert.Equal(T0.AddMinutes(1), minutes[0].MinuteStartUtc);
        Assert.Equal(T0.AddMinutes(60), minutes[^1].MinuteStartUtc);
    }

    [Fact]
    public void GetSnapshot_AfterMoreMinutes_EarlierSnapshotUnchanged()
    {
        _tracker.RecordMinute(Active(0));
        var before = _tracker.GetSnapshot();

        RecordActive(1, 5);

        Assert.Single(before.RecentMinutes);
        Assert.Equal(TimeSpan.FromMinutes(1), before.ContinuousDuration);
    }

    [Fact]
    public void SessionEnded_AfterUnsubscribe_HandlerNotCalled()
    {
        int calls = 0;
        void Handler(object? sender, SessionEndedEventArgs e) => calls++;

        _tracker.SessionEnded += Handler;
        _tracker.SessionEnded -= Handler;
        _tracker.RecordMinute(Active(0));
        _tracker.InterruptSession(SessionEndReason.Shutdown);

        Assert.Equal(0, calls);
        Assert.Single(_ended); // the other subscriber still got it
    }

    [Fact]
    public async Task RecordMinute_WithConcurrentGetSnapshot_NoExceptions()
    {
        const int Minutes = 5_000;
        var ct = TestContext.Current.CancellationToken;

        var writer = Task.Run(
            () =>
            {
                for (int i = 0; i < Minutes; i++)
                {
                    _tracker.RecordMinute(i % 10 < 7 ? Active(i) : Inactive(i));
                }
            },
            ct);

        var reader = Task.Run(
            () =>
            {
                while (!writer.IsCompleted)
                {
                    var snapshot = _tracker.GetSnapshot();
                    Assert.True(snapshot.RecentMinutes.Count <= 60);
                }
            },
            ct);

        await Task.WhenAll(writer, reader);

        Assert.Equal(60, _tracker.GetSnapshot().RecentMinutes.Count);
    }

    private static ActivityMinute Active(int minute) => new(T0.AddMinutes(minute), 50, 2, 300);

    private static ActivityMinute Inactive(int minute) => new(T0.AddMinutes(minute), 0, 0, 0);

    private void RecordActive(int fromMinute, int toMinute)
    {
        for (int m = fromMinute; m <= toMinute; m++)
        {
            _tracker.RecordMinute(Active(m));
        }
    }

    private void RecordInactive(int fromMinute, int toMinute)
    {
        for (int m = fromMinute; m <= toMinute; m++)
        {
            _tracker.RecordMinute(Inactive(m));
        }
    }
}
