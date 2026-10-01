using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.AspNetCore.Mvc;

namespace ExtensionSuite.Host;

public static class OverlayEndpoints
{
    public static void MapOverlayEndpoints(this WebApplication app)
    {
        app.MapGet("/overlay/{id}", async (string id, OverlayStore store, CancellationToken ct) =>
            await store.GetAsync(id, ct) is null ? Results.NotFound() : Shell(app));
        app.MapGet("/chat/{id}", async (string id, OverlayStore store, CancellationToken ct) =>
            await store.GetAsync(id, ct) is null ? Results.NotFound() : Shell(app));
        app.MapGet("/api/overlays/{id}", async (string id, OverlayStore store, CancellationToken ct) =>
            await store.GetAsync(id, ct) is { } value ? Results.Ok(value) : Results.NotFound()).Produces<OverlayDefinition>();
        app.MapPut("/api/overlays/{id}", async (string id, [FromBody] OverlayDefinition value, OverlayStore store, AssetStore assets, EditorEventHub hub, CancellationToken ct) =>
        {
            if (value.Id != id) return Results.BadRequest();
            try { value.Validate(); } catch (ArgumentException) { return Results.BadRequest(); }
            if (value.Chat.FontAssetId is { } font && (await assets.GetAsync(font, ct))?.Mime.StartsWith("font/", StringComparison.Ordinal) != true) return Results.BadRequest();
            if (!await store.SaveAsync(value, ct)) return Results.Conflict();
            var updated = (await store.GetAsync(id, ct))!; hub.UpdateOverlay(updated);
            return Results.Ok(updated);
        }).Produces<OverlayDefinition>();
        app.MapGet("/api/overlays/{id}/chat", async (string id, OverlayStore overlays, EventStore events, CancellationToken ct) =>
        {
            var overlay = await overlays.GetAsync(id, ct);
            if (overlay is null) return Results.NotFound();
            var history = await events.ChatAsync(500, ct);
            return Results.Ok(history.Where(overlay.Chat.Accepts).Take(overlay.Chat.MaximumMessages).Select(PublicChat).ToArray());
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
        app.MapGet("/assets/{id}", async (string id, AssetStore store, HttpContext context) =>
        {
            var info = await store.GetAsync(id, context.RequestAborted);
            if (info is null || !File.Exists(store.PathFor(id))) return Results.NotFound();
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            return Results.File(store.PathFor(id), info.Mime, enableRangeProcessing: true);
        });
    }
    public static CanonicalEvent PublicChat(CanonicalEvent item) => item with { Raw = null, Monetary = null };
    private static IResult Shell(WebApplication app)
    {
        var file = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "runtime", "index.html");
        return File.Exists(file) ? Results.File(file, "text/html") : Results.Problem("Build the overlay runtime before starting the host.", statusCode: 503);
    }
}
