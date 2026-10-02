using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.AspNetCore.Mvc;
using ExtensionSuite.StreamerBot;

namespace ExtensionSuite.Host;

public sealed record AutomationPreviewRequest(CanonicalEvent Event, bool? Anonymous = null, string? Language = null, AutomationRule? Rule = null);
public sealed record AutomationPreviewResponse(AutomationPlannedAction[] Actions, bool Persisted = false, bool LiveActionsAllowed = false);
public sealed record AutomationModerationRequest(int Version, bool Approve, string State = "moderation-pending");
public sealed record AutomationLanguageRequest(int Version, string Language);
public sealed record AutomationRestoredRequest(int Version, bool ExternalStateRestored);

public static class AutomationEndpoints
{
    public static void MapAutomationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/automation");
        group.MapGet("/temporary-effects", async (AutomationTemporaryStore store, CancellationToken ct) => TypedResults.Ok(await store.ListAsync(ct)));
        group.MapPost("/temporary-effects/{id:guid}/resolve", async (Guid id, [FromBody] AutomationRestoredRequest request,
            AutomationTemporaryActions service, CancellationToken ct) =>
            await service.ResolveRestoredAsync(id, request.Version, request.ExternalStateRestored, ct) ? Results.Ok() : Results.Conflict());
        group.MapGet("/capabilities", async (AutomationRuleStore rules, ApplicationConfiguration configuration,
            StreamerBotConnection streamer, SpeakerBotConnection speaker, AssetStore assets, OverlayStore overlays, CancellationToken ct) =>
            TypedResults.Ok(await AutomationDiagnostics.ReadAsync(rules, configuration, streamer, speaker, assets, overlays, ct)));
        group.MapGet("/rules", async (AutomationRuleStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(ct))).Produces<AutomationRule[]>();
        group.MapPut("/rules/{id:guid}", async (Guid id, [FromBody] AutomationRule rule, AutomationRuleStore store, CancellationToken ct) =>
        {
            if (id != rule.Id) return Results.BadRequest();
            try
            {
                var saved = await store.SaveAsync(rule, ct);
                return saved is null ? Results.Conflict(new { error = "Rule version changed." }) : Results.Ok(saved);
            }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid automation rule." }); }
        });
        group.MapDelete("/rules/{id:guid}", async (Guid id, int version, AutomationRuleStore store, CancellationToken ct) =>
            await store.DeleteAsync(id, version, ct) ? Results.NoContent() : Results.Conflict());
        group.MapPost("/simulate", async ([FromBody] AutomationPreviewRequest request, AutomationPlanningStore store, CancellationToken ct) =>
        {
            try
            {
                request.Event.Validate();
                request.Event.Support?.Validate();
                var simulated = request.Event with { Provenance = EventProvenance.Simulation };
                var actions = request.Rule is { } draft
                    ? AutomationPlanner.Evaluate([draft with { Enabled = true }], simulated, request.Anonymous, request.Language)
                    : await store.PreviewAsync(simulated, request.Anonymous, request.Language, ct);
                return Results.Ok(new AutomationPreviewResponse(actions));
            }
            catch (ArgumentException) { return Results.BadRequest(new { error = "Invalid test event." }); }
        }).Produces<AutomationPreviewResponse>();
        group.MapGet("/executions", async (AutomationExecutionStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(ct: ct))).Produces<AutomationExecution[]>();
        group.MapPost("/executions/{id:guid}/language", async (Guid id, [FromBody] AutomationLanguageRequest request,
            AutomationExecutionStore store, CancellationToken ct) =>
        {
            try { return await store.ResolveLanguageAsync(id, request.Version, request.Language, ct) ? Results.Ok() : Results.Conflict(); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "A valid verified language is required." }); }
        });
        group.MapPost("/executions/{id:guid}/moderate", async (Guid id, [FromBody] AutomationModerationRequest request,
            AutomationExecutionStore store, CancellationToken ct) =>
        {
            if (request.State != "moderation-pending" && (request.State != "language-review" || request.Approve)) return Results.BadRequest();
            return await store.TransitionAsync(id, request.Version, request.State, request.Approve ? "queued" : "rejected", ct)
                ? Results.Ok(new { state = request.Approve ? "queued" : "rejected" }) : Results.Conflict();
        });
    }
}
