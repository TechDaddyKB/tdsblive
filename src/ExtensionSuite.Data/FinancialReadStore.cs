using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed record FinancialEntry(Guid Id, Guid EventId, Guid SupporterId, Guid IdentityId,
    string SupporterName, string Platform, string Type, string Source, string? NativeEventId,
    DateTimeOffset OccurredAt, string Quantity, string? NativeAmountMinor, string? NativeCurrency, int? NativeMinorUnitDigits,
    string? UsdAmountMinor, string ValuationMethod, bool Estimated, string? FxRate, DateOnly? FxRateDate,
    string? FxProvider, string AccountingState, string? PendingReason, string Tier, string GiftRole, string MetadataJson, int Version);
public sealed record FinancialIdentity(Guid Id, Guid SupporterId, string SupporterName, string Platform, string IdentityKey, string DisplayName);
public sealed record FinancialLedgerPage(FinancialEntry[] Items, int TotalCount, int Offset, int Limit);
public sealed record FinancialProjectionDiagnostics(int Processed, int Unsupported, int Quarantined, int Pending,
    int PendingConversions, int GatedContributions);

public sealed class FinancialReadStore(IDbContextFactory<FoundationDbContext> factory)
{
    public async Task<FinancialLedgerPage> EntriesAsync(int offset = 0, int limit = 100, string state = "all",
        string? platform = null, CancellationToken cancellationToken = default)
    {
        if (offset is < 0 or > 1000000 || limit is < 1 or > 500 || platform?.Length > 64 ||
            !new[] { "all", "counted", "excluded", "gated", "pending" }.Contains(state)) throw new ArgumentException("Invalid ledger query.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var events = db.FinancialEvents.AsNoTracking();
        if (platform is not null) events = events.Where(row => row.Platform == platform);
        if (state == "pending") events = events.Where(row => row.AccountingState == "counted" && row.UsdAmountMinor == null);
        else if (state != "all") events = events.Where(row => row.AccountingState == state);
        var total = await events.CountAsync(cancellationToken);
        var rows = await (from row in events join supporter in db.Supporters.AsNoTracking() on row.SupporterId equals supporter.Id
                          orderby row.OccurredAtTicks descending, row.Id descending
                          select new { Row = row, supporter.Name }).Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        return new(rows.Select(item => Entry(item.Row, item.Name)).ToArray(), total, offset, limit);
    }

    public async Task<FinancialIdentity[]> IdentitiesAsync(string? search = null, int limit = 500, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000 || search?.Length > 128) throw new ArgumentException("Invalid identity query.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = from identity in db.SupporterIdentities.AsNoTracking() join supporter in db.Supporters.AsNoTracking()
                    on identity.SupporterId equals supporter.Id select new { Identity = identity, supporter.Name };
        if (!string.IsNullOrEmpty(search)) query = query.Where(row => row.Identity.DisplayName.Contains(search) || row.Name.Contains(search) || row.Identity.IdentityKey.Contains(search));
        return await query.OrderBy(row => row.Identity.Platform).ThenBy(row => row.Identity.DisplayName).ThenBy(row => row.Identity.Id).Take(limit)
            .Select(row => new FinancialIdentity(row.Identity.Id, row.Identity.SupporterId, row.Name, row.Identity.Platform, row.Identity.IdentityKey, row.Identity.DisplayName))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<FinancialProjectionDiagnostics> DiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var counts = await db.FinancialProjectionReceipts.AsNoTracking().GroupBy(row => row.State)
            .Select(group => new { State = group.Key, Count = group.Count() }).ToDictionaryAsync(row => row.State, row => row.Count, cancellationToken);
        return new(counts.GetValueOrDefault("processed"), counts.GetValueOrDefault("unsupported"), counts.GetValueOrDefault("quarantined"),
            await db.Events.CountAsync(row => row.Provenance == "Live" && row.Type.StartsWith("support.") &&
                !db.FinancialProjectionReceipts.Any(receipt => receipt.EventId == row.Id), cancellationToken),
            await db.FinancialEvents.CountAsync(row => row.AccountingState == "counted" && row.PendingReason == "fx_unavailable", cancellationToken),
            await db.FinancialEvents.CountAsync(row => row.AccountingState == "gated", cancellationToken));
    }

    private static FinancialEntry Entry(FinancialContribution row, string name) => new(row.Id, row.EventId, row.SupporterId, row.IdentityId,
        name, row.Platform, row.Type, row.Source, row.NativeEventId, new(row.OccurredAtTicks, TimeSpan.Zero), Text(row.Quantity)!,
        Text(row.NativeAmountMinor), row.NativeCurrency, row.NativeMinorUnitDigits, Text(row.UsdAmountMinor), row.ValuationMethod,
        row.Estimated, row.FxRate, row.FxRateDay is { } day ? DateOnly.FromDayNumber(day) : null, row.FxProvider,
        row.AccountingState, row.PendingReason, row.GiftTier, row.GiftRole, row.MetadataJson, row.Version);
    private static string? Text(long? value) => value?.ToString(CultureInfo.InvariantCulture);
}
