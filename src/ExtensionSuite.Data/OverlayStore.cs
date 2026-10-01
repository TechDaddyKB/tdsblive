using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExtensionSuite.Core;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class OverlayStore(IDbContextFactory<FoundationDbContext> factory, SensitiveValues sensitive, TimeProvider clock)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Overlays.AnyAsync(cancellationToken))
        {
            var overlay = new OverlayDefinition();
            db.Overlays.Add(new() { Id = overlay.Id, Version = overlay.Version, Json = JsonSerializer.Serialize(overlay, EventStore.JsonOptions) });
            await db.SaveChangesAsync(cancellationToken);
        }
        var missing = await db.Overlays.Where(o => !db.OverlayRevisions.Any(r => r.OverlayId == o.Id)).ToArrayAsync(cancellationToken);
        foreach (var item in missing) db.OverlayRevisions.Add(new() { OverlayId = item.Id, Version = item.Version, Json = item.Json, SavedAtTicks = clock.GetUtcNow().UtcTicks });
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<OverlayDefinition[]> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.Overlays.OrderBy(o => o.Id).Select(o => o.Json).ToArrayAsync(ct))
            .Select(j => JsonSerializer.Deserialize<OverlayDefinition>(j, EventStore.JsonOptions)!).ToArray();
    }
    public async Task<bool> CreateAsync(OverlayDefinition value, CancellationToken ct = default)
    {
        value.Validate();
        if (value.Version != 1) throw new ArgumentException("A new overlay starts at version one.");
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Overlays.AnyAsync(o => o.Id == value.Id, ct)) return false;
        var json = JsonSerializer.Serialize(value, EventStore.JsonOptions);
        db.Overlays.Add(new() { Id = value.Id, Version = 1, Json = json });
        db.OverlayRevisions.Add(new() { OverlayId = value.Id, Version = 1, Json = json, SavedAtTicks = clock.GetUtcNow().UtcTicks });
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 }) { return false; }
    }
    public async Task<OverlayRevision[]> RevisionsAsync(string id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.OverlayRevisions.Where(r => r.OverlayId == id).OrderByDescending(r => r.Version).ToArrayAsync(ct);
        return rows.Select(r => new OverlayRevision(r.Version, new(r.SavedAtTicks, TimeSpan.Zero),
            JsonSerializer.Deserialize<OverlayDefinition>(r.Json, EventStore.JsonOptions)!.Name)).ToArray();
    }
    public async Task<OverlayDefinition?> RevisionAsync(string id, int version, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var json = await db.OverlayRevisions.Where(r => r.OverlayId == id && r.Version == version).Select(r => r.Json).SingleOrDefaultAsync(ct);
        return json is null ? null : JsonSerializer.Deserialize<OverlayDefinition>(json, EventStore.JsonOptions);
    }
    public async Task<OverlayDefinition?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!OverlayDefinition.ValidId(id)) return null;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var json = await db.Overlays.Where(o => o.Id == id).Select(o => o.Json).SingleOrDefaultAsync(cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<OverlayDefinition>(json, EventStore.JsonOptions);
    }
    public async Task<bool> SaveAsync(OverlayDefinition value, CancellationToken cancellationToken = default)
    {
        value.Validate();
        var next = value with { Version = value.Version + 1 };
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.Overlays.Where(o => o.Id == value.Id && o.Version == value.Version).ExecuteUpdateAsync(setters => setters
            .SetProperty(o => o.Json, JsonSerializer.Serialize(next, EventStore.JsonOptions)).SetProperty(o => o.Version, next.Version), cancellationToken);
        if (updated != 1) return false;
        db.OverlayRevisions.Add(new() { OverlayId = next.Id, Version = next.Version, Json = JsonSerializer.Serialize(next, EventStore.JsonOptions), SavedAtTicks = clock.GetUtcNow().UtcTicks });
        await db.SaveChangesAsync(cancellationToken);
        var old = await db.OverlayRevisions.Where(r => r.OverlayId == next.Id).OrderByDescending(r => r.Version)
            .Skip(next.RevisionLimit).Select(r => r.Version).ToArrayAsync(cancellationToken);
        if (old.Length > 0) await db.OverlayRevisions.Where(r => r.OverlayId == next.Id && old.Contains(r.Version)).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
    public async Task<CreatedOverlayToken> CreateTokenAsync(string overlayId, int days, CancellationToken cancellationToken)
    {
        if (days is < 1 or > 365) throw new ArgumentException("Invalid token lifetime.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var info = new OverlayTokenInfo(Guid.CreateVersion7(), overlayId, clock.GetUtcNow().AddDays(days), false);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.OverlayTokens.Add(new() { Id = info.Id, OverlayId = overlayId, Hash = Hash(token), ExpiresAtTicks = info.ExpiresAt.UtcTicks });
        await db.SaveChangesAsync(cancellationToken);
        sensitive.Set("overlay-token-" + info.Id, token);
        return new(info, token);
    }
    public async Task<bool> AuthorizeAsync(string token, string overlayId, CancellationToken cancellationToken)
    {
        if (!AssetIdentity.IsValid(token) || !OverlayDefinition.ValidId(overlayId)) return false;
        var hash = Hash(token); var now = clock.GetUtcNow().UtcTicks;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.OverlayTokens.AnyAsync(t => t.OverlayId == overlayId && t.Hash == hash && !t.Revoked && t.ExpiresAtTicks > now, cancellationToken);
    }
    public async Task<OverlayTokenInfo[]> TokensAsync(string overlayId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return (await db.OverlayTokens.Where(t => t.OverlayId == overlayId).ToArrayAsync(cancellationToken))
            .Select(t => new OverlayTokenInfo(t.Id, t.OverlayId, new(t.ExpiresAtTicks, TimeSpan.Zero), t.Revoked)).ToArray();
    }
    public async Task RevokeAsync(string overlayId, Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.OverlayTokens.Where(t => t.OverlayId == overlayId && t.Id == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Revoked, true), cancellationToken);
        sensitive.Remove("overlay-token-" + id);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
