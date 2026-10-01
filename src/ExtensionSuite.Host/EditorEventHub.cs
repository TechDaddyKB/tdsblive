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
            if (subscriber.Accepts(item.Type) && !subscriber.Queue.Writer.TryWrite(payload)) subscriber.TryStop();
    }

    public async Task ConnectAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var subscriber = new Subscriber(stop);
        var id = Guid.CreateVersion7();
        if (!await connectionSlots.WaitAsync(0, context.RequestAborted))
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Connection limit reached", CancellationToken.None);
            return;
        }
        subscribers[id] = subscriber;
        var sending = SendAsync(socket, subscriber);
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
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Session closed", timeout.Token); }
                catch (Exception error) when (error is OperationCanceledException or WebSocketException) { }
            }
        }
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
        public CancellationTokenSource Stop { get; } = stop;
        public Channel<string> Queue { get; } = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
            { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        public bool Accepts(string type) => types.Contains(type, StringComparer.Ordinal) || types.Contains("*", StringComparer.Ordinal);
        public void Subscribe(string[] selected) => types = selected;
        public void TryStop()
        {
            try { Stop.Cancel(); }
            catch (ObjectDisposedException) { /* A snapshot can retain a subscriber after disconnect/disposal. */ }
        }
    }
}
