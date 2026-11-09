using System.ComponentModel.DataAnnotations;

namespace ActivityNotifier.Core.Options;

/// <summary>
/// Settings for session tracking, bound from the <c>Tracking</c> section of <c>appsettings.json</c> (from M3).
/// </summary>
public sealed class TrackingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Tracking";

    /// <summary>
    /// The definition of a "real break": minutes without any input after which the session ends.
    /// </summary>
    // [Range] is checked at app startup (ValidateDataAnnotations + ValidateOnStart, M3).
    [Range(1, 60)]
    public int IdleThresholdMinutes { get; set; } = 5;

    /// <summary><see cref="IdleThresholdMinutes"/> as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan IdleThreshold => TimeSpan.FromMinutes(IdleThresholdMinutes);

    /// <summary>
    /// "Active now" means the last input was at most this many minutes ago.
    /// Reminders are sent only to someone who is active now.
    /// </summary>
    [Range(1, 10)]
    public int ActiveNowMinutes { get; set; } = 2;

    /// <summary><see cref="ActiveNowMinutes"/> as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan ActiveNow => TimeSpan.FromMinutes(ActiveNowMinutes);
}
