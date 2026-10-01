using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using ExtensionSuite.Overlays;
using ExtensionSuite.Rumble;
using ExtensionSuite.StreamerBot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class OverlayRuntimeTests
{
    private static CanonicalEvent Message(string platform = "rumble", string text = "Synthetic chat") => new() {
        Source = platform == "rumble" ? "rumble" : "streamerbot", Platform = platform, Type = "chat.message", NativeType = "ChatMessage",
        OccurredAt = DateTimeOffset.UtcNow, DedupeKey = Guid.CreateVersion7().ToString(), User = new("synthetic-id", "synthetic-viewer", "Synthetic Viewer"),
        Message = new(text), Raw = new() { ["privatePayload"] = "not for overlay" } };
    private static async Task Csrf(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", response!.RequestToken);
    }
    private static async Task<JsonObject> Read(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var bytes = new byte[65536]; var length = 0; WebSocketReceiveResult result;
        do { result = await socket.ReceiveAsync(new ArraySegment<byte>(bytes, length, bytes.Length - length), timeout.Token); length += result.Count; } while (!result.EndOfMessage);
        return JsonNode.Parse(Encoding.UTF8.GetString(bytes, 0, length))!.AsObject();
    }
    private static Task Send(WebSocket socket, string value) => socket.SendAsync(Encoding.UTF8.GetBytes(value), WebSocketMessageType.Text, true, CancellationToken.None);

    [Fact]
    public async Task RepeatedRumbleSnapshotsDeliverOneDetectedChatToOverlay()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        var ws = app.Server.CreateWebSocketClient(); ws.ConfigureRequest = r => r.Headers["Origin"] = "http://localhost";
        using var socket = await ws.ConnectAsync(new Uri("ws://localhost/ws/overlay/combined-chat?preview=1"), default);
        await Send(socket, """{"op":"subscribe","types":["chat.message"]}"""); await Read(socket);
        var store = app.Services.GetRequiredService<RumbleStore>(); var engine = new RumbleSnapshotEngine(new());
        var payload = JsonNode.Parse("""{"type":"user","user_id":"synthetic-overlay","livestreams":[{"id":"stream","is_live":true,"chat":{"recent_messages":[]}}]}""")!.AsObject();
        var messages = payload["livestreams"]![0]!["chat"]!["recent_messages"]!.AsArray();
        var accepted = new List<CanonicalEvent>(); var now = DateTimeOffset.UtcNow;
        for (var poll = 0; poll < 5; poll++)
        {
            if (poll == 1) messages.Add(new JsonObject { ["username"] = "synthetic-viewer", ["text"] = "Repeated snapshot check", ["created_on"] = now.ToString("O") });
            var batch = engine.Reconcile(await store.LoadAsync("overlay-replay", EventProvenance.Replay, default),
                new(now.AddSeconds(poll * 7), "ok", 200, payload), "overlay-replay", poll == 0, EventProvenance.Replay);
            accepted.AddRange(await store.CommitAsync("overlay-replay", EventProvenance.Replay, batch, true, true, default));
        }
        var chat = Assert.Single(accepted, item => item.Type == "chat.message");
        var received = await Read(socket); Assert.Equal(chat.Id.ToString(), received["event"]!["id"]!.GetValue<string>());
        await Send(socket, """{"op":"ping"}"""); Assert.Equal("pong", (await Read(socket))["op"]!.GetValue<string>());
        Assert.Empty(await store.PendingTriggersAsync(default));
    }

    [Fact]
    public async Task SettingsValidatePersistVersionAndFilterHistoryBeforeRendering()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient(); await Csrf(client);
        var settings = (await client.GetFromJsonAsync<OverlayDefinition>("/api/overlays/combined-chat"))!;
        var value = settings with { Chat = settings.Chat with { Platforms = ["rumble"], IgnoredPrefixes = ["!"] } };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/overlays/combined-chat", value)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/overlays/combined-chat", value)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/overlays/combined-chat", value with { Chat = value.Chat with { MaximumMessages = 501 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/overlays/combined-chat", value with { Chat = value.Chat with { FontAssetId = new string('a', 64) } })).StatusCode);
        var store = app.Services.GetRequiredService<EventStore>();
        foreach (var message in new[] { Message(), Message("twitch"), Message(text: "!command") }) await store.AcceptAsync(message, "chat-test");
        await store.AcceptAsync(Message() with { Provenance = EventProvenance.Simulation }, "simulation", true);
        var history = (await client.GetFromJsonAsync<CanonicalEvent[]>("/api/overlays/combined-chat/chat", EventStore.JsonOptions))!;
        Assert.Single(history); Assert.Null(history[0].Raw); Assert.Null(history[0].Monetary);
        var persisted = await new OverlayStore(app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(), new(), TimeProvider.System).GetAsync("combined-chat");
        Assert.Equal(2, persisted!.Version); Assert.Equal(["rumble"], persisted.Chat.Platforms);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/overlays/missing")).StatusCode);
    }

    [Fact]
    public async Task OverlaySocketCannotExpandSubscriptionsAndNeverReceivesRawOrDefaultSimulation()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        var ws = app.Server.CreateWebSocketClient(); ws.ConfigureRequest = request => request.Headers["Origin"] = "http://localhost";
        using var socket = await ws.ConnectAsync(new Uri("ws://localhost/ws/overlay/combined-chat"), default);
        await Send(socket, """{"op":"subscribe","types":["chat.message"]}"""); Assert.Equal("subscribed", (await Read(socket))["op"]!.GetValue<string>());
        var hub = app.Services.GetRequiredService<EditorEventHub>();
        hub.Publish(Message("twitch") with { Type = "support.bits" }); hub.Publish(Message() with { Provenance = EventProvenance.Simulation });
        var message = Message(); hub.Publish(message);
        var received = await Read(socket); Assert.Equal(message.Id.ToString(), received["event"]!["id"]!.GetValue<string>()); Assert.Null(received["event"]!["raw"]);
        var definition = (await app.Services.GetRequiredService<OverlayStore>().GetAsync("combined-chat"))!;
        hub.UpdateOverlay(definition with { Chat = definition.Chat with { Platforms = ["kick"] } }); Assert.Equal("settings", (await Read(socket))["op"]!.GetValue<string>());
        hub.Publish(Message()); hub.Publish(Message("kick")); Assert.Equal("kick", (await Read(socket))["event"]!["platform"]!.GetValue<string>());
        await Send(socket, """{"op":"subscribe","types":["*"]}""");
        var close = await socket.ReceiveAsync(new byte[100], new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token); Assert.Equal(WebSocketMessageType.Close, close.MessageType);
    }

    [Fact]
    public async Task PreviewRequiresExplicitUrlAndCanDisplayIsolatedSimulation()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        var ws = app.Server.CreateWebSocketClient(); ws.ConfigureRequest = r => r.Headers["Origin"] = "http://localhost";
        using var socket = await ws.ConnectAsync(new Uri("ws://localhost/ws/overlay/combined-chat?preview=1"), default);
        await Send(socket, """{"op":"subscribe","types":["chat.message"]}"""); await Read(socket);
        app.Services.GetRequiredService<EditorEventHub>().Publish(Message() with { Provenance = EventProvenance.Simulation });
        Assert.Equal("simulation", (await Read(socket))["event"]!["provenance"]!.GetValue<string>());
    }

    [Fact]
    public async Task UpgradeFromG04PreservesExistingChatAndRecreatesDefaultOverlay()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        var store = app.Services.GetRequiredService<EventStore>(); var message = Message(); await store.AcceptAsync(message, "upgrade-test");
        await using var db = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>().CreateDbContext();
        var migrator = db.Database.GetService<IMigrator>(); await migrator.MigrateAsync("20261001131537_RumbleIngestion");
        await migrator.MigrateAsync(); await app.Services.GetRequiredService<OverlayStore>().InitializeAsync();
        var history = (await http.GetFromJsonAsync<CanonicalEvent[]>("/api/overlays/combined-chat/chat", EventStore.JsonOptions))!;
        Assert.Equal(message.Id, Assert.Single(history).Id); Assert.False(history[0].User!.IsBot);
        Assert.Equal(1, (await app.Services.GetRequiredService<OverlayStore>().GetAsync("combined-chat"))!.Version);
    }

    [Fact]
    public async Task LanTokenIsReadOnlyScopedRevocableAndNotReturnedByListing()
    {
        using var app = new FoundationHostFactory(true); using var admin = app.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", app.AdminCredential); await Csrf(admin);
        var minted = (await (await admin.PostAsJsonAsync("/api/overlays/combined-chat/tokens", new CreateOverlayToken())).Content.ReadFromJsonAsync<CreatedOverlayToken>())!;
        using var viewer = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/overlays/combined-chat")).StatusCode);
        viewer.DefaultRequestHeaders.Authorization = new("Bearer", minted.Token);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/overlays/combined-chat")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/overlays/combined-chat/chat")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/overlays/other/chat")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/configuration")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/overlays/combined-chat/tokens")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.PutAsJsonAsync("/api/overlays/combined-chat", new OverlayDefinition())).StatusCode);
        var font = (await app.Services.GetRequiredService<AssetStore>().UploadAsync(new MemoryStream(Encoding.ASCII.GetBytes("wOF20000")), "font.woff2", "font/woff2", "OFL-1.1", default))!;
        var definition = (await admin.GetFromJsonAsync<OverlayDefinition>("/api/overlays/combined-chat"))!;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/overlays/combined-chat", definition with { Chat = definition.Chat with { FontAssetId = font.Id } })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/assets/" + font.Id)).StatusCode);
        viewer.DefaultRequestHeaders.Add("X-TDSBLive-Overlay", "combined-chat");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/assets/" + font.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/assets/" + new string('a', 64))).StatusCode);
        var list = await admin.GetStringAsync("/api/overlays/combined-chat/tokens"); Assert.DoesNotContain(minted.Token, list);
        var ws = app.Server.CreateWebSocketClient(); ws.SubProtocols.Add("tdsblive.overlay.v1"); ws.SubProtocols.Add(minted.Token); ws.ConfigureRequest = r => r.Headers["Origin"] = "http://localhost";
        using var socket = await ws.ConnectAsync(new Uri("ws://localhost/ws/overlay/combined-chat"), default);
        await Send(socket, """{"op":"subscribe","types":["chat.message"]}"""); await Read(socket);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/overlays/combined-chat/tokens/{minted.Info.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/overlays/combined-chat/chat")).StatusCode);
        var close = await socket.ReceiveAsync(new byte[100], new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token); Assert.Equal(WebSocketMessageType.Close, close.MessageType);
    }

    [Fact]
    public async Task AssetsDeduplicateSanitizeServeOnlyIdsAndRejectTraversalOrWrongMime()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient(); await Csrf(client);
        async Task<HttpResponseMessage> Upload(string name, string mime, string body) {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/assets") { Content = new StringContent(body, Encoding.UTF8, mime) };
            request.Content.Headers.ContentType = new(mime); request.Headers.Add("X-Asset-Filename", name);
            return await client.SendAsync(request);
        }
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" onload="alert(1)"><script>alert(1)</script><rect width="20" height="20" fill="red"/><image href="https://example.invalid/private"/></svg>""";
        var first = (await (await Upload("icon.svg", "image/svg+xml", svg)).Content.ReadFromJsonAsync<AssetInfo>())!;
        var duplicate = (await (await Upload("second.svg", "image/svg+xml", svg)).Content.ReadFromJsonAsync<AssetInfo>())!;
        Assert.Equal(first.Id, duplicate.Id); Assert.True(first.Sanitized);
        var served = await client.GetStringAsync("/assets/" + first.Id); Assert.Contains("rect", served); Assert.DoesNotContain("script", served); Assert.DoesNotContain("href", served); Assert.DoesNotContain("onload", served);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload("../escape.svg", "image/svg+xml", svg)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload("mismatch.png", "image/png", svg)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload("entity.svg", "image/svg+xml", """<!DOCTYPE svg [<!ENTITY x SYSTEM "file:///etc/passwd">]><svg xmlns="http://www.w3.org/2000/svg">&x;</svg>""")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/assets/not-a-hash")).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<AssetInfo[]>("/api/assets"))!);
        var upload = new HttpRequestMessage(HttpMethod.Post, "/api/assets") { Content = new ByteArrayContent(new byte[AssetValidation.MaximumBytes + 1]) };
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.SendAsync(upload)).StatusCode);
    }

    [Theory]
    [InlineData("Twitch", "ChatMessage", "messageId", "avatarUrl")]
    [InlineData("YouTube", "Message", "eventId", "profileImageUrl")]
    [InlineData("Kick", "ChatMessage", "messageId", "profilePicture")]
    public async Task DocumentedPlatformPayloadsRetainIdentityAndReachDurableChatHistory(string platform, string type, string idField, string avatarField)
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var envelope = new JsonObject { ["event"] = new JsonObject { ["source"] = platform, ["type"] = type }, ["data"] = new JsonObject {
            [idField] = "synthetic-native-message", [platform == "YouTube" ? "publishedAt" : "createdAt"] = "2026-10-01T00:00:00Z",
            [platform == "YouTube" ? "message" : "text"] = "Synthetic documented chat", ["user"] = new JsonObject {
                ["id"] = "synthetic-user", ["login"] = "viewer", ["name"] = "Viewer", [avatarField] = "https://example.invalid/avatar.png",
                ["badges"] = new JsonArray(new JsonObject { ["name"] = "moderator" }) } } };
        var item = new StreamerBotEventNormalizer(new()).Normalize(envelope, DateTimeOffset.UtcNow).Event!;
        Assert.Equal("synthetic-native-message", item.NativeId); Assert.Equal("https://example.invalid/avatar.png", item.User!.AvatarUrl);
        Assert.Equal("chat.message", item.Type); Assert.Equal(["moderator"], item.User.Badges!);
        var store = app.Services.GetRequiredService<EventStore>(); Assert.True(await store.AcceptAsync(item, "native")); Assert.False(await store.AcceptAsync(item, "native"));
        var history = (await client.GetFromJsonAsync<CanonicalEvent[]>("/api/overlays/combined-chat/chat", EventStore.JsonOptions))!;
        Assert.Single(history); Assert.Equal(platform.ToLowerInvariant(), history[0].Platform); Assert.Null(history[0].Raw);
    }
}
