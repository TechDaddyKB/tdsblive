using System.Security.Cryptography;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Overlays;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Host;

public sealed class AssetStore(ApplicationPaths paths, IDbContextFactory<FoundationDbContext> factory, TimeProvider clock)
{
    private readonly SemaphoreSlim uploads = new(2, 2);
    private readonly SemaphoreSlim writes = new(1, 1);
    public string PathFor(string id) => AssetIdentity.IsValid(id) ? Path.Combine(paths.Root, "assets", id) : throw new ArgumentException("Invalid asset ID.");
    public async Task<AssetInfo?> GetAsync(string id, CancellationToken cancellationToken)
    {
        if (!AssetIdentity.IsValid(id)) return null;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var json = await db.Assets.Where(a => a.Id == id).Select(a => a.Json).SingleOrDefaultAsync(cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<AssetInfo>(json, EventStore.JsonOptions);
    }
    public async Task<AssetInfo[]> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return (await db.Assets.OrderBy(a => a.Id).Take(1000).Select(a => a.Json).ToArrayAsync(cancellationToken))
            .Select(j => JsonSerializer.Deserialize<AssetInfo>(j, EventStore.JsonOptions)!).ToArray();
    }
    public async Task<AssetInfo?> UploadAsync(Stream stream, string filename, string mime, string? license, CancellationToken cancellationToken)
    {
        mime = AssetValidation.NormalizeMime(mime);
        if (string.IsNullOrWhiteSpace(filename) || filename.Length > 128 || filename.IndexOfAny(['/', '\\', ':']) >= 0 || filename.Any(char.IsControl) ||
            license?.Length > 256 || mime.StartsWith("font/", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(license)) throw new ArgumentException("Invalid asset name or font license declaration.");
        if (!await uploads.WaitAsync(0, cancellationToken)) return null;
        try
        {
            using var buffer = new MemoryStream(); var chunk = new byte[65536]; int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > AssetValidation.MaximumBytes) throw new ArgumentException("Asset exceeds size limit.");
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
            var validated = AssetValidation.Validate(buffer.ToArray(), mime);
            var hash = Convert.ToHexString(SHA256.HashData(validated.Bytes)).ToLowerInvariant();
            await writes.WaitAsync(cancellationToken);
            try
            {
                var existing = await GetAsync(hash, cancellationToken);
                if (existing is not null) return existing;
                var info = new AssetInfo(hash, filename, mime, validated.Bytes.LongLength, hash, clock.GetUtcNow(), validated.Sanitized, license);
                Directory.CreateDirectory(Path.Combine(paths.Root, "assets"));
                // Orphaned content from an interrupted metadata commit is safe to replace by its hash.
                var temporary = PathFor(hash) + ".tmp";
                await File.WriteAllBytesAsync(temporary, validated.Bytes, cancellationToken);
                File.Move(temporary, PathFor(hash), true);
                await using var db = await factory.CreateDbContextAsync(cancellationToken);
                db.Assets.Add(new() { Id = hash, Json = JsonSerializer.Serialize(info, EventStore.JsonOptions) });
                await db.SaveChangesAsync(cancellationToken);
                return info;
            }
            finally { writes.Release(); }
        }
        finally { uploads.Release(); }
    }
}
