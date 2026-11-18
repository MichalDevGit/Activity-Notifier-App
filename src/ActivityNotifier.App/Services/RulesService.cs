using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Rules;
using ActivityNotifier.Core.Tracking;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ActivityNotifier.App.Services;

/// <summary>
/// Background loop that, every <see cref="Interval"/>, takes a snapshot of the <see cref="ActivityTracker"/>
/// and asks the <see cref="RulesEngine"/> whether a reminder is due.
/// </summary>
/// <remarks>
/// M3: reminders are only logged. M4 shows them as toasts.
/// Runs on a thread-pool thread. The snapshot is immutable, and the engine guards its state with its own lock.
/// </remarks>
public sealed partial class RulesService : BackgroundService
{
    private readonly ActivityTracker _tracker;
    private readonly RulesEngine _engine;
    private readonly TimeProvider _time;
    private readonly ILogger<RulesService> _logger;

    private PeriodicTimer? _timer;

    /// <summary>Creates the service.</summary>
    /// <param name="tracker">Source of the activity snapshot.</param>
    /// <param name="engine">Decides which reminder (if any) is due.</param>
    /// <param name="timeProvider">The clock (fake in tests).</param>
    /// <param name="logger">Diagnostic log.</param>
    public RulesService(
        ActivityTracker tracker,
        RulesEngine engine,
        TimeProvider timeProvider,
        ILogger<RulesService> logger)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _tracker = tracker;
        _engine = engine;
        _time = timeProvider;
        _logger = logger;
    }

    /// <summary>How often the rules are checked.</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(15);

    /// <summary>Starts the clock: the first check is one <see cref="Interval"/> from now.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A task that completes when the service has started.</returns>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Created here, not in ExecuteAsync, for the same reason as in MinuteAggregatorService:
        // the timer must exist as soon as StartAsync returns.
        _timer = new PeriodicTimer(Interval, _time);
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

        _engine.NotificationRequested += OnNotificationRequested;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                _engine.Evaluate(_tracker.GetSnapshot());
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown: the host cancelled the token.
        }
        finally
        {
            // Always unsubscribe: the engine lives as long as the app, and a forgotten subscription
            // keeps this service alive and keeps calling it.
            _engine.NotificationRequested -= OnNotificationRequested;
        }
    }

    // Raised by the engine on this service's thread (inside Evaluate).
    private void OnNotificationRequested(object? sender, NotificationRequestedEventArgs e)
    {
        NotificationRequest request = e.Request;
        LogReminderDue(_logger, request.Rule, request.ContinuousWork, request.KeystrokesLastHour);
    }

    // Source-generated log method (see MinuteAggregatorService for why).
    [LoggerMessage(Level = LogLevel.Information, Message = "Reminder due: {Rule} (continuous work {ContinuousWork}, {Keystrokes} keystrokes in the last hour)")]
    private static partial void LogReminderDue(ILogger logger, RuleType rule, TimeSpan continuousWork, int keystrokes);
}
