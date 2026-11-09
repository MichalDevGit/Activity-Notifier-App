using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Rules;
using static ActivityNotifier.Core.Tests.Rules.RuleTestData;

namespace ActivityNotifier.Core.Tests.Rules;

public sealed class LongBreakRuleTests
{
    private readonly LongBreakRule _rule = new(new RulesOptions()); // 90 minutes

    [Fact]
    public void IsDue_NoSession_False()
    {
        Assert.False(_rule.IsDue(Context(NoSession(), At(100))));
    }

    [Fact]
    public void IsDue_Session89Minutes_False()
    {
        Assert.False(_rule.IsDue(Context(Session(0, 89), At(89))));
    }

    [Fact]
    public void IsDue_Session90Minutes_True()
    {
        Assert.True(_rule.IsDue(Context(Session(0, 90), At(90))));
    }

    [Fact]
    public void IsDue_MicroBreakReminderInBetween_StillCountsFromSessionStart()
    {
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.MicroBreak] = At(50) };

        Assert.True(_rule.IsDue(Context(Session(0, 90), At(90), lastFired)));
    }

    [Fact]
    public void IsDue_LessThan90MinutesSinceLastLongBreak_False()
    {
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.LongBreak] = At(90) };

        Assert.False(_rule.IsDue(Context(Session(0, 179), At(179), lastFired)));
    }

    [Fact]
    public void IsDue_90MinutesSinceLastLongBreak_True()
    {
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.LongBreak] = At(90) };

        Assert.True(_rule.IsDue(Context(Session(0, 180), At(180), lastFired)));
    }

    [Fact]
    public void IsDue_ReminderFromPreviousSession_Ignored()
    {
        // Reminded at 09:30 in an earlier session; the current session started at 10:00 and lasted 90 min.
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.LongBreak] = At(30) };

        Assert.True(_rule.IsDue(Context(Session(60, 150), At(150), lastFired)));
    }
}
