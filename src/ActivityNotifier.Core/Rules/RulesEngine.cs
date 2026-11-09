using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;

namespace ActivityNotifier.Core.Rules;

/// <summary>
/// Decides which reminder (if any) to send now. Asks every rule "are you due?" and applies the shared policies:
/// only when the user is active now, a minimum gap between notifications, snooze, and the most important rule wins.
/// At most one notification per <see cref="Evaluate"/>.
/// </summary>
/// <remarks>
/// Thread-safe: <see cref="Evaluate"/> runs on the rules thread and <see cref="Snooze"/> on the notification thread.
/// State is guarded by one <see cref="Lock"/>; the event is raised after the lock is released.
/// </remarks>
public sealed class RulesEngine
{
    private static readonly RuleType[] BreakRules = [RuleType.MicroBreak, RuleType.LongBreak];

    private readonly IActivityRule[] _rulesByPriority;
    private readonly TimeProvider _time;
    private readonly TimeSpan _activeNow;
    private readonly TimeSpan _minGap;
    private readonly TimeSpan _defaultSnooze;

    private readonly Lock _lock = new();
    private readonly Dictionary<RuleType, DateTimeOffset> _lastFiredUtc = [];
    private readonly Dictionary<RuleType, SnoozeEntry> _snoozes = [];
    private DateTimeOffset? _lastNotificationUtc;

    /// <summary>Creates the engine.</summary>
    /// <param name="rules">The rules to evaluate.</param>
    /// <param name="rulesOptions">Engine policies (minimum gap, snooze length).</param>
    /// <param name="trackingOptions">"Active now" definition.</param>
    /// <param name="timeProvider">The clock (fake in tests).</param>
    public RulesEngine(
        IEnumerable<IActivityRule> rules,
        RulesOptions rulesOptions,
        TrackingOptions trackingOptions,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(rulesOptions);
        ArgumentNullException.ThrowIfNull(trackingOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // Sort once, so "the first due rule" is always the most important one.
        _rulesByPriority = [.. rules.OrderBy(r => r.Priority)];
        _time = timeProvider;
        _activeNow = trackingOptions.ActiveNow;
        _minGap = TimeSpan.FromMinutes(rulesOptions.MinGapMinutes);
        _defaultSnooze = TimeSpan.FromMinutes(rulesOptions.SnoozeMinutes);
    }

    /// <summary>Raised when a reminder should be shown.</summary>
    public event EventHandler<NotificationRequestedEventArgs>? NotificationRequested;

    /// <summary>
    /// Checks the rules against the given snapshot and raises <see cref="NotificationRequested"/>
    /// for at most one reminder.
    /// </summary>
    /// <param name="snapshot">The tracker's current state.</param>
    public void Evaluate(ActivitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        DateTimeOffset nowUtc = _time.GetUtcNow();
        NotificationRequest? request;

        lock (_lock)
        {
            request = Decide(snapshot, nowUtc);
        }

        // Raised outside the lock (same reason as in ActivityTracker).
        if (request is not null)
        {
            NotificationRequested?.Invoke(this, new NotificationRequestedEventArgs(request));
        }
    }

    /// <summary>
    /// "Remind me again later": the rule stays silent until the snooze ends, then the same reminder comes back.
    /// A snoozed break reminder is dropped if the user takes a real break (a new session starts) in the meantime.
    /// </summary>
    /// <param name="rule">The reminder to snooze.</param>
    /// <param name="duration">How long; the configured default if <c>null</c>.</param>
    public void Snooze(RuleType rule, TimeSpan? duration = null)
    {
        TimeSpan length = duration ?? _defaultSnooze;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(length, TimeSpan.Zero);

        DateTimeOffset nowUtc = _time.GetUtcNow();
        lock (_lock)
        {
            _snoozes[rule] = new SnoozeEntry(SnoozedAtUtc: nowUtc, UntilUtc: nowUtc + length);
        }
    }

    // Must be called while holding _lock. Applies the policies in order and returns the winner, if any.
    private NotificationRequest? Decide(ActivitySnapshot snapshot, DateTimeOffset nowUtc)
    {
        // Policy 1: only remind someone who is at the computer.
        if (snapshot.LastActiveUtc is not { } lastActiveUtc || nowUtc - lastActiveUtc > _activeNow)
        {
            return null;
        }

        // Policy 2: don't nag. Keep a minimum gap between any two notifications.
        if (_lastNotificationUtc is { } lastSentUtc && nowUtc - lastSentUtc < _minGap)
        {
            return null;
        }

        DropBreakSnoozesAfterRealBreak(snapshot);

        // A copy, so a rule can never change the engine's own bookkeeping.
        var context = new RuleContext(snapshot, nowUtc, _time.LocalTimeZone, new Dictionary<RuleType, DateTimeOffset>(_lastFiredUtc));

        // Policies 3 + 4: rules are sorted by priority, so the first ready rule is the most important one.
        foreach (IActivityRule rule in _rulesByPriority)
        {
            if (IsReady(rule, context))
            {
                _lastFiredUtc[rule.Type] = nowUtc;
                _lastNotificationUtc = nowUtc;
                _snoozes.Remove(rule.Type);

                int keystrokesLastHour = snapshot.RecentMinutes.Sum(m => m.Keystrokes);
                return new NotificationRequest(rule.Type, nowUtc, snapshot.ContinuousDuration, keystrokesLastHour);
            }
        }

        return null;
    }

    // Must be called while holding _lock.
    private bool IsReady(IActivityRule rule, RuleContext context)
    {
        if (_snoozes.TryGetValue(rule.Type, out SnoozeEntry? snooze))
        {
            // Snoozed: silent until it ends, then it comes back even if the rule itself is not due.
            return context.NowUtc >= snooze.UntilUtc;
        }

        return rule.IsDue(context);
    }

    // Must be called while holding _lock.
    // A session that started after the snooze means the user took a real break, so a snoozed break reminder is no longer needed.
    private void DropBreakSnoozesAfterRealBreak(ActivitySnapshot snapshot)
    {
        foreach (RuleType rule in BreakRules)
        {
            if (_snoozes.TryGetValue(rule, out SnoozeEntry? snooze)
                && (snapshot.SessionStartUtc is not { } sessionStartUtc || sessionStartUtc > snooze.SnoozedAtUtc))
            {
                _snoozes.Remove(rule);
            }
        }
    }

    private sealed record SnoozeEntry(DateTimeOffset SnoozedAtUtc, DateTimeOffset UntilUtc);
}
