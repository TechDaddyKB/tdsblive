namespace ExtensionSuite.Core;

public static class ValuationMethods
{
    public const string Exact = "exact";
    public const string Fx = "fx";
    public const string ConfiguredNominal = "configured_nominal";
    public const string Unknown = "unknown";
}

/// <summary>Verified financial facts carried independently of optional raw capture.</summary>
public sealed record SupportDetails(string Kind, long Quantity, NativeMoney? NativeMoney = null,
    string Tier = "", string? GiftCorrelationKey = null, string GiftRole = "none", string? GatedReason = null,
    string? ReportedAmountMajor = null, string? ReportedCurrency = null, string[]? GiftRecipientKeys = null)
{
    public void Validate()
    {
        if (!new[] { "bits", "donation", "subscription", "membership", "gift", "rant" }.Contains(Kind) ||
            Quantity < 0 || (Quantity == 0 && GatedReason is null) || Tier is null || Tier.Length > 64 ||
            GiftCorrelationKey?.Length > 256 || GatedReason?.Length > 128 ||
            ReportedAmountMajor?.Length > 128 || ReportedCurrency?.Length > 128 ||
            !new[] { "none", "batch", "individual", "standalone", "recipient" }.Contains(GiftRole))
            throw new ArgumentException("Invalid financial support facts.");
        if (GiftRecipientKeys is { } keys && (keys.Length > 1000 || keys.Any(key => string.IsNullOrWhiteSpace(key) || key.Length > 512)))
            throw new ArgumentException("Invalid gift recipient evidence.");
        NativeMoney?.Validate();
    }
}

/// <summary>Native spend, distinct from a configured supporter-value estimate.</summary>
public sealed record NativeMoney(long AmountMinor, string Currency, int MinorUnitDigits)
{
    public void Validate()
    {
        if (AmountMinor < 0 || !CurrencyCode.IsValid(Currency) || MinorUnitDigits is < 0 or > 4)
            throw new ArgumentException("Money requires a nonnegative amount, uppercase currency code and explicit minor-unit scale.");
        if (Currency == "USD" && MinorUnitDigits != 2)
            throw new ArgumentException("USD requires two minor-unit digits.");
    }
}

public static class CurrencyCode
{
    public static bool IsValid(string? value) => value is { Length: 3 } && value.All(c => c is >= 'A' and <= 'Z');
}

/// <summary>Rate direction is always one native major unit to USD major units.</summary>
public sealed record CurrencyRate(string Currency, DateOnly RequestedDate, DateOnly RateDate,
    decimal UsdPerNativeUnit, string Provider, bool Estimated)
{
    public void Validate()
    {
        if (!CurrencyCode.IsValid(Currency) || UsdPerNativeUnit <= 0 || string.IsNullOrWhiteSpace(Provider) || Provider.Length > 128)
            throw new ArgumentException("Currency rate requires a positive decimal rate and bounded provider attribution.");
    }
}

public interface ICurrencyRateProvider
{
    Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default);
}

public sealed record SupportValuation(long? UsdAmountMinor, string Method, bool Estimated,
    decimal? FxRate = null, DateOnly? FxRateDate = null, string? FxProvider = null,
    string? PendingReason = null);

public static class FinancialPrecision
{
    // Round the contribution total once, rather than rounding each unit first.
    public static long NominalUsdMinor(long quantity, decimal usdMinorPerUnit)
    {
        if (quantity <= 0 || usdMinorPerUnit < 0) throw new ArgumentException("Invalid nominal quantity or unit value.");
        return RoundMinor(checked(quantity * usdMinorPerUnit));
    }

    public static long ConvertToUsdMinor(NativeMoney native, decimal usdPerNativeUnit)
    {
        native.Validate();
        if (usdPerNativeUnit <= 0) throw new ArgumentOutOfRangeException(nameof(usdPerNativeUnit));
        decimal divisor = native.MinorUnitDigits switch { 0 => 1m, 1 => 10m, 2 => 100m, 3 => 1000m, _ => 10000m };
        return RoundMinor(checked(native.AmountMinor / divisor * usdPerNativeUnit * 100m));
    }

    private static long RoundMinor(decimal amount) => checked((long)decimal.Round(amount, 0, MidpointRounding.AwayFromZero));
}

public static class SupportValuator
{
    public static SupportValuation Value(NativeMoney? native, long quantity, decimal? nominalUsdMinorPerUnit,
        CurrencyRate? rate = null)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (native is not null)
        {
            native.Validate();
            if (native.Currency == "USD") return new(native.AmountMinor, ValuationMethods.Exact, false);
            if (rate is null) return new(null, ValuationMethods.Unknown, false, PendingReason: "fx_unavailable");
            rate.Validate();
            if (rate.Currency != native.Currency) throw new ArgumentException("Rate currency does not match native money.");
            return new(FinancialPrecision.ConvertToUsdMinor(native, rate.UsdPerNativeUnit), ValuationMethods.Fx,
                rate.Estimated, rate.UsdPerNativeUnit, rate.RateDate, rate.Provider);
        }
        return nominalUsdMinorPerUnit is { } nominal
            ? new(FinancialPrecision.NominalUsdMinor(quantity, nominal), ValuationMethods.ConfiguredNominal, true)
            : new(null, ValuationMethods.Unknown, false, PendingReason: "nominal_unconfigured");
    }
}
