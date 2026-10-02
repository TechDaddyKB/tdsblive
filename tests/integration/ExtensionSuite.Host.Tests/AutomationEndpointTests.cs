using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Host;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationEndpointTests
{
    [Fact]
    public async Task RestoredEffectResolutionRequiresConfirmationAndCurrentVersionWithoutBotDispatch()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        http.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", (await http.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"))!.RequestToken);
        var action = new AutomationAction { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), DurationSeconds = 5 };
        var planned = new AutomationPlannedAction(Guid.NewGuid(), 1, action, "queued", null, "main", "queue", 20, 0);
        var payload = new TemporaryEffectPayload(Guid.NewGuid(), planned, [planned]);
        await using (var db = await app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>().CreateDbContextAsync())
        {
            db.AutomationTemporaryEffects.Add(new() { ActionId = action.Id, Version = 3, State = "uncertain",
                ExpiresAtTicks = DateTimeOffset.UtcNow.UtcTicks, Json = JsonSerializer.Serialize(payload, EventStore.JsonOptions) });
            await db.SaveChangesAsync();
        }
        var path = $"/api/automation/temporary-effects/{action.Id}/resolve";
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync(path, new AutomationRestoredRequest(3, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync(path, new AutomationRestoredRequest(2, true))).StatusCode);
        (await http.PostAsJsonAsync(path, new AutomationRestoredRequest(3, true))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync(path, new AutomationRestoredRequest(3, true))).StatusCode);
        var resolved = Assert.Single((await http.GetFromJsonAsync<AutomationTemporaryEffect[]>("/api/automation/temporary-effects"))!);
        Assert.Equal("idle", resolved.State);
        Assert.Equal(4, resolved.Version);
        Assert.Empty(JsonSerializer.Deserialize<TemporaryEffectPayload>(resolved.Json, EventStore.JsonOptions)!.Queue);
        Assert.Empty((await http.GetFromJsonAsync<JsonElement[]>("/api/integrations/streamerbot/executions"))!);
    }

    [Fact]
    public async Task CapabilityReportExposesMissingDependenciesWithoutExecutingActions()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        http.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", (await http.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"))!.RequestToken);
        var rule = new AutomationRule { Condition = new("kofi", "support.donation"), Actions = [
            new() { Speech = new() { Voice = "unverified-owned-alias" } },
            new() { Kind = "sound", OverlayId = "missing-owned-canvas", SoundAssetIds = [new string('a', 64)] },
            new() { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), RevertActionId = Guid.NewGuid(), DurationSeconds = 5 }
        ] };
        (await http.PutAsJsonAsync($"/api/automation/rules/{rule.Id}", rule)).EnsureSuccessStatusCode();
        var report = (await http.GetFromJsonAsync<AutomationCapabilityReport>("/api/automation/capabilities"))!;
        Assert.False(report.VoiceDiscoverySupported);
        foreach (var code in new[] { "speaker-unavailable", "voice-alias-unverified", "kofi-metadata", "missing-canvas-overlay", "missing-audio-asset", "streamer-unavailable", "missing-action", "action-not-selected" })
            Assert.Contains(report.Issues, issue => issue.Code == code && issue.RuleId == rule.Id);
        Assert.Equal(2, report.Issues.Count(issue => issue.Code == "action-not-selected"));
        Assert.Empty((await http.GetFromJsonAsync<JsonElement[]>("/api/automation/executions"))!);
    }

    [Fact]
    public async Task RuleCrudAndSimulationUseRealHttpWithoutLiveReceipts()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        http.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", (await http.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"))!.RequestToken);
        var rule = new AutomationRule { Enabled = true, Condition = new("kofi", "support.donation"),
            Actions = [new() { Speech = new() { Voice = "owned" } }] };
        var created = await http.PutAsJsonAsync($"/api/automation/rules/{rule.Id}", rule);
        created.EnsureSuccessStatusCode();
        var saved = (await created.Content.ReadFromJsonAsync<AutomationRule>())!;
        Assert.Equal(1, saved.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync($"/api/automation/rules/{rule.Id}", rule)).StatusCode);
        var item = new CanonicalEvent { Source = "owned", Platform = "kofi", Type = "support.donation", NativeType = "owned",
            DedupeKey = "simulation", OccurredAt = DateTimeOffset.UtcNow, Support = new("donation", 1, new(1000, "USD", 2)) };
        var simulation = await http.PostAsJsonAsync("/api/automation/simulate", new AutomationPreviewRequest(item));
        simulation.EnsureSuccessStatusCode();
        var result = await simulation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("persisted").GetBoolean());
        Assert.False(result.GetProperty("liveActionsAllowed").GetBoolean());
        Assert.Single(result.GetProperty("actions").EnumerateArray());
        Assert.Empty((await http.GetFromJsonAsync<JsonElement[]>("/api/automation/executions"))!);
        var draft = rule with { Id = Guid.NewGuid(), Enabled = false, Name = "Unsaved owned draft" };
        var draftSimulation = await http.PostAsJsonAsync("/api/automation/simulate", new AutomationPreviewRequest(item, Anonymous: false, Rule: draft));
        draftSimulation.EnsureSuccessStatusCode();
        var preview = (await draftSimulation.Content.ReadFromJsonAsync<AutomationPreviewResponse>())!;
        Assert.Equal(draft.Id, Assert.Single(preview.Actions).RuleId);
        Assert.False(preview.Persisted);
        Assert.False(preview.LiveActionsAllowed);
        Assert.Equal(rule.Id, Assert.Single((await http.GetFromJsonAsync<AutomationRule[]>("/api/automation/rules"))!).Id);
        Assert.Empty((await http.GetFromJsonAsync<JsonElement[]>("/api/automation/executions"))!);
        Assert.Equal(HttpStatusCode.Conflict, (await http.DeleteAsync($"/api/automation/rules/{rule.Id}?version=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync($"/api/automation/rules/{rule.Id}?version=1")).StatusCode);
    }
}
