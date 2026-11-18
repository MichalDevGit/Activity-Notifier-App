using System.Threading.Channels;
using ActivityNotifier.App.Services;
using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Rules;
using ActivityNotifier.Core.Tracking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace ActivityNotifier.App.Tests.Services;

/// <summary>Runs the real rules loop on a fake clock (same waiting technique as <see cref="MinuteAggregatorServiceTests"/>).</summary>
public sealed class RulesServiceTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(10);
    private static readonly DateTimeOffset NineOClock = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_EveryInterval_EvaluatesTheRules()
    {
        var time = new FakeTimeProvider(NineOClock);

        // The user was active in the minute 08:59–09:00, so they count as "active now".
        var tracker = new ActivityTracker(new TrackingOptions());
        tracker.RecordMinute(new ActivityMinute(NineOClock.AddMinutes(-1), 10, 0, 0));

        // A rule that is always due, and no minimum gap: every evaluation produces a notification.
        var rule = Substitute.For<IActivityRule>();
        rule.Type.Returns(RuleType.Hydration);
        rule.IsDue(Arg.Any<RuleContext>()).Returns(true);
        var engine = new RulesEngine([rule], new RulesOptions { MinGapMinutes = 0 }, new TrackingOptions(), time);

        var notifications = Channel.CreateUnbounded<NotificationRequest>();
        engine.NotificationRequested += (_, e) => notifications.Writer.TryWrite(e.Request);

        using var service = new RulesService(tracker, engine, time, NullLogger<RulesService>.Instance);
        await service.StartAsync(TestContext.Current.CancellationToken);

        time.Advance(RulesService.Interval); // 09:00:15
        var first = await NextAsync(notifications);

        time.Advance(RulesService.Interval); // 09:00:30
        var second = await NextAsync(notifications);

        // CreatedUtc is the engine's "now", so it shows exactly when each evaluation happened.
        Assert.Equal(NineOClock.AddSeconds(15), first.CreatedUtc);
        Assert.Equal(NineOClock.AddSeconds(30), second.CreatedUtc);

        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    // Waits for the next notification the loop produced (with a safety timeout so a broken test cannot hang).
    private static async Task<NotificationRequest> NextAsync(Channel<NotificationRequest> notifications) =>
        await notifications.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);
}
