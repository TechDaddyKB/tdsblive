using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class AutomationTemporaryStore(IDbContextFactory<FoundationDbContext> factory, TimeProvider clock)
{
    public async Task<bool> SaveQueuedAsync(AutomationTemporaryEffect effect, Guid executionId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var stored = await db.AutomationTemporaryEffects.SingleOrDefaultAsync(item => item.ActionId == effect.ActionId, ct);
        var receipt = await db.AutomationExecutions.SingleOrDefaultAsync(item => item.Id == executionId, ct);
        if (stored is null || stored.Version != effect.Version || stored.State != "active" || receipt?.State != "dispatching" || receipt.CancelRequested || receipt.ActionId != effect.ActionId) return false;
        stored.Version++; stored.Json = effect.Json; stored.ExpiresAtTicks = effect.ExpiresAtTicks;
        receipt.Version++; receipt.State = "waiting-effect"; receipt.Detail = "temporary-queue-accepted";
        // One SaveChanges transaction couples queue membership to the original receipt.
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<AutomationTemporaryEffect[]> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomationTemporaryEffects.AsNoTracking().OrderBy(item => item.ActionId).Take(500).ToArrayAsync(ct);
    }

    public async Task<bool> ResolveRestoredAsync(Guid actionId, int version, string json, Guid ruleId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Resolution requires a retained effect payload.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var effect = await db.AutomationTemporaryEffects.SingleOrDefaultAsync(item => item.ActionId == actionId && item.Version == version && item.State == "uncertain", ct);
        if (effect is null) return false;
        effect.State = "idle"; effect.Json = json; effect.Version++;
        foreach (var receipt in await db.AutomationExecutions.Where(item => item.ActionId == actionId && item.RuleId == ruleId && item.State == "waiting-effect").ToArrayAsync(ct))
        {
            receipt.State = "cancelled"; receipt.Detail = "operator-restored-queue-discarded"; receipt.Version++;
        }
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<AutomationTemporaryEffect?> GetAsync(Guid actionId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomationTemporaryEffects.AsNoTracking().SingleOrDefaultAsync(item => item.ActionId == actionId, ct);
    }

    public async Task<bool> SaveAsync(AutomationTemporaryEffect effect, CancellationToken ct = default)
    {
        if (effect.ActionId == Guid.Empty || effect.Version < 0 || effect.ExpiresAtTicks <= 0 ||
            effect.State is not ("enabling" or "active" or "reverting" or "uncertain" or "idle") || string.IsNullOrWhiteSpace(effect.Json))
            throw new ArgumentException("Invalid temporary effect record.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var stored = await db.AutomationTemporaryEffects.SingleOrDefaultAsync(item => item.ActionId == effect.ActionId, ct);
        if (stored is null)
        {
            if (effect.Version != 0) return false;
            db.AutomationTemporaryEffects.Add(new() { ActionId = effect.ActionId, Version = 1,
                ExpiresAtTicks = effect.ExpiresAtTicks, State = effect.State, Json = effect.Json });
        }
        else
        {
            if (stored.Version != effect.Version) return false;
            stored.Version = checked(effect.Version + 1); stored.ExpiresAtTicks = effect.ExpiresAtTicks;
            stored.State = effect.State; stored.Json = effect.Json;
        }
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 }) { return false; }
    }

    public async Task<AutomationTemporaryEffect[]> DueAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcTicks;
        return await db.AutomationTemporaryEffects.AsNoTracking().Where(item => item.State == "active" && item.ExpiresAtTicks <= now)
            .OrderBy(item => item.ExpiresAtTicks).Take(100).ToArrayAsync(ct);
    }

    public async Task<int> RecoverInterruptedAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomationTemporaryEffects.Where(item => item.State == "enabling" || item.State == "reverting")
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, "uncertain")
                .SetProperty(item => item.Version, item => item.Version + 1), ct);
    }
}
