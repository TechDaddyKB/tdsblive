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
        if (await db.Overlays.AnyAsync(cancellationToken)) return;
        var overlay = new OverlayDefinition();
        db.Overlays.Add(new() { Id = overlay.Id, Version = overlay.Version, Json = JsonSerializer.Serialize(overlay, EventStore.JsonOptions) });
        await db.SaveChangesAsync(cancellationToken);
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
        return await db.Overlays.Where(o => o.Id == value.Id && o.Version == value.Version).ExecuteUpdateAsync(setters => setters
            .SetProperty(o => o.Json, JsonSerializer.Serialize(next, EventStore.JsonOptions)).SetProperty(o => o.Version, next.Version), cancellationToken) == 1;
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
