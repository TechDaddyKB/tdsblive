using System.Net;
using System.Security.Cryptography;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.StreamerBot;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ExtensionSuite.Host;

public sealed record AdminLogin(string Credential);
public sealed record SecretUpdate(string Value);
public sealed record StreamerCredentialUpdate(string Value, bool SessionOnly = true);
public sealed record TestEventRequest(CanonicalEvent Event, bool Persist = false);
public sealed record IntegrationStates(string StreamerBot, string SpeakerBot, string Rumble);
public sealed record StatusResponse(string Name, bool HttpSupported, bool LanEnabled, IntegrationStates Integrations);
public sealed record CsrfResponse(string? RequestToken);

public static class FoundationEndpoints
{
    public static void MapFoundationEndpoints(this WebApplication app)
    {
        app.MapRecoveryEndpoints();
        app.MapSetupEndpoints();
        app.MapPost("/api/integrations/streamerbot/credential", async (StreamerCredentialUpdate update, HttpContext context, SensitiveValues sensitive) =>
        {
            if (string.IsNullOrEmpty(update.Value) || update.Value.Length > 4096)
                return Results.BadRequest(new { error = "Enter a valid Streamer.bot password." });
            if (!update.SessionOnly)
            {
                if (!OperatingSystem.IsWindows()) return Results.Problem("Persistent credential storage requires Windows DPAPI. Use session-only storage.", statusCode: 501);
                await context.RequestServices.GetRequiredService<WindowsSecretVault>().SetAsync("streamerbot-password", update.Value, context.RequestAborted);
            }
            sensitive.Set("streamerbot-password", update.Value);
            return Results.NoContent();
        });
        app.MapRumbleEndpoints();
        app.MapOverlayEndpoints();
        app.MapCustomWidgetEndpoints();
        app.MapPortablePackageEndpoints();
        app.MapFinancialEndpoints();
        app.MapAutomationEndpoints();
        app.Map("/ws/editor", (HttpContext context, EditorEventHub hub) => hub.ConnectAsync(context));
        app.MapBotEndpoints();
        app.MapOpenApi("/api/openapi/{documentName}.json");
        app.MapGet("/", () => Results.Redirect("/editor"));
        app.MapGet("/editor", () => EditorShell(app));
        app.MapGet("/login", () => EditorShell(app));
        app.MapGet("/api/status", (ApplicationConfiguration configuration, StreamerBotConnection streamer, SpeakerBotConnection speaker, RumbleIntegration rumble) => TypedResults.Ok(new StatusResponse(
            configuration.DisplayName, true, configuration.Server.EnableLan, new IntegrationStates(streamer.State.State, speaker.State.State, rumble.Status.State))));
        app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            TypedResults.Ok(new CsrfResponse(antiforgery.GetAndStoreTokens(context).RequestToken)));
        app.MapPost("/api/auth/login", (AdminLogin login, HttpContext context, AccessControl access) =>
        {
            var session = access.TryCreateSession(login.Credential);
            if (session is null) return Results.Unauthorized();
            context.Response.Cookies.Append("tdsblive-session", session, new CookieOptions
                { HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = false, MaxAge = TimeSpan.FromHours(8), Path = "/" });
            return Results.NoContent();
        }).RequireRateLimiting("admin-login");
        app.MapPost("/api/auth/logout", (HttpContext context, AccessControl access) =>
        {
            access.RevokeSession(context);
            return Results.NoContent();
        });
        app.MapPost("/api/auth/provision", async (HttpContext context, ApplicationPaths paths, AccessControl access, SensitiveValues sensitive) =>
        {
            if (context.Connection.RemoteIpAddress is not { } peer || !IPAddress.IsLoopback(peer)) return Results.StatusCode(403);
            if (!OperatingSystem.IsWindows()) return Results.Problem("Credential provisioning requires Windows DPAPI.", statusCode: 501);
            var credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await context.RequestServices.GetRequiredService<WindowsSecretVault>().SetAsync("admin-token", credential, context.RequestAborted);
            access.SetAdminCredential(credential);
            sensitive.Set("admin-token", credential);
            return Results.Ok(new { credential });
        });
        app.MapGet("/api/configuration", (ConfigurationStore store) => TypedResults.Ok(store.Load()));
        app.MapPut("/api/configuration", async ([FromBody] ApplicationConfiguration configuration, ConfigurationStore store, HttpContext context) =>
        {
            try { await store.SaveAsync(configuration, context.RequestAborted); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid configuration values." }); }
            return Results.Ok(new { restartRequired = true });
        });
        app.MapPost("/api/secrets/{name}", async (string name, SecretUpdate update, ApplicationPaths paths, HttpContext context, SensitiveValues sensitive) =>
        {
            if (!OperatingSystem.IsWindows()) return Results.Problem("Credential storage requires Windows DPAPI.", statusCode: 501);
            if (name == "admin-token") return Results.BadRequest(new { error = "Use the local admin provisioning endpoint." });
            try { await context.RequestServices.GetRequiredService<WindowsSecretVault>().SetAsync(name, update.Value, context.RequestAborted); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid secret name." }); }
            sensitive.Set(name, update.Value);
            return Results.NoContent();
        });
        app.MapDelete("/api/secrets/{name}", async (string name, ApplicationPaths paths, HttpContext context, SensitiveValues sensitive) =>
        {
            if (!OperatingSystem.IsWindows()) return Results.Problem("Credential storage requires Windows DPAPI.", statusCode: 501);
            if (name == "admin-token") return Results.BadRequest(new { error = "Rotate admin credentials through local provisioning." });
            try { await context.RequestServices.GetRequiredService<WindowsSecretVault>().DeleteAsync(name, context.RequestAborted); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid secret name." }); }
            sensitive.Remove(name);
            return Results.NoContent();
        });
        app.MapGet("/api/events", async (EventStore store, int? limit, CancellationToken cancellationToken) =>
        {
            if (limit is < 1 or > 500) return Results.BadRequest();
            return Results.Ok(await store.ReadAsync(limit ?? 100, cancellationToken: cancellationToken));
        }).Produces<CanonicalEvent[]>();
        app.MapPost("/api/test-event", async (TestEventRequest request, EventStore store, ApplicationConfiguration configuration, EditorEventHub hub, CancellationToken cancellationToken) =>
        {
            var item = request.Event with { Id = Guid.CreateVersion7(), Provenance = EventProvenance.Simulation, ReceivedAt = DateTimeOffset.UtcNow };
            try { item.Validate(); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid canonical event." }); }
            var persisted = await store.AcceptAsync(item, "simulation", request.Persist, configuration.RetainRawEvents, cancellationToken);
            if (!request.Persist) hub.Publish(item with { Raw = configuration.RetainRawEvents ? item.Raw : null });
            return Results.Ok(new { eventId = item.Id, persisted, provenance = "simulation", liveActionsAllowed = false });
        });
        app.MapGet("/api/diagnostics", async (IDbContextFactory<FoundationDbContext> factory, FoundationStateStore state,
            IntegrationHealthRegistry health, IEnumerable<ILoggerProvider> providers, CancellationToken cancellationToken) =>
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            return Results.Ok(new
            {
                database = "ready", migrations = await db.Database.GetAppliedMigrationsAsync(cancellationToken),
                eventCount = await db.Events.CountAsync(cancellationToken), pendingDeliveries = await db.Outbox.CountAsync(row => row.DeliveredAtTicks == null, cancellationToken),
                secretStorage = OperatingSystem.IsWindows() ? "windowsDpapi" : "unavailable", liveIntegrations = "ownedByG03AndG04",
                integrationHealth = health.Snapshot(), sqliteLogFailures = state.LogPersistenceFailures,
                fileLogFailures = providers.OfType<RedactedFileLoggerProvider>().Sum(provider => provider.WriteFailures)
            });
        });
        app.MapGet("/api/diagnostics/logs", (ApplicationPaths paths) =>
        {
            var current = Path.Combine(paths.Logs, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl");
            return File.Exists(current) ? Results.File(current, "application/x-ndjson", "tdsblive-log.jsonl") : Results.NotFound();
        });
    }

    private static IResult EditorShell(WebApplication app)
    {
        var path = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "editor", "index.html");
        return File.Exists(path) ? Results.File(path, "text/html") : Results.Problem("Editor assets are missing. Run npm run build before starting the host.", statusCode: 503);
    }
}
