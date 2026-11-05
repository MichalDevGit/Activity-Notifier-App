namespace ActivityNotifier.Core.Models;

/// <summary>Data sent with the <c>ActivityTracker.SessionStarted</c> event.</summary>
public sealed class SessionStartedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="startUtc">When the session started (UTC).</param>
    public SessionStartedEventArgs(DateTimeOffset startUtc)
    {
        StartUtc = startUtc;
    }

    /// <summary>When the session started (UTC).</summary>
    public DateTimeOffset StartUtc { get; }
}
