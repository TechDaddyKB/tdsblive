using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class CustomWidgetTests
{
    private static readonly JsonSerializerOptions Json = EventStore.JsonOptions;
    private static OverlayDefinition Scene(params OverlayWidget[] widgets) => new() { Id = "custom-test", CanvasEnabled = true, Widgets = widgets };
    private static OverlayWidget Widget(params string[] permissions) => new() { Kind = "custom", Custom = new() { Permissions = permissions, Subscriptions = ["*"] } };
    private static async Task<HttpClient> Client(FoundationHostFactory app)
    {
        var client = app.CreateClient(); var csrf = await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf!["requestToken"]!.GetValue<string>()); return client;
    }
    [Theory]
    [InlineData("localhost")][InlineData("127.0.0.1")][InlineData("example.com/escape")][InlineData("*.example.com")]
    [InlineData("example.com:80")][InlineData("EXAMPLE.com")][InlineData("api.local")][InlineData("example.com\n")]
    public void NetworkPermissionsRejectLocalWildcardAndMalformedDomains(string domain) => Assert.Throws<ArgumentException>(() =>
        new CustomWidgetSettings { Permissions = ["network"], NetworkDomains = [domain] }.Validate());

    [Fact]
    public void ManifestSchemasAndCapabilityBoundsAreEnforced()
    {
        new CustomWidgetSettings { Permissions = ["network"], NetworkDomains = ["example.com"] }.Validate();
        foreach (var settings in new[] { new CustomWidgetSettings { ManifestVersion = 2 }, new() { Permissions = ["admin"] }, new() { Permissions = ["chat", "chat"] },
            new() { NetworkDomains = ["example.com"] }, new() { PackageVersion = "latest" }, new() { Html = new string('x', 131073) },
            new() { Fields = JsonNode.Parse("[{\"key\":\"__proto__\",\"label\":\"bad\",\"type\":\"text\"}]")!.AsArray() },
            new() { Fields = JsonNode.Parse("[{\"key\":\"size\",\"label\":\"size\",\"type\":\"unknown\"}]")!.AsArray() } }) Assert.Throws<ArgumentException>(settings.Validate);
        var now = DateTimeOffset.UtcNow;
        var item = new CanonicalEvent { Source = "test", Platform = "general", Type = "future.available", NativeType = "Owned", DedupeKey = "owned", OccurredAt = now };
        var custom = new CustomWidgetSettings { Subscriptions = ["*"] };
        Assert.True(custom.Accepts(item)); Assert.False(custom.Accepts(item with { Type = "chat.message" }));
        Assert.False(custom.Accepts(item with { Type = "support.rant" }));
        Assert.True((custom with { Permissions = ["chat", "financial"] }).Accepts(item with { Type = "chat.message" }));
    }

    [Fact]
    public async Task WidgetStoreIsPermissionScopedPreviewIsolatedAndDurable()
    {
        using var app = new FoundationHostFactory(); using var client = await Client(app);
        var store = app.Services.GetRequiredService<OverlayStore>(); var widget = Widget("storage"); var denied = Widget();
        Assert.True(await store.CreateAsync(Scene(widget, denied)));
        var path = $"/api/overlays/custom-test/widgets/{widget.Id}/store";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(path, new { counter = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(path + "?preview=1", new { counter = 99 })).StatusCode);
        Assert.Equal(3, (await client.GetFromJsonAsync<JsonObject>(path))!["counter"]!.GetValue<int>());
        Assert.Equal(99, (await client.GetFromJsonAsync<JsonObject>(path + "?preview=1"))!["counter"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/overlays/custom-test/widgets/{denied.Id}/store")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path.Replace("custom-test", "other"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new { text = new string('a', 33000) })).StatusCode);
        await using var db = app.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<FoundationDbContext>>().CreateDbContext();
        Assert.Contains("\"counter\":3", (await db.Configurations.FindAsync("widget-store:live:custom-test:" + widget.Id))!.Json);
        // A fresh DB context is a persisted SQLite read, independent of endpoint memory.
    }

    [Fact]
    public async Task PackagesRoundTripCodeAssetsGroupsAndRevokeImportedCapabilities()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var store = app.Services.GetRequiredService<OverlayStore>(); var packages = app.Services.GetRequiredService<PortablePackages>();
        var assets = app.Services.GetRequiredService<AssetStore>(); var hub = app.Services.GetRequiredService<EditorEventHub>();
        var asset = (await assets.UploadAsync(new MemoryStream(System.Text.Encoding.ASCII.GetBytes("GIF89a0000")), "owned.gif", "image/gif", null, default))!;
        var group = Guid.CreateVersion7().ToString(); var widget = Widget("storage", "chat") with { GroupId = group, Custom = new() {
            Permissions = ["storage", "chat"], AssetIds = [asset.Id], Html = $"<img src='{asset.Id}'>", Config = new() { ["count"] = 2 } } };
        var text = new OverlayWidget { GroupId = group };
        Assert.True(await store.CreateAsync(Scene(widget, text)));
        var bytes = (await packages.ExportAsync("custom-test", null, default))!;
        var prepared = packages.Validate(bytes); Assert.Equal(2, prepared.Widgets.Length); Assert.Single(prepared.Assets);
        var imported = await packages.ImportAsync(prepared, null, hub, default);
        Assert.NotEqual("custom-test", imported.Id); Assert.NotEqual(widget.Id, imported.Widgets[0].Id);
        Assert.Equal(imported.Widgets[0].GroupId, imported.Widgets[1].GroupId); Assert.NotEqual(group, imported.Widgets[0].GroupId);
        Assert.Empty(imported.Widgets[0].Custom.Permissions); Assert.Equal(widget.Custom.Html, imported.Widgets[0].Custom.Html);
        Assert.Equal(2, imported.Widgets[0].Custom.Config["count"]!.GetValue<int>());
        var one = packages.Validate((await packages.ExportAsync("custom-test", widget.Id, default))!);
        var appended = await packages.ImportAsync(one, "custom-test", hub, default); Assert.Equal(3, appended.Widgets.Length);
        Assert.NotEqual(widget.Id, appended.Widgets[2].Id);
    }

    [Theory]
    [InlineData("../outside")][InlineData("/absolute")][InlineData("assets/../escape")][InlineData("assets\\escape")][InlineData("C:/private")]
    public void TraversalAndAbsoluteArchiveEntriesAreRejected(string path)
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient(); var packages = app.Services.GetRequiredService<PortablePackages>();
        using var stream = new MemoryStream(); using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) { zip.CreateEntry("manifest.json"); zip.CreateEntry(path); }
        Assert.Throws<ArgumentException>(() => packages.Validate(stream.ToArray()));
    }

    [Fact]
    public async Task ExtraDuplicateOversizedAndSecretMembersAreRejectedBeforeImport()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var store = app.Services.GetRequiredService<OverlayStore>(); var packages = app.Services.GetRequiredService<PortablePackages>();
        var widget = Widget(); Assert.True(await store.CreateAsync(Scene(widget)));
        var valid = (await packages.ExportAsync("custom-test", null, default))!;
        foreach (var name in new[] { "unexpected.json", "MANIFEST.JSON" })
        {
            using var stream = new MemoryStream(); stream.Write(valid); using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true)) zip.CreateEntry(name);
            Assert.Throws<ArgumentException>(() => packages.Validate(stream.ToArray()));
        }
        using var huge = new MemoryStream(); using (var zip = new ZipArchive(huge, ZipArchiveMode.Create, true))
        {
            zip.CreateEntry("manifest.json"); using var entry = zip.CreateEntry("assets/oversized").Open(); entry.Write(new byte[21 * 1024 * 1024]);
        }
        Assert.Throws<ArgumentException>(() => packages.Validate(huge.ToArray()));
        var secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        app.Services.GetRequiredService<SensitiveValues>().Set("owned-test", secret);
        Assert.True(await store.SaveAsync(Scene(widget with { Custom = widget.Custom with { Html = secret } })));
        await Assert.ThrowsAsync<ArgumentException>(() => packages.ExportAsync("custom-test", null, default));
    }

    [Fact]
    public async Task RealSocketDeliversPerWidgetCapabilitiesAndRedactsRawCredentials()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        var store = app.Services.GetRequiredService<OverlayStore>();
        var raw = Widget("raw", "financial"); var ordinary = Widget(); var chat = Widget("chat");
        Assert.True(await store.CreateAsync(Scene(raw, ordinary, chat)));
        var client = app.Server.CreateWebSocketClient(); client.ConfigureRequest = request => request.Headers.Origin = "http://127.0.0.1";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var socket = await client.ConnectAsync(new Uri("ws://127.0.0.1/ws/overlay/custom-test"), timeout.Token);
        await socket.SendAsync(System.Text.Encoding.UTF8.GetBytes("""{"op":"subscribe","types":["*"]}"""), System.Net.WebSockets.WebSocketMessageType.Text, true, timeout.Token);
        var bytes = new byte[65536]; await socket.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token);
        var item = new CanonicalEvent { Source = "owned", Platform = "general", Type = "future.available", NativeType = "Synthetic",
            DedupeKey = "owned", OccurredAt = DateTimeOffset.UtcNow, Raw = new() { ["password"] = "synthetic-value", ["shape"] = 7 } };
        var hub = app.Services.GetRequiredService<EditorEventHub>(); hub.Publish(item);
        var frame = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token);
        using var message = JsonDocument.Parse(bytes.AsMemory(0, frame.Count));
        Assert.Equal("custom-events", message.RootElement.GetProperty("op").GetString());
        var delivered = message.RootElement.GetProperty("widgets").EnumerateArray().ToArray(); Assert.Equal(3, delivered.Length);
        Assert.Equal(CredentialRedactor.Replacement, delivered.Single(w => w.GetProperty("widgetId").GetString() == raw.Id).GetProperty("event").GetProperty("raw").GetProperty("password").GetString());
        Assert.Equal(JsonValueKind.Null, delivered.Single(w => w.GetProperty("widgetId").GetString() == ordinary.Id).GetProperty("event").GetProperty("raw").ValueKind);
        // Drain the legacy public event, then verify chat reaches only its grant.
        await socket.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token);
        hub.Publish(item with { Type = "chat.message" });
        frame = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token);
        using var chatMessage = JsonDocument.Parse(bytes.AsMemory(0, frame.Count));
        Assert.Equal(chat.Id, Assert.Single(chatMessage.RootElement.GetProperty("widgets").EnumerateArray()).GetProperty("widgetId").GetString());
        await socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "owned complete", timeout.Token);
    }

    [Fact]
    public async Task LimitedViewingTokenCanWriteOnlyItsGrantedWidgetStoreAndIsRevocable()
    {
        using var app = new FoundationHostFactory(true); using var client = app.CreateClient();
        var store = app.Services.GetRequiredService<OverlayStore>(); var widget = Widget("storage");
        Assert.True(await store.CreateAsync(Scene(widget))); var created = await store.CreateTokenAsync("custom-test", 1, default);
        var csrf = await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf!["requestToken"]!.GetValue<string>());
        client.DefaultRequestHeaders.Authorization = new("Bearer", created.Token);
        var path = $"/api/overlays/custom-test/widgets/{widget.Id}/store";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(path, new { count = 5 })).StatusCode);
        Assert.Equal(5, (await client.GetFromJsonAsync<JsonObject>(path))!["count"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path + "?preview=1", new { count = 6 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/overlays/custom-test", Scene(widget))).StatusCode);
        await store.RevokeAsync("custom-test", created.Info.Id, default);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task PortableHttpEndpointsValidateExtensionsAndRoundTripWithoutOverwriting()
    {
        using var app = new FoundationHostFactory(); using var client = await Client(app);
        var store = app.Services.GetRequiredService<OverlayStore>(); var widget = Widget();
        Assert.True(await store.CreateAsync(Scene(widget)));
        var exported = await client.GetAsync("/api/overlays/custom-test/export"); Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Contains(".sbxoverlay", exported.Content.Headers.ContentDisposition!.ToString());
        var bytes = await exported.Content.ReadAsByteArrayAsync();
        var body = new ByteArrayContent(bytes); body.Headers.Add("X-Package-Filename", "owned.sbxoverlay");
        var result = await client.PostAsync("/api/packages/import", body); Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var imported = await result.Content.ReadFromJsonAsync<OverlayDefinition>(Json); Assert.NotNull(imported); Assert.NotEqual("custom-test", imported.Id);
        body = new ByteArrayContent(bytes); body.Headers.Add("X-Package-Filename", "wrong.sbxwidget");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/packages/import", body)).StatusCode);
        body = new ByteArrayContent([1, 2, 3]); body.Headers.Add("X-Package-Filename", "bad.sbxoverlay");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/packages/import", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/overlays/missing/export")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/overlays/custom-test/export?widgetId={Guid.CreateVersion7()}")).StatusCode);
        var single = await client.GetAsync($"/api/overlays/custom-test/export?widgetId={widget.Id}");
        body = new ByteArrayContent(await single.Content.ReadAsByteArrayAsync()); body.Headers.Add("X-Package-Filename", "owned.sbxwidget");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/packages/import?overlayId=custom-test", body)).StatusCode);
        Assert.Equal(2, (await store.GetAsync("custom-test"))!.Widgets.Length);
    }
}
