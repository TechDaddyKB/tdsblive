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
    public int SubscriberCount => subscribers.Count;
    public void Shutdown()
    {
        foreach (var subscriber in subscribers.Values) subscriber.TryStop();
    }

    public void Publish(CanonicalEvent item)
    {
        var payload = CredentialRedactor.Json(JsonSerializer.SerializeToNode(new { op = "event", @event = item }, EventStore.JsonOptions), sensitive.Snapshot())!
            .ToJsonString(EventStore.JsonOptions);
        foreach (var subscriber in subscribers.Values)
            if (subscriber.Accepts(item) && !subscriber.Queue.Writer.TryWrite(subscriber.Overlay is null ? payload :
                CredentialRedactor.Json(JsonSerializer.SerializeToNode(new { op = "event", @event = OverlayEndpoints.PublicChat(item) }, EventStore.JsonOptions), sensitive.Snapshot())!.ToJsonString(EventStore.JsonOptions))) subscriber.TryStop();
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
        var sending = SendAsync(socket, subscriber);
        var authorization = CheckAuthorizationAsync(context, subscriber);
        try { await ReceiveAsync(socket, subscriber); }
        catch (Exception error) when (error is OperationCanceledException or WebSocketException or JsonException) { }
        finally
        {
            subscribers.TryRemove(id, out _);
            connectionSlots.Release();
            stop.Cancel();
            subscriber.Queue.Writer.TryComplete();
            try { await sending; }
            catch (Exception error) when (error is OperationCanceledException or WebSocketException) { }
            try { await authorization; } catch (OperationCanceledException) { }
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Session closed", timeout.Token); }
                catch (Exception error) when (error is OperationCanceledException or WebSocketException) { }
            }
        }
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
                case "ping": response = "{\"op\":\"pong\"}"; break;
                case "subscribe":
                    if (!root.TryGetProperty("types", out var selected) || selected.ValueKind != JsonValueKind.Array || selected.GetArrayLength() > 128)
                        throw new JsonException();
                    var types = selected.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()! : "").ToArray();
                    if (types.Any(type => string.IsNullOrWhiteSpace(type) || type.Length > 128)) throw new JsonException();
                    if (subscriber.Overlay is not null && types.Any(type => type != "chat.message")) throw new JsonException();
                    subscriber.Subscribe(types);
                    response = "{\"op\":\"subscribed\"}";
                    break;
                default: throw new JsonException();
            }
            if (!subscriber.Queue.Writer.TryWrite(response)) return;
        }
    }

    private sealed class Subscriber(CancellationTokenSource stop)
    {
        private volatile string[] types = [];
        public volatile OverlayDefinition? Overlay;
        public bool Preview { get; init; }
        public bool Limited { get; init; }
        public CancellationTokenSource Stop { get; } = stop;
        public Channel<string> Queue { get; } = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
            { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        public bool Accepts(CanonicalEvent item) => (types.Contains(item.Type, StringComparer.Ordinal) || types.Contains("*", StringComparer.Ordinal)) &&
            (Overlay is null || Overlay.Chat.Accepts(item) && (item.Provenance == EventProvenance.Live || Preview));
        public void Subscribe(string[] selected) => types = selected;
        public void TryStop()
        {
            try { Stop.Cancel(); }
            catch (ObjectDisposedException) { /* A snapshot can retain a subscriber after disconnect/disposal. */ }
        }
    }
}
