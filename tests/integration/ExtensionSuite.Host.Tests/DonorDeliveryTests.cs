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
    public async Task LanOverlayTokenCanReadOnlyItsDonorAssetsAndCannotReadFinancialAdministration()
    {
        using var app = new FoundationHostFactory(true); using var http = app.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var assets = app.Services.GetRequiredService<AssetStore>();
        var crown = (await assets.UploadAsync(new MemoryStream(System.Text.Encoding.ASCII.GetBytes("GIF89a0000")), "owned-crown.gif", "image/gif", null, timeout.Token))!;
        var font = (await assets.UploadAsync(new MemoryStream(System.Text.Encoding.ASCII.GetBytes("wOF20000")), "owned-font.woff2", "font/woff2", "OFL-1.1", timeout.Token))!;
        var overlays = app.Services.GetRequiredService<OverlayStore>();
        await overlays.CreateAsync(new() { Id = "scoped-donors", CanvasEnabled = true, Widgets = [new() { Kind = "donor-crown", Donor = new() { CrownAssetId = crown.Id, FontAssetId = font.Id } }] }, timeout.Token);
        var token = await overlays.CreateTokenAsync("scoped-donors", 1, timeout.Token);
        http.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        http.DefaultRequestHeaders.Add("X-TDSBLive-Overlay", "scoped-donors");
        Assert.Equal(System.Net.HttpStatusCode.OK, (await http.GetAsync("/assets/" + crown.Id, timeout.Token)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await http.GetAsync("/assets/" + font.Id, timeout.Token)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await http.GetAsync("/assets/" + new string('a', 64), timeout.Token)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await http.GetAsync("/api/financial/ledger", timeout.Token)).StatusCode);
        var client = app.Server.CreateWebSocketClient();
        client.SubProtocols.Add("tdsblive.overlay.v1"); client.SubProtocols.Add(token.Token);
        client.ConfigureRequest = request => request.Headers.Origin = "http://localhost";
        using var socket = await client.ConnectAsync(new("ws://localhost/ws/overlay/scoped-donors"), timeout.Token);
        using var snapshot = await ReceiveDonors(socket, timeout.Token);
        Assert.Equal("empty", snapshot.RootElement.GetProperty("widgets")[0].GetProperty("state").GetString());
        socket.Abort();
    }

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
