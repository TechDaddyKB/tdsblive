using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ExtensionSuite.StreamerBot;

namespace ExtensionSuite.Host;

public static class OverlayEndpoints
{
    public static void MapOverlayEndpoints(this WebApplication app)
    {
        app.MapGet("/api/overlays", async (OverlayStore store, CancellationToken ct) => TypedResults.Ok(await store.ListAsync(ct)));
        app.MapPost("/api/overlays", async (OverlayDefinition value, OverlayStore store, AssetStore assets, SensitiveValues sensitive, CancellationToken ct) =>
        {
            if (value.Version != 1) return Results.BadRequest();
            try { value.Validate(); } catch (ArgumentException) { return Results.BadRequest(); }
            if (!await ValidAssetsAsync(value, assets, sensitive, ct)) return Results.BadRequest();
            return await store.CreateAsync(value, ct) ? Results.Created($"/api/overlays/{value.Id}", value) : Results.Conflict();
        }).Produces<OverlayDefinition>(201);
        app.MapGet("/api/overlays/{id}/revisions", async (string id, OverlayStore store, CancellationToken ct) =>
            await store.GetAsync(id, ct) is null ? Results.NotFound() : Results.Ok(await store.RevisionsAsync(id, ct))).Produces<OverlayRevision[]>();
        app.MapPost("/api/overlays/{id}/revisions/{version:int}/restore", async (string id, int version, RestoreOverlayRevision request,
            OverlayStore store, AssetStore assets, SensitiveValues sensitive, EditorEventHub hub, CancellationToken ct) =>
        {
            var saved = await store.RevisionAsync(id, version, ct);
            if (saved is null) return Results.NotFound();
            var restored = saved with { Version = request.ExpectedVersion };
            try { restored.Validate(); } catch (ArgumentException) { return Results.BadRequest(); }
            if (!await ValidAssetsAsync(restored, assets, sensitive, ct)) return Results.BadRequest();
            if (!await store.SaveAsync(restored, ct)) return Results.Conflict();
            var updated = (await store.GetAsync(id, ct))!; hub.UpdateOverlay(updated); return Results.Ok(updated);
        }).Produces<OverlayDefinition>();
        app.MapPost("/api/overlays/{id}/preview-events", async (string id, PreviewEventRequest request, OverlayStore store,
            EditorEventHub hub, SensitiveValues sensitive, StreamerBotEventNormalizer normalizer, TimeProvider clock, CancellationToken ct) =>
        {
            if (await store.GetAsync(id, ct) is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Type) || request.Type.Length > 128 || string.IsNullOrWhiteSpace(request.Platform) ||
                request.Platform.Length > 64 || request.User.Length > 128 || request.Message.Length > 4096) return Results.BadRequest();
            var now = clock.GetUtcNow();
            var item = new CanonicalEvent { Source = "overlay-preview", Platform = request.Platform, Type = request.Type,
                NativeType = "SyntheticPreview", OccurredAt = now, ReceivedAt = now, Provenance = EventProvenance.Simulation,
                DedupeKey = Guid.CreateVersion7().ToString(), User = new(DisplayName: request.User), Message = new(request.Message), Raw = request.Raw };
            if (request.Mode == "native")
            {
                if (request.Raw is null || normalizer.Normalize(request.Raw, now).Event is not { } normalized) return Results.BadRequest();
                item = normalized with { Id = Guid.CreateVersion7(), Source = "overlay-preview", OccurredAt = now, ReceivedAt = now,
                    Provenance = EventProvenance.Simulation, DedupeKey = Guid.CreateVersion7().ToString(), BridgePath = [] };
            }
            else if (request.Mode != "synthetic") return Results.BadRequest();
            var clean = CredentialRedactor.Json(System.Text.Json.JsonSerializer.SerializeToNode(item, EventStore.JsonOptions), sensitive.Snapshot());
            item = clean!.Deserialize<CanonicalEvent>(EventStore.JsonOptions)!;
            hub.PublishPreview(id, item);
            return Results.Ok(new { id = item.Id, provenance = "simulation", persisted = false, liveActionsAllowed = false });
        });
        app.MapGet("/overlay/{id}", async (string id, OverlayStore store, CancellationToken ct) =>
            await store.GetAsync(id, ct) is null ? Results.NotFound() : Shell(app));
        app.MapGet("/chat/{id}", async (string id, OverlayStore store, CancellationToken ct) =>
            await store.GetAsync(id, ct) is null ? Results.NotFound() : Shell(app));
        app.MapGet("/api/overlays/{id}", async (string id, OverlayStore store, CancellationToken ct) =>
            await store.GetAsync(id, ct) is { } value ? Results.Ok(value) : Results.NotFound()).Produces<OverlayDefinition>();
        app.MapPut("/api/overlays/{id}", async (string id, [FromBody] OverlayDefinition value, OverlayStore store, AssetStore assets, SensitiveValues sensitive, EditorEventHub hub, CancellationToken ct) =>
        {
            if (value.Id != id) return Results.BadRequest();
            try { value.Validate(); } catch (ArgumentException) { return Results.BadRequest(); }
            if (!await ValidAssetsAsync(value, assets, sensitive, ct)) return Results.BadRequest();
            if (!await store.SaveAsync(value, ct)) return Results.Conflict();
            var updated = (await store.GetAsync(id, ct))!; hub.UpdateOverlay(updated);
            return Results.Ok(updated);
        }).Produces<OverlayDefinition>();
        app.MapGet("/api/overlays/{id}/chat", async (string id, OverlayStore overlays, EventStore events, CancellationToken ct) =>
        {
            var overlay = await overlays.GetAsync(id, ct);
            if (overlay is null) return Results.NotFound();
            var history = await events.ChatAsync(500, ct);
            return Results.Ok(history.Where(item => overlay.CanvasEnabled ? overlay.Widgets.Any(w => !w.Hidden && w.Kind == "chat" && w.Chat.Accepts(item)) : overlay.Chat.Accepts(item))
                .Take(overlay.CanvasEnabled ? 500 : overlay.Chat.MaximumMessages).Select(PublicChat).ToArray());
        }).Produces<CanonicalEvent[]>();
        app.MapGet("/api/overlays/{id}/events", async (string id, OverlayStore overlays, EventStore events, CancellationToken ct) =>
        {
            var overlay = await overlays.GetAsync(id, ct);
            if (overlay is null) return Results.NotFound();
            var history = await events.ReadAsync(500, cancellationToken: ct);
            return Results.Ok(history.Where(item => overlay.CanvasEnabled && overlay.Widgets.Any(w => !w.Hidden && w.Kind == "event-list" && w.EventList.Accepts(item)))
                .Select(PublicChat).ToArray());
        }).Produces<CanonicalEvent[]>();
        app.Map("/ws/overlay/{id}", async (string id, OverlayStore overlays, EditorEventHub hub, HttpContext context) =>
        {
            var overlay = await overlays.GetAsync(id, context.RequestAborted);
            if (overlay is null) { context.Response.StatusCode = 404; return; }
            await hub.ConnectAsync(context, overlay);
        });
        app.MapPost("/api/overlays/{id}/tokens", async (string id, CreateOverlayToken request, OverlayStore store, CancellationToken ct) =>
        {
            if (await store.GetAsync(id, ct) is null) return Results.NotFound();
            try { return Results.Ok(await store.CreateTokenAsync(id, request.LifetimeDays, ct)); }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<CreatedOverlayToken>();
        app.MapGet("/api/overlays/{id}/tokens", async (string id, OverlayStore store, CancellationToken ct) => TypedResults.Ok(await store.TokensAsync(id, ct)));
        app.MapDelete("/api/overlays/{id}/tokens/{tokenId:guid}", async (string id, Guid tokenId, OverlayStore store, EditorEventHub hub, CancellationToken ct) =>
        {
            await store.RevokeAsync(id, tokenId, ct); hub.CloseLimitedOverlay(id); return Results.NoContent();
        });
        app.MapGet("/api/assets", async (AssetStore store, CancellationToken ct) => TypedResults.Ok(await store.ListAsync(ct)));
        app.MapPost("/api/assets", async (HttpContext context, AssetStore store) =>
        {
            try
            {
                var info = await store.UploadAsync(context.Request.Body, context.Request.Headers["X-Asset-Filename"].ToString(), context.Request.ContentType ?? "",
                    context.Request.Headers["X-Asset-License"].FirstOrDefault(), context.RequestAborted);
                return info is null ? Results.StatusCode(429) : Results.Ok(info);
            }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid asset MIME, size, filename, SVG or font license declaration." }); }
        }).Produces<AssetInfo>();
        app.MapMethods("/assets/{id}", ["GET", "HEAD"], async (string id, AssetStore store, HttpContext context) =>
        {
            var info = await store.GetAsync(id, context.RequestAborted);
            if (info is null || !File.Exists(store.PathFor(id))) return Results.NotFound();
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            return Results.File(store.PathFor(id), info.Mime, enableRangeProcessing: true);
        });
    }
    private static async Task<bool> ValidAssetsAsync(OverlayDefinition overlay, AssetStore assets, SensitiveValues sensitive, CancellationToken ct)
    {
        var fonts = new[] { overlay.Chat.FontAssetId }.Concat(overlay.Widgets.SelectMany(w => new[] { w.Chat.FontAssetId, w.Donor.FontAssetId })).Where(id => id is not null);
        foreach (var id in fonts) if ((await assets.GetAsync(id!, ct))?.Mime.StartsWith("font/", StringComparison.Ordinal) != true) return false;
        foreach (var widget in overlay.Widgets)
        {
            var customNode = JsonSerializer.SerializeToNode(widget.Custom, EventStore.JsonOptions);
            if (!System.Text.Json.Nodes.JsonNode.DeepEquals(customNode, CredentialRedactor.Json(customNode?.DeepClone(), sensitive.Snapshot()))) return false;
            foreach (var customAsset in widget.Custom.AssetIds) if (await assets.GetAsync(customAsset, ct) is null) return false;
            if (widget.Donor.CrownAssetId is { } crown && (await assets.GetAsync(crown, ct))?.Mime.StartsWith("image/", StringComparison.Ordinal) != true) return false;
            if (widget.AssetId is { } id)
            {
                var mime = (await assets.GetAsync(id, ct))?.Mime;
                var family = widget.Kind switch { "image" => "image/", "video" => "video/", "audio" => "audio/", _ => "invalid/" };
                if (mime?.StartsWith(family, StringComparison.Ordinal) != true) return false;
            }
            if (widget.Alert.MediaAssetId is { } media)
            {
                var mime = (await assets.GetAsync(media, ct))?.Mime;
                if (mime?.StartsWith("image/", StringComparison.Ordinal) != true && mime?.StartsWith("video/", StringComparison.Ordinal) != true) return false;
            }
            if (widget.Alert.SoundAssetId is { } sound && (await assets.GetAsync(sound, ct))?.Mime.StartsWith("audio/", StringComparison.Ordinal) != true) return false;
        }
        return true;
    }
    public static CanonicalEvent PublicChat(CanonicalEvent item) => item with { Raw = null, Monetary = null, Support = null };
    private static IResult Shell(WebApplication app)
    {
        var file = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "runtime", "index.html");
        return File.Exists(file) ? Results.File(file, "text/html") : Results.Problem("Build the overlay runtime before starting the host.", statusCode: 503);
    }
}

public sealed record PreviewEventRequest(string Type, string Platform = "twitch", string User = "Test viewer", string Message = "Test message", System.Text.Json.Nodes.JsonObject? Raw = null, string Mode = "synthetic");
