using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Rules;
using static ActivityNotifier.Core.Tests.Rules.RuleTestData;

namespace ActivityNotifier.Core.Tests.Rules;

public sealed class MicroBreakRuleTests
{
    private readonly MicroBreakRule _rule = new(new RulesOptions()); // 50 minutes

    [Fact]
    public void IsDue_NoSession_False()
    {
        Assert.False(_rule.IsDue(Context(NoSession(), At(60))));
    }

    [Fact]
    public void IsDue_Session49Minutes_False()
    {
        Assert.False(_rule.IsDue(Context(Session(0, 49), At(49))));
    }

    [Fact]
    public void IsDue_Session50Minutes_True()
    {
        Assert.True(_rule.IsDue(Context(Session(0, 50), At(50))));
    }

    [Fact]
    public void IsDue_LessThan50MinutesSinceLastMicroBreak_False()
    {
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.MicroBreak] = At(51) };

        Assert.False(_rule.IsDue(Context(Session(0, 100), At(100), lastFired)));
    }

    [Fact]
    public void IsDue_50MinutesSinceLastMicroBreak_True()
    {
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.MicroBreak] = At(50) };

        Assert.True(_rule.IsDue(Context(Session(0, 100), At(100), lastFired)));
    }

    [Fact]
    public void IsDue_LongBreakReminderRecently_False()
    {
        // 100 min of work, but a LongBreak reminder 10 min ago restarted the micro-break count.
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.LongBreak] = At(90) };

        Assert.False(_rule.IsDue(Context(Session(0, 100), At(100), lastFired)));
    }
}
