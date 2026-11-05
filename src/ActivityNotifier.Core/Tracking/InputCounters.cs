using ActivityNotifier.Core.Models;

namespace ActivityNotifier.Core.Tracking;

/// <summary>
/// Thread-safe input counters. The hook thread increments them on every input event;
/// once a minute another thread calls <see cref="Snapshot"/> to read and reset them.
/// </summary>
/// <remarks>
/// Only counts are kept. Which key was pressed, or where the mouse is, is never stored here.
/// </remarks>
public sealed class InputCounters
{
    // Plain fields changed only through Interlocked, which makes each read/change one atomic CPU step.
    // A normal "_keystrokes++" is read + add + write, and two threads could lose an increment.
    private int _keystrokes;
    private int _clicks;
    private int _mouseDistancePx;

    /// <summary>Counts one key press.</summary>
    public void AddKeystroke() => Interlocked.Increment(ref _keystrokes);

    /// <summary>Counts one mouse button press.</summary>
    public void AddClick() => Interlocked.Increment(ref _clicks);

    /// <summary>Adds mouse movement distance.</summary>
    /// <param name="pixels">Distance moved since the last mouse event, in pixels. Must not be negative.</param>
    public void AddMouseDistance(int pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pixels);
        Interlocked.Add(ref _mouseDistancePx, pixels);
    }

    /// <summary>
    /// Returns the counts since the previous call and resets the counters to zero.
    /// </summary>
    /// <param name="minuteStartUtc">The start of the minute these counts belong to.</param>
    /// <returns>The counted minute.</returns>
    public ActivityMinute Snapshot(DateTimeOffset minuteStartUtc)
    {
        // Exchange = "give me the old value and store 0" in one atomic step, so no input is lost
        // between reading and resetting. Each counter is swapped separately: an input that lands
        // between two swaps is simply counted in the next minute.
        return new ActivityMinute(
            minuteStartUtc,
            Keystrokes: Interlocked.Exchange(ref _keystrokes, 0),
            Clicks: Interlocked.Exchange(ref _clicks, 0),
            MouseDistancePx: Interlocked.Exchange(ref _mouseDistancePx, 0));
    }
}
