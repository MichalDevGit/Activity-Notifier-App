using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Rules;
using ActivityNotifier.Core.Tracking;
using Microsoft.Extensions.Time.Testing;
using static ActivityNotifier.Core.Tests.Rules.RuleTestData;

namespace ActivityNotifier.Core.Tests.Rules;

/// <summary>
/// End-to-end stories with the real tracker, the real rules, and the engine. Each simulated minute:
/// record it in the tracker, move the clock to the end of that minute, evaluate.
/// </summary>
public sealed class RulesScenarioTests
{
    private readonly FakeTimeProvider _time = new(T0); // local time zone = UTC by default
    private readonly ActivityTracker _tracker = new(new TrackingOptions());
    private readonly RulesEngine _engine;
    private readonly List<NotificationRequest> _requests = [];

    public RulesScenarioTests()
    {
        var options = new RulesOptions();
        IActivityRule[] rules = [new HydrationRule(options), new MicroBreakRule(options), new LongBreakRule(options), new FingersRule(options)];
        _engine = new RulesEngine(rules, options, new TrackingOptions(), _time);
        _engine.NotificationRequested += (_, e) => _requests.Add(e.Request);
    }

    [Fact]
    public void ContinuousWork90Minutes_RemindersInExpectedOrder()
    {
        RunMinutes(0, 90, active: true); // 09:00–10:30

        (RuleType, DateTimeOffset)[] expected =
        [
            (RuleType.Hydration, At(2)),   // 09:02: 2 active minutes in the 09:00 hour
            (RuleType.MicroBreak, At(50)), // 09:50: 50 min of work
            (RuleType.Hydration, At(62)),  // 10:02: new clock hour
            (RuleType.LongBreak, At(90)),  // 10:30: 90 min of work
        ];
        Assert.Equal(expected, _requests.Select(r => (r.Rule, r.CreatedUtc)));
    }

    [Fact]
    public void RealBreakInTheMiddle_BreakCountsStartOver()
    {
        RunMinutes(0, 40, active: true);   // work 09:00–09:40
        RunMinutes(40, 50, active: false); // away 10 min: the session ends
        RunMinutes(50, 101, active: true); // new session from 09:50

        // Without the reset, 50 min of work would be reached at 10:00. With it: 09:50 + 50 = 10:40.
        var microBreak = Assert.Single(_requests, r => r.Rule == RuleType.MicroBreak);
        Assert.Equal(At(100), microBreak.CreatedUtc);
    }

    private void RunMinutes(int fromMinute, int toMinute, bool active)
    {
        for (int m = fromMinute; m < toMinute; m++)
        {
            int keys = active ? 50 : 0;
            _tracker.RecordMinute(new ActivityMinute(At(m), keys, 0, 0));
            _time.SetUtcNow(At(m + 1));
            _engine.Evaluate(_tracker.GetSnapshot());
        }
    }
}
