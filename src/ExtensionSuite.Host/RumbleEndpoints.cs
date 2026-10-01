using ExtensionSuite.Data;

namespace ExtensionSuite.Host;

public sealed record RumbleCredentialRequest(string Value, bool SessionOnly = true);

public static class RumbleEndpoints
{
    public static void MapRumbleEndpoints(this WebApplication app)
    {
        app.MapGet("/api/rumble/status", (RumbleIntegration rumble) => TypedResults.Ok(rumble.Status));
        app.MapGet("/api/rumble/deliveries", async (RumbleStore store, CancellationToken cancellationToken) => TypedResults.Ok(await store.DeliveryStatusAsync(cancellationToken)));
        app.MapPost("/api/rumble/credential", async (RumbleCredentialRequest request, RumbleIntegration rumble, HttpContext context) =>
        {
            try { await rumble.SetCredentialAsync(request.Value, request.SessionOnly, context.RequestServices, context.RequestAborted); return Results.NoContent(); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Use the official Rumble HTTPS Live API URL." }); }
            catch (PlatformNotSupportedException) { return Results.Problem("Persistent credentials require Windows DPAPI. Use session-only storage on this host.", statusCode: 501); }
        });
        app.MapPost("/api/rumble/reset-baseline", async (RumbleIntegration rumble, CancellationToken cancellationToken) =>
        {
            await rumble.ResetBaselineAsync(cancellationToken); return TypedResults.NoContent();
        });
        app.MapDelete("/api/rumble/credential", async (RumbleIntegration rumble, HttpContext context) =>
        {
            await rumble.DisconnectAsync(context.RequestAborted);
            if (OperatingSystem.IsWindows())
            {
                var vault = context.RequestServices.GetRequiredService<WindowsSecretVault>();
                await vault.DeleteAsync("rumble-url", context.RequestAborted); await vault.DeleteAsync("rumble-context", context.RequestAborted);
            }
            return TypedResults.NoContent();
        });
    }
}
