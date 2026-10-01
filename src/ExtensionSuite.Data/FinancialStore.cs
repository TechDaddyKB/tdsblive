using System.Globalization;
using System.Numerics;
using System.Text.Json;
using ExtensionSuite.Core;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class FinancialStore(IDbContextFactory<FoundationDbContext> factory)
{
    public async Task<FinancialTotals[]> TotalsAsync(LedgerPeriodRange period, int limit = 100,
        CancellationToken cancellationToken = default)
    {
        period.Validate();
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var events = db.FinancialEvents.AsNoTracking().Where(row => row.AccountingState != "excluded");
        if (period.StartInclusive is { } start) events = events.Where(row => row.OccurredAtTicks >= start.UtcTicks);
        if (period.EndExclusive is { } end) events = events.Where(row => row.OccurredAtTicks < end.UtcTicks);
        var query = from row in events join supporter in db.Supporters.AsNoTracking() on row.SupporterId equals supporter.Id
                    select new { row.SupporterId, supporter.Name, row.UsdAmountMinor, row.ValuationMethod, row.Estimated, row.AccountingState };
        var totals = new Dictionary<Guid, TotalAccumulator>();
        // SQLite SUM can overflow or fall back to floating point. Stream integer rows and sum exactly.
        await foreach (var row in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            if (!totals.TryGetValue(row.SupporterId, out var total))
                totals.Add(row.SupporterId, total = new(row.SupporterId, row.Name));
            total.Count++;
            if (row.AccountingState != "counted") { total.Gated++; continue; }
            if (row.UsdAmountMinor is not { } amount) { total.Unknown++; continue; }
            total.Amount += amount;
            if (row.Estimated) total.Estimated++;
            switch (row.ValuationMethod)
            {
                case ValuationMethods.Exact: total.Exact += amount; break;
                case ValuationMethods.Fx: total.Fx += amount; break;
                case ValuationMethods.ConfiguredNominal: total.Nominal += amount; break;
            }
        }
        return totals.Values.OrderByDescending(row => row.Amount).ThenBy(row => row.Id).Take(limit).Select(row => row.Result()).ToArray();
    }

    public async Task<bool> AcceptAsync(CanonicalEvent item, CurrencyRate? rate = null,
        decimal? nominalUsdMinorPerUnit = null, CancellationToken cancellationToken = default)
    {
        item.Validate();
        if (item.Provenance != EventProvenance.Live || item.Support is not { } support) return false;
        support.Validate();
        // Gift accounting is gated until the correlation strategy establishes purchase evidence.
        var gated = support.GatedReason ?? (support.Kind == "gift" ?
            item.Platform == "rumble" ? "rumble_gift_unverified" : "gift_correlation_pending" : null);
        var recipient = support.GiftRole == "recipient";
        var valuation = gated is not null || recipient
            ? new SupportValuation(null, ValuationMethods.Unknown, false, PendingReason: gated ?? "gift_recipient_notification")
            : SupportValuator.Value(support.NativeMoney, support.Quantity, nominalUsdMinorPerUnit, rate);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.FinancialEvents.AnyAsync(row => row.EventId == item.Id || (row.Platform == item.Platform &&
            (row.DedupeKey == item.DedupeKey || (item.NativeId != null && row.NativeEventId == item.NativeId))), cancellationToken))
            return false;
        // Display names are labels, never identity keys. Unknown givers stay event-scoped.
        var key = item.User?.PlatformUserId is { Length: > 0 } id ? "id:" + id :
            item.User?.Login is { Length: > 0 } login ? "login:" + login.ToLowerInvariant() : "event:" + item.DedupeKey;
        if (key.Length > 512) throw new ArgumentException("Identity key exceeds its supported length.");
        var identity = await db.SupporterIdentities.SingleOrDefaultAsync(row => row.Platform == item.Platform && row.IdentityKey == key, cancellationToken);
        if (identity is null)
        {
            var supporter = new Supporter { Id = Guid.CreateVersion7(), Name = BoundedName(item.User?.DisplayName) };
            identity = new SupporterIdentity { Id = Guid.CreateVersion7(), SupporterId = supporter.Id,
                Platform = item.Platform, IdentityKey = key, DisplayName = supporter.Name };
            db.Supporters.Add(supporter); db.SupporterIdentities.Add(identity);
        }
        db.FinancialEvents.Add(new FinancialContribution
        {
            Id = Guid.CreateVersion7(), EventId = item.Id, SupporterId = identity.SupporterId, IdentityId = identity.Id,
            Source = item.Source, Platform = item.Platform, Type = support.Kind, NativeEventId = item.NativeId,
            DedupeKey = item.DedupeKey, OccurredAtTicks = item.OccurredAt.UtcTicks, Quantity = support.Quantity,
            NativeAmountMinor = support.NativeMoney?.AmountMinor, NativeCurrency = support.NativeMoney?.Currency,
            NativeMinorUnitDigits = support.NativeMoney?.MinorUnitDigits, UsdAmountMinor = valuation.UsdAmountMinor,
            ValuationMethod = valuation.Method, Estimated = valuation.Estimated,
            FxRate = valuation.FxRate?.ToString(CultureInfo.InvariantCulture), FxRateDay = valuation.FxRateDate?.DayNumber,
            FxProvider = valuation.FxProvider, PendingReason = valuation.PendingReason, StreamId = item.Stream?.Id,
            GiftCorrelationKey = support.GiftCorrelationKey, AccountingState = gated is not null ? "gated" : recipient ? "excluded" : "counted",
            MetadataJson = JsonSerializer.Serialize(new { support.Tier, support.GiftRole }), Version = 1
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task LinkIdentityAsync(Guid identityId, Guid supporterId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var identity = await db.SupporterIdentities.SingleOrDefaultAsync(row => row.Id == identityId, cancellationToken)
            ?? throw new KeyNotFoundException("Supporter identity not found.");
        if (!await db.Supporters.AnyAsync(row => row.Id == supporterId, cancellationToken))
            throw new KeyNotFoundException("Supporter not found.");
        if (identity.SupporterId == supporterId) return;
        var previous = identity.SupporterId;
        identity.SupporterId = supporterId;
        await db.FinancialEvents.Where(row => row.IdentityId == identityId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.SupporterId, supporterId), cancellationToken);
        db.FinancialAudits.Add(new FinancialAudit { Id = Guid.CreateVersion7(), CreatedAtTicks = DateTimeOffset.UtcNow.UtcTicks,
            Operation = "identity_link", BeforeJson = JsonSerializer.Serialize(new { identityId, supporterId = previous }),
            AfterJson = JsonSerializer.Serialize(new { identityId, supporterId }) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string BoundedName(string? name) => string.IsNullOrWhiteSpace(name) ? "Unknown supporter" : name[..Math.Min(name.Length, 128)];

    private sealed class TotalAccumulator(Guid id, string name)
    {
        public Guid Id { get; } = id;
        public BigInteger Amount, Exact, Fx, Nominal;
        public long Count, Unknown, Estimated, Gated;
        public FinancialTotals Result() => new(Id, name, Text(Amount), Text(Exact), Text(Fx), Text(Nominal), Count, Unknown, Estimated, Gated);
        private static string Text(BigInteger value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
