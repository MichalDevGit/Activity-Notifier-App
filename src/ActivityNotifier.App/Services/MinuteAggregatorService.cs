using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Tracking;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ActivityNotifier.App.Services;

/// <summary>
/// Background loop that, at every whole clock minute (10:00, 10:01, ...), takes the input counts of the
/// minute that just ended from <see cref="InputCounters"/> and records them in the <see cref="ActivityTracker"/>.
/// On shutdown it records the last, unfinished minute too, so no input is lost.
/// </summary>
/// <remarks>
/// Runs on a thread-pool thread. Shares <see cref="InputCounters"/> with the hook thread (lock-free, Interlocked)
/// and <see cref="ActivityTracker"/> with other threads (the tracker's own lock).
/// </remarks>
public sealed partial class MinuteAggregatorService : BackgroundService
{
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    private readonly InputCounters _counters;
    private readonly ActivityTracker _tracker;
    private readonly TimeProvider _time;
    private readonly ILogger<MinuteAggregatorService> _logger;

    private PeriodicTimer? _timer;

    // The whole minute the counters are currently collecting for. Only the loop touches it.
    private DateTimeOffset _bucketStartUtc;

    /// <summary>Creates the service.</summary>
    /// <param name="counters">The counters filled by the input hooks.</param>
    /// <param name="tracker">Receives one minute at a time.</param>
    /// <param name="timeProvider">The clock (fake in tests).</param>
    /// <param name="logger">Diagnostic log.</param>
    public MinuteAggregatorService(
        InputCounters counters,
        ActivityTracker tracker,
        TimeProvider timeProvider,
        ILogger<MinuteAggregatorService> logger)
    {
        ArgumentNullException.ThrowIfNull(counters);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _counters = counters;
        _tracker = tracker;
        _time = timeProvider;
        _logger = logger;
    }

    /// <summary>Starts the clock: the first tick is at the next whole minute.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A task that completes when the service has started.</returns>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // The timer is created here, not in ExecuteAsync: StartAsync runs on the caller's thread, so the timer is
        // guaranteed to exist when StartAsync returns. ExecuteAsync is started later on a thread-pool thread
        // (.NET 10 behavior), and a tick that happens before it waits is kept by PeriodicTimer, not lost.
        DateTimeOffset nowUtc = _time.GetUtcNow();
        _bucketStartUtc = StartOfMinute(nowUtc);
        _timer = new PeriodicTimer(_bucketStartUtc + OneMinute - nowUtc, _time);

        return base.StartAsync(cancellationToken);
    }

    /// <summary>Releases the timer.</summary>
    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PeriodicTimer timer = _timer ?? throw new InvalidOperationException("StartAsync must run before ExecuteAsync.");

        try
        {
            // WaitForNextTickAsync returns true on each tick and throws OperationCanceledException on shutdown.
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                OnTick(timer);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown: the host cancelled the token.
        }

        // Graceful shutdown: record the unfinished minute, so its input is not lost.
        RecordBucket(_bucketStartUtc);
        LogFlushedLastMinute(_logger, _bucketStartUtc);
    }

    private void OnTick(PeriodicTimer timer)
    {
        // Prepare the next minute BEFORE recording the finished one. Recording raises tracker events,
        // and anyone reacting to them (e.g. a test) must already see the timer set for the next minute.
        DateTimeOffset nowUtc = _time.GetUtcNow();

        // A real timer can fire a few ms early or late, so round to the nearest whole minute (10:00:59.998 → 10:01).
        DateTimeOffset thisMinuteUtc = RoundToMinute(nowUtc);

        // Re-aim the timer at the next whole minute on every tick, so it never drifts away from the clock.
        timer.Period = thisMinuteUtc + OneMinute - nowUtc;

        DateTimeOffset finishedBucketUtc = _bucketStartUtc;

        // Normally thisMinute = finished bucket + 1 min. After sleep it jumps ahead (the tracker treats the gap
        // as a break). If the wall clock was set back, still move forward: the tracker needs minutes in time order.
        _bucketStartUtc = thisMinuteUtc > finishedBucketUtc ? thisMinuteUtc : finishedBucketUtc + OneMinute;

        RecordBucket(finishedBucketUtc);
    }

    private void RecordBucket(DateTimeOffset bucketStartUtc)
    {
        ActivityMinute minute = _counters.Snapshot(bucketStartUtc);
        _tracker.RecordMinute(minute);

        // Counts only (aggregated numbers): never which keys or where the mouse was.
        LogMinuteRecorded(_logger, minute.MinuteStartUtc, minute.Keystrokes, minute.Clicks, minute.MouseDistancePx);
    }

    private static DateTimeOffset StartOfMinute(DateTimeOffset time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Offset);

    private static DateTimeOffset RoundToMinute(DateTimeOffset time) =>
        StartOfMinute(time + TimeSpan.FromSeconds(30));

    // [LoggerMessage]: the compiler generates the body of these methods (a "source generator").
    // It is faster than _logger.LogDebug("...", args): the message template is parsed once, and nothing is
    // formatted or boxed when the log level is turned off.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Minute {MinuteStartUtc}: {Keystrokes} keys, {Clicks} clicks, {MouseDistancePx} px")]
    private static partial void LogMinuteRecorded(ILogger logger, DateTimeOffset minuteStartUtc, int keystrokes, int clicks, int mouseDistancePx);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Shutdown: recorded the unfinished minute {MinuteStartUtc}")]
    private static partial void LogFlushedLastMinute(ILogger logger, DateTimeOffset minuteStartUtc);
}
