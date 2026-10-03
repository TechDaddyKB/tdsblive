using System.Net;
using System.Net.Http.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;
public sealed class AdvancedEditorTests
{
    [Fact]
    public async Task GroupsTransformsAndWidgetSettingsPersistAndRestoreWithoutLosingMvpFields()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        http.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", (await http.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"))!.RequestToken);
        var group = Guid.NewGuid().ToString();
        var scene = new OverlayDefinition { Id = "advanced", CanvasEnabled = true, Widgets = [
            new() { GroupId = group, Rotation = 45, X = 100, Y = 250, Kind = "event-list", EventList = new() { Count = 5, Platforms = ["rumble"], IgnoredUsers = ["Ignored"] } },
            new() { GroupId = group, Rotation = 45, X = 400, Y = 500, Kind = "goal-bar", Progress = new() { Source = "ledger-usd", Target = 250 } },
            new() { Kind = "progress-bar", Progress = new() { Value = 25, Target = 50 } }, new() { Kind = "video", Muted = false, Volume = .25, Loop = false } ] };
        (await http.PostAsJsonAsync("/api/overlays", scene)).EnsureSuccessStatusCode();
        var loaded = (await http.GetFromJsonAsync<OverlayDefinition>("/api/overlays/advanced"))!;
        Assert.Equal(group, loaded.Widgets[0].GroupId); Assert.Equal(45, loaded.Widgets[1].Rotation); Assert.Equal(250, loaded.Widgets[1].Progress.Target);
        (await http.PutAsJsonAsync("/api/overlays/advanced", loaded with { Widgets = loaded.Widgets.Select(w => w with { GroupId = null, Rotation = 0 }).ToArray() })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/api/overlays/advanced/revisions/1/restore", new RestoreOverlayRevision(2))).EnsureSuccessStatusCode();
        loaded = (await http.GetFromJsonAsync<OverlayDefinition>("/api/overlays/advanced"))!;
        Assert.Equal(group, loaded.Widgets[0].GroupId); Assert.Equal(5, loaded.Widgets[0].EventList.Count);
        Assert.Equal(.25, loaded.Widgets[3].Volume); Assert.False(loaded.Widgets[3].Muted); Assert.False(loaded.Widgets[3].Loop);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync("/api/overlays/advanced", loaded with { Widgets = [new() { Kind = "goal-bar", Progress = new() { Target = 0 } }] })).StatusCode);
    }
    [Fact]
    public async Task LimitedEventHistoryIsFilteredLiveOnlyAndExcludesRawAndFinancialPayloads()
    {
        using var app = new FoundationHostFactory(true); using var admin = app.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", app.AdminCredential);
        admin.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", (await admin.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"))!.RequestToken);
        var scene = new OverlayDefinition { Id = "event-history", CanvasEnabled = true, Widgets = [new() { Kind = "event-list", EventList = new() { Platforms = ["rumble"], IgnoredUsers = ["Ignored"] } }] };
        (await admin.PostAsJsonAsync("/api/overlays", scene)).EnsureSuccessStatusCode();
        var store = app.Services.GetRequiredService<EventStore>();
        var item = new CanonicalEvent { Source = "owned", Platform = "rumble", Type = "community.follow", NativeType = "Follow", DedupeKey = "one", OccurredAt = DateTimeOffset.UtcNow,
            User = new(DisplayName: "Viewer"), Raw = new() { ["owned"] = true }, Monetary = new(100, "USD", "actual") };
        await store.AcceptAsync(item, "1");
        foreach (var other in new[] { item with { Id = Guid.CreateVersion7(), DedupeKey = "ignored", User = new(DisplayName: "Ignored") },
            item with { Id = Guid.CreateVersion7(), DedupeKey = "other-platform", Platform = "twitch" },
            item with { Id = Guid.CreateVersion7(), DedupeKey = "simulation", Provenance = EventProvenance.Simulation } }) await store.AcceptAsync(other, "2", persistTest: true);
        var token = (await (await admin.PostAsJsonAsync("/api/overlays/event-history/tokens", new CreateOverlayToken())).Content.ReadFromJsonAsync<CreatedOverlayToken>())!;
        using var viewer = app.CreateClient(); viewer.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        var history = (await viewer.GetFromJsonAsync<CanonicalEvent[]>("/api/overlays/event-history/events", EventStore.JsonOptions))!;
        Assert.Single(history); Assert.Equal(item.Id, history[0].Id); Assert.Null(history[0].Raw); Assert.Null(history[0].Monetary); Assert.Null(history[0].Support);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/overlays/combined-chat/events")).StatusCode);
    }
}
