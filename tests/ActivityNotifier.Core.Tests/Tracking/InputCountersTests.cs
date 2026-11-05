using ActivityNotifier.Core.Tracking;

namespace ActivityNotifier.Core.Tests.Tracking;

public sealed class InputCountersTests
{
    private static readonly DateTimeOffset MinuteStart = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Snapshot_AfterIncrements_ReturnsCounts()
    {
        var counters = new InputCounters();
        counters.AddKeystroke();
        counters.AddKeystroke();
        counters.AddKeystroke();
        counters.AddClick();
        counters.AddMouseDistance(150);
        counters.AddMouseDistance(50);

        var minute = counters.Snapshot(MinuteStart);

        Assert.Equal(MinuteStart, minute.MinuteStartUtc);
        Assert.Equal(3, minute.Keystrokes);
        Assert.Equal(1, minute.Clicks);
        Assert.Equal(200, minute.MouseDistancePx);
    }

    [Fact]
    public void Snapshot_CalledTwice_SecondIsZero()
    {
        var counters = new InputCounters();
        counters.AddKeystroke();
        counters.AddClick();
        counters.AddMouseDistance(10);

        counters.Snapshot(MinuteStart);
        var second = counters.Snapshot(MinuteStart.AddMinutes(1));

        Assert.False(second.IsActive);
    }

    [Fact]
    public void AddMouseDistance_Negative_Throws()
    {
        var counters = new InputCounters();

        Assert.Throws<ArgumentOutOfRangeException>(() => counters.AddMouseDistance(-1));
    }

    [Fact]
    public async Task Snapshot_ConcurrentIncrements_NoCountsLost()
    {
        const int TypingThreads = 4;
        const int KeysPerThread = 100_000;
        var counters = new InputCounters();
        var ct = TestContext.Current.CancellationToken;
        using var stopSnapshots = new CancellationTokenSource();

        // One thread keeps taking snapshots (like the minute timer) while others type.
        long totalCounted = 0;
        var snapshotTask = Task.Run(
            () =>
            {
                while (!stopSnapshots.IsCancellationRequested)
                {
                    totalCounted += counters.Snapshot(MinuteStart).Keystrokes;
                }
            },
            ct);

        var typingTasks = Enumerable.Range(0, TypingThreads)
            .Select(_ => Task.Run(
                () =>
                {
                    for (int i = 0; i < KeysPerThread; i++)
                    {
                        counters.AddKeystroke();
                    }
                },
                ct))
            .ToArray();

        await Task.WhenAll(typingTasks);
        await stopSnapshots.CancelAsync();
        await snapshotTask;
        totalCounted += counters.Snapshot(MinuteStart).Keystrokes; // whatever is left after the last snapshot

        Assert.Equal(TypingThreads * KeysPerThread, totalCounted);
    }
}
