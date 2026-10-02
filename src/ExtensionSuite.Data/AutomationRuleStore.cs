using System.Text.Json;
using ExtensionSuite.Core;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class AutomationRuleStore(IDbContextFactory<FoundationDbContext> factory)
{
    public async Task<AutomationRule[]> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.AutomationRules.OrderBy(rule => rule.Id).Select(rule => rule.Json).ToArrayAsync(ct))
            .Select(json => JsonSerializer.Deserialize<AutomationRule>(json, EventStore.JsonOptions)!).ToArray();
    }

    public async Task<AutomationRule?> SaveAsync(AutomationRule rule, CancellationToken ct = default)
    {
        rule.Validate();
        var next = rule with { Version = checked(rule.Version + 1) };
        await using var db = await factory.CreateDbContextAsync(ct);
        var stored = await db.AutomationRules.SingleOrDefaultAsync(item => item.Id == rule.Id, ct);
        if (stored is null)
        {
            if (rule.Version != 0) return null;
            db.AutomationRules.Add(new() { Id = rule.Id, Version = next.Version, Json = JsonSerializer.Serialize(next, EventStore.JsonOptions) });
        }
        else
        {
            if (stored.Version != rule.Version) return null;
            stored.Version = next.Version;
            stored.Json = JsonSerializer.Serialize(next, EventStore.JsonOptions);
        }
        try { await db.SaveChangesAsync(ct); return next; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 }) { return null; }
    }

    public async Task<bool> DeleteAsync(Guid id, int version, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomationRules.Where(item => item.Id == id && item.Version == version).ExecuteDeleteAsync(ct) == 1;
    }
}
