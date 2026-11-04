namespace ActivityNotifier.Infrastructure.Tests;

// M0 only: proves the test pipeline works. Replaced by real tests in M5.
public sealed class PlaceholderTests
{
    [Fact]
    public void Pipeline_WhenRun_Passes()
    {
        Assert.Equal(2, 1 + 1);
    }
}
