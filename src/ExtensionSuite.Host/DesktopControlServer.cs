using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ExtensionSuite.DesktopControl;

namespace ExtensionSuite.Host;

/// <summary>Session-only loopback capability; never mounted on the overlay/editor HTTP server.</summary>
public sealed class DesktopControlServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly SemaphoreSlim clients = new(8);
    private readonly TaskCompletionSource<DesktopReply> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ApplicationLifecycle lifecycle;
    private readonly IHostApplicationLifetime lifetime;
    private readonly string sessionToken;
    private readonly string editorUrl;
    private readonly HashSet<Task> connections = [];
    private Task? accepting;
    private volatile bool ready;
    private int openRequests;
    private long lastContactTicks;
    internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public DateTimeOffset? LastContact => Interlocked.Read(ref lastContactTicks) is var ticks && ticks != 0 ? new(ticks, TimeSpan.Zero) : null;

    public DesktopControlServer(ApplicationLifecycle lifecycle, IHostApplicationLifetime lifetime, string editorUrl, string sessionToken)
    {
        if (!DesktopProtocol.ValidSessionToken(sessionToken)) throw new ArgumentException("Invalid desktop session.");
        this.lifecycle = lifecycle;
        this.lifetime = lifetime;
        this.editorUrl = editorUrl;
        this.sessionToken = sessionToken;
    }

    public DesktopBootstrap Start()
    {
        listener.Start(8);
        accepting = AcceptAsync();
        return new(((IPEndPoint)listener.LocalEndpoint).Port, sessionToken);
    }

    public void SetReady() => ready = true;
    public void RequestOpen() => Interlocked.Increment(ref openRequests);

    public async Task CompleteAsync(string state)
    {
        completed.TrySetResult(new(state, editorUrl));
        // A connected companion waits for the post-checkpoint/restore outcome.
        // No companion or a failed companion must never hold shutdown indefinitely.
        await Task.WhenAny(delivered.Task, Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None));
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stopping.Token);
                if (!await clients.WaitAsync(0, stopping.Token)) { client.Dispose(); continue; }
                var task = ServeAsync(client);
                lock (connections) connections.Add(task);
                _ = task.ContinueWith(finished => { lock (connections) connections.Remove(finished); }, TaskScheduler.Default);
            }
        }
        catch (Exception error) when (error is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // Listener closure and cancellation are normal during owned shutdown.
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        using (var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token))
        {
            requestDeadline.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                await using var stream = client.GetStream();
                var request = await DesktopProtocol.ReadAsync<DesktopRequest>(stream, requestDeadline.Token);
                if (!DesktopProtocol.Authenticate(sessionToken, request.SessionToken)) return;
                Interlocked.Exchange(ref lastContactTicks, DateTimeOffset.UtcNow.Ticks);
                var (reply, stopAfterReply) = await ReplyAsync(request.Command);
                using var writeDeadline = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                writeDeadline.CancelAfter(TimeSpan.FromSeconds(3));
                // Once admitted, a lifecycle request stops even if its caller disconnects.
                try { await DesktopProtocol.WriteAsync(stream, reply, writeDeadline.Token); }
                finally { if (stopAfterReply) lifetime.StopApplication(); }
                if (request.Command == "wait") delivered.TrySetResult();
            }
            catch (Exception error) when (error is IOException or SocketException or JsonException or OperationCanceledException or ObjectDisposedException)
            {
                // Reject malformed or disconnected clients without logging capabilities.
            }
            finally { clients.Release(); }
        }
    }

    private async Task<(DesktopReply Reply, bool Stop)> ReplyAsync(string command)
    {
        switch (command)
        {
            case "wait": return (await completed.Task.WaitAsync(stopping.Token), false);
            case "status": return (Status(), false);
            case "restart" or "quit":
                var admitted = ready && !completed.Task.IsCompleted && lifecycle.Request(command);
                return (new(admitted ? "stopping" : "busy", editorUrl, admitted), admitted);
            default: return (new("unsupported"), false);
        }
    }

    private DesktopReply Status()
    {
        if (completed.Task.IsCompletedSuccessfully) return completed.Task.Result;
        var state = lifecycle.Operation?.Kind switch
        {
            "restart" or "restore" => "restarting",
            "quit" => "stopping",
            _ => ready ? "running" : "starting"
        };
        return new(state, editorUrl, OpenRequests: Volatile.Read(ref openRequests));
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        listener.Stop();
        if (accepting is not null) await accepting;
        Task[] pending;
        lock (connections) pending = connections.ToArray();
        await Task.WhenAll(pending);
        clients.Dispose();
        stopping.Dispose();
    }
}
