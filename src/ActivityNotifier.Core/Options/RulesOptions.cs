using System.ComponentModel.DataAnnotations;

namespace ActivityNotifier.Core.Options;

/// <summary>
/// Thresholds for the reminder rules and the engine policies,
/// bound from the <c>Rules</c> section of <c>appsettings.json</c> (from M3).
/// </summary>
public sealed class RulesOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Rules";

    /// <summary>Hydration: active minutes needed in the current local clock hour.</summary>
    [Range(1, 60)]
    public int HydrationMinActiveMinutes { get; set; } = 2;

    /// <summary>MicroBreak: minutes of work since the session start or the last break reminder.</summary>
    [Range(5, 240)]
    public int MicroBreakMinutes { get; set; } = 50;

    /// <summary>LongBreak: minutes of work since the session start or the last long-break reminder.</summary>
    [Range(10, 480)]
    public int LongBreakMinutes { get; set; } = 90;

    /// <summary>Fingers: keystrokes within the window that trigger the reminder.</summary>
    [Range(100, 100_000)]
    public int FingersKeystrokes { get; set; } = 4000;

    /// <summary>Fingers: the window to count keystrokes in.</summary>
    // Max 60: the tracker keeps only the last 60 minutes.
    [Range(5, 60)]
    public int FingersWindowMinutes { get; set; } = 30;

    /// <summary>Fingers: minimum minutes between two Fingers reminders.</summary>
    [Range(5, 240)]
    public int FingersCooldownMinutes { get; set; } = 45;

    /// <summary>Engine: minimum minutes between any two notifications.</summary>
    [Range(0, 60)]
    public int MinGapMinutes { get; set; } = 3;

    /// <summary>Engine: default snooze length in minutes.</summary>
    [Range(1, 120)]
    public int SnoozeMinutes { get; set; } = 10;
}
