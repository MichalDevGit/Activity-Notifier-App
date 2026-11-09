using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Rules;
using static ActivityNotifier.Core.Tests.Rules.RuleTestData;

namespace ActivityNotifier.Core.Tests.Rules;

public sealed class HydrationRuleTests
{
    private readonly HydrationRule _rule = new(new RulesOptions()); // 2 active minutes per clock hour

    [Fact]
    public void IsDue_TwoActiveMinutesThisHourNotReminded_True()
    {
        Assert.True(_rule.IsDue(Context(Session(0, 2), At(2)))); // 09:00 and 09:01
    }

    [Fact]
    public void IsDue_OneActiveMinuteThisHour_False()
    {
        Assert.False(_rule.IsDue(Context(Session(0, 1), At(1))));
    }

    [Fact]
    public void IsDue_AlreadyRemindedThisHour_False()
    {
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.Hydration] = At(2) };

        Assert.False(_rule.IsDue(Context(Session(0, 30), At(30), lastFired)));
    }

    [Fact]
    public void IsDue_RemindedInPreviousHour_TrueAgainInNewHour()
    {
        // Reminded at 09:02; now 10:02 with activity at 10:00 and 10:01.
        var lastFired = new Dictionary<RuleType, DateTimeOffset> { [RuleType.Hydration] = At(2) };

        Assert.True(_rule.IsDue(Context(Session(0, 62), At(62), lastFired)));
    }

    [Fact]
    public void IsDue_ActivityOnlyInPreviousHour_False()
    {
        // No catch-up: worked 09:00–09:40, it is now 10:01 and nothing happened in the 10:00 hour yet.
        Assert.False(_rule.IsDue(Context(Session(0, 40), At(61))));
    }

    [Fact]
    public void IsDue_LocalClockHourDiffersFromUtcHour_UsesLocalHour()
    {
        // Active 09:25–09:31 UTC. In a UTC+05:30 zone that is 14:55–15:01 local,
        // so the current local hour (15:00) has only one active minute.
        var plus0530 = TimeZoneInfo.CreateCustomTimeZone("Test+05:30", TimeSpan.FromMinutes(330), "Test+05:30", "Test+05:30");
        var snapshot = Session(25, 31);

        Assert.True(_rule.IsDue(Context(snapshot, At(31), zone: TimeZoneInfo.Utc)));
        Assert.False(_rule.IsDue(Context(snapshot, At(31), zone: plus0530)));
    }
}
