using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace ExtensionSuite.Host;

public static class CustomWidgetEndpoints
{
    public static void MapCustomWidgetEndpoints(this WebApplication app)
    {
        app.MapGet("/api/overlays/{id}/widgets/{widgetId}/store", async (string id, string widgetId, HttpContext context,
            OverlayStore overlays, IDbContextFactory<FoundationDbContext> factory, CancellationToken ct) =>
        {
            if (!await Allowed(id, widgetId, overlays, ct)) return Results.StatusCode(403);
            if (context.Items.ContainsKey("OverlayToken") && context.Request.Query["preview"] == "1") return Results.StatusCode(403);
            await using var db = await factory.CreateDbContextAsync(ct);
            var name = Name(id, widgetId, context);
            var json = await db.Configurations.Where(c => c.Name == name).Select(c => c.Json).SingleOrDefaultAsync(ct);
            return Results.Text(json ?? "{}", "application/json");
        });
        app.MapPut("/api/overlays/{id}/widgets/{widgetId}/store", async (string id, string widgetId, JsonObject value, HttpContext context,
            OverlayStore overlays, IDbContextFactory<FoundationDbContext> factory, SensitiveValues sensitive, CancellationToken ct) =>
        {
            if (!await Allowed(id, widgetId, overlays, ct)) return Results.StatusCode(403);
            if (context.Items.ContainsKey("OverlayToken") && context.Request.Query["preview"] == "1") return Results.StatusCode(403);
            var json = value.ToJsonString();
            if (json.Length > 32768 || !JsonNode.DeepEquals(value, CredentialRedactor.Json(value.DeepClone(), sensitive.Snapshot()))) return Results.BadRequest();
            await using var db = await factory.CreateDbContextAsync(ct);
            var name = Name(id, widgetId, context);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Configurations (Name, Json) VALUES ({name}, {json}) ON CONFLICT(Name) DO UPDATE SET Json = {json}", ct);
            return Results.NoContent();
        });
    }
    private static string Name(string id, string widget, HttpContext context) => "widget-store:" + (context.Request.Query["preview"] == "1" ? "preview:" : "live:") + id + ":" + widget;
    private static async Task<bool> Allowed(string id, string widgetId, OverlayStore store, CancellationToken ct) =>
        (await store.GetAsync(id, ct))?.Widgets.Any(w => w.Id == widgetId && w.Kind == "custom" && w.Custom.Permissions.Contains("storage")) == true;
}
