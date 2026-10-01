using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace ExtensionSuite.StreamerBot;

public sealed class BotRequestException(string kind, bool mayHaveExecuted = false) : Exception("Bot request failed: " + kind)
{
    public string Kind { get; } = kind;
    public bool MayHaveExecuted { get; } = mayHaveExecuted;
}

public sealed class BotProtocolSession : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly SemaphoreSlim requestSlots = new(64, 64);
    private readonly ConcurrentDictionary<string, Pending> pending = new();
    private readonly Channel<JsonObject> events = Channel.CreateBounded<JsonObject>(new BoundedChannelOptions(1024)
        { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private Task receiver = Task.CompletedTask;
    public ChannelReader<JsonObject> Events => events.Reader;
    public Task Completion => receiver;

    public async Task<JsonObject?> ConnectAsync(Uri endpoint, bool expectHello, CancellationToken cancellationToken)
    {
        await socket.ConnectAsync(endpoint, cancellationToken);
        var hello = expectHello ? await ReceiveMessageAsync(cancellationToken) : null;
        if (expectHello && (hello?["request"] is not JsonValue helloRequest || !helloRequest.TryGetValue<string>(out var helloName) || helloName != "Hello")) throw new BotRequestException("invalidHello");
        receiver = ReceiveLoopAsync();
        return hello;
    }

    public async Task<JsonObject> RequestAsync(string request, JsonObject? arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        deadline.CancelAfter(timeout);
        if (socket.State != WebSocketState.Open) throw new BotRequestException("disconnected");
        if (!await requestSlots.WaitAsync(0, cancellationToken)) throw new BotRequestException("requestLimit");
        var id = Guid.CreateVersion7().ToString();
        var item = new Pending();
        pending[id] = item;
        try
        {
            var payload = arguments?.DeepClone() as JsonObject ?? new JsonObject();
            payload["id"] = id;
            payload["request"] = request;
            await sendGate.WaitAsync(deadline.Token);
            try
            {
                // Once sending starts, a lost response cannot establish whether an external action ran.
                item.Sent = true;
                await socket.SendAsync(Encoding.UTF8.GetBytes(payload.ToJsonString()), WebSocketMessageType.Text, true, deadline.Token);
            }
            finally { sendGate.Release(); }
            JsonObject response;
            response = await item.Response.Task.WaitAsync(deadline.Token);
            if (response["status"] is not JsonValue status || !status.TryGetValue<string>(out var statusName)) throw new BotRequestException("invalidResponse", item.Sent);
            if (statusName != "ok") throw new BotRequestException("rejected");
            return response;
        }
        catch (WebSocketException) { throw new BotRequestException("disconnected", item.Sent); }
        catch (ObjectDisposedException) { throw new BotRequestException("disconnected", item.Sent); }
        catch (OperationCanceledException)
        {
            throw new BotRequestException(lifetime.IsCancellationRequested ? "disconnected" : cancellationToken.IsCancellationRequested ? "cancelled" : "timeout", item.Sent);
        }
        finally { pending.TryRemove(id, out _); requestSlots.Release(); }
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var message = await ReceiveMessageAsync(lifetime.Token);
                if (message["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var id))
                {
                    if (pending.TryGetValue(id, out var request)) request.Response.TrySetResult(message);
                }
                else if (message["event"] is JsonObject && !events.Writer.TryWrite(message))
                    throw new BotRequestException("eventQueueOverflow");
            }
        }
        finally
        {
            foreach (var item in pending.Values) item.Response.TrySetException(new BotRequestException("disconnected", item.Sent));
            events.Writer.TryComplete();
        }
    }

    private async Task<JsonObject> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16384];
        using var payload = new MemoryStream();
        WebSocketReceiveResult frame;
        do
        {
            frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (frame.MessageType != WebSocketMessageType.Text || payload.Length + frame.Count > 1048576)
                throw new BotRequestException("invalidFrame");
            payload.Write(buffer, 0, frame.Count);
        } while (!frame.EndOfMessage);
        return JsonNode.Parse(payload.ToArray(), documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject
            ?? throw new BotRequestException("invalidPayload");
    }

    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync();
        socket.Abort();
        try { await receiver; }
        catch (Exception error) when (error is OperationCanceledException or WebSocketException or BotRequestException or JsonException) { /* Expected closed session. */ }
        socket.Dispose();
        lifetime.Dispose();
    }

    private sealed class Pending
    {
        public volatile bool Sent;
        public TaskCompletionSource<JsonObject> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
