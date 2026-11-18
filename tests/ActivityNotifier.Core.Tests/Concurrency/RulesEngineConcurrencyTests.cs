using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;
using ActivityNotifier.Core.Rules;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using static ActivityNotifier.Core.Tests.Rules.RuleTestData;

namespace ActivityNotifier.Core.Tests.Concurrency;

/// <summary>
/// Many threads call <see cref="RulesEngine.Evaluate"/> at the same moment.
/// The minimum-gap policy must still allow only one notification: "check the gap, then remember the send time"
/// has to happen as one step (inside the engine's lock), or two threads could both pass the check.
/// </summary>
public sealed class RulesEngineConcurrencyTests
{
    [Fact]
    public void Evaluate_ManyThreadsAtTheSameTime_RaisesOnlyOneNotification()
    {
        var time = new FakeTimeProvider(At(60)); // 10:00, time does not move during the test
        var rule = Substitute.For<IActivityRule>();
        rule.Type.Returns(RuleType.MicroBreak);
        rule.Priority.Returns(2);
        rule.IsDue(Arg.Any<RuleContext>()).Returns(true); // always due: only the gap policy can stop it

        var engine = new RulesEngine([rule], new RulesOptions(), new TrackingOptions(), time);

        int notifications = 0;
        // The event is raised on whichever thread won, so count with Interlocked too.
        engine.NotificationRequested += (_, _) => Interlocked.Increment(ref notifications);

        ActivitySnapshot snapshot = Session(0, 60); // active until 10:00

        // Parallel.For runs the body on many thread-pool threads at once.
        Parallel.For(0, 10_000, _ => engine.Evaluate(snapshot));

        Assert.Equal(1, notifications);
    }
}
