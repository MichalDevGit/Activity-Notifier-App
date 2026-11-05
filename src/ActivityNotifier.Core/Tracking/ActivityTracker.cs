using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;

namespace ActivityNotifier.Core.Tracking;

/// <summary>
/// Turns minutes of activity into work sessions (the "manager").
/// A session starts with the first active minute and ends after a real break
/// (no input for at least the idle threshold), or when the computer sleeps / the app shuts down.
/// </summary>
/// <remarks>
/// Thread-safe: called from the minute timer, OS event, and rules threads.
/// All state is guarded by one <see cref="Lock"/>. Events are raised after the lock is released.
/// </remarks>
public sealed class ActivityTracker
{
    /// <summary>How much history is kept for the rules.</summary>
    public static TimeSpan RecentWindow { get; } = TimeSpan.FromMinutes(60);

    private readonly TimeSpan _idleThreshold;

    // System.Threading.Lock (C# 13): a dedicated lock object. Only one thread at a time can be inside lock (_lock).
    private readonly Lock _lock = new();

    private readonly Queue<ActivityMinute> _recentMinutes = new();
    private DateTimeOffset? _sessionStartUtc;
    private DateTimeOffset? _lastActiveUtc;

    /// <summary>Creates a tracker.</summary>
    /// <param name="options">Tracking settings (idle threshold).</param>
    public ActivityTracker(TrackingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _idleThreshold = options.IdleThreshold;
    }

    /// <summary>Raised when a new session starts.</summary>
    public event EventHandler<SessionStartedEventArgs>? SessionStarted;

    /// <summary>Raised when the current session ends.</summary>
    public event EventHandler<SessionEndedEventArgs>? SessionEnded;

    /// <summary>
    /// Records one minute of activity (active or not) and updates the session.
    /// Minutes must be recorded in time order.
    /// </summary>
    /// <param name="minute">The minute to record.</param>
    public void RecordMinute(ActivityMinute minute)
    {
        ArgumentNullException.ThrowIfNull(minute);

        SessionEndedEventArgs? ended = null;
        SessionStartedEventArgs? started = null;

        lock (_lock)
        {
            AddToRecent(minute);

            if (minute.IsActive)
            {
                // No minutes arrive while the computer sleeps. If this active minute comes after a long
                // gap, the old session really ended at its last activity, so close it before starting a new one.
                if (_sessionStartUtc is not null && IsIdleLongEnough(minute.MinuteStartUtc))
                {
                    ended = EndSession(SessionEndReason.Idle);
                }

                if (_sessionStartUtc is null)
                {
                    _sessionStartUtc = minute.MinuteStartUtc;
                    started = new SessionStartedEventArgs(minute.MinuteStartUtc);
                }

                _lastActiveUtc = minute.MinuteEndUtc;
            }
            else if (_sessionStartUtc is not null && IsIdleLongEnough(minute.MinuteEndUtc))
            {
                ended = EndSession(SessionEndReason.Idle);
            }
        }

        // Raise events outside the lock: a slow or misbehaving subscriber must not block other threads,
        // and a subscriber that calls back into the tracker must not deadlock.
        if (ended is not null)
        {
            SessionEnded?.Invoke(this, ended);
        }

        if (started is not null)
        {
            SessionStarted?.Invoke(this, started);
        }
    }

    /// <summary>
    /// Ends the current session immediately (for example on sleep or shutdown). Does nothing if there is no session.
    /// </summary>
    /// <param name="reason">Why the session is ending.</param>
    public void InterruptSession(SessionEndReason reason)
    {
        SessionEndedEventArgs? ended = null;

        lock (_lock)
        {
            if (_sessionStartUtc is not null)
            {
                ended = EndSession(reason);
            }
        }

        if (ended is not null)
        {
            SessionEnded?.Invoke(this, ended);
        }
    }

    /// <summary>Returns an immutable copy of the current state.</summary>
    /// <returns>The snapshot.</returns>
    public ActivitySnapshot GetSnapshot()
    {
        lock (_lock)
        {
            TimeSpan continuous = _sessionStartUtc is { } start && _lastActiveUtc is { } lastActive
                ? lastActive - start
                : TimeSpan.Zero;

            // [.. _recentMinutes] copies the queue into a new list, so later changes do not affect this snapshot.
            return new ActivitySnapshot(_sessionStartUtc, _lastActiveUtc, continuous, [.. _recentMinutes]);
        }
    }

    // Must be called while holding _lock.
    private void AddToRecent(ActivityMinute minute)
    {
        _recentMinutes.Enqueue(minute);

        DateTimeOffset oldestAllowed = minute.MinuteStartUtc - RecentWindow;
        while (_recentMinutes.Peek().MinuteStartUtc <= oldestAllowed)
        {
            _recentMinutes.Dequeue();
        }
    }

    // Must be called while holding _lock.
    private bool IsIdleLongEnough(DateTimeOffset timeUtc) =>
        _lastActiveUtc is { } lastActive && timeUtc - lastActive >= _idleThreshold;

    // Must be called while holding _lock, and only while a session is running.
    // The session ends at the last activity (when it really ended), not when we noticed.
    private SessionEndedEventArgs EndSession(SessionEndReason reason)
    {
        var ended = new SessionEndedEventArgs(_sessionStartUtc!.Value, _lastActiveUtc!.Value, reason);
        _sessionStartUtc = null;
        return ended;
    }
}
