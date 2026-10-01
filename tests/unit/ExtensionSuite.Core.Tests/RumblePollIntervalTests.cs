using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class RumblePollIntervalTests
{
    [Fact]
    public void DefaultSupportsTheSpecifiedSevenSecondDelay()
    {
        var interval = new RumblePollInterval();
        Assert.Equal(7, interval.Seconds);
        Assert.Equal(TimeSpan.FromSeconds(7), interval.Duration);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    public void NormalSettingsAcceptTheirInclusiveBounds(int seconds)
    {
        Assert.Equal(seconds, new RumblePollInterval(seconds).Seconds);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(4, false)]
    [InlineData(11, false)]
    [InlineData(4, true)]
    public void UnsafeOrNonAdvancedIntervalsAreRejected(int seconds, bool advanced)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RumblePollInterval(seconds, advanced));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(60)]
    public void AdvancedSettingsAllowSlowerPolling(int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), new RumblePollInterval(seconds, advanced: true).Duration);
    }
}
