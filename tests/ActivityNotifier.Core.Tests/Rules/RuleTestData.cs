using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Rules;

namespace ActivityNotifier.Core.Tests.Rules;

/// <summary>Builds hand-made snapshots ("photos") and contexts for rule tests.</summary>
internal static class RuleTestData
{
    /// <summary>Minute 0 = 09:00 UTC.</summary>
    public static readonly DateTimeOffset T0 = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset At(int minute) => T0.AddMinutes(minute);

    /// <summary>Active minutes [fromMinute, toMinute), each with the given keystrokes.</summary>
    public static ActivityMinute[] ActiveMinutes(int fromMinute, int toMinute, int keysPerMinute = 50) =>
        [.. Enumerable.Range(fromMinute, toMinute - fromMinute).Select(m => new ActivityMinute(At(m), keysPerMinute, 2, 300))];

    /// <summary>
    /// A session of continuous work in minutes [fromMinute, toMinute):
    /// starts at <paramref name="fromMinute"/>, last activity at <paramref name="toMinute"/>.
    /// Like the real tracker, only the last 60 minutes are kept.
    /// </summary>
    public static ActivitySnapshot Session(int fromMinute, int toMinute, int keysPerMinute = 50) =>
        Snapshot(ActiveMinutes(fromMinute, toMinute, keysPerMinute));

    /// <summary>A session made of the given (active) minutes: from the first minute's start to the last one's end.</summary>
    public static ActivitySnapshot Snapshot(IReadOnlyList<ActivityMinute> minutes)
    {
        DateTimeOffset start = minutes[0].MinuteStartUtc;
        DateTimeOffset lastActive = minutes[^1].MinuteEndUtc;
        return new ActivitySnapshot(start, lastActive, lastActive - start, [.. minutes.TakeLast(60)]);
    }

    public static ActivitySnapshot NoSession() => new(null, null, TimeSpan.Zero, []);

    public static RuleContext Context(
        ActivitySnapshot snapshot,
        DateTimeOffset nowUtc,
        Dictionary<RuleType, DateTimeOffset>? lastFired = null,
        TimeZoneInfo? zone = null) =>
        new(snapshot, nowUtc, zone ?? TimeZoneInfo.Utc, lastFired ?? []);
}
