using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;

namespace ActivityNotifier.Core.Rules;

/// <summary>
/// Finger exercises: due after at least <see cref="RulesOptions.FingersKeystrokes"/> keystrokes in the last
/// <see cref="RulesOptions.FingersWindowMinutes"/>, at most once per <see cref="RulesOptions.FingersCooldownMinutes"/>.
/// </summary>
public sealed class FingersRule : IActivityRule
{
    private readonly int _keystrokeThreshold;
    private readonly TimeSpan _window;
    private readonly TimeSpan _cooldown;

    /// <summary>Creates the rule.</summary>
    /// <param name="options">Rule thresholds.</param>
    public FingersRule(RulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _keystrokeThreshold = options.FingersKeystrokes;
        _window = TimeSpan.FromMinutes(options.FingersWindowMinutes);
        _cooldown = TimeSpan.FromMinutes(options.FingersCooldownMinutes);
    }

    /// <inheritdoc />
    public RuleType Type => RuleType.Fingers;

    /// <inheritdoc />
    public int Priority => 4;

    /// <inheritdoc />
    public bool IsDue(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.LastFiredUtc(RuleType.Fingers) is { } lastFiredUtc && context.NowUtc - lastFiredUtc < _cooldown)
        {
            return false; // still in cooldown
        }

        DateTimeOffset windowStartUtc = context.NowUtc - _window;
        int keystrokesInWindow = context.Snapshot.RecentMinutes
            .Where(m => m.MinuteStartUtc >= windowStartUtc)
            .Sum(m => m.Keystrokes);

        return keystrokesInWindow >= _keystrokeThreshold;
    }
}
