namespace ActivityNotifier.Core.Models;

/// <summary>
/// An immutable "photo" of the tracker's state at one moment.
/// The rules read this photo instead of the live tracker, so the numbers cannot change while they are being checked.
/// </summary>
/// <param name="SessionStartUtc">Start of the current session, or <c>null</c> if there is no session.</param>
/// <param name="LastActiveUtc">End of the last minute that had input, or <c>null</c> if there was none yet. Kept after a session ends.</param>
/// <param name="ContinuousDuration">How long the current session has lasted (start → last activity). Zero if there is no session.</param>
/// <param name="RecentMinutes">The recorded minutes of the last hour, oldest first (inactive minutes included).</param>
public sealed record ActivitySnapshot(
    DateTimeOffset? SessionStartUtc,
    DateTimeOffset? LastActiveUtc,
    TimeSpan ContinuousDuration,
    IReadOnlyList<ActivityMinute> RecentMinutes)
{
    /// <summary>True while a work session is running.</summary>
    public bool IsInSession => SessionStartUtc is not null;
}
