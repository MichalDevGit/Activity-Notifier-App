using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;

namespace ActivityNotifier.Core.Rules;

/// <summary>
/// Long break (10–15 min): due after <see cref="RulesOptions.LongBreakMinutes"/> of work since the
/// session started or since the last LongBreak reminder.
/// </summary>
/// <remarks>
/// A MicroBreak reminder does not restart this count: a short stretch does not replace a real break.
/// </remarks>
public sealed class LongBreakRule : IActivityRule
{
    private readonly TimeSpan _interval;

    /// <summary>Creates the rule.</summary>
    /// <param name="options">Rule thresholds.</param>
    public LongBreakRule(RulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _interval = TimeSpan.FromMinutes(options.LongBreakMinutes);
    }

    /// <inheritdoc />
    public RuleType Type => RuleType.LongBreak;

    /// <inheritdoc />
    public int Priority => 1;

    /// <inheritdoc />
    public bool IsDue(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.WorkSinceLastReminder(RuleType.LongBreak) >= _interval;
    }
}
