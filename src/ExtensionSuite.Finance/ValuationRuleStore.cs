using System.Globalization;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Finance;

public sealed record NominalValuationRule(string Platform, string Type, string Tier, string UsdMinorPerUnit, int Version, bool Enabled = true);

public sealed class ValuationRuleStore(IDbContextFactory<FoundationDbContext> factory)
{
    public async Task<decimal?> FindAsync(string platform, string type, string tier, CancellationToken cancellationToken = default)
    {
        ValidateKey(platform, type, tier);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = await db.ValuationRules.AsNoTracking().SingleOrDefaultAsync(item => item.Platform == platform && item.Type == type && item.Tier == tier, cancellationToken);
        return row is null || !row.Enabled ? null : decimal.Parse(row.UsdMinorPerUnit, CultureInfo.InvariantCulture);
    }

    public async Task<NominalValuationRule[]> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.ValuationRules.AsNoTracking().OrderBy(row => row.Platform).ThenBy(row => row.Type).ThenBy(row => row.Tier)
            .Select(row => new NominalValuationRule(row.Platform, row.Type, row.Tier, row.UsdMinorPerUnit, row.Version, row.Enabled)).ToArrayAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(string platform, string type, string tier, int expectedVersion, CancellationToken cancellationToken = default)
    {
        ValidateKey(platform, type, tier);
        if (expectedVersion < 1 || expectedVersion == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var row = await db.ValuationRules.AsNoTracking().SingleOrDefaultAsync(item => item.Platform == platform && item.Type == type && item.Tier == tier, cancellationToken);
        if (row is null || row.Version != expectedVersion) return false;
        if (!row.Enabled) return true;
        var before = new NominalValuationRule(platform, type, tier, row.UsdMinorPerUnit, row.Version, true);
        var changed = await db.ValuationRules.Where(item => item.Platform == platform && item.Type == type && item.Tier == tier && item.Version == expectedVersion)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Enabled, false).SetProperty(item => item.Version, expectedVersion + 1), cancellationToken);
        if (changed != 1) return false;
        db.FinancialAudits.Add(new FinancialAudit { Id = Guid.CreateVersion7(), CreatedAtTicks = DateTimeOffset.UtcNow.UtcTicks,
            Operation = "nominal_rule_remove", BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(before with { Enabled = false, Version = expectedVersion + 1 }) });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return true;
    }

    public async Task<bool> SetAsync(string platform, string type, string tier, decimal usdMinorPerUnit,
        int expectedVersion, CancellationToken cancellationToken = default)
    {
        ValidateKey(platform, type, tier);
        _ = FinancialPrecision.NominalUsdMinor(1, usdMinorPerUnit);
        if (expectedVersion < 0 || expectedVersion == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var row = await db.ValuationRules.FindAsync([platform, type, tier], cancellationToken);
        if ((row?.Version ?? 0) != expectedVersion) return false;
        var before = row is null ? "null" : JsonSerializer.Serialize(new NominalValuationRule(platform, type, tier, row.UsdMinorPerUnit, row.Version, row.Enabled));
        var text = usdMinorPerUnit.ToString(CultureInfo.InvariantCulture);
        if (row is null)
        {
            row = new StoredValuationRule { Platform = platform, Type = type, Tier = tier, UsdMinorPerUnit = text, Version = 1 };
            db.ValuationRules.Add(row);
        }
        else
        {
            var changed = await db.ValuationRules.Where(item => item.Platform == platform && item.Type == type && item.Tier == tier && item.Version == expectedVersion)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsdMinorPerUnit, text).SetProperty(item => item.Enabled, true)
                    .SetProperty(item => item.Version, expectedVersion + 1), cancellationToken);
            if (changed != 1) return false;
            row.UsdMinorPerUnit = text; row.Version = expectedVersion + 1; row.Enabled = true;
            db.Entry(row).State = EntityState.Unchanged;
        }
        db.FinancialAudits.Add(new FinancialAudit { Id = Guid.CreateVersion7(), CreatedAtTicks = DateTimeOffset.UtcNow.UtcTicks,
            Operation = "nominal_rule", BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new NominalValuationRule(platform, type, tier, text, row.Version)) });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return true;
    }

    private static void ValidateKey(string platform, string type, string tier)
    {
        if (string.IsNullOrWhiteSpace(platform) || platform.Length > 64 || platform.Any(char.IsControl) ||
            !new[] { "bits", "subscription", "membership", "gift" }.Contains(type) || tier is null || tier.Length > 64 || tier.Any(char.IsControl))
            throw new ArgumentException("Invalid nominal valuation rule key.");
    }
}
