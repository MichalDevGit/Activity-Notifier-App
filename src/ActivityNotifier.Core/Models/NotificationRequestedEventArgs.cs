namespace ActivityNotifier.Core.Models;

/// <summary>Data sent with the <c>RulesEngine.NotificationRequested</c> event.</summary>
public sealed class NotificationRequestedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="request">The reminder to send.</param>
    public NotificationRequestedEventArgs(NotificationRequest request)
    {
        Request = request;
    }

    /// <summary>The reminder to send.</summary>
    public NotificationRequest Request { get; }
}
