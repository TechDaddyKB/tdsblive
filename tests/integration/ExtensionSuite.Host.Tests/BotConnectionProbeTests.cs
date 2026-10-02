using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using ExtensionSuite.StreamerBot;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class BotConnectionProbeTests
{
    [Theory]
    [InlineData(false, "ok", "connected", null)]
    [InlineData(true, "ok", "connected", null)]
    [InlineData(false, "error", "probeFailed", "rejected")]
    [InlineData(true, "error", "probeFailed", "rejected")]
    [InlineData(false, "timeout", "probeFailed", "timeout")]
    public async Task ProbeUsesOnlyCorrelatedReadOnlyMetadata(bool speaker, string response, string expected, string? failure)
    {
        var requests = new ConcurrentQueue<string>();
        await using var server = await FakeBot.Start(async socket =>
        {
            if (!speaker) await FakeBot.Send(socket, new() { ["request"] = "Hello", ["info"] = new JsonObject { ["version"] = "test" } });
            while (socket.State == WebSocketState.Open)
            {
                var request = await FakeBot.Read(socket);
                var name = request["request"]!.GetValue<string>(); requests.Enqueue(name);
                if (name == "GetInfo" && response == "timeout") continue;
                await FakeBot.Reply(socket, request, new(), name == "GetInfo" ? response : "ok");
            }
        });
        var streamer = new StreamerBotConnection(server.Configuration, () => null, new());
        var speech = new SpeakerBotConnection(server.Configuration);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var run = speaker ? speech.RunAsync(lifetime.Token) : streamer.RunAsync((_, _) => Task.CompletedTask, lifetime.Token);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((speaker ? speech.State : streamer.State).State != "connected" && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.Equal("connected", (speaker ? speech.State : streamer.State).State);
        var before = requests.Count;
        var result = speaker ? await speech.TestConnectionAsync(default) : await streamer.TestConnectionAsync(default);
        Assert.Equal(expected, result.State); Assert.Equal(failure, result.FailureKind);
        Assert.Equal("connected", (speaker ? speech.State : streamer.State).State);
        Assert.Contains("GetInfo", requests.Skip(before));
        Assert.All(requests, request => Assert.Contains(request, new[] { "GetInfo", "GetEvents", "GetActions", "GetCodeTriggers", "GetBroadcaster", "Subscribe" }));
        Assert.Empty(streamer.Executions); Assert.Empty(speech.Executions);
        await lifetime.CancelAsync(); await run.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData("streamerbot")]
    [InlineData("speakerbot")]
    public async Task DisabledProbeRequiresCsrfAndDoesNotEnableConnections(string bot)
    {
        await using var factory = new FoundationHostFactory(); using var client = factory.CreateClient();
        var route = $"/api/integrations/{bot}/test";
        client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(route, null)).StatusCode);
        var csrf = await client.GetFromJsonAsync<ExtensionSuite.Host.CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf!.RequestToken);
        var result = await client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("disabled", (await result.Content.ReadFromJsonAsync<BotConnectionState>())!.State);
    }
}
