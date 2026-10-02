using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class DonorWidgetContractTests
{
    [Theory]
    [InlineData("donor-crown")]
    [InlineData("donor-leaderboard")]
    [InlineData("latest-supporter")]
    [InlineData("current-stream-leader")]
    [InlineData("current-stream-total")]
    public void DonorKindsRoundTripThroughOverlayValidation(string kind)
    {
        new OverlayDefinition { CanvasEnabled = true, Widgets = [new() { Kind = kind }] }.Validate();
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData("9223372036854775808")]
    public void ThresholdRejectsNonIntegerOrOverflowMoney(string minimum)
        => Assert.Throws<ArgumentException>(() => new DonorWidgetSettings { MinimumUsdMinor = minimum }.Validate());

    [Fact]
    public void CustomPeriodRequiresOrderedHalfOpenDates()
    {
        var settings = new DonorWidgetSettings { Period = "custom", CustomStart = new(2026, 10, 1) };
        Assert.Throws<ArgumentException>(settings.Validate);
        Assert.Throws<ArgumentException>(() => (settings with { CustomEndExclusive = new(2026, 10, 1) }).Validate());
        (settings with { CustomEndExclusive = new(2026, 10, 2), MinimumUsdMinor = "9007199254740993" }).Validate();
    }

    [Fact]
    public void UnboundedCountsUnsafeAssetsAndNullSettingsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new DonorWidgetSettings { Count = 26 }.Validate());
        Assert.Throws<ArgumentException>(() => new DonorWidgetSettings { CrownAssetId = "../secret" }.Validate());
        Assert.Throws<ArgumentException>(() => new DonorWidgetSettings { FontAssetId = "https://remote.invalid/font" }.Validate());
        Assert.Throws<ArgumentException>(() => new OverlayWidget { Donor = null! }.Validate());
    }
}
