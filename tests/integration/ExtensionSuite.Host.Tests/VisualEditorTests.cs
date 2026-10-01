using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class VisualEditorTests
{
    private static async Task Csrf(HttpClient http) => http.DefaultRequestHeaders.Add("X-TDSBLive-CSRF",
        (await http.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"))!.RequestToken);
    [Fact]
    public async Task CreateSaveRestartRestoreAndConflictKeepBoundedChronologicalRevisions()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient(); await Csrf(http);
        var original = new OverlayDefinition { Id = "portrait", Name = "Portrait", Width = 1080, Height = 1920,
            CanvasEnabled = true, Widgets = [new() { Text = "Original" }] };
        Assert.Equal(HttpStatusCode.Created, (await http.PostAsJsonAsync("/api/overlays", original)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/overlays", original)).StatusCode);
        var saved = original;
        for (var i = 0; i < 55; i++)
        {
            var response = await http.PutAsJsonAsync("/api/overlays/portrait", saved with { Name = "Revision " + i });
            response.EnsureSuccessStatusCode(); saved = (await response.Content.ReadFromJsonAsync<OverlayDefinition>())!;
        }
        Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync("/api/overlays/portrait", original)).StatusCode);
        var rows = (await http.GetFromJsonAsync<OverlayRevision[]>("/api/overlays/portrait/revisions"))!;
        Assert.Equal(50, rows.Length); Assert.Equal(56, rows[0].Version); Assert.Equal(7, rows[^1].Version);
        var fresh = new OverlayStore(app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(), new(), TimeProvider.System);
        Assert.Equal(JsonSerializer.Serialize(saved, EventStore.JsonOptions), JsonSerializer.Serialize(await fresh.GetAsync("portrait"), EventStore.JsonOptions));
        var responseRestore = await http.PostAsJsonAsync("/api/overlays/portrait/revisions/7/restore", new RestoreOverlayRevision(saved.Version));
        responseRestore.EnsureSuccessStatusCode(); var restored = (await responseRestore.Content.ReadFromJsonAsync<OverlayDefinition>())!;
        Assert.Equal(57, restored.Version); Assert.Equal("Revision 5", restored.Name); Assert.Equal(original.Widgets[0].Text, restored.Widgets[0].Text);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/overlays/portrait/revisions/8/restore", new RestoreOverlayRevision(56))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsJsonAsync("/api/overlays/portrait/revisions/1/restore", new RestoreOverlayRevision(57))).StatusCode);
        Assert.Equal(50, (await fresh.RevisionsAsync("portrait")).Length);
    }
    [Fact]
    public async Task InvalidCanvasAssetsAndUnprotectedMutationDoNotCreateOverlays()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        var value = new OverlayDefinition { Id = "unsafe", CanvasEnabled = true };
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsJsonAsync("/api/overlays", value)).StatusCode);
        await Csrf(http);
        foreach (var invalid in new[] { value with { Width = 8000 }, value with { Widgets = [new() { X = double.PositiveInfinity }] },
            value with { Widgets = [new() { Kind = "image", AssetId = new('a', 64) }] }, value with { RevisionLimit = 201 } })
        {
            // JSON itself rejects non-finite numbers; validation below covers that case independently.
            if (invalid.Widgets.Any(w => !double.IsFinite(w.X))) { Assert.Throws<ArgumentException>(invalid.Validate); continue; }
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/overlays", invalid)).StatusCode);
        }
        Assert.DoesNotContain((await http.GetFromJsonAsync<OverlayDefinition[]>("/api/overlays"))!, o => o.Id == "unsafe");
    }
    [Fact]
    public async Task PreviewInjectionReachesOnlyTargetPreviewWithoutPersistenceOrOutbox()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient(); await Csrf(http);
        var definition = new OverlayDefinition { Id = "alerts", CanvasEnabled = true, Widgets = [new() { Kind = "alert" }] };
        (await http.PostAsJsonAsync("/api/overlays", definition)).EnsureSuccessStatusCode();
        var ws = app.Server.CreateWebSocketClient(); ws.ConfigureRequest = r => r.Headers["Origin"] = "http://localhost";
        using var preview = await ws.ConnectAsync(new("ws://localhost/ws/overlay/alerts?preview=1"), default);
        await preview.SendAsync(Encoding.UTF8.GetBytes("{\"op\":\"subscribe\",\"types\":[\"community.follow\"]}"), WebSocketMessageType.Text, true, default);
        var bytes = new byte[16384]; using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await preview.ReceiveAsync(bytes, timeout.Token);
        (await http.PostAsJsonAsync("/api/overlays/alerts/preview-events", new PreviewEventRequest("community.follow"))).EnsureSuccessStatusCode();
        var frame = await preview.ReceiveAsync(bytes, timeout.Token);
        using var payload = JsonDocument.Parse(bytes.AsMemory(0, frame.Count));
        Assert.Equal("simulation", payload.RootElement.GetProperty("event").GetProperty("provenance").GetString());
        await using var db = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>().CreateDbContext();
        Assert.Empty(await db.Events.ToArrayAsync()); Assert.Empty(await db.Outbox.ToArrayAsync()); Assert.Empty(await db.RumbleDeliveries.ToArrayAsync());
    }
    [Fact]
    public void WidgetsRejectDuplicateIdentitiesInvalidQueuePoliciesAndInconsistentGroups()
    {
        var widget = new OverlayWidget { Kind = "alert" };
        Assert.Throws<ArgumentException>(() => new OverlayDefinition { Widgets = [widget, widget] }.Validate());
        Assert.Throws<ArgumentException>(() => (widget with { Alert = widget.Alert with { InterruptPolicy = "always" } }).Validate());
        Assert.Throws<ArgumentException>(() => new OverlayDefinition { Widgets = [widget,
            widget with { Id = Guid.CreateVersion7().ToString(), Alert = widget.Alert with { Concurrency = 2 } }] }.Validate());
        (widget with { Alert = widget.Alert with { Group = "sounds", Concurrency = 2, SoundAssetId = new('b', 64) } }).Validate();
    }
    [Fact]
    public async Task RevisionInsertFailureRollsBackDocumentAndConcurrentWritesAcceptExactlyOneVersion()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        var store = app.Services.GetRequiredService<OverlayStore>(); var original = (await store.GetAsync("combined-chat"))!;
        await using var db = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>().CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_revision BEFORE INSERT ON OverlayRevisions BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveAsync(original with { Name = "Must roll back" }));
        Assert.Equal(original.Version, (await store.GetAsync(original.Id))!.Version); Assert.Single(await store.RevisionsAsync(original.Id));
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_revision;");
        var results = await Task.WhenAll(store.SaveAsync(original with { Name = "First writer" }), store.SaveAsync(original with { Name = "Second writer" }));
        Assert.Single(results, v => v); Assert.Equal(original.Version + 1, (await store.GetAsync(original.Id))!.Version);
        Assert.Equal(2, (await store.RevisionsAsync(original.Id)).Length);
    }
    [Fact]
    public async Task ScopedLanTokenServesOnlyReferencedMediaAndCannotCreateOrRestore()
    {
        using var app = new FoundationHostFactory(true); using var admin = app.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", app.AdminCredential); await Csrf(admin);
        var assets = app.Services.GetRequiredService<AssetStore>();
        var image = (await assets.UploadAsync(new MemoryStream(Encoding.ASCII.GetBytes("GIF89a00000000")), "owned.gif", "image/gif", null, default))!;
        var unused = (await assets.UploadAsync(new MemoryStream(Encoding.ASCII.GetBytes("GIF89a11111111")), "unused.gif", "image/gif", null, default))!;
        var scene = new OverlayDefinition { Id = "private-media", CanvasEnabled = true, Widgets = [new() { Kind = "image", AssetId = image.Id }] };
        (await admin.PostAsJsonAsync("/api/overlays", scene)).EnsureSuccessStatusCode();
        var token = (await (await admin.PostAsJsonAsync("/api/overlays/private-media/tokens", new CreateOverlayToken())).Content.ReadFromJsonAsync<CreatedOverlayToken>())!;
        using var viewer = app.CreateClient(); viewer.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        viewer.DefaultRequestHeaders.Add("X-TDSBLive-Overlay", scene.Id);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/assets/" + image.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.SendAsync(new(HttpMethod.Head, "/assets/" + image.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/assets/" + unused.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/overlays/private-media/revisions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.PostAsJsonAsync("/api/overlays/private-media/revisions/1/restore", new RestoreOverlayRevision(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.PostAsJsonAsync("/api/overlays/private-media/preview-events", new PreviewEventRequest("community.follow"))).StatusCode);
    }
    [Fact]
    public async Task NativeInjectionForcesSimulationAndRejectsInvalidOrReturnedBridgeEnvelopes()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient(); await Csrf(http);
        (await http.PostAsJsonAsync("/api/overlays", new OverlayDefinition { Id = "native-preview", CanvasEnabled = true,
            Widgets = [new() { Kind = "alert" }] })).EnsureSuccessStatusCode();
        var ws = app.Server.CreateWebSocketClient(); ws.ConfigureRequest = r => r.Headers["Origin"] = "http://localhost";
        using var preview = await ws.ConnectAsync(new("ws://localhost/ws/overlay/native-preview?preview=1"), default);
        await preview.SendAsync(Encoding.UTF8.GetBytes("{\"op\":\"subscribe\",\"types\":[\"*\"]}"), WebSocketMessageType.Text, true, default);
        var bytes = new byte[16384]; using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await preview.ReceiveAsync(bytes, timeout.Token);
        var native = System.Text.Json.Nodes.JsonNode.Parse("{\"event\":{\"source\":\"Twitch\",\"type\":\"Follow\"},\"data\":{\"userName\":\"Synthetic native viewer\",\"tdsbliveProvenance\":\"live\"}}")!.AsObject();
        (await http.PostAsJsonAsync("/api/overlays/native-preview/preview-events", new PreviewEventRequest("unused", Raw: native, Mode: "native"))).EnsureSuccessStatusCode();
        var frame = await preview.ReceiveAsync(bytes, timeout.Token); using var payload = JsonDocument.Parse(bytes.AsMemory(0, frame.Count));
        var item = payload.RootElement.GetProperty("event"); Assert.Equal("simulation", item.GetProperty("provenance").GetString());
        Assert.Equal("overlay-preview", item.GetProperty("source").GetString()); Assert.Equal("community.follow", item.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("raw").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/overlays/native-preview/preview-events", new PreviewEventRequest("unused", Raw: new(), Mode: "native"))).StatusCode);
        native["data"]!["tdsbliveOrigin"] = "tdsblive";
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/overlays/native-preview/preview-events", new PreviewEventRequest("unused", Raw: native, Mode: "native"))).StatusCode);
        await using var db = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>().CreateDbContext(); Assert.Empty(await db.Events.ToArrayAsync()); Assert.Empty(await db.Outbox.ToArrayAsync());
    }

}
