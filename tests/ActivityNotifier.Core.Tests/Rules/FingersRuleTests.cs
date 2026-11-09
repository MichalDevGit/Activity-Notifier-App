using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Rules;
using static ActivityNotifier.Core.Tests.Rules.RuleTestData;

namespace ActivityNotifier.Core.Tests.Rules;

public sealed class FingersRuleTests
{
    private readonly FingersRule _rule = new(new RulesOptions()); // 4000 keys in 30 min, cooldown 45 min

    [Fact]
    public void IsDue_4000KeystrokesIn30Minutes_True()
    {
        Assert.True(_rule.IsDue(Context(Session(0, 30, keysPerMinute: 134), At(30)))); // 4020
    }

    [Fact]
    public void IsDue_Under4000KeystrokesIn30Minutes_False()
    {
        Assert.False(_rule.IsDue(Context(Session(0, 30, keysPerMinute: 133), At(30)))); // 3990
    }

    [Fact]
    public void IsDue_KeystrokesOlderThanWindow_NotCounted()
    {
        // 6000 keys typed 09:00–09:30, light typing after. At 09:45 only 09:15–09:45 counts: 3000 + 15.
        ActivityMinute[] minutes = [.. ActiveMinutes(0, 30, keysPerMinute: 200), .. ActiveMinutes(30, 45, keysPerMinute: 1)];

        Assert.False(_rule.IsDue(Context(Snapshot(minutes), At(45))));
    }

    [Fact]
    public void IsDue_WithinCooldown_False()
    {
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.Fingers] = At(30 - 44) };

        Assert.False(_rule.IsDue(Context(Session(0, 30, keysPerMinute: 200), At(30), lastFired)));
    }

    [Fact]
    public void IsDue_CooldownOver_True()
    {
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.Fingers] = At(30 - 45) };

        Assert.True(_rule.IsDue(Context(Session(0, 30, keysPerMinute: 200), At(30), lastFired)));
    }
}
