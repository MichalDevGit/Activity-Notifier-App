namespace ActivityNotifier.Core.Models;

/// <summary>
/// One minute of activity: how much input happened, never <em>what</em> the input was.
/// This is the basic unit the tracker works with and (later) what is saved to the database.
/// </summary>
/// <param name="MinuteStartUtc">Start of the minute, in UTC.</param>
/// <param name="Keystrokes">Number of key presses in this minute.</param>
/// <param name="Clicks">Number of mouse button presses in this minute.</param>
/// <param name="MouseDistancePx">Total mouse movement in this minute, in pixels.</param>
public sealed record ActivityMinute(
    DateTimeOffset MinuteStartUtc,
    int Keystrokes,
    int Clicks,
    int MouseDistancePx)
{
    /// <summary>The length of one bucket (one minute).</summary>
    public static TimeSpan Length { get; } = TimeSpan.FromMinutes(1);

    /// <summary>End of the minute, in UTC (start + 1 minute).</summary>
    public DateTimeOffset MinuteEndUtc => MinuteStartUtc + Length;

    /// <summary>True if there was any input at all in this minute.</summary>
    public bool IsActive => Keystrokes > 0 || Clicks > 0 || MouseDistancePx > 0;
}
