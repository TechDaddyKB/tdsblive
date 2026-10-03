using System.IO.Compression;
using System.Text.Json;
using ExtensionSuite.Core;

namespace ExtensionSuite.Host;

public static class PortablePackageEndpoints
{
    public static void MapPortablePackageEndpoints(this WebApplication app)
    {
        app.MapGet("/api/overlays/{id}/export", async (string id, string? widgetId, PortablePackages packages, CancellationToken ct) =>
        {
            try { return await packages.ExportAsync(id, widgetId, ct) is { } bytes ? Results.File(bytes, "application/zip", widgetId is null ? "overlay.sbxoverlay" : "widget.sbxwidget") : Results.NotFound(); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Remove credentials/absolute paths and check referenced assets and size before exporting." }); }
        });
        app.MapPost("/api/packages/import", async (HttpContext context, PortablePackages packages, EditorEventHub hub) =>
        {
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[65536]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > PortablePackages.MaximumArchiveBytes) return Results.StatusCode(413);
                    await buffer.WriteAsync(chunk.AsMemory(0, count), context.RequestAborted);
                }
                var prepared = packages.Validate(buffer.ToArray());
                var extension = Path.GetExtension(context.Request.Headers["X-Package-Filename"].ToString());
                if (extension != (prepared.Manifest.Kind == "overlay" ? ".sbxoverlay" : ".sbxwidget")) return Results.BadRequest();
                var target = context.Request.Query["overlayId"].FirstOrDefault();
                return Results.Ok(await packages.ImportAsync(prepared, target, hub, context.RequestAborted));
            }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or JsonException or NotSupportedException)
            { return Results.BadRequest(new { error = "Invalid or unsafe portable package. Check its version, entries, assets and size." }); }
        }).Produces<OverlayDefinition>();
    }
}
