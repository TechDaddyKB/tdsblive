using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.StreamerBot;

namespace ExtensionSuite.Host;

public sealed record ActionExecutionRequest(Guid ActionId, JsonObject? Arguments = null, bool ExecuteLive = false);
public sealed record CodeTriggerExecutionRequest(string EventName, JsonObject? Arguments = null, bool ExecuteLive = false);
public sealed record SpeechRequest(string Voice, string Message, bool BadWordFilter = true, bool ExecuteLive = false);
public sealed record SpeakerQueueRequest(string Operation, string? Value = null, bool ExecuteLive = false);
public sealed record InspectorReplayRequest(bool Persist = false);
public sealed record IntegrationOverview(BotConnectionState StreamerBot, BotConnectionState SpeakerBot);

public static class BotEndpoints
{
    public static void MapBotEndpoints(this WebApplication app)
    {
        app.MapGet("/api/integrations", (StreamerBotConnection streamer, SpeakerBotConnection speaker) =>
            TypedResults.Ok(new IntegrationOverview(streamer.State, speaker.State)));
        app.MapPost("/api/integrations/streamerbot/test", async (StreamerBotConnection streamer, CancellationToken ct) =>
            TypedResults.Ok(await streamer.TestConnectionAsync(ct)));
        app.MapPost("/api/integrations/speakerbot/test", async (SpeakerBotConnection speaker, CancellationToken ct) =>
            TypedResults.Ok(await speaker.TestConnectionAsync(ct)));
        app.MapGet("/api/integrations/streamerbot/discovery", (StreamerBotConnection streamer) => TypedResults.Ok(streamer.Discovery));
        app.MapPost("/api/integrations/streamerbot/discovery/refresh", async (StreamerBotConnection streamer, CancellationToken cancellationToken) =>
        {
            try { await streamer.RefreshDiscoveryAsync(cancellationToken); return Results.Ok(streamer.Discovery); }
            catch (BotRequestException error) { return Results.Problem(error.Kind, statusCode: 503); }
        });
        app.MapPost("/api/integrations/streamerbot/actions/execute", async (ActionExecutionRequest request, StreamerBotConnection streamer, CancellationToken cancellationToken) =>
            TypedResults.Ok(await streamer.ExecuteActionAsync(request.ActionId, request.Arguments ?? new JsonObject(), request.ExecuteLive, cancellationToken)));
        app.MapPost("/api/integrations/streamerbot/triggers/execute", async (CodeTriggerExecutionRequest request, StreamerBotConnection streamer, CancellationToken cancellationToken) =>
            TypedResults.Ok(await streamer.ExecuteCodeTriggerAsync(request.EventName, request.Arguments ?? new JsonObject(), request.ExecuteLive, cancellationToken)));
        app.MapGet("/api/integrations/streamerbot/executions", (StreamerBotConnection streamer) => TypedResults.Ok(streamer.Executions));
        app.MapGet("/api/integrations/speakerbot/executions", (SpeakerBotConnection speaker) => TypedResults.Ok(speaker.Executions));
        app.MapPost("/api/integrations/speakerbot/speak", async (SpeechRequest request, SpeakerBotConnection speaker, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await speaker.SpeakAsync(request.Voice, request.Message, request.BadWordFilter, request.ExecuteLive, cancellationToken)); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid voice or message." }); }
        });
        app.MapPost("/api/integrations/speakerbot/queue", async (SpeakerQueueRequest request, SpeakerBotConnection speaker, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await speaker.QueueAsync(request.Operation, request.Value, request.ExecuteLive, cancellationToken)); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid queue operation." }); }
        });
        app.MapGet("/api/inspector", (EventInspectorStore inspector, string? filter, int? limit) =>
        {
            try { return Results.Ok(inspector.Read(filter, limit ?? 100)); }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<InspectorEntry[]>();
        app.MapGet("/api/inspector/{id:guid}/fixture", (Guid id, EventInspectorStore inspector) =>
        {
            var entry = inspector.Find(id);
            return entry is null ? Results.NotFound() : Results.Json(entry, EventStore.JsonOptions);
        });
        app.MapGet("/api/inspector/{id:guid}/sanitized-fixture", (Guid id, EventInspectorStore inspector) =>
        {
            var entry = inspector.Find(id);
            return entry is null ? Results.NotFound() : Results.Json(new
            {
                formatVersion = 1, purpose = "shapeOnly", valuesRemoved = true,
                payload = DiagnosticSanitizer.Shape(entry.Payload)
            });
        });
        app.MapPost("/api/inspector/{id:guid}/replay", async (Guid id, InspectorReplayRequest request, EventInspectorStore inspector,
            EventStore store, EditorEventHub hub, ApplicationConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var original = inspector.Find(id)?.Event;
            if (original is null) return Results.NotFound();
            var replay = original with { Id = Guid.CreateVersion7(), ReceivedAt = DateTimeOffset.UtcNow,
                Provenance = EventProvenance.Replay, DedupeKey = "inspector-replay:" + Guid.CreateVersion7() };
            var persisted = await store.AcceptAsync(replay, replay.Id.ToString(), request.Persist, configuration.RetainRawEvents, cancellationToken);
            if (!request.Persist) hub.Publish(replay);
            return Results.Ok(new { eventId = replay.Id, provenance = "replay", persisted, liveActionsAllowed = false });
        });
    }
}
