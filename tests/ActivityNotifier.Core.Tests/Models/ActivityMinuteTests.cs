using ActivityNotifier.Core.Models;

namespace ActivityNotifier.Core.Tests.Models;

public sealed class ActivityMinuteTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 0, 0, false)]
    [InlineData(1, 0, 0, true)]
    [InlineData(0, 1, 0, true)]
    [InlineData(0, 0, 1, true)]
    public void IsActive_GivenCounts_ReturnsExpected(int keys, int clicks, int distance, bool expected)
    {
        var minute = new ActivityMinute(T0, keys, clicks, distance);

        Assert.Equal(expected, minute.IsActive);
    }

    [Fact]
    public void MinuteEndUtc_Always_IsOneMinuteAfterStart()
    {
        var minute = new ActivityMinute(T0, 0, 0, 0);

        Assert.Equal(T0.AddMinutes(1), minute.MinuteEndUtc);
    }
}
