using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using ExtensionSuite.StreamerBot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class BotIntegrationTests
{
    [Fact]
    public async Task HostedIngestionPersistsOnlyLiveAndRejectsReturnedBridgeEvents()
    {
        await using var server = await FakeBot.Start(async socket =>
        {
            await FakeBot.Send(socket, new() { ["request"] = "Hello" });
            while (socket.State == WebSocketState.Open)
            {
                var request = await FakeBot.Read(socket);
                var operation = request["request"]!.GetValue<string>();
                var response = operation switch
                {
                    "GetEvents" => new JsonObject { ["events"] = new JsonObject { ["Twitch"] = new JsonArray("ChatMessage") } },
                    "GetActions" => new JsonObject { ["actions"] = new JsonArray() },
                    "GetCodeTriggers" => new JsonObject { ["triggers"] = new JsonArray() },
                    _ => new JsonObject()
                };
                await FakeBot.Reply(socket, request, response);
                if (operation != "Subscribe") continue;
                foreach (var kind in new[] { "live", "simulation", "loop" })
                    await FakeBot.Send(socket, new() { ["event"] = new JsonObject { ["source"] = "Twitch", ["type"] = "ChatMessage" },
                        ["data"] = new JsonObject { ["messageId"] = kind, ["text"] = "synthetic", ["isTest"] = kind == "simulation", ["tdsbliveOrigin"] = kind == "loop" ? "tdsblive" : "external" } });
            }
        });
        await using var factory = new FoundationHostFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton(new StreamerBotConnection(server.Configuration, () => null, new()))));
        using var client = configured.CreateClient();
        var inspector = configured.Services.GetRequiredService<EventInspectorStore>();
        await Until(() => inspector.Read().Length == 3);
        var store = configured.Services.GetRequiredService<EventStore>();
        Assert.Single(await store.ReadAsync()); Assert.Empty(await store.ReadAsync(provenance: EventProvenance.Simulation));
        Assert.Contains(inspector.Read(), entry => entry.Classification == "bridgeLoop");
    }

    [Theory]
    [InlineData(false, "missingCredential")]
    [InlineData(true, "authenticationFailed")]
    public async Task ReportsAuthenticationFailuresWithoutDiscovery(bool hasCredential, string failure)
    {
        await using var server = await FakeBot.Start(async socket =>
        {
            await FakeBot.Send(socket, new() { ["request"] = "Hello", ["authentication"] = new JsonObject { ["salt"] = "test-salt", ["challenge"] = "test-challenge" } });
            if (hasCredential) await FakeBot.Reply(socket, await FakeBot.Read(socket), new(), "error");
            await FakeBot.WaitForClose(socket);
        });
        var adapter = new StreamerBotConnection(server.Configuration, () => hasCredential ? "synthetic-only" : null, new());
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = adapter.RunAsync((_, _) => Task.CompletedTask, lifetime.Token);
        await Until(() => adapter.State.FailureKind == failure);
        Assert.Equal("authenticationFailed", adapter.State.State); Assert.Empty(adapter.Discovery.Actions);
        await lifetime.CancelAsync(); await run.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task MalformedResponseAndRemoteRejectionHaveSafeSemantics()
    {
        await using var server = await FakeBot.Start(async socket =>
        {
            var first = await FakeBot.Read(socket);
            await FakeBot.Send(socket, new() { ["id"] = first["id"]!.DeepClone(), ["status"] = 7 });
            await FakeBot.Reply(socket, await FakeBot.Read(socket), new() { ["error"] = "synthetic-private-value" }, "error");
            await FakeBot.WaitForClose(socket);
        });
        await using var session = new BotProtocolSession(); await session.ConnectAsync(server.Uri, false, default);
        var malformed = await Assert.ThrowsAsync<BotRequestException>(() => session.RequestAsync("One", null, TimeSpan.FromSeconds(2), default));
        Assert.True(malformed.MayHaveExecuted); Assert.Equal("invalidResponse", malformed.Kind);
        var rejected = await Assert.ThrowsAsync<BotRequestException>(() => session.RequestAsync("Two", null, TimeSpan.FromSeconds(2), default));
        Assert.False(rejected.MayHaveExecuted); Assert.Equal("rejected", rejected.Kind); Assert.DoesNotContain("synthetic-private-value", rejected.Message);
    }

    [Fact]
    public async Task DisabledAndDisconnectedSpeakerNeverSend()
    {
        var adapter = new SpeakerBotConnection(new("127.0.0.1", 1));
        await adapter.RunAsync(default); Assert.Equal("disabled", adapter.State.State);
        Assert.Equal("disconnected", (await adapter.QueueAsync("Pause", null, true, default)).State);
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.QueueAsync("Events", "invalid", true, default));
        for (var index = 0; index < 201; index++) await adapter.QueueAsync("Pause", null, false, default);
        Assert.Equal(200, adapter.Executions.Length);
    }

    [Fact]
    public async Task CorrelatesOutOfOrderResponsesAndSeparatesEvents()
    {
        await using var server = await FakeBot.Start(async socket =>
        {
            await FakeBot.Send(socket, new() { ["request"] = "Hello" });
            var first = await FakeBot.Read(socket); var second = await FakeBot.Read(socket);
            await FakeBot.Send(socket, new() { ["event"] = new JsonObject { ["source"] = "Twitch", ["type"] = "ChatMessage" } });
            await FakeBot.Reply(socket, second, new() { ["value"] = "second" });
            await FakeBot.Reply(socket, first, new() { ["value"] = "first" });
            await FakeBot.WaitForClose(socket);
        });
        await using var session = new BotProtocolSession();
        await session.ConnectAsync(server.Uri, true, default);
        var first = session.RequestAsync("One", null, TimeSpan.FromSeconds(3), default);
        var second = session.RequestAsync("Two", null, TimeSpan.FromSeconds(3), default);
        Assert.Equal("first", (await first)["value"]!.GetValue<string>());
        Assert.Equal("second", (await second)["value"]!.GetValue<string>());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        Assert.Equal("Twitch", (await session.Events.ReadAsync(deadline.Token))["event"]!["source"]!.GetValue<string>());
    }

    [Fact]
    public async Task TimeoutReportsAmbiguityWithoutRetry()
    {
        var requests = 0;
        await using var server = await FakeBot.Start(async socket =>
        {
            await FakeBot.Read(socket); Interlocked.Increment(ref requests);
            await FakeBot.WaitForClose(socket);
        });
        await using var session = new BotProtocolSession();
        await session.ConnectAsync(server.Uri, false, default);
        var error = await Assert.ThrowsAsync<BotRequestException>(() => session.RequestAsync("DoAction", null, TimeSpan.FromMilliseconds(100), default));
        Assert.Equal("timeout", error.Kind); Assert.True(error.MayHaveExecuted); Assert.Equal(1, requests);
    }

    [Fact]
    public async Task RejectsMalformedHelloAndMalformedResponse()
    {
        await using var server = await FakeBot.Start(async socket =>
        {
            await FakeBot.Send(socket, new() { ["request"] = 42 });
            await FakeBot.WaitForClose(socket);
        });
        await using var session = new BotProtocolSession();
        Assert.Equal("invalidHello", (await Assert.ThrowsAsync<BotRequestException>(() => session.ConnectAsync(server.Uri, true, default))).Kind);
    }

    [Fact]
    public async Task AuthenticationDiscoveryActionSelectionAndUnsupportedTriggers()
    {
        var actionId = Guid.NewGuid(); var actionCalls = 0; var authenticated = false;
        await using var server = await FakeBot.Start(async socket =>
        {
            await FakeBot.Send(socket, new() { ["request"] = "Hello", ["info"] = new JsonObject { ["version"] = "test" },
                ["authentication"] = new JsonObject { ["salt"] = "synthetic-salt", ["challenge"] = "synthetic-challenge" } });
            while (socket.State == WebSocketState.Open)
            {
                var request = await FakeBot.Read(socket);
                var response = new JsonObject();
                switch (request["request"]!.GetValue<string>())
                {
                    case "Authenticate":
                        authenticated = request["authentication"]!.GetValue<string>() == StreamerBotConnection.AuthenticationResponse("synthetic-only", "synthetic-salt", "synthetic-challenge");
                        Assert.True(authenticated); break;
                    case "GetEvents": response["events"] = new JsonObject { ["Twitch"] = new JsonArray("ChatMessage") }; break;
                    case "GetActions": response["actions"] = new JsonArray(new JsonObject { ["id"] = actionId.ToString(), ["name"] = "test", ["enabled"] = true }); break;
                    case "GetCodeTriggers": await FakeBot.Reply(socket, request, new(), "error"); continue;
                    case "DoAction": Interlocked.Increment(ref actionCalls); break;
                }
                await FakeBot.Reply(socket, request, response);
            }
        });
        var adapter = new StreamerBotConnection(server.Configuration with { AllowedActionIds = [actionId], ForwardLiveEvents = true }, () => "synthetic-only", new());
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = adapter.RunAsync((_, _) => Task.CompletedTask, lifetime.Token);
        await Until(() => adapter.State.State == "connected");
        Assert.True(authenticated); Assert.True(adapter.Discovery.EventsSupported); Assert.False(adapter.Discovery.CodeTriggersSupported);
        Assert.Equal("simulated", (await adapter.ExecuteActionAsync(actionId, new(), false, default)).State);
        Assert.Equal("actionNotSelected", (await adapter.ExecuteActionAsync(Guid.NewGuid(), new(), true, default)).State);
        Assert.Equal("unsupportedCapability", (await adapter.ExecuteCodeTriggerAsync("tdsblive.test", new(), true, default)).State);
        Assert.Equal("acknowledged", (await adapter.ExecuteActionAsync(actionId, new(), true, default)).State);
        Assert.Equal(1, actionCalls);
        await lifetime.CancelAsync(); await run.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task ReconnectsAndRejectsMissingActionsWithoutSending()
    {
        var connections = 0;
        await using var server = await FakeBot.Start(async socket =>
        {
            var index = Interlocked.Increment(ref connections);
            await FakeBot.Send(socket, new() { ["request"] = "Hello" });
            if (index == 1) { socket.Abort(); return; }
            while (socket.State == WebSocketState.Open)
            {
                var request = await FakeBot.Read(socket);
                var response = request["request"]!.GetValue<string>() switch
                {
                    "GetEvents" => new JsonObject { ["events"] = new JsonObject() },
                    "GetActions" => new JsonObject { ["actions"] = new JsonArray() },
                    "GetCodeTriggers" => new JsonObject { ["triggers"] = new JsonArray() },
                    _ => throw new InvalidOperationException("Unexpected live command")
                };
                await FakeBot.Reply(socket, request, response);
            }
        });
        var missingId = Guid.NewGuid();
        var adapter = new StreamerBotConnection(server.Configuration with { AllowedActionIds = [missingId], ForwardLiveEvents = true }, () => null, new());
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = adapter.RunAsync((_, _) => Task.CompletedTask, lifetime.Token);
        await Until(() => adapter.State.State == "connected");
        Assert.True(connections >= 2);
        Assert.Equal("missingAction", (await adapter.ExecuteActionAsync(missingId, new(), true, default)).State);
        Assert.Equal("missingTrigger", (await adapter.ExecuteCodeTriggerAsync("missing", new(), true, default)).State);
        await lifetime.CancelAsync(); await run.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task SpeakerSupportsDocumentedQueueAndSpeechWithoutRequiringMetadata()
    {
        var operations = new List<JsonObject>();
        await using var server = await FakeBot.Start(async socket =>
        {
            while (socket.State == WebSocketState.Open)
            {
                var request = await FakeBot.Read(socket);
                if (request["request"]!.GetValue<string>() == "GetInfo") { await FakeBot.Reply(socket, request, new(), "error"); continue; }
                lock (operations) operations.Add(request);
                await FakeBot.Reply(socket, request, new());
            }
        });
        var adapter = new SpeakerBotConnection(server.Configuration);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = adapter.RunAsync(lifetime.Token);
        await Until(() => adapter.State.State == "connected");
        Assert.Equal("simulated", (await adapter.SpeakAsync("test", "synthetic speech", true, false, default)).State);
        foreach (var op in new[] { "Pause", "Resume", "Clear", "Stop", "Enable", "Disable" })
            Assert.Equal("acknowledged", (await adapter.QueueAsync(op, null, true, default)).State);
        await adapter.QueueAsync("Events", "off", true, default);
        await adapter.QueueAsync("Mode", "command", true, default);
        await adapter.SpeakAsync("test", "synthetic speech", true, true, default);
        Assert.Equal(9, operations.Count);
        Assert.Equal("off", operations[6]["state"]!.GetValue<string>());
        Assert.Equal("command", operations[7]["mode"]!.GetValue<string>());
        Assert.True(operations[8]["badWordFilter"]!.GetValue<bool>());
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.QueueAsync("Unknown", null, true, default));
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.QueueAsync("Mode", "invalid", true, default));
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.SpeakAsync("", "text", true, true, default));
        await lifetime.CancelAsync(); await run.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void NormalizesRedactsAndPreservesSimulationAcrossCustomForwarding()
    {
        var sensitive = new SensitiveValues(); sensitive.Set("test", "synthetic-private-value");
        var normalizer = new StreamerBotEventNormalizer(sensitive);
        var inner = new JsonObject { ["tdsbliveForwardedSource"] = "Twitch", ["tdsbliveForwardedType"] = "ChatMessage", ["tdsbliveProvenance"] = "Live",
            ["payload"] = new JsonObject { ["messageId"] = "test-id", ["text"] = "synthetic-private-value", ["isTest"] = true, ["userName"] = "tester" } };
        var envelope = new JsonObject { ["event"] = new JsonObject { ["source"] = "General", ["type"] = "Custom" }, ["data"] = new JsonObject { ["data"] = inner.ToJsonString() } };
        var result = normalizer.Normalize(envelope, DateTimeOffset.UtcNow);
        Assert.Equal("chat.message", result.Event!.Type);
        Assert.Equal(EventProvenance.Simulation, result.Event.Provenance);
        Assert.Equal("tester", result.Event.User!.DisplayName);
        Assert.DoesNotContain("synthetic-private-value", result.Event.Raw!.ToJsonString());
        inner["tdsbliveOrigin"] = "tdsblive"; envelope["data"] = inner;
        Assert.Equal("bridgeLoop", normalizer.Normalize(envelope, DateTimeOffset.UtcNow).Classification);
        Assert.Null(normalizer.Normalize(new JsonObject { ["event"] = "invalid" }, DateTimeOffset.UtcNow).Event);
    }

    [Theory]
    [InlineData("Twitch", "Cheer", "support.bits")]
    [InlineData("YouTube", "Message", "chat.message")]
    [InlineData("Kick", "GiftSubscription", "support.gift")]
    [InlineData("Kofi", "Donation", "support.donation")]
    [InlineData("FuturePlatform", "Unknown", "integration.unknown")]
    public void ConservativePlatformNormalization(string platform, string native, string expected)
    {
        var result = new StreamerBotEventNormalizer(new()).Normalize(new JsonObject { ["event"] = new JsonObject { ["source"] = platform, ["type"] = native },
            ["data"] = new JsonObject { ["messageId"] = "synthetic", ["bits"] = 100, ["amount"] = "12.34", ["currency"] = "USD" } }, DateTimeOffset.UtcNow);
        Assert.Equal(expected, result.Event!.Type); result.Event.Validate();
        if (expected == "support.bits") Assert.Equal(100, result.Event.Monetary!.MinorUnits);
        if (expected is "support.gift" or "support.donation") Assert.Null(result.Event.Monetary!.MinorUnits);
    }

    [Fact]
    public void InspectorBoundsPrivacyAndSearch()
    {
        var normalizer = new StreamerBotEventNormalizer(new());
        var payload = new JsonObject { ["event"] = new JsonObject { ["source"] = "Twitch", ["type"] = "ChatMessage" }, ["data"] = new JsonObject { ["text"] = "needle" } };
        var result = normalizer.Normalize(payload, DateTimeOffset.UtcNow);
        var inspector = new EventInspectorStore(new ApplicationConfiguration { RetainRawEvents = false });
        for (var i = 0; i < 501; i++) inspector.Add(result, payload);
        Assert.Equal(1, inspector.Discarded); Assert.Single(inspector.Read("needle", 1));
        Assert.Null(inspector.Read()[0].Payload); Assert.Null(inspector.Read()[0].Event!.Raw);
        Assert.Throws<ArgumentException>(() => inspector.Read(limit: 201));
        Assert.NotNull(inspector.Find(inspector.Read()[0].Id));
    }

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }
}

