using System.ComponentModel.DataAnnotations;
using ActivityNotifier.Core.Options;

namespace ActivityNotifier.Core.Tests.Options;

public sealed class RulesOptionsTests
{
    [Fact]
    public void Validate_Defaults_Passes()
    {
        Assert.True(IsValid(new RulesOptions()));
    }

    [Fact]
    public void Validate_FingersWindowLongerThanTrackedHistory_Fails()
    {
        Assert.False(IsValid(new RulesOptions { FingersWindowMinutes = 61 }));
    }

    [Fact]
    public void Validate_NegativeMinGap_Fails()
    {
        Assert.False(IsValid(new RulesOptions { MinGapMinutes = -1 }));
    }

    [Fact]
    public void Validate_ZeroSnooze_Fails()
    {
        Assert.False(IsValid(new RulesOptions { SnoozeMinutes = 0 }));
    }

    private static bool IsValid(RulesOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), validationResults: null, validateAllProperties: true);
}
