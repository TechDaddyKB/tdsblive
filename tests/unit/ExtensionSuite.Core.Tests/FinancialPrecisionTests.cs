using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class FinancialPrecisionTests
{
    [Fact]
    public void ExplicitScalesAndContributionLevelRoundingPreservePrecision()
    {
        Assert.Equal(123, FinancialPrecision.ConvertToUsdMinor(new(100, "EUR", 2), 1.2345m));
        Assert.Equal(1, FinancialPrecision.ConvertToUsdMinor(new(1, "EUR", 2), .5m));
        Assert.Equal(125, FinancialPrecision.ConvertToUsdMinor(new(100, "JPY", 0), .0125m));
        Assert.Equal(325, FinancialPrecision.ConvertToUsdMinor(new(1000, "KWD", 3), 3.25m));
        Assert.Equal(1, FinancialPrecision.NominalUsdMinor(10, .05m));
        Assert.Equal(0, FinancialPrecision.NominalUsdMinor(1, 0m));
        Assert.Throws<OverflowException>(() => FinancialPrecision.NominalUsdMinor(long.MaxValue, 2m));
        Assert.Throws<OverflowException>(() => FinancialPrecision.ConvertToUsdMinor(new(long.MaxValue, "EUR", 0), decimal.MaxValue));
    }

    [Fact]
    public void ExactSpendOverridesNominalAndMissingRatesRemainPending()
    {
        var exact = SupportValuator.Value(new(999, "USD", 2), 1, 500m);
        Assert.Equal(new SupportValuation(999, "exact", false), exact);
        var pending = SupportValuator.Value(new(999, "EUR", 2), 1, 500m);
        Assert.Null(pending.UsdAmountMinor); Assert.Equal("fx_unavailable", pending.PendingReason);
        var date = new DateOnly(2026, 1, 5);
        var fx = SupportValuator.Value(new(999, "EUR", 2), 1, null, new("EUR", date, date, 1.25m, "manual", false));
        Assert.Equal(1249, fx.UsdAmountMinor); Assert.Equal("fx", fx.Method); Assert.False(fx.Estimated);
        Assert.Equal("manual", fx.FxProvider); Assert.Equal(date, fx.FxRateDate);
        Assert.True(SupportValuator.Value(new(1, "EUR", 2), 1, null, new("EUR", date, date.AddDays(1), 1m, "latest", true)).Estimated);
        Assert.Equal("configured_nominal", SupportValuator.Value(null, 500, 1m).Method);
        Assert.True(SupportValuator.Value(null, 500, 1m).Estimated);
        Assert.Null(SupportValuator.Value(null, 500, null).UsdAmountMinor);
    }

    [Fact]
    public void InvalidInputsCannotCreateAuthoritativeAmounts()
    {
        foreach (var value in new[] { new NativeMoney(-1, "USD", 2), new(1, "usd", 2), new(1, "US", 2), new(1, "USD", 0), new(1, "EUR", 5) })
            Assert.Throws<ArgumentException>(value.Validate);
        Assert.Throws<ArgumentException>(() => FinancialPrecision.NominalUsdMinor(0, 1));
        Assert.Throws<ArgumentException>(() => FinancialPrecision.NominalUsdMinor(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => FinancialPrecision.ConvertToUsdMinor(new(1, "EUR", 2), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SupportValuator.Value(null, 0, null));
        var date = new DateOnly(2026, 1, 1);
        Assert.Throws<ArgumentException>(() => SupportValuator.Value(new(1, "EUR", 2), 1, null, new("JPY", date, date, 1m, "manual", false)));
        Assert.Throws<ArgumentException>(() => new CurrencyRate("EUR", date, date, -1m, "manual", false).Validate());
        Assert.Throws<ArgumentException>(() => new CurrencyRate("EUR", date, date, 1m, "", false).Validate());
    }
}