internal sealed class FakeBot(WebApplication app, Uri uri) : IAsyncDisposable
{
    public Uri Uri { get; } = uri;
    public IntegrationConfiguration Configuration => new("127.0.0.1", Uri.Port) { Enabled = true, RequestTimeoutSeconds = 2, ReconnectDelaySeconds = 1, MaximumReconnectDelaySeconds = 1 };
    public static async Task<FakeBot> Start(Func<WebSocket, Task> handler)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        var app = builder.Build(); app.UseWebSockets();
        app.Run(async context =>
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            try { await handler(socket); }
            catch (Exception error) when (error is WebSocketException or OperationCanceledException or BotRequestException) { }
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new(app, new Uri(address.Replace("http:", "ws:", StringComparison.Ordinal)));
    }
    public static async Task<JsonObject> Read(WebSocket socket)
    {
        var buffer = new byte[65536]; using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        if (result.MessageType != WebSocketMessageType.Text) throw new BotRequestException("closed");
        return JsonNode.Parse(buffer.AsSpan(0, result.Count))!.AsObject();
    }
    public static Task Send(WebSocket socket, JsonObject data) => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(data.ToJsonString())), WebSocketMessageType.Text, true, default);
    public static Task Reply(WebSocket socket, JsonObject request, JsonObject response, string status = "ok")
    { response["id"] = request["id"]!.DeepClone(); response["status"] = status; return Send(socket, response); }
    public static async Task WaitForClose(WebSocket socket) { while (socket.State == WebSocketState.Open) await Read(socket); }
    public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
}
