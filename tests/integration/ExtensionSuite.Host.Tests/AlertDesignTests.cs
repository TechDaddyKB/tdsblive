using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using ExtensionSuite.StreamerBot;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ExtensionSuite.Host.Tests;
public sealed class AlertDesignTests
{
    [Fact]
    public void CatalogDistinguishesMaintainedIncomingEventsFromOutgoingCodeAndAmbiguousCustomNames()
    {
        var normalizer = new StreamerBotEventNormalizer(new());
        var e = normalizer.Normalize(new JsonObject { ["event"] = new JsonObject { ["source"] = "General", ["type"] = "Custom" }, ["data"] = new JsonObject { ["tdsbliveAlertTrigger"] = "owned.sparkle" } }, DateTimeOffset.UtcNow).Event!;
        var discovered = new BotDiscovery(new Dictionary<string,string[]> { ["Twitch"] = ["Follow", "Sub"], ["General"] = ["Custom"] }, [new(Guid.NewGuid(), "Outgoing action", true, null)], [new("Outgoing code", "outgoing.execute", "Owned")], true, true, true);
        var entry = new InspectorEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, "canonical", null, e, null);
        var choices = AlertTriggerCatalog.Build(discovered, [entry, entry, entry with { Event = e with { AlertTriggerKey = null } }], normalizer);
        Assert.Contains(choices, c => c.NativeType == "Twitch.Sub" && c.Label.Contains("New subscription")); Assert.Contains(choices, c => c.NativeType == "Twitch.Follow");
        Assert.Equal("observed", Assert.Single(choices, c => c.CustomTriggerKey == "owned.sparkle").Availability);
        Assert.Equal(choices.Length, choices.Select(c => c.Id).Distinct().Count()); Assert.DoesNotContain(choices, c => c.Label.Contains("Outgoing") || c.Id.Contains("outgoing.execute"));
    }
    private static OverlayWidget Alert() => new() { Kind = "alert", Alert = new() { EventTypes = ["support.donation"], Platforms = ["kofi"], Condition = new("native-money", "minimum", 500, Currency: "USD", MinorUnitDigits: 2) } };
    [Fact]
    public async Task EnhancedPackagesUseV2AndRemapOrderedMembershipWhileLegacyUsesV1()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        var store = app.Services.GetRequiredService<OverlayStore>(); var packages = app.Services.GetRequiredService<PortablePackages>();
        var a = Alert(); var b = Alert(); var set = new AlertSet(Guid.NewGuid().ToString(), "Owned tier", [b.Id, a.Id]);
        Assert.True(await store.CreateAsync(new() { Id = "tier-test", CanvasEnabled = true, Widgets = [a, b], AlertSets = [set] }));
        var prepared = packages.Validate((await packages.ExportAsync("tier-test", null, default))!); Assert.Equal(2, prepared.Manifest.FormatVersion);
        var imported = await packages.ImportAsync(prepared, null, app.Services.GetRequiredService<EditorEventHub>(), default);
        Assert.Equal([imported.Widgets[1].Id, imported.Widgets[0].Id], Assert.Single(imported.AlertSets).WidgetIds); Assert.NotEqual(set.Id, imported.AlertSets[0].Id); Assert.Equal(a.Alert.Condition, imported.Widgets[0].Alert.Condition);
        var single = packages.Validate((await packages.ExportAsync("tier-test", a.Id, default))!); Assert.Equal(2, single.Manifest.FormatVersion); Assert.Null(single.Overlay);
        Assert.True(await store.CreateAsync(new() { Id = "legacy-test", Widgets = [a with { Alert = a.Alert with { Condition = null } }] }));
        Assert.Equal(1, packages.Validate((await packages.ExportAsync("legacy-test", null, default))!).Manifest.FormatVersion);
    }
    [Fact]
    public async Task PreviewEligibilityIsPrivateValidatedAndDoesNotPersist()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        var csrf = await http.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"); http.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf!.RequestToken);
        var a = Alert(); var b = Alert(); var scene = new OverlayDefinition { Id = "preview-tiers", CanvasEnabled = true, Widgets = [a, b], AlertSets = [new(Guid.NewGuid().ToString(), "Tier", [b.Id, a.Id])] };
        Assert.True(await app.Services.GetRequiredService<OverlayStore>().CreateAsync(scene));
        var ws = app.Server.CreateWebSocketClient(); ws.ConfigureRequest = r => r.Headers.Origin = "http://localhost";
        using var socket = await ws.ConnectAsync(new Uri("ws://localhost/ws/overlay/preview-tiers?preview=1"), default);
        await socket.SendAsync(Encoding.UTF8.GetBytes("""{"op":"subscribe","types":["support.donation"]}"""), WebSocketMessageType.Text, true, default); await Read(socket);
        var response = await http.PostAsJsonAsync("/api/overlays/preview-tiers/preview-events", new PreviewEventRequest("support.donation", "kofi", Quantity: 1, NativeMoney: new(500, "USD", 2)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var receipt = await response.Content.ReadFromJsonAsync<JsonObject>(); Assert.False(receipt!["persisted"]!.GetValue<bool>()); Assert.False(receipt["liveActionsAllowed"]!.GetValue<bool>());
        var delivery = await Read(socket); Assert.Equal(b.Id, Assert.Single(delivery["alertWidgetIds"]!.AsArray())!.GetValue<string>()); Assert.Null(delivery["event"]!["support"]); Assert.Null(delivery["event"]!["raw"]); Assert.Null(delivery["event"]!["alertTriggerKey"]);
        var events = await http.GetStringAsync("/api/events"); Assert.DoesNotContain(receipt["id"]!.GetValue<string>(), events);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/overlays/preview-tiers/preview-events", new PreviewEventRequest("support.donation", "kofi", Quantity: -1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/overlays/preview-tiers/preview-events", new PreviewEventRequest("integration.custom", CustomTriggerKey: " "))).StatusCode);
    }
    private static async Task<JsonObject> Read(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); var bytes = new byte[65536]; var length = 0; WebSocketReceiveResult result;
        do { result = await socket.ReceiveAsync(new ArraySegment<byte>(bytes, length, bytes.Length - length), timeout.Token); length += result.Count; } while (!result.EndOfMessage);
        return JsonNode.Parse(Encoding.UTF8.GetString(bytes, 0, length))!.AsObject();
    }
    [Theory]
    [InlineData("owned.sparkle", "owned.sparkle")][InlineData("", null)]
    public void NamespacedIncomingCustomIdentityIsExplicit(string key, string? expected)
    {
        var normalized = new StreamerBotEventNormalizer(new()).Normalize(new JsonObject { ["event"] = new JsonObject { ["source"] = "General", ["type"] = "Custom" }, ["data"] = new JsonObject { ["tdsbliveAlertTrigger"] = key, ["triggerName"] = "ambiguous" } }, DateTimeOffset.UtcNow);
        Assert.Equal(expected, normalized.Event!.AlertTriggerKey);
    }
    [Fact]
    public async Task DisconnectedCatalogHasFriendlyMaintainedChoicesAndNoOutgoingActions()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient(); var choices = (await http.GetFromJsonAsync<AlertTriggerChoice[]>("/api/alert-triggers"))!;
        Assert.Contains(choices, c => c.Platform == "twitch" && c.EventType == "community.follow"); Assert.Contains(choices, c => c.Platform == "kofi" && c.EventType == "support.donation");
        Assert.All(choices, c => Assert.DoesNotContain("execute", c.Id)); Assert.DoesNotContain(choices, c => c.CustomTriggerKey is not null);
    }
}
