namespace ActivityNotifier.Core.Models;

/// <summary>Data sent with the <c>ActivityTracker.SessionEnded</c> event.</summary>
public sealed class SessionEndedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="startUtc">When the session started (UTC).</param>
    /// <param name="endUtc">When the session really ended: the end of the last active minute (UTC).</param>
    /// <param name="reason">Why the session ended.</param>
    public SessionEndedEventArgs(DateTimeOffset startUtc, DateTimeOffset endUtc, SessionEndReason reason)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
        Reason = reason;
    }

    /// <summary>When the session started (UTC).</summary>
    public DateTimeOffset StartUtc { get; }

    /// <summary>When the session really ended: the end of the last active minute (UTC).</summary>
    public DateTimeOffset EndUtc { get; }

    /// <summary>Why the session ended.</summary>
    public SessionEndReason Reason { get; }
}
