using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using ExtensionSuite.StreamerBot;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class BotEndpointTests
{
    [Fact]
    public async Task DisabledConnectionsExposeCapabilitiesAndSimulateAllOperations()
    {
        await using var factory = new FoundationHostFactory(); using var client = factory.CreateClient();
        Assert.Equal("disabled", (await client.GetFromJsonAsync<IntegrationOverview>("/api/integrations"))!.StreamerBot.State);
        Assert.Empty((await client.GetFromJsonAsync<BotDiscovery>("/api/integrations/streamerbot/discovery"))!.Actions);
        await Protect(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/integrations/streamerbot/discovery/refresh", null)).StatusCode);
        var action = await client.PostAsJsonAsync("/api/integrations/streamerbot/actions/execute", new ActionExecutionRequest(Guid.NewGuid()));
        Assert.Equal("simulated", (await action.Content.ReadFromJsonAsync<BotExecution>())!.State);
        var trigger = await client.PostAsJsonAsync("/api/integrations/streamerbot/triggers/execute", new CodeTriggerExecutionRequest("test"));
        Assert.Equal("simulated", (await trigger.Content.ReadFromJsonAsync<BotExecution>())!.State);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/integrations/speakerbot/speak", new SpeechRequest("test", "synthetic"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/integrations/speakerbot/speak", new SpeechRequest("", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/integrations/speakerbot/queue", new SpeakerQueueRequest("Pause"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/integrations/speakerbot/queue", new SpeakerQueueRequest("invalid"))).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<BotExecution[]>("/api/integrations/streamerbot/executions"))!.Length);
        Assert.Equal(2, (await client.GetFromJsonAsync<BotExecution[]>("/api/integrations/speakerbot/executions"))!.Length);
    }

    [Fact]
    public async Task InspectorReplayRequiresExplicitPersistenceAndNeverRunsBots()
    {
        await using var factory = new FoundationHostFactory(); using var client = factory.CreateClient();
        var normalizer = factory.Services.GetRequiredService<StreamerBotEventNormalizer>();
        var inspector = factory.Services.GetRequiredService<EventInspectorStore>();
        var payload = JsonNode.Parse("""{"event":{"source":"Twitch","type":"ChatMessage"},"data":{"messageId":"synthetic-id","text":"test message","isTest":true}}""")!.AsObject();
        inspector.Add(normalizer.Normalize(payload, DateTimeOffset.UtcNow), payload);
        var entry = Assert.Single((await client.GetFromJsonAsync<InspectorEntry[]>("/api/inspector", EventStore.JsonOptions))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/inspector?limit=201")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/inspector/{entry.Id}/fixture")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inspector/{Guid.NewGuid()}/fixture")).StatusCode);
        await Protect(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/inspector/{Guid.NewGuid()}/replay", new InspectorReplayRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/inspector/{entry.Id}/replay", new InspectorReplayRequest())).StatusCode);
        var store = factory.Services.GetRequiredService<EventStore>(); Assert.Empty(await store.ReadAsync(provenance: EventProvenance.Replay));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/inspector/{entry.Id}/replay", new InspectorReplayRequest(true))).StatusCode);
        Assert.Single(await store.ReadAsync(provenance: EventProvenance.Replay)); Assert.Empty(await store.ReadAsync());
        Assert.Empty(factory.Services.GetRequiredService<StreamerBotConnection>().Executions);
        Assert.Empty(factory.Services.GetRequiredService<SpeakerBotConnection>().Executions);
    }

    [Fact]
    public void TriggerArgumentsPreserveIdentityProvenanceAndExcludeRawData()
    {
        var item = new CanonicalEvent { Source = "rumble", Platform = "rumble", Type = "support.rant", NativeType = "rant", DedupeKey = "test",
            OccurredAt = DateTimeOffset.UtcNow, User = new("id", "login", "display"), Message = new("synthetic"), Monetary = new(1234, "USD", "exact"),
            Stream = new("stream", "title"), Provenance = EventProvenance.Replay, Raw = new JsonObject { ["private"] = "not forwarded" } };
        var arguments = TriggerArgumentMapper.Map(item);
        Assert.Equal("replay", arguments["tdsbliveProvenance"]!.GetValue<string>());
        Assert.Equal(1234, arguments["amountMinorUnits"]!.GetValue<long>());
        Assert.Equal("tdsblive.rumble.rant", TriggerArgumentMapper.EventNames[item.Type]);
        Assert.DoesNotContain("not forwarded", arguments.ToJsonString());
    }

    private static async Task Protect(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf!.RequestToken);
    }
}
