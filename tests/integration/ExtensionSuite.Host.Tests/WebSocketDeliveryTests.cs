using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class WebSocketDeliveryTests
{
    [Fact]
    public async Task PublicationAndShutdownRemainSafeDuringConcurrentDisconnects()
    {
        using var factory = new FoundationHostFactory();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Origin = "http://127.0.0.1";
        var hub = factory.Services.GetRequiredService<EditorEventHub>();
        var item = new CanonicalEvent { Source = "internal", Platform = "system", Type = "test", NativeType = "test",
            DedupeKey = "disconnect-test", OccurredAt = DateTimeOffset.UtcNow };
        var sockets = new List<WebSocket>();
        try
        {
            for (var index = 0; index < 16; index++)
            {
                var socket = await client.ConnectAsync(new Uri("ws://127.0.0.1/ws/editor"), timeout.Token);
                sockets.Add(socket);
                await SendAsync(socket, """{"op":"subscribe","types":["*"]}""", timeout.Token);
                using var ack = await ReceiveAsync(socket, timeout.Token);
            }
            await Task.WhenAll(Task.Run(() => { for (var index = 0; index < 1000; index++) hub.Publish(item); }),
                Task.Run(() => { foreach (var socket in sockets) socket.Abort(); hub.Shutdown(); }));
            using var http = factory.CreateClient(new() { BaseAddress = new Uri("http://127.0.0.1") });
            Assert.True((await http.GetAsync("/api/status", timeout.Token)).IsSuccessStatusCode);
            using var reopened = await client.ConnectAsync(new Uri("ws://127.0.0.1/ws/editor"), timeout.Token);
            await SendAsync(reopened, """{"op":"ping"}""", timeout.Token);
            using var pong = await ReceiveAsync(reopened, timeout.Token);
            Assert.Equal("pong", pong.RootElement.GetProperty("op").GetString());
            await reopened.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", timeout.Token);
        }
        finally { foreach (var socket in sockets) socket.Dispose(); }
    }

    private static async Task SendAsync(WebSocket socket, string json, CancellationToken token) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, token);

    private static async Task<JsonDocument> ReceiveAsync(WebSocket socket, CancellationToken token)
    {
        var bytes = new byte[65536];
        var frame = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), token);
        Assert.True(frame.EndOfMessage);
        return JsonDocument.Parse(bytes.AsMemory(0, frame.Count));
    }

    [Fact]
    public async Task SubscriptionsHeartbeatAndLiveOutboxUseRealSocketWithFiltering()
    {
        using var factory = new FoundationHostFactory();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Origin = "http://127.0.0.1";
        using var socket = await client.ConnectAsync(new Uri("ws://127.0.0.1/ws/editor"), timeout.Token);
        await SendAsync(socket, """{"op":"ping"}""", timeout.Token);
        using var pong = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal("pong", pong.RootElement.GetProperty("op").GetString());
        await SendAsync(socket, """{"op":"subscribe","types":["chat.message"]}""", timeout.Token);
        using var subscribed = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal("subscribed", subscribed.RootElement.GetProperty("op").GetString());
        var hub = factory.Services.GetRequiredService<EditorEventHub>();
        var item = new CanonicalEvent { Source = "internal", Platform = "system", Type = "chat.message", NativeType = "test",
            DedupeKey = "websocket", OccurredAt = DateTimeOffset.UtcNow };
        hub.Publish(item with { Type = "unsubscribed.type" });
        Assert.True(await factory.Services.GetRequiredService<EventStore>().AcceptAsync(item, "socket-test", cancellationToken: timeout.Token));
        using var received = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal("event", received.RootElement.GetProperty("op").GetString());
        Assert.Equal(item.Id.ToString(), received.RootElement.GetProperty("event").GetProperty("id").GetString());
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", timeout.Token);
    }

    [Fact]
    public async Task SimulationIsDeliveredWithoutPersistenceAndKnownSecretsAreScrubbed()
    {
        using var factory = new FoundationHostFactory();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var http = factory.CreateClient(new() { BaseAddress = new Uri("http://127.0.0.1") });
        using var csrf = JsonDocument.Parse(await http.GetStringAsync("/api/auth/csrf"));
        http.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf.RootElement.GetProperty("requestToken").GetString());
        factory.Services.GetRequiredService<SensitiveValues>().Set("test", "synthetic-private-value");
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Origin = "http://127.0.0.1";
        using var socket = await client.ConnectAsync(new Uri("ws://127.0.0.1/ws/editor"), timeout.Token);
        await SendAsync(socket, """{"op":"subscribe","types":["*"]}""", timeout.Token);
        using var ack = await ReceiveAsync(socket, timeout.Token);
        var item = new CanonicalEvent { Source = "internal", Platform = "system", Type = "test", NativeType = "test", DedupeKey = "simulation",
            OccurredAt = DateTimeOffset.UtcNow, Message = new EventMessage("synthetic-private-value") };
        using var response = await http.PostAsJsonAsync("/api/test-event", new TestEventRequest(item), timeout.Token);
        response.EnsureSuccessStatusCode();
        using var received = await ReceiveAsync(socket, timeout.Token);
        Assert.DoesNotContain("synthetic-private-value", received.RootElement.GetRawText());
        Assert.Equal("simulation", received.RootElement.GetProperty("event").GetProperty("provenance").GetString());
        Assert.Empty(await factory.Services.GetRequiredService<EventStore>().ReadAsync(provenance: EventProvenance.Simulation));
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", timeout.Token);
    }

    [Fact]
    public async Task CrashingIntegrationCannotStopHealthyIntegrationOrHttpHost()
    {
        using var factory = new FoundationHostFactory();
        var healthy = new HealthyIntegration();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IIsolatedIntegration>(new CrashingIntegration());
            services.AddSingleton<IIsolatedIntegration>(healthy);
        }));
        using var client = configured.CreateClient(new() { BaseAddress = new Uri("http://127.0.0.1") });
        await healthy.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await client.GetAsync("/api/status")).IsSuccessStatusCode);
        var states = configured.Services.GetRequiredService<IntegrationHealthRegistry>().Snapshot();
        Assert.Equal("degraded", states["crashing-test"].State);
        Assert.Equal("running", states["healthy-test"].State);
    }

    private sealed class CrashingIntegration : IIsolatedIntegration
    {
        public string Name => "crashing-test";
        public Task RunAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("synthetic failure");
    }

    private sealed class HealthyIntegration : IIsolatedIntegration
    {
        public string Name => "healthy-test";
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
