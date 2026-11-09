using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Rules;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using static ActivityNotifier.Core.Tests.Rules.RuleTestData;

namespace ActivityNotifier.Core.Tests.Rules;

/// <summary>
/// Tests the engine's policies with fake rules (NSubstitute), so real rule math does not get in the way.
/// Defaults: active now = 2 min, minimum gap = 3 min, snooze = 10 min.
/// </summary>
public sealed class RulesEngineTests
{
    private readonly FakeTimeProvider _time = new(At(60)); // "now" starts at 10:00
    private readonly List<NotificationRequest> _requests = [];

    [Fact]
    public void Evaluate_DueRule_RaisesNotificationRequested()
    {
        var engine = CreateEngine(FakeRule(RuleType.MicroBreak, priority: 2, isDue: true));

        engine.Evaluate(ActiveNow());

        var request = Assert.Single(_requests);
        Assert.Equal(RuleType.MicroBreak, request.Rule);
        Assert.Equal(_time.GetUtcNow(), request.CreatedUtc);
    }

    [Fact]
    public void Evaluate_NoDueRule_RaisesNothing()
    {
        var engine = CreateEngine(FakeRule(RuleType.MicroBreak, priority: 2, isDue: false));

        engine.Evaluate(ActiveNow());

        Assert.Empty(_requests);
    }

    [Fact]
    public void Evaluate_TwoDueRules_HighestPriorityWins()
    {
        // Passed in the "wrong" order on purpose: the engine must sort by priority itself.
        var engine = CreateEngine(
            FakeRule(RuleType.Fingers, priority: 4, isDue: true),
            FakeRule(RuleType.LongBreak, priority: 1, isDue: true));

        engine.Evaluate(ActiveNow());

        Assert.Equal(RuleType.LongBreak, Assert.Single(_requests).Rule);
    }

    [Fact]
    public void Evaluate_UserNotActiveNow_RaisesNothingAndDoesNotAskRules()
    {
        var rule = FakeRule(RuleType.MicroBreak, priority: 2, isDue: true);
        var engine = CreateEngine(rule);
        var lastInput3MinutesAgo = Snapshot(ActiveMinutes(0, 57)); // last activity 09:57, now 10:00

        engine.Evaluate(lastInput3MinutesAgo);

        Assert.Empty(_requests);
        rule.DidNotReceive().IsDue(Arg.Any<RuleContext>()); // NSubstitute: verify a call did NOT happen
    }

    [Fact]
    public void Evaluate_WithinMinimumGap_RaisesNothingUntilGapPassed()
    {
        var engine = CreateEngine(FakeRule(RuleType.Fingers, priority: 4, isDue: true));

        engine.Evaluate(ActiveNow());
        _time.Advance(TimeSpan.FromMinutes(2));
        engine.Evaluate(ActiveNow());

        Assert.Single(_requests);

        _time.Advance(TimeSpan.FromMinutes(1)); // 3 minutes since the first one
        engine.Evaluate(ActiveNow());

        Assert.Equal(2, _requests.Count);
    }

    [Fact]
    public void Evaluate_AfterRuleFired_PassesLastFiredTimeToRules()
    {
        var contexts = new List<RuleContext>();
        var rule = Substitute.For<IActivityRule>();
        rule.Type.Returns(RuleType.MicroBreak);
        rule.Priority.Returns(2);
        rule.IsDue(Arg.Do<RuleContext>(contexts.Add)).Returns(true); // Arg.Do: capture every context passed in
        var engine = CreateEngine(rule);
        DateTimeOffset firedAt = _time.GetUtcNow();

        engine.Evaluate(ActiveNow());
        _time.Advance(TimeSpan.FromMinutes(5));
        engine.Evaluate(ActiveNow());

        Assert.Null(contexts[0].LastFiredUtc(RuleType.MicroBreak));
        Assert.Equal(firedAt, contexts[1].LastFiredUtc(RuleType.MicroBreak));
    }

