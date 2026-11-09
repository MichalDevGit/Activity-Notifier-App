namespace ActivityNotifier.Core.Models;

/// <summary>
/// The engine's decision: "send this reminder now", with aggregated context numbers
/// that a suggester (static texts, later AI) can use to personalize the message.
/// </summary>
/// <param name="Rule">Which reminder to send.</param>
/// <param name="CreatedUtc">When the decision was made (UTC).</param>
/// <param name="ContinuousWork">How long the current session has lasted.</param>
/// <param name="KeystrokesLastHour">Total keystrokes in the last hour (a count only).</param>
public sealed record NotificationRequest(
    RuleType Rule,
    DateTimeOffset CreatedUtc,
    TimeSpan ContinuousWork,
    int KeystrokesLastHour);
