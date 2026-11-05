using System.ComponentModel.DataAnnotations;
using ActivityNotifier.Core.Options;

namespace ActivityNotifier.Core.Tests.Options;

public sealed class TrackingOptionsTests
{
    [Fact]
    public void Validate_Defaults_Passes()
    {
        Assert.True(IsValid(new TrackingOptions()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Validate_IdleThresholdOutOfRange_Fails(int minutes)
    {
        Assert.False(IsValid(new TrackingOptions { IdleThresholdMinutes = minutes }));
    }

    // The same check the app runs at startup (ValidateDataAnnotations).
    private static bool IsValid(TrackingOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), validationResults: null, validateAllProperties: true);
}
