using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ExtensionSuite.Core;
using ExtensionSuite.Data;

namespace ExtensionSuite.Host;

public sealed class EditorEventHub(SensitiveValues sensitive)
{
    private readonly ConcurrentDictionary<Guid, Subscriber> subscribers = new();
    private readonly SemaphoreSlim connectionSlots = new(32, 32);
    private int stopping;
    public int SubscriberCount => subscribers.Count;
    public async Task<AutomationDispatchOutcome> PlaySoundAsync(string overlayId, object command, Guid executionId,
        int timeoutSeconds, CancellationToken ct)
    {
        var subscriber = subscribers.Values.FirstOrDefault(item => item.Overlay?.Id == overlayId &&
            item.Overlay.CanvasEnabled && !item.Preview && item.Subscribed && !item.Stop.IsCancellationRequested);
        if (subscriber is null) return new("failed", "overlay-not-connected");
        var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!subscriber.SoundResults.TryAdd(executionId, result)) return new("rejected", "duplicate-sound-command");
        var stopPlayback = false;
        try
        {
            if (!subscriber.Queue.Writer.TryWrite(JsonSerializer.Serialize(new { op = "sound", command }, EventStore.JsonOptions)))
                return new("failed", "overlay-queue-full");
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct, subscriber.Stop.Token);
            var state = await result.Task.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds + 2), stop.Token);
            return SoundOutcome(state);
        }
        catch (TimeoutException) { stopPlayback = true; return new("uncertain", "browser-receipt-timeout"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new("uncertain", "overlay-disconnected"); }
        finally
        {
            if (stopPlayback || ct.IsCancellationRequested) subscriber.Queue.Writer.TryWrite(JsonSerializer.Serialize(new { op = "sound-stop", executionId }, EventStore.JsonOptions));
            subscriber.SoundResults.TryRemove(executionId, out _);
        }
    }
    private static AutomationDispatchOutcome SoundOutcome(string state) => state switch
    {
        "completed" => new("completed", "browser-playback-completed"),
        "failed" => new("failed", "browser-audio-failed"),
        _ => new("uncertain", "browser-" + state)
    };
    public void Shutdown()
    {
        foreach (var subscriber in subscribers.Values) subscriber.TryStop();
    }
    public void BeginShutdown()
    {
        Interlocked.Exchange(ref stopping, 1);
        Shutdown();
    }
    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        BeginShutdown();
        var active = subscribers.Values.ToArray();
        foreach (var subscriber in active) subscriber.TryStop();
        await Task.WhenAll(active.Select(subscriber => subscriber.Drained.Task)).WaitAsync(cancellationToken);
    }

    public void Publish(CanonicalEvent item)
    {
        var payload = CredentialRedactor.Json(JsonSerializer.SerializeToNode(new { op = "event", @event = item }, EventStore.JsonOptions), sensitive.Snapshot())!
            .ToJsonString(EventStore.JsonOptions);
        foreach (var subscriber in subscribers.Values)
            if (subscriber.Accepts(item) && !subscriber.Queue.Writer.TryWrite(subscriber.Overlay is null ? payload :
                CredentialRedactor.Json(JsonSerializer.SerializeToNode(new { op = "event", @event = OverlayEndpoints.PublicChat(item) }, EventStore.JsonOptions), sensitive.Snapshot())!.ToJsonString(EventStore.JsonOptions))) subscriber.TryStop();
    }

    public void PublishPreview(string overlayId, CanonicalEvent item)
    {
        var payload = JsonSerializer.Serialize(new { op = "event", @event = OverlayEndpoints.PublicChat(item) }, EventStore.JsonOptions);
        foreach (var subscriber in subscribers.Values.Where(s => s.Preview && !s.Limited && s.Overlay?.Id == overlayId))
            if (subscriber.Accepts(item) && !subscriber.Queue.Writer.TryWrite(payload)) subscriber.TryStop();
    }

    public void UpdateOverlay(OverlayDefinition definition)
    {
        foreach (var subscriber in subscribers.Values.Where(s => s.Overlay?.Id == definition.Id))
        {
            subscriber.Overlay = definition;
            if (!subscriber.Queue.Writer.TryWrite(JsonSerializer.Serialize(new { op = "settings", settings = definition }, EventStore.JsonOptions))) subscriber.TryStop();
        }
    }
    public void CloseLimitedOverlay(string id)
    {
        foreach (var subscriber in subscribers.Values.Where(s => s.Overlay?.Id == id && s.Limited)) subscriber.TryStop();
    }

    public async Task ConnectAsync(HttpContext context, OverlayDefinition? overlay = null)
    {
        if (Volatile.Read(ref stopping) != 0) { context.Response.StatusCode = 503; return; }
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync(context.WebSockets.WebSocketRequestedProtocols.Contains("tdsblive.overlay.v1") ? "tdsblive.overlay.v1" : null);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var subscriber = new Subscriber(stop) { Overlay = overlay, Preview = context.Request.Query["preview"] == "1", Limited = context.Items.ContainsKey("OverlayToken") };
        var id = Guid.CreateVersion7();
        if (!await connectionSlots.WaitAsync(0, context.RequestAborted))
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Connection limit reached", CancellationToken.None);
            return;
        }
        subscribers[id] = subscriber;
        // A browser can finish its handshake after the shutdown snapshot. It must
        // join the same shutdown rather than keeping Kestrel alive for 30 seconds.
        if (Volatile.Read(ref stopping) != 0) subscriber.TryStop();
        var sending = SendAsync(socket, subscriber);
        var authorization = CheckAuthorizationAsync(context, subscriber);
        var donors = SendDonorsAsync(context, subscriber);
        try { await ReceiveAsync(socket, subscriber); }
        catch (Exception error) when (error is OperationCanceledException or WebSocketException or JsonException) { }
        finally
        {
            try
            {
            stop.Cancel();
            subscriber.Queue.Writer.TryComplete();
            try { await sending; }
            catch (Exception error) when (error is OperationCanceledException or WebSocketException or ObjectDisposedException) { }
            try { await authorization; } catch (OperationCanceledException) { }
            try { await donors; } catch (OperationCanceledException) { }
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Session closed", timeout.Token); }
                catch (Exception error) when (error is OperationCanceledException or WebSocketException) { }
            }
            }
            finally
            {
                subscribers.TryRemove(id, out _);
                connectionSlots.Release();
                subscriber.Drained.TrySetResult();
            }
        }
    }

    private static async Task SendDonorsAsync(HttpContext context, Subscriber subscriber)
    {
        if (subscriber.Overlay is null) return;
        var store = context.RequestServices.GetRequiredService<DonorWidgetStore>();
        var settingsStore = context.RequestServices.GetRequiredService<FinancialSettingsStore>();
        var clock = context.RequestServices.GetRequiredService<TimeProvider>();
        string? previous = null;
        try
        {
            while (!subscriber.Stop.IsCancellationRequested)
            {
                var definition = subscriber.Overlay;
                var snapshots = new List<DonorWidgetSnapshot>();
                var now = clock.GetUtcNow();
                var settings = await settingsStore.GetAsync(subscriber.Stop.Token);
                foreach (var widget in definition!.Widgets.Where(w => !w.Hidden && w.Kind is
                    "donor-crown" or "donor-leaderboard" or "latest-supporter" or "current-stream-leader" or "current-stream-total"))
                {
                    // Preview never reads production financial totals.
                    if (subscriber.Preview)
                    {
                        snapshots.Add(new(widget.Id, "preview", now, [], "0", 0, 0, 0));
                        continue;
                    }
                    try
                    {
                        var period = widget.Kind.StartsWith("current-stream-", StringComparison.Ordinal) ? "current-stream" : widget.Donor.Period;
                        var range = LedgerPeriods.Resolve(period, settings.TimeZone, now, widget.Donor.CustomStart,
                            widget.Donor.CustomEndExclusive, settings.CurrentStreamStartUtc);
                        snapshots.Add(await store.SnapshotAsync(widget, range, now, subscriber.Stop.Token));
                    }
                    catch (ArgumentException)
                    {
                        snapshots.Add(new(widget.Id, "period-unavailable", now, [], "0", 0, 0, 0));
                    }
                }
                var fingerprint = JsonSerializer.Serialize(snapshots.Select(s => s with { GeneratedAt = default }), EventStore.JsonOptions);
                if (fingerprint != previous && (snapshots.Count > 0 || previous is not null))
                {
                    var payload = JsonSerializer.Serialize(new { op = "donors", widgets = snapshots }, EventStore.JsonOptions);
                    if (!subscriber.Queue.Writer.TryWrite(payload)) { subscriber.TryStop(); return; }
                    previous = fingerprint;
                }
                await Task.Delay(TimeSpan.FromSeconds(1), subscriber.Stop.Token);
            }
        }
        catch (OperationCanceledException) when (subscriber.Stop.IsCancellationRequested) { }
        catch (Exception) { subscriber.TryStop(); } // Reconnect obtains a fresh snapshot after a transient database failure.
    }

    private static async Task CheckAuthorizationAsync(HttpContext context, Subscriber subscriber)
    {
        if (!subscriber.Limited || subscriber.Overlay is null) return;
        var store = context.RequestServices.GetRequiredService<OverlayStore>();
        try
        {
            while (!subscriber.Stop.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), subscriber.Stop.Token);
                if (!await store.AuthorizeAsync((string)context.Items["OverlayToken"]!, subscriber.Overlay.Id, subscriber.Stop.Token)) { subscriber.TryStop(); return; }
            }
        }
        catch (OperationCanceledException) when (subscriber.Stop.IsCancellationRequested) { /* Normal close. */ }
        catch (Exception) { subscriber.TryStop(); } // Authorization-store failure closes the session rather than failing open.
    }

    private static async Task SendAsync(WebSocket socket, Subscriber subscriber)
    {
        try
        {
            await foreach (var payload in subscriber.Queue.Reader.ReadAllAsync(subscriber.Stop.Token))
                await socket.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, subscriber.Stop.Token);
        }
        finally { subscriber.Stop.Cancel(); }
    }

    private static async Task ReceiveAsync(WebSocket socket, Subscriber subscriber)
    {
        var buffer = new byte[16384];
        while (!subscriber.Stop.IsCancellationRequested)
        {
            var length = 0;
            WebSocketReceiveResult frame;
            do
            {
                frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), subscriber.Stop.Token);
                if (frame.MessageType == WebSocketMessageType.Close) return;
                length += frame.Count;
                if (frame.MessageType != WebSocketMessageType.Text || length >= buffer.Length)
                    throw new JsonException("Invalid WebSocket frame.");
            } while (!frame.EndOfMessage);
            using var document = JsonDocument.Parse(buffer.AsMemory(0, length));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
            if (!root.TryGetProperty("op", out var op) || op.ValueKind != JsonValueKind.String) throw new JsonException();
            string response;
            switch (op.GetString())
            {
                case "sound-result":
                    response = ReceiveSoundResult(root, subscriber);
                    break;
                case "ping": response = "{\"op\":\"pong\"}"; break;
                case "subscribe":
                    if (!root.TryGetProperty("types", out var selected) || selected.ValueKind != JsonValueKind.Array || selected.GetArrayLength() > 128)
                        throw new JsonException();
                    var types = selected.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()! : "").ToArray();
                    if (types.Any(type => string.IsNullOrWhiteSpace(type) || type.Length > 128)) throw new JsonException();
                    if (subscriber.Overlay is { CanvasEnabled: false } && types.Any(type => type != "chat.message")) throw new JsonException();
                    subscriber.Subscribe(types);
                    response = "{\"op\":\"subscribed\"}";
                    break;
                default: throw new JsonException();
            }
            if (!subscriber.Queue.Writer.TryWrite(response)) return;
        }
    }

    private static string ReceiveSoundResult(JsonElement root, Subscriber subscriber)
    {
        if (subscriber.Preview || subscriber.Overlay is null ||
            !root.TryGetProperty("executionId", out var execution) || execution.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(execution.GetString(), out var executionId) ||
            !root.TryGetProperty("state", out var soundState) || soundState.ValueKind != JsonValueKind.String)
            throw new JsonException("Invalid sound receipt.");
        var state = soundState.GetString();
        if (state is not ("started" or "completed" or "failed" or "timeout" or "interrupted")) throw new JsonException("Invalid sound state.");
        if (state != "started" && subscriber.SoundResults.TryGetValue(executionId, out var completion)) completion.TrySetResult(state);
        return "{\"op\":\"sound-received\"}";
    }

    private sealed class Subscriber(CancellationTokenSource stop)
    {
        public bool Subscribed { get; private set; }
        public ConcurrentDictionary<Guid, TaskCompletionSource<string>> SoundResults { get; } = new();
        private volatile string[] types = [];
        public volatile OverlayDefinition? Overlay;
        public bool Preview { get; init; }
        public bool Limited { get; init; }
        public CancellationTokenSource Stop { get; } = stop;
        public TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Channel<string> Queue { get; } = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
            { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        public bool Accepts(CanonicalEvent item) => (types.Contains(item.Type, StringComparer.Ordinal) || types.Contains("*", StringComparer.Ordinal)) &&
            (Overlay is null || (item.Provenance == EventProvenance.Live || Preview && !Limited) &&
                (Overlay.CanvasEnabled ? Overlay.Widgets.Any(w => !w.Hidden && (w.Kind == "chat" && w.Chat.Accepts(item) ||
                    w.Kind == "alert" && (w.Alert.EventTypes.Contains(item.Type) || w.Alert.EventTypes.Contains("*")) && w.Alert.Platforms.Contains(item.Platform))) : Overlay.Chat.Accepts(item)));
        public void Subscribe(string[] selected) { types = selected; Subscribed = true; }
        public void TryStop()
        {
            try { Stop.Cancel(); }
            catch (ObjectDisposedException) { /* A snapshot can retain a subscriber after disconnect/disposal. */ }
        }
    }
}
