using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Finance;

/// <summary>Independent durable projection; automation outbox acknowledgement never advances this reader.</summary>
public sealed class FinancialProjection(IDbContextFactory<FoundationDbContext> factory,
    FinancialStore ledger, ICurrencyRateProvider rates, ValuationRuleStore rules)
{
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Events.AsNoTracking()
            .Where(row => row.Provenance == nameof(EventProvenance.Live) && row.Type.StartsWith("support.") &&
                !db.FinancialProjectionReceipts.Any(receipt => receipt.EventId == row.Id))
            .OrderBy(row => row.OccurredAtTicks).ThenBy(row => row.Id).Take(32)
            .Select(row => new { row.Id, row.Json }).ToArrayAsync(cancellationToken);
        foreach (var row in rows)
        {
            CanonicalEvent? item;
            try
            {
                item = JsonSerializer.Deserialize<CanonicalEvent>(row.Json, EventStore.JsonOptions);
                if (item is null || item.Id != row.Id) throw new ArgumentException("Stored event identity mismatch.");
                item.Validate();
                item.Support?.Validate();
            }
            catch (Exception error) when (error is JsonException or ArgumentException or OverflowException)
            {
                await ReceiptAsync(db, row.Id, "quarantined", "invalid_typed_event", cancellationToken);
                continue;
            }
            if (item.Provenance != EventProvenance.Live || item.Support is null)
            {
                await ReceiptAsync(db, row.Id, "unsupported", "typed_support_unavailable", cancellationToken);
                continue;
            }
            if (await db.FinancialEvents.AsNoTracking().AnyAsync(existing => existing.EventId == item.Id ||
                (existing.Platform == item.Platform && (existing.DedupeKey == item.DedupeKey ||
                    (item.NativeId != null && existing.NativeEventId == item.NativeId))), cancellationToken))
            {
                await ReceiptAsync(db, row.Id, "processed", null, cancellationToken);
                continue;
            }
            CurrencyRate? rate = null;
            decimal? nominal = null;
            if (item.Support.GatedReason is null)
            {
                if (item.Support.NativeMoney is { Currency: not "USD" } money)
                    rate = await rates.GetRateAsync(money.Currency, DateOnly.FromDateTime(item.OccurredAt.UtcDateTime), cancellationToken);
                else if (item.Support.NativeMoney is null && item.Support.Kind is "bits" or "subscription" or "membership" or "gift")
                    nominal = await rules.FindAsync(item.Platform, item.Support.Kind, item.Support.Tier, cancellationToken);
            }
            // A crash between these writes safely retries against ledger uniqueness. Never turn a storage failure into a receipt.
            try { await ledger.AcceptAsync(item, rate, nominal, cancellationToken); }
            catch (Exception error) when (error is ArgumentException or OverflowException)
            {
                await ReceiptAsync(db, row.Id, "quarantined", "ledger_rejected", cancellationToken);
                continue;
            }
            await ReceiptAsync(db, row.Id, "processed", null, cancellationToken);
        }
        return rows.Length;
    }

    private static async Task ReceiptAsync(FoundationDbContext db, Guid id, string state, string? reason, CancellationToken cancellationToken)
    {
        var ticks = DateTimeOffset.UtcNow.UtcTicks;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO FinancialProjectionReceipts (EventId, State, Reason, ProcessedAtTicks) VALUES ({id}, {state}, {reason}, {ticks}) ON CONFLICT(EventId) DO NOTHING", cancellationToken);
    }
}
