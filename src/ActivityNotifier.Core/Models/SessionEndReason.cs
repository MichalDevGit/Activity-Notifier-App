namespace ActivityNotifier.Core.Models;

/// <summary>Why a work session ended.</summary>
/// <remarks>
/// Locking the screen is intentionally not a reason: a short lock keeps the session,
/// and a long lock ends it through the normal idle rule (<see cref="Idle"/>).
/// </remarks>
public enum SessionEndReason
{
    /// <summary>No input for at least the idle threshold (a real break).</summary>
    Idle,

    /// <summary>The computer went to sleep.</summary>
    Sleep,

    /// <summary>The app or Windows was shutting down.</summary>
    Shutdown,
}
