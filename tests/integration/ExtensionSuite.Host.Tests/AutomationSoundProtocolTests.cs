using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationSoundProtocolTests
{
    [Fact]
    public async Task LiveSoundUsesScopedLeaseAndOnlySelectedSocketCanCompleteIt()
    {
        using var fixture = new SoundFixture(); await fixture.InitializeAsync();
        using var preview = await fixture.ConnectAsync(preview: true);
        var previewOnly = await fixture.PlayAsync();
        Assert.Equal(new AutomationDispatchOutcome("failed", "overlay-not-connected"), previewOnly);
        using var source = await fixture.ConnectAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Viewer.GetAsync("/assets/" + fixture.AudioId)).StatusCode);
        var execution = Guid.CreateVersion7(); var playing = fixture.PlayAsync(execution);
        using var command = await fixture.ReadAsync(source);
        Assert.Equal("sound", command.RootElement.GetProperty("op").GetString());
        var settings = command.RootElement.GetProperty("command");
        Assert.Equal(execution, settings.GetProperty("executionId").GetGuid());
        Assert.Equal(fixture.AudioId, settings.GetProperty("assetId").GetString());
        Assert.Equal(.4, settings.GetProperty("volume").GetDouble());
        Assert.Equal(.3, settings.GetProperty("duckingVolume").GetDouble());
        Assert.True(fixture.Sound.Authorizes(SoundFixture.OverlayId, fixture.AudioId));
        Assert.False(fixture.Sound.Authorizes(SoundFixture.OtherOverlayId, fixture.AudioId));
        Assert.Equal(HttpStatusCode.OK, (await fixture.Viewer.GetAsync("/assets/" + fixture.AudioId)).StatusCode);
        using var otherViewer = fixture.OtherViewer();
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherViewer.GetAsync("/assets/" + fixture.AudioId)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Viewer.GetAsync("/api/automation/executions")).StatusCode);
        using var wrongSource = await fixture.ConnectAsync(); // Arrives after the command selected its source.
        await fixture.ReceiptAsync(wrongSource, execution, "completed");
        Assert.False(playing.IsCompleted);
        await fixture.ReceiptAsync(source, execution, "started");
        Assert.False(playing.IsCompleted);
        await fixture.ReceiptAsync(source, execution, "completed");
        Assert.Equal(new AutomationDispatchOutcome("completed", "browser-playback-completed"), await playing);
        Assert.False(fixture.Sound.Authorizes(SoundFixture.OverlayId, fixture.AudioId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Viewer.GetAsync("/assets/" + fixture.AudioId)).StatusCode);
        await fixture.SendAsync(preview, new { op = "ping" });
        using var pong = await fixture.ReadAsync(preview);
        Assert.Equal("pong", pong.RootElement.GetProperty("op").GetString());
    }

    [Theory]
    [InlineData("failed", "failed", "browser-audio-failed")]
    [InlineData("timeout", "uncertain", "browser-timeout")]
    [InlineData("interrupted", "uncertain", "browser-interrupted")]
    public async Task TerminalReceiptsReleaseLeaseWithoutClaimingPlayback(string receipt, string state, string detail)
    {
        using var fixture = new SoundFixture(); await fixture.InitializeAsync();
        using var source = await fixture.ConnectAsync();
        var execution = Guid.CreateVersion7(); var playing = fixture.PlayAsync(execution);
        using var command = await fixture.ReadAsync(source);
        await fixture.ReceiptAsync(source, execution, receipt);
        Assert.Equal(new AutomationDispatchOutcome(state, detail), await playing);
        Assert.False(fixture.Sound.Authorizes(SoundFixture.OverlayId, fixture.AudioId));
    }

    [Fact]
    public async Task MissingReceiptTimesOutAndReleasesAssetAccess()
    {
        using var fixture = new SoundFixture(); await fixture.InitializeAsync();
        using var source = await fixture.ConnectAsync();
        var playing = fixture.PlayAsync(settings: fixture.Settings with { PlaybackTimeoutSeconds = 1 });
        using var command = await fixture.ReadAsync(source);
        Assert.Equal(new AutomationDispatchOutcome("uncertain", "browser-receipt-timeout"), await playing.WaitAsync(fixture.Timeout.Token));
        using var stopped = await fixture.ReadAsync(source);
        Assert.Equal("sound-stop", stopped.RootElement.GetProperty("op").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Viewer.GetAsync("/assets/" + fixture.AudioId)).StatusCode);
    }

    [Fact]
    public async Task RevokingSourceTokenInterruptsPendingReceiptAndRemovesLease()
    {
        using var fixture = new SoundFixture(); await fixture.InitializeAsync();
        using var source = await fixture.ConnectAsync();
        var playing = fixture.PlayAsync(); using var command = await fixture.ReadAsync(source);
        using var admin = fixture.App.CreateClient(); admin.DefaultRequestHeaders.Authorization = new("Bearer", fixture.App.AdminCredential);
        var csrf = (await admin.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"))!;
        admin.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf.RequestToken);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/overlays/{SoundFixture.OverlayId}/tokens/{fixture.Token.Info.Id}")).StatusCode);
        Assert.Equal(new AutomationDispatchOutcome("uncertain", "overlay-disconnected"), await playing.WaitAsync(fixture.Timeout.Token));
        Assert.False(fixture.Sound.Authorizes(SoundFixture.OverlayId, fixture.AudioId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Viewer.GetAsync("/assets/" + fixture.AudioId)).StatusCode);
    }

    [Fact]
    public async Task CancellationSendsStopAndRemovesPendingCommand()
    {
        using var fixture = new SoundFixture(); await fixture.InitializeAsync();
        using var source = await fixture.ConnectAsync(); using var stop = new CancellationTokenSource();
        var execution = Guid.CreateVersion7(); var playing = fixture.PlayAsync(execution, ct: stop.Token);
        using var command = await fixture.ReadAsync(source);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await playing);
        using var stopped = await fixture.ReadAsync(source);
        Assert.Equal("sound-stop", stopped.RootElement.GetProperty("op").GetString());
        Assert.Equal(execution, stopped.RootElement.GetProperty("executionId").GetGuid());
        Assert.False(fixture.Sound.Authorizes(SoundFixture.OverlayId, fixture.AudioId));
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("number-id")]
    [InlineData("unknown-state")]
    public async Task MalformedOrPreviewReceiptClosesOnlyThatSocket(string scenario)
    {
        using var fixture = new SoundFixture(); await fixture.InitializeAsync();
        using var socket = await fixture.ConnectAsync(preview: scenario == "preview");
        object id = scenario == "number-id" ? 123 : Guid.CreateVersion7().ToString();
        await fixture.SendAsync(socket, new { op = "sound-result", executionId = id, state = scenario == "unknown-state" ? "bogus" : "completed" });
        var close = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[1024]), fixture.Timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);
        using var admin = fixture.App.CreateClient(); admin.DefaultRequestHeaders.Authorization = new("Bearer", fixture.App.AdminCredential);
        Assert.True((await admin.GetAsync("/api/status")).IsSuccessStatusCode);
    }

    [Theory]
    [InlineData("missing-overlay", "missing-canvas-overlay")]
    [InlineData("chat-only", "missing-canvas-overlay")]
    [InlineData("missing-audio", "missing-audio-asset")]
    [InlineData("wrong-mime", "missing-audio-asset")]
    public async Task MissingDependenciesRejectBeforePublishingOrGrantingLease(string scenario, string detail)
    {
        using var fixture = new SoundFixture(); await fixture.InitializeAsync();
        var settings = fixture.Settings;
        if (scenario == "missing-overlay") settings = settings with { OverlayId = "missing" };
        if (scenario == "chat-only") settings = settings with { OverlayId = "combined-chat" };
        if (scenario == "missing-audio") settings = settings with { SoundAssetIds = [new string('a', 64)] };
        if (scenario == "wrong-mime") settings = settings with { SoundAssetIds = [fixture.FontId] };
        Assert.Equal(new AutomationDispatchOutcome("rejected", detail), await fixture.PlayAsync(settings: settings));
        Assert.False(fixture.Sound.Authorizes(SoundFixture.OverlayId, fixture.AudioId));
    }

    private sealed class SoundFixture : IDisposable
    {
        public const string OverlayId = "owned-sound";
        public const string OtherOverlayId = "other-owned-sound";
        public FoundationHostFactory App { get; } = new(true);
        public CancellationTokenSource Timeout { get; } = new(TimeSpan.FromSeconds(15));
        public HttpClient Viewer { get; private set; } = null!;
        public CreatedOverlayToken Token { get; private set; } = null!;
        private CreatedOverlayToken otherToken = null!;
        public string AudioId { get; private set; } = "";
        public string FontId { get; private set; } = "";
        public OverlayStore Store => App.Services.GetRequiredService<OverlayStore>();
        public AutomationOverlaySound Sound => App.Services.GetRequiredService<AutomationOverlaySound>();
        public AutomationAction Settings => new() { Kind = "sound", OverlayId = OverlayId, SoundAssetIds = [AudioId],
            Volume = .4m, DuckingVolume = .3m, PlaybackTimeoutSeconds = 5 };

        public async Task InitializeAsync()
        {
            using var startup = App.CreateClient();
            Assert.True(await Store.CreateAsync(new() { Id = OverlayId, CanvasEnabled = true }, Timeout.Token));
            Assert.True(await Store.CreateAsync(new() { Id = OtherOverlayId, CanvasEnabled = true }, Timeout.Token));
            Token = await Store.CreateTokenAsync(OverlayId, 1, Timeout.Token);
            otherToken = await Store.CreateTokenAsync(OtherOverlayId, 1, Timeout.Token);
            Viewer = App.CreateClient(); Viewer.DefaultRequestHeaders.Authorization = new("Bearer", Token.Token);
            Viewer.DefaultRequestHeaders.Add("X-TDSBLive-Overlay", OverlayId);
            var assets = App.Services.GetRequiredService<AssetStore>();
            AudioId = (await assets.UploadAsync(new MemoryStream(SilentWave()), "owned.wav", "audio/wav", null, Timeout.Token))!.Id;
            FontId = (await assets.UploadAsync(new MemoryStream(Encoding.ASCII.GetBytes("wOF20000")), "owned.woff2", "font/woff2", "OFL-1.1", Timeout.Token))!.Id;
        }

        public HttpClient OtherViewer()
        {
            var viewer = App.CreateClient(); viewer.DefaultRequestHeaders.Authorization = new("Bearer", otherToken.Token);
            viewer.DefaultRequestHeaders.Add("X-TDSBLive-Overlay", OtherOverlayId); return viewer;
        }

        public async Task<WebSocket> ConnectAsync(bool preview = false)
        {
            var client = App.Server.CreateWebSocketClient(); client.SubProtocols.Add("tdsblive.overlay.v1"); client.SubProtocols.Add(Token.Token);
            client.ConfigureRequest = request => request.Headers["Origin"] = "http://localhost";
            var socket = await client.ConnectAsync(new Uri($"ws://localhost/ws/overlay/{OverlayId}" + (preview ? "?preview=1" : "")), Timeout.Token);
            await SendAsync(socket, new { op = "subscribe", types = new[] { "chat.message" } });
            using var subscribed = await ReadAsync(socket);
            Assert.Equal("subscribed", subscribed.RootElement.GetProperty("op").GetString());
            return socket;
        }

        public Task<AutomationDispatchOutcome> PlayAsync(Guid? executionId = null, AutomationAction? settings = null, CancellationToken? ct = null) =>
            Sound.PlayAsync(executionId ?? Guid.CreateVersion7(), new(Guid.NewGuid(), 1, settings ?? Settings, "queued", null, "sound", "queue", 20, 0), ct ?? Timeout.Token);
        public Task SendAsync(WebSocket socket, object value) => socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)), WebSocketMessageType.Text, true, Timeout.Token);
        public async Task ReceiptAsync(WebSocket socket, Guid id, string state)
        {
            await SendAsync(socket, new { op = "sound-result", executionId = id, state });
            using var ack = await ReadAsync(socket); Assert.Equal("sound-received", ack.RootElement.GetProperty("op").GetString());
        }
        public async Task<JsonDocument> ReadAsync(WebSocket socket)
        {
            var buffer = new byte[16384]; var frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), Timeout.Token);
            Assert.True(frame.EndOfMessage); Assert.Equal(WebSocketMessageType.Text, frame.MessageType);
            return JsonDocument.Parse(buffer.AsMemory(0, frame.Count));
        }
        private static byte[] SilentWave()
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(1636); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
            writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(1600);
            writer.Write(new byte[1600]); return stream.ToArray();
        }
        public void Dispose() { Viewer?.Dispose(); App.Dispose(); Timeout.Dispose(); }
    }
}
