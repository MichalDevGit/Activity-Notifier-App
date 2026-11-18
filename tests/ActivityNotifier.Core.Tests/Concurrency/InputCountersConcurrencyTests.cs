using ActivityNotifier.Core.Tracking;

namespace ActivityNotifier.Core.Tests.Concurrency;

/// <summary>
/// Many threads use the counters at the same time, like the hook thread (incrementing) and the
/// minute timer (reading and resetting) in the real app.
/// </summary>
public sealed class InputCountersConcurrencyTests
{
    private const int Threads = 4;
    private const int IncrementsPerThread = 100_000;

    private static readonly DateTimeOffset MinuteStart = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Snapshot_WhileManyThreadsIncrement_LosesAndDuplicatesNothing()
    {
        var counters = new InputCounters();

        // Writers: several thread-pool threads increment at the same time.
        Task writers = Task.WhenAll(Enumerable.Range(0, Threads).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < IncrementsPerThread; i++)
            {
                counters.AddKeystroke();
                counters.AddClick();
                counters.AddMouseDistance(1);
            }
        }, TestContext.Current.CancellationToken)));

        // Reader: meanwhile, this thread keeps taking snapshots (read + reset), like the minute timer.
        long keystrokes = 0, clicks = 0, distance = 0;
        while (!writers.IsCompleted)
        {
            var minute = counters.Snapshot(MinuteStart);
            keystrokes += minute.Keystrokes;
            clicks += minute.Clicks;
            distance += minute.MouseDistancePx;
        }

        await writers; // rethrows if a writer failed

        var last = counters.Snapshot(MinuteStart); // whatever arrived after the last loop snapshot
        keystrokes += last.Keystrokes;
        clicks += last.Clicks;
        distance += last.MouseDistancePx;

        // Every increment ends up in exactly one snapshot: none lost, none counted twice.
        const long expected = (long)Threads * IncrementsPerThread;
        Assert.Equal(expected, keystrokes);
        Assert.Equal(expected, clicks);
        Assert.Equal(expected, distance);
    }
}
