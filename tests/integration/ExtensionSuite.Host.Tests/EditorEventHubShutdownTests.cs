using System.Net.WebSockets;
using ExtensionSuite.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class EditorEventHubShutdownTests
{
    [Fact]
    public async Task ShutdownRejectsNewConnectionsWithoutAcceptingTheirSocket()
    {
        var hub = new EditorEventHub(new SensitiveValues());
        var context = new DefaultHttpContext();
        var handshake = new ControlledHandshake();
        context.Features.Set<IHttpWebSocketFeature>(handshake);
        hub.BeginShutdown();
        await hub.ConnectAsync(context);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.False(handshake.Started.Task.IsCompleted);
        Assert.Equal(0, hub.SubscriberCount);
    }

    [Fact]
    public async Task HandshakeAlreadyInFlightCannotKeepHostAliveAfterShutdown()
    {
        var hub = new EditorEventHub(new SensitiveValues());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var context = new DefaultHttpContext { RequestAborted = timeout.Token };
        var handshake = new ControlledHandshake();
        context.Features.Set<IHttpWebSocketFeature>(handshake);
        var connection = hub.ConnectAsync(context);
        await handshake.Started.Task.WaitAsync(timeout.Token);
        // No subscriber exists yet when the host takes its shutdown snapshot.
        hub.BeginShutdown();
        Assert.Equal(0, hub.SubscriberCount);
        using var socket = new WaitingSocket();
        handshake.Accepted.SetResult(socket);
        await connection.WaitAsync(timeout.Token);
        Assert.Equal(0, hub.SubscriberCount);
        Assert.Equal(WebSocketState.Closed, socket.State);
        await hub.ShutdownAsync(timeout.Token);
    }

    private sealed class ControlledHandshake : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest => true;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<WebSocket> Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            Started.TrySetResult();
            return Accepted.Task;
        }
    }

    private sealed class WaitingSocket : WebSocket
    {
        private WebSocketState state = WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => state;
        public override string? SubProtocol => null;
        public override void Abort() => state = WebSocketState.Aborted;
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? description, CancellationToken cancellationToken)
        { state = WebSocketState.Closed; return Task.CompletedTask; }
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? description, CancellationToken cancellationToken)
        { state = WebSocketState.Closed; return Task.CompletedTask; }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Receive must be cancelled during shutdown.");
        }
    }
}
