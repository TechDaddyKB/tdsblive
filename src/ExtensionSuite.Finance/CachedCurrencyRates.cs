using System.Globalization;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Finance;

public sealed record CachedRateEntry(string Currency, DateOnly RequestedDate, DateOnly RateDate,
    string Origin, string UsdPerNativeUnit, string Provider, bool Estimated);

public sealed class CachedCurrencyRates(ICurrencyRateProvider remote, IDbContextFactory<FoundationDbContext> factory) : ICurrencyRateProvider
{
    public async Task<CachedRateEntry[]> ListAsync(int limit = 500, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.FxRates.AsNoTracking().OrderByDescending(row => row.RequestedDay).ThenBy(row => row.Currency).ThenBy(row => row.Origin)
            .Take(limit).ToArrayAsync(cancellationToken);
        return rows.Select(row => new CachedRateEntry(row.Currency, DateOnly.FromDayNumber(row.RequestedDay), DateOnly.FromDayNumber(row.RateDay),
            row.Origin, row.UsdPerNativeUnit, row.Provider, row.Estimated)).ToArray();
    }

    public async Task<bool> RefreshAsync(string currency, DateOnly date, CancellationToken cancellationToken = default)
    {
        ValidateCurrency(currency);
        if (currency == "USD") return true;
        // Explicit refresh is the only operation allowed to replace a provider observation.
        // Fetch first: failures must preserve both the cache and accepted financial history.
        var rate = await remote.GetRateAsync(currency, date, cancellationToken);
        if (rate is null) return false;
        rate.Validate();
        if (rate.Currency != currency || rate.RequestedDate != date) throw new ArgumentException("Provider returned mismatched rate routing.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var row = await db.FxRates.FindAsync([currency, date.DayNumber, "frankfurter"], cancellationToken);
        var before = row is null ? "null" : JsonSerializer.Serialize(Decode(row));
        if (row is null)
        {
            row = new() { Currency = currency, RequestedDay = date.DayNumber, Origin = "frankfurter",
                RateDay = rate.RateDate.DayNumber, UsdPerNativeUnit = rate.UsdPerNativeUnit.ToString(CultureInfo.InvariantCulture), Provider = rate.Provider, Estimated = rate.Estimated };
            db.FxRates.Add(row);
        }
        else
        {
            row.RateDay = rate.RateDate.DayNumber; row.UsdPerNativeUnit = rate.UsdPerNativeUnit.ToString(CultureInfo.InvariantCulture);
            row.Provider = rate.Provider; row.Estimated = rate.Estimated;
        }
        db.FinancialAudits.Add(Audit("fx_cache_refresh", before, JsonSerializer.Serialize(rate)));
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return true;
    }
    public async Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default)
    {
        ValidateCurrency(currency);
        if (currency == "USD") return new("USD", date, date, 1m, "USD", false);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var manual = await db.FxRates.AsNoTracking().SingleOrDefaultAsync(row => row.Currency == currency && row.RequestedDay == date.DayNumber && row.Origin == "manual", cancellationToken);
        if (manual is not null) return Decode(manual);
        var cached = await db.FxRates.AsNoTracking().SingleOrDefaultAsync(row => row.Currency == currency && row.RequestedDay == date.DayNumber && row.Origin == "frankfurter", cancellationToken);
        if (cached is not null) return Decode(cached);
        var rate = await remote.GetRateAsync(currency, date, cancellationToken);
        if (rate is null) return null;
        rate.Validate();
        if (rate.Currency != currency || rate.RequestedDate != date) throw new ArgumentException("Provider returned mismatched rate routing.");
        var amount = rate.UsdPerNativeUnit.ToString(CultureInfo.InvariantCulture);
        // First successful cached observation wins even when concurrent callers fetch different revisions.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO FxRates (Currency, RequestedDay, Origin, RateDay, UsdPerNativeUnit, Provider, Estimated) VALUES ({currency}, {date.DayNumber}, {"frankfurter"}, {rate.RateDate.DayNumber}, {amount}, {rate.Provider}, {rate.Estimated}) ON CONFLICT(Currency, RequestedDay, Origin) DO NOTHING", cancellationToken);
        // Recheck manual precedence after a network request, which may overlap an operator override.
        var winner = await db.FxRates.AsNoTracking().Where(row => row.Currency == currency && row.RequestedDay == date.DayNumber)
            .OrderByDescending(row => row.Origin == "manual").FirstAsync(cancellationToken);
        return Decode(winner);
    }

    public async Task SetManualAsync(string currency, DateOnly date, decimal usdPerNativeUnit, CancellationToken cancellationToken = default)
    {
        ValidateCurrency(currency);
        var rate = new CurrencyRate(currency, date, date, usdPerNativeUnit, "manual", false); rate.Validate();
        if (currency == "USD" && usdPerNativeUnit != 1m) throw new ArgumentException("USD cannot have a non-identity conversion rate.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stored = await db.FxRates.FindAsync([currency, date.DayNumber, "manual"], cancellationToken);
        var before = stored is null ? "null" : JsonSerializer.Serialize(Decode(stored));
        if (stored is null)
        {
            stored = new StoredFxRate { Currency = currency, RequestedDay = date.DayNumber, Origin = "manual",
                RateDay = date.DayNumber, UsdPerNativeUnit = usdPerNativeUnit.ToString(CultureInfo.InvariantCulture), Provider = "manual" };
            db.FxRates.Add(stored);
        }
        else stored.UsdPerNativeUnit = usdPerNativeUnit.ToString(CultureInfo.InvariantCulture);
        db.FinancialAudits.Add(Audit("fx_override", before, JsonSerializer.Serialize(rate)));
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> RemoveManualAsync(string currency, DateOnly date, CancellationToken cancellationToken = default)
    {
        ValidateCurrency(currency);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stored = await db.FxRates.FindAsync([currency, date.DayNumber, "manual"], cancellationToken);
        if (stored is null) return false;
        db.FxRates.Remove(stored); db.FinancialAudits.Add(Audit("fx_override_remove", JsonSerializer.Serialize(Decode(stored)), "null"));
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return true;
    }

    private static void ValidateCurrency(string currency)
    {
        if (!CurrencyCode.IsValid(currency)) throw new ArgumentException("Invalid currency code.");
    }
    private static CurrencyRate Decode(StoredFxRate row) => new(row.Currency, DateOnly.FromDayNumber(row.RequestedDay),
        DateOnly.FromDayNumber(row.RateDay), decimal.Parse(row.UsdPerNativeUnit, CultureInfo.InvariantCulture), row.Provider, row.Estimated);
    private static FinancialAudit Audit(string operation, string before, string after) => new()
    {
        Id = Guid.CreateVersion7(), CreatedAtTicks = DateTimeOffset.UtcNow.UtcTicks, Operation = operation,
        BeforeJson = before, AfterJson = after
    };
}
