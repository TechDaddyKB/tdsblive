using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Overlays;

namespace ExtensionSuite.Host;

public sealed record PortableManifest(int FormatVersion, string Kind, string PackageVersion, string Author, PortableAsset[] Assets);
public sealed record PortableAsset(string Id, string Filename, string Mime, string? License);
public sealed record PreparedPortablePackage(OverlayDefinition? Overlay, OverlayWidget[] Widgets, PortableManifest Manifest, Dictionary<string, byte[]> Assets);

public sealed class PortablePackages(OverlayStore overlays, AssetStore assets, SensitiveValues sensitive)
{
    public const int MaximumArchiveBytes = 32 * 1024 * 1024;
    public const int MaximumExpandedBytes = 64 * 1024 * 1024;
    public static IEnumerable<string> AssetIds(OverlayDefinition overlay) => new[] { overlay.Chat.FontAssetId }
        .Concat(overlay.Widgets.SelectMany(w => new[] { w.AssetId, w.Chat.FontAssetId, w.Donor.FontAssetId, w.Donor.CrownAssetId, w.Alert.MediaAssetId, w.Alert.SoundAssetId }.Concat(w.Custom.AssetIds)))
        .OfType<string>().Distinct(StringComparer.Ordinal);

    public async Task<byte[]?> ExportAsync(string id, string? widgetId, CancellationToken ct)
    {
        var overlay = await overlays.GetAsync(id, ct); if (overlay is null) return null;
        var widget = widgetId is null ? null : overlay.Widgets.SingleOrDefault(w => w.Id == widgetId);
        if (widgetId is not null && widget is null) return null;
        var package = widget is null ? overlay : overlay with { Widgets = [widget], Chat = new(), AlertSets = [] };
        var data = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var metadata = new List<PortableAsset>();
        foreach (var assetId in AssetIds(package))
        {
            var info = await assets.GetAsync(assetId, ct) ?? throw new ArgumentException("Missing referenced asset.");
            metadata.Add(new(info.Id, info.Filename, info.Mime, info.License));
            var bytes = await File.ReadAllBytesAsync(assets.PathFor(assetId), ct);
            if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != info.Id) throw new ArgumentException("Asset hash mismatch.");
            data.Add("assets/" + info.Id, bytes);
        }
        foreach (var item in package.Widgets) data.Add("widgets/" + item.Id + ".json", SafeJson(item));
        if (widget is null) data.Add("overlay.json", SafeJson(package));
        var custom = widget?.Custom;
        data.Add("manifest.json", SafeJson(CreateManifest(package, widget, custom, metadata)));
        if (data.Sum(d => (long)d.Value.Length) > MaximumExpandedBytes || data.Count > 512) throw new ArgumentException("Package exceeds limits.");
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var pair in data)
            {
                var entry = zip.CreateEntry(pair.Key, CompressionLevel.Fastest);
                await using var stream = entry.Open(); await stream.WriteAsync(pair.Value, ct);
            }
        if (output.Length > MaximumArchiveBytes) throw new ArgumentException("Package exceeds limits.");
        return output.ToArray();
    }

    private static PortableManifest CreateManifest(OverlayDefinition package, OverlayWidget? widget, CustomWidgetSettings? custom, List<PortableAsset> metadata) =>
        new(AlertMatching.RequiresV2(package) ? 2 : 1, widget is null ? "overlay" : "widget", custom?.PackageVersion ?? "1.0.0", custom?.Author ?? "", metadata.ToArray());

    private byte[] SafeJson<T>(T value)
    {
        var node = JsonSerializer.SerializeToNode(value, EventStore.JsonOptions)!;
        var clean = CredentialRedactor.Json(node.DeepClone(), sensitive.Snapshot());
        if (!JsonNode.DeepEquals(node, clean) || HasFilesystemPath(node)) throw new ArgumentException("Remove credentials or absolute filesystem paths before exporting.");
        return JsonSerializer.SerializeToUtf8Bytes(value, EventStore.JsonOptions);
    }
    private static bool HasFilesystemPath(JsonNode? node) => node switch
    {
        JsonValue value when value.TryGetValue<string>(out var text) => text.StartsWith("/", StringComparison.Ordinal) && !text.StartsWith("</", StringComparison.Ordinal) && !text.StartsWith("//", StringComparison.Ordinal) && !text.StartsWith("/*", StringComparison.Ordinal) || text.StartsWith("\\\\", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z]:[\\/]", System.Text.RegularExpressions.RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100)),
        JsonObject obj => obj.Any(pair => HasFilesystemPath(pair.Value)),
        JsonArray array => array.Any(HasFilesystemPath), _ => false
    };

    // Validate every entry and asset before admitting any imported objects. No
    // archive member is ever extracted to a path supplied by the archive.
    public PreparedPortablePackage Validate(byte[] bytes)
    {
        if (bytes.Length is < 22 or > MaximumArchiveBytes) throw new ArgumentException("Invalid package size.");
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (zip.Entries.Count is < 2 or > 512) throw new ArgumentException("Invalid package entry count.");
        long total = 0; var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;
            if (name.Contains('\\') || name.Contains(':') || name.StartsWith('/') || name.Split('/').Any(p => p is "" or "." or "..") ||
                (entry.ExternalAttributes >> 16 & 0xf000) == 0xa000 || entry.Length > AssetValidation.MaximumBytes || !files.TryAdd(name, []))
                throw new ArgumentException("Unsafe archive entry.");
            total += entry.Length; if (total > MaximumExpandedBytes) throw new ArgumentException("Package decompression limit exceeded.");
            using var stream = entry.Open(); using var output = new MemoryStream(); var buffer = new byte[65536]; int count;
            while ((count = stream.Read(buffer)) > 0)
            {
                if (output.Length + count > entry.Length) throw new ArgumentException("Invalid expanded size.");
                output.Write(buffer, 0, count);
            }
            if (output.Length != entry.Length) throw new ArgumentException("Truncated entry.");
            files[name] = output.ToArray();
        }
        var manifest = Read<PortableManifest>(files, "manifest.json");
        if (manifest.FormatVersion is not (1 or 2) || manifest.Kind is not ("overlay" or "widget") || manifest.Assets is null || manifest.Assets.Length > 100 ||
            manifest.Author is null || manifest.Author.Length > 128 || !System.Text.RegularExpressions.Regex.IsMatch(manifest.PackageVersion ?? "", @"^\d{1,6}\.\d{1,6}\.\d{1,6}\z", System.Text.RegularExpressions.RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100))) throw new ArgumentException("Unsupported package manifest.");
        var widgets = files.Keys.Where(k => k.StartsWith("widgets/", StringComparison.Ordinal)).Select(k => Read<OverlayWidget>(files, k)).ToArray();
        if (widgets.Length > 100 || manifest.Kind == "widget" && widgets.Length != 1) throw new ArgumentException("Invalid widget count.");
        foreach (var widget in widgets) { widget.Validate(); if (!files.ContainsKey("widgets/" + widget.Id + ".json")) throw new ArgumentException("Widget identity mismatch."); }
        if (widgets.Select(w => w.Id).Distinct().Count() != widgets.Length) throw new ArgumentException("Duplicate widgets.");
        OverlayDefinition? overlay = null;
        if (manifest.Kind == "overlay")
        {
            overlay = Read<OverlayDefinition>(files, "overlay.json"); overlay.Validate();
            if (overlay.Widgets.Length != widgets.Length || overlay.Widgets.Any(w => !widgets.Any(x => x.Id == w.Id && JsonSerializer.Serialize(x, EventStore.JsonOptions) == JsonSerializer.Serialize(w, EventStore.JsonOptions))))
                throw new ArgumentException("Overlay and widget definitions disagree.");
        }
        if (manifest.FormatVersion == 1 && AlertMatching.RequiresV2(overlay ?? new OverlayDefinition { Widgets = widgets })) throw new ArgumentException("Alert rules require package format v2.");
        var content = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var meta in manifest.Assets)
        {
            if (!AssetIdentity.IsValid(meta.Id) || !files.TryGetValue("assets/" + meta.Id, out var asset) || !content.TryAdd(meta.Id, asset) ||
                meta.Filename is null || meta.Filename.Length is < 1 or > 128 || meta.Filename.IndexOfAny(['/', '\\', ':']) >= 0 || meta.Filename.Any(char.IsControl) ||
                meta.Mime is null || meta.License?.Length > 256 || meta.Mime.StartsWith("font/", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(meta.License)) throw new ArgumentException("Invalid asset metadata.");
            var validated = AssetValidation.Validate(asset, meta.Mime);
            if (!validated.Bytes.AsSpan().SequenceEqual(asset) || Convert.ToHexString(SHA256.HashData(asset)).ToLowerInvariant() != meta.Id) throw new ArgumentException("Asset integrity mismatch.");
        }
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "manifest.json" };
        if (overlay is not null) expected.Add("overlay.json");
        foreach (var widget in widgets) expected.Add("widgets/" + widget.Id + ".json");
        foreach (var asset in manifest.Assets) expected.Add("assets/" + asset.Id);
        if (expected.Count != files.Count || files.Keys.Any(k => !expected.Contains(k))) throw new ArgumentException("Unexpected package entries.");
        var references = AssetIds(overlay ?? new OverlayDefinition { Widgets = widgets });
        if (references.Any(id => !content.ContainsKey(id)) || content.Keys.Any(id => !references.Contains(id))) throw new ArgumentException("Unresolved or unreferenced assets.");
        // Importing code never grants permissions. The operator reviews and opts
        // into each capability after import; private widget state is not portable.
        var imported = widgets.Select(w => w with { Custom = w.Custom with { Permissions = [], NetworkDomains = [] } }).ToArray();
        return new(overlay is null ? null : overlay with { Widgets = imported }, imported, manifest, content);
    }
    private T Read<T>(Dictionary<string, byte[]> files, string name)
    {
        if (!files.TryGetValue(name, out var bytes) || bytes.Length > 2 * 1024 * 1024) throw new ArgumentException("Missing or oversized JSON member.");
        var node = JsonNode.Parse(bytes) ?? throw new ArgumentException("Invalid JSON.");
        if (!JsonNode.DeepEquals(node, CredentialRedactor.Json(node.DeepClone(), sensitive.Snapshot())) || HasFilesystemPath(node)) throw new ArgumentException("Package contains credentials or filesystem paths.");
        return node.Deserialize<T>(EventStore.JsonOptions) ?? throw new ArgumentException("Invalid package member.");
    }
    public async Task<OverlayDefinition> ImportAsync(PreparedPortablePackage package, string? targetId, EditorEventHub hub, CancellationToken ct)
    {
        var target = targetId is null ? null : await overlays.GetAsync(targetId, ct);
        if (package.Manifest.Kind == "widget" && (target is null || target.Widgets.Length >= 100)) throw new ArgumentException("Select an overlay with space for another widget.");
        foreach (var asset in package.Manifest.Assets)
            if (await assets.UploadAsync(new MemoryStream(package.Assets[asset.Id]), asset.Filename, asset.Mime, asset.License, ct) is null) throw new ArgumentException("Asset import is busy; retry.");
        var groups = new Dictionary<string, string>(StringComparer.Ordinal);
        var widgets = package.Widgets.Select(w => w with { Id = Guid.CreateVersion7().ToString(), GroupId = w.GroupId is { } group ?
            groups.TryGetValue(group, out var mapped) ? mapped : groups[group] = Guid.CreateVersion7().ToString() : null }).ToArray();
        if (package.Overlay is { } source)
        {
            var identities = package.Widgets.Select((w, i) => (w.Id, NewId: widgets[i].Id)).ToDictionary(p => p.Id, p => p.NewId);
            var imported = source with { Id = "import-" + Guid.CreateVersion7().ToString("N"), Version = 1, Widgets = widgets,
                AlertSets = source.AlertSets.Select(s => s with { Id = Guid.CreateVersion7().ToString(), WidgetIds = s.WidgetIds.Select(id => identities[id]).ToArray() }).ToArray() };
            if (!await overlays.CreateAsync(imported, ct)) throw new ArgumentException("Import identity conflict.");
            return imported;
        }
        var next = target! with { Widgets = [.. target!.Widgets, .. widgets], CanvasEnabled = true };
        if (!await overlays.SaveAsync(next, ct)) throw new ArgumentException("Overlay changed; retry import.");
        var saved = (await overlays.GetAsync(next.Id, ct))!; hub.UpdateOverlay(saved); return saved;
    }
}
