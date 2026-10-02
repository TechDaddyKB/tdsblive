using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExtensionSuite.Core;

namespace ExtensionSuite.Host;

public sealed record ConfigurationExport(int Format, ApplicationConfiguration Application);
public sealed record RestoreRequest(Guid Id, bool Confirm);

public static class RecoveryEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true };

    private static FileStream OpenPrivateTemporaryFile(string path)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None,
            BufferSize = 65536, Options = FileOptions.DeleteOnClose
        };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    public static void MapRecoveryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/application/status", (ApplicationLifecycle lifecycle) => TypedResults.Ok(new
            { generation = lifecycle.Generation, operation = lifecycle.Operation?.Kind }));
        app.MapPost("/api/application/restart", (HttpContext context, ApplicationLifecycle lifecycle) => Request(context, lifecycle, "restart"));
        app.MapPost("/api/application/quit", (HttpContext context, ApplicationLifecycle lifecycle) => Request(context, lifecycle, "quit"));
        app.MapPost("/api/recovery/backup", async (HttpContext context, RecoveryArchive archive) =>
        {
            if (!LocalOwner(context)) return Results.StatusCode(403);
            var file = Path.Combine(Path.GetTempPath(), "tdsblive-backup-" + Guid.NewGuid().ToString("N") + ".zip");
            var stream = OpenPrivateTemporaryFile(file);
            try
            {
                await archive.CreateAsync(stream, context.RequestAborted);
                stream.Position = 0;
                return Results.File(stream, "application/zip", $"TDSBLive-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
            }
            catch (Exception error) when (error is IOException or System.Data.Common.DbException)
            {
                await stream.DisposeAsync();
                return Results.Problem("Backup could not be created. Check free space and the local asset library.", statusCode: 409);
            }
            catch { await stream.DisposeAsync(); throw; }
        });
        app.MapPost("/api/recovery/validate", async (HttpContext context, RecoveryArchive archive, ApplicationLifecycle lifecycle) =>
        {
            if (!LocalOwner(context)) return Results.StatusCode(403);
            var file = Path.Combine(Path.GetTempPath(), "tdsblive-upload-" + Guid.NewGuid().ToString("N") + ".zip");
            await using var stream = OpenPrivateTemporaryFile(file);
            try
            {
                var buffer = new byte[65536];
                long count = 0;
                int read;
                while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
                {
                    count += read;
                    if (count > RecoveryArchive.MaximumArchiveBytes) return Results.StatusCode(413);
                    await stream.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
                }
                stream.Position = 0;
                var prepared = await archive.PrepareAsync(stream, context.RequestAborted);
                var preview = await lifecycle.StageAsync(prepared);
                if (preview is not null) return Results.Ok(preview);
                await prepared.DisposeAsync();
                return Results.Conflict(new { error = "An application operation is already in progress." });
            }
            catch (Exception error) when (error is IOException or InvalidDataException or JsonException or System.Data.Common.DbException)
            { return Results.BadRequest(new { error = "Backup could not be validated. Use an intact backup made by this application version." }); }
        }).Produces<RecoveryPreview>();
        app.MapPost("/api/recovery/restore", (RestoreRequest request, HttpContext context, ApplicationLifecycle lifecycle) =>
        {
            if (!LocalOwner(context)) return Results.StatusCode(403);
            if (!request.Confirm) return Results.BadRequest(new { error = "Confirm replacement of current application data." });
            if (!lifecycle.RequestRestore(request.Id)) return Results.Conflict(new { error = "Backup validation expired or an application operation is in progress. Validate again." });
            lifecycle.StopAfterResponse(context);
            return Results.Accepted(value: new { operation = "restore", connectionsDisabled = true, automationDisabled = true });
        });
        app.MapPost("/api/configuration/export", (HttpContext context, ConfigurationStore store, SensitiveValues sensitive) =>
        {
            if (!LocalOwner(context)) return Results.StatusCode(403);
            var document = JsonSerializer.SerializeToNode(new ConfigurationExport(1, store.Load()), Json)!;
            var clean = CredentialRedactor.Json(document, sensitive.Snapshot())!.ToJsonString(Json);
            return Results.File(System.Text.Encoding.UTF8.GetBytes(clean), "application/json", "TDSBLive-connection-settings.json");
        });
        app.MapPost("/api/configuration/import", async (HttpContext context, ConfigurationStore store) =>
        {
            if (!LocalOwner(context)) return Results.StatusCode(403);
            try
            {
                var document = await JsonSerializer.DeserializeAsync<ConfigurationExport>(context.Request.Body, Json, context.RequestAborted);
                if (document is null || document.Format != 1 || document.Application is null) return Results.BadRequest();
                document.Application.Validate();
                var configuration = document.Application with
                {
                    Server = document.Application.Server with { Host = "127.0.0.1", EnableLan = false, AllowedHosts = [] },
                    StreamerBot = document.Application.StreamerBot with { Enabled = false, ForwardLiveEvents = false },
                    SpeakerBot = document.Application.SpeakerBot with { Enabled = false },
                    Rumble = document.Application.Rumble with { Enabled = false, ForwardTriggers = false }
                };
                await store.SaveAsync(configuration, context.RequestAborted);
                return Results.Ok(new { restartRequired = true, credentialsIncluded = false, connectionsDisabled = true });
            }
            catch (Exception error) when (error is JsonException or ArgumentException)
            { return Results.BadRequest(new { error = "Invalid connection settings file." }); }
        });
    }

    private static IResult Request(HttpContext context, ApplicationLifecycle lifecycle, string kind)
    {
        if (!LocalOwner(context)) return Results.StatusCode(403);
        if (!lifecycle.Request(kind)) return Results.Conflict(new { error = "An application operation is already in progress." });
        lifecycle.StopAfterResponse(context);
        return Results.Accepted(value: new { operation = kind });
    }
    private static bool LocalOwner(HttpContext context) => context.Connection.RemoteIpAddress is null || IPAddress.IsLoopback(context.Connection.RemoteIpAddress);
}
