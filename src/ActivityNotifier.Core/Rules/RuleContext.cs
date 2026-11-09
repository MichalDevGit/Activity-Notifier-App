using ActivityNotifier.Core.Models;

namespace ActivityNotifier.Core.Rules;

/// <summary>
/// Everything a rule needs to answer "am I due?" (the advisor's briefing file).
/// </summary>
/// <param name="Snapshot">The tracker's state at this moment.</param>
/// <param name="NowUtc">The current time (UTC).</param>
/// <param name="LocalTimeZone">The user's time zone, for "clock hour" logic.</param>
/// <param name="LastFired">When each rule last sent a reminder (UTC). Rules that never fired are missing.</param>
public sealed record RuleContext(
    ActivitySnapshot Snapshot,
    DateTimeOffset NowUtc,
    TimeZoneInfo LocalTimeZone,
    IReadOnlyDictionary<RuleType, DateTimeOffset> LastFired)
{
    /// <summary>When the given rule last sent a reminder, or <c>null</c> if never.</summary>
    /// <param name="rule">The rule to look up.</param>
    /// <returns>The time, or <c>null</c>.</returns>
    public DateTimeOffset? LastFiredUtc(RuleType rule) =>
        LastFired.TryGetValue(rule, out DateTimeOffset firedUtc) ? firedUtc : null;

    /// <summary>
    /// Work time in the current session, counted from the session start or from the latest
    /// reminder of any of the given rules, whichever is later. Zero if there is no session.
    /// </summary>
    /// <param name="rules">The reminders that restart the count.</param>
    /// <returns>The work time (up to the last activity).</returns>
    public TimeSpan WorkSinceLastReminder(params RuleType[] rules)
    {
        if (Snapshot.SessionStartUtc is not { } countFrom || Snapshot.LastActiveUtc is not { } lastActive)
        {
            return TimeSpan.Zero;
        }

        // A new session starts after a real break, so reminders from an earlier session
        // are older than the session start and are ignored automatically.
        foreach (RuleType rule in rules)
        {
            if (LastFiredUtc(rule) is { } firedUtc && firedUtc > countFrom)
            {
                countFrom = firedUtc;
            }
        }

        return lastActive - countFrom;
    }
}
