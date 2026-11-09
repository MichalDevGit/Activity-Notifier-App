using ActivityNotifier.Core.Models;

namespace ActivityNotifier.Core.Rules;

/// <summary>
/// One reminder rule: an "advisor" that answers only "am I due right now?".
/// Shared policies (priority, minimum gap, snooze, active now) are applied by the <see cref="RulesEngine"/>.
/// </summary>
/// <remarks>
/// Rules are called only by the engine while it holds its lock, so implementations need no locking.
/// </remarks>
public interface IActivityRule
{
    /// <summary>Which reminder this rule produces.</summary>
    RuleType Type { get; }

    /// <summary>Importance when several rules are due. Lower number = more important.</summary>
    int Priority { get; }

    /// <summary>Returns true if this reminder should be sent now (before engine policies).</summary>
    /// <param name="context">The snapshot, the time, and when each rule last fired.</param>
    /// <returns>True if due.</returns>
    bool IsDue(RuleContext context);
}