    [Fact]
    public void Evaluate_RaisedRequest_ContainsAggregatedNumbers()
    {
        var engine = CreateEngine(FakeRule(RuleType.Fingers, priority: 4, isDue: true));

        engine.Evaluate(Session(30, 60, keysPerMinute: 100)); // 09:30–10:00

        var request = Assert.Single(_requests);
        Assert.Equal(TimeSpan.FromMinutes(30), request.ContinuousWork);
        Assert.Equal(3000, request.KeystrokesLastHour);
    }

    [Fact]
    public void Snooze_BeforeExpiry_RuleNotRaisedEvenIfDue()
    {
        var engine = CreateEngine(FakeRule(RuleType.Hydration, priority: 3, isDue: true));

        engine.Snooze(RuleType.Hydration);
        _time.Advance(TimeSpan.FromMinutes(9));
        engine.Evaluate(ActiveNow());

        Assert.Empty(_requests);
    }

    [Fact]
    public void Snooze_AfterExpiry_RaisedAgainOnce()
    {
        var engine = CreateEngine(FakeRule(RuleType.Hydration, priority: 3, isDue: false));

        engine.Snooze(RuleType.Hydration);
        _time.Advance(TimeSpan.FromMinutes(10));
        engine.Evaluate(ActiveNow());

        Assert.Equal(RuleType.Hydration, Assert.Single(_requests).Rule);

        _time.Advance(TimeSpan.FromMinutes(5)); // snooze was used up: not raised again
        engine.Evaluate(ActiveNow());

        Assert.Single(_requests);
    }

    [Fact]
    public void Snooze_BreakRuleThenRealBreak_SnoozeDropped()
    {
        var engine = CreateEngine(FakeRule(RuleType.MicroBreak, priority: 2, isDue: false));

        engine.Snooze(RuleType.MicroBreak); // at 10:00, during a session that started at 09:00
        _time.Advance(TimeSpan.FromMinutes(20));
        var newSessionAfterBreak = Session(75, 80); // 10:15–10:20: started after the snooze
        engine.Evaluate(newSessionAfterBreak);

        Assert.Empty(_requests);
    }

    [Fact]
    public void Snooze_ZeroDuration_Throws()
    {
        var engine = CreateEngine();

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Snooze(RuleType.Hydration, TimeSpan.Zero));
    }

    [Fact]
    public async Task Evaluate_WithConcurrentSnooze_NoExceptions()
    {
        var engine = CreateEngine(
            FakeRule(RuleType.MicroBreak, priority: 2, isDue: true),
            FakeRule(RuleType.Hydration, priority: 3, isDue: true));
        var snapshot = ActiveNow();
        var ct = TestContext.Current.CancellationToken;

        var evaluator = Task.Run(
            () =>
            {
                for (int i = 0; i < 10_000; i++)
                {
                    engine.Evaluate(snapshot);
                }
            },
            ct);
        var snoozer = Task.Run(
            () =>
            {
                for (int i = 0; i < 10_000; i++)
                {
                    engine.Snooze(i % 2 == 0 ? RuleType.MicroBreak : RuleType.Hydration);
                }
            },
            ct);

        await Task.WhenAll(evaluator, snoozer);

        Assert.True(_requests.Count <= 1); // the clock never moves, so the 3-min gap allows at most one
    }

    private static IActivityRule FakeRule(RuleType type, int priority, bool isDue)
    {
        var rule = Substitute.For<IActivityRule>(); // a fake object that implements the interface
        rule.Type.Returns(type);
        rule.Priority.Returns(priority);
        rule.IsDue(Arg.Any<RuleContext>()).Returns(isDue);
        return rule;
    }

    private RulesEngine CreateEngine(params IActivityRule[] rules)
    {
        var engine = new RulesEngine(rules, new RulesOptions(), new TrackingOptions(), _time);
        engine.NotificationRequested += (_, e) => _requests.Add(e.Request);
        return engine;
    }

    /// <summary>A session from 09:00 whose last activity is exactly "now".</summary>
    private ActivitySnapshot ActiveNow()
    {
        int nowMinute = (int)(_time.GetUtcNow() - T0).TotalMinutes;
        return Session(0, nowMinute);
    }
}
