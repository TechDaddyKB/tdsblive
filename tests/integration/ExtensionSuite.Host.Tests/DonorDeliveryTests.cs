using System.Net.WebSockets;
using System.Net.Http.Json;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class DonorDeliveryTests
{
    [Fact]
    public async Task NewOverlayWithExistingRevisionIsRejectedAsBadRequest()
    {
        using var app = new FoundationHostFactory();
        using var http = app.CreateClient(new() { BaseAddress = new("http://127.0.0.1") });
        using var csrf = JsonDocument.Parse(await http.GetStringAsync("/api/auth/csrf"));
        http.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf.RootElement.GetProperty("requestToken").GetString());
        using var response = await http.PostAsJsonAsync("/api/overlays", new OverlayDefinition { Id = "invalid-create-version", Version = 2 });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await app.Services.GetRequiredService<OverlayStore>().GetAsync("invalid-create-version"));
    }

    [Fact]
    public async Task OpenOverlayReceivesCommittedTotalsAndReconnectGetsCurrentSnapshot()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var widget = new OverlayWidget { Kind = "donor-crown" };
        var overlay = new OverlayDefinition { Id = "owned-donors", CanvasEnabled = true, Widgets = [widget] };
        Assert.True(await app.Services.GetRequiredService<OverlayStore>().CreateAsync(overlay, timeout.Token));
        var client = app.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Origin = "http://127.0.0.1";
        using var socket = await client.ConnectAsync(new("ws://127.0.0.1/ws/overlay/owned-donors"), timeout.Token);
        using var initial = await ReceiveDonors(socket, timeout.Token);
        Assert.Equal("empty", initial.RootElement.GetProperty("widgets")[0].GetProperty("state").GetString());
        var item = new CanonicalEvent { Source = "owned-delivery-fixture", Platform = "twitch", Type = "support.donation",
            NativeType = "Fixture.Donation", NativeId = "owned-donor", DedupeKey = "owned-donor", OccurredAt = DateTimeOffset.UtcNow,
            User = new("owned-user", DisplayName: "Owned donor"), Support = new("donation", 1, new(1250, "USD", 2)) };
        Assert.True(await app.Services.GetRequiredService<FinancialStore>().AcceptAsync(item, cancellationToken: timeout.Token));
        using var update = await ReceiveDonors(socket, timeout.Token);
        var row = update.RootElement.GetProperty("widgets")[0].GetProperty("rows")[0];
        Assert.Equal("1250", row.GetProperty("usdAmountMinor").GetString());
        Assert.DoesNotContain("owned-user", update.RootElement.GetRawText());
        Assert.DoesNotContain("nativeId", update.RootElement.GetRawText());
        socket.Abort();
        using var reopened = await client.ConnectAsync(new("ws://127.0.0.1/ws/overlay/owned-donors"), timeout.Token);
        using var restored = await ReceiveDonors(reopened, timeout.Token);
        Assert.Equal("1250", restored.RootElement.GetProperty("widgets")[0].GetProperty("totalUsdMinor").GetString());
        reopened.Abort();
        using var preview = await client.ConnectAsync(new("ws://127.0.0.1/ws/overlay/owned-donors?preview=1"), timeout.Token);
        using var isolated = await ReceiveDonors(preview, timeout.Token);
        Assert.Equal("preview", isolated.RootElement.GetProperty("widgets")[0].GetProperty("state").GetString());
        Assert.DoesNotContain("Owned donor", isolated.RootElement.GetRawText());
        preview.Abort();
    }

    private static async Task<JsonDocument> ReceiveDonors(WebSocket socket, CancellationToken token)
    {
        var bytes = new byte[65536];
        while (true)
        {
            var frame = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), token);
            Assert.True(frame.EndOfMessage);
            var document = JsonDocument.Parse(bytes.AsMemory(0, frame.Count));
            if (document.RootElement.GetProperty("op").GetString() == "donors") return document;
            document.Dispose();
        }
    }
}
