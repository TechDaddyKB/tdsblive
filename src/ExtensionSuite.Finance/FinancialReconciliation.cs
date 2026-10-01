using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Finance;

public sealed record FinancialVersionRef(Guid Id, int Version);
public sealed record FinancialReconcileResult(Guid Id, string Outcome, int? Version = null);

public sealed class FinancialReconciliation(IDbContextFactory<FoundationDbContext> factory, FinancialStore ledger,
    ICurrencyRateProvider rates, ValuationRuleStore rules)
{
    public async Task<FinancialReconcileResult[]> ReconcileAsync(FinancialVersionRef[] selected, CancellationToken cancellationToken = default)
    {
        if (selected is null || selected.Length is < 1 or > 100 ||
            selected.Any(row => row is null || row.Id == Guid.Empty || row.Version < 1 || row.Version == int.MaxValue) ||
            selected.Select(row => row.Id).Distinct().Count() != selected.Length) throw new ArgumentException("Select 1–100 distinct versioned ledger entries.");
        var results = new List<FinancialReconcileResult>();
        foreach (var selection in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var row = await db.FinancialEvents.AsNoTracking().SingleOrDefaultAsync(row => row.Id == selection.Id, cancellationToken);
            if (row is null) { results.Add(new(selection.Id, "not_found")); continue; }
            if (row.Version != selection.Version) { results.Add(new(selection.Id, "conflict", row.Version)); continue; }
            if (row.AccountingState != "counted") { results.Add(new(selection.Id, "gated", row.Version)); continue; }
            CurrencyRate? rate = null; decimal? nominal = null;
            if (row.NativeCurrency is { } currency && currency != "USD")
                rate = await rates.GetRateAsync(currency, DateOnly.FromDateTime(new DateTimeOffset(row.OccurredAtTicks, TimeSpan.Zero).UtcDateTime), cancellationToken);
            else if (row.NativeAmountMinor is null && row.Type is "bits" or "subscription" or "membership" or "gift")
                nominal = await rules.FindAsync(row.Platform, row.Type, row.GiftTier, cancellationToken);
            try
            {
                if (!await ledger.ReconcileAsync(row.Id, selection.Version, rate, nominal, cancellationToken))
                { results.Add(new(row.Id, "conflict")); continue; }
            }
            catch (OverflowException) { results.Add(new(row.Id, "out_of_range", row.Version)); continue; }
            var after = await db.FinancialEvents.AsNoTracking().SingleAsync(entry => entry.Id == row.Id, cancellationToken);
            results.Add(new(row.Id, after.UsdAmountMinor is null ? "pending" : after.Version == row.Version ? "unchanged" : "reconciled", after.Version));
        }
        return results.ToArray();
    }
}
