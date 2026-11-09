using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;

namespace ActivityNotifier.Core.Rules;

/// <summary>
/// Micro-break (1–3 min): due after <see cref="RulesOptions.MicroBreakMinutes"/> of work since the
/// session started or since the last break reminder of either kind.
/// </summary>
/// <remarks>
/// A LongBreak reminder also restarts this count: a long break includes everything a micro-break does.
/// </remarks>
public sealed class MicroBreakRule : IActivityRule
{
    private readonly TimeSpan _interval;

    /// <summary>Creates the rule.</summary>
    /// <param name="options">Rule thresholds.</param>
    public MicroBreakRule(RulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _interval = TimeSpan.FromMinutes(options.MicroBreakMinutes);
    }

    /// <inheritdoc />
    public RuleType Type => RuleType.MicroBreak;

    /// <inheritdoc />
    public int Priority => 2;

    /// <inheritdoc />
    public bool IsDue(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.WorkSinceLastReminder(RuleType.MicroBreak, RuleType.LongBreak) >= _interval;
    }
}
