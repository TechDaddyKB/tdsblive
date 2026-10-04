using System.Net;
using System.Net.Sockets;
using ExtensionSuite.DesktopControl;
using ExtensionSuite.Host;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class DesktopControlTests
{
    [Theory]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("::1", "::1")]
    [InlineData("0.0.0.0", "0.0.0.0")]
    [InlineData("::", "::")]
    [InlineData("192.168.1.20", "127.0.0.1")]
    [InlineData("fd00::20", "127.0.0.1")]
    public void DesktopHandoffStaysLocalWithoutChangingSavedLanBinding(string configured, string editor)
    {
        var source = new ExtensionSuite.Core.ServerConfiguration { Host = configured, Port = 23456, EnableLan = true };
        var local = DesktopSession.LocalEditorServer(source);
        Assert.Equal(editor, local.Host);
        Assert.Equal(23456, local.Port);
        Assert.True(EditorBrowserLauncher.EditorUri(local).IsLoopback);
        Assert.Equal(configured, source.Host);
    }

    [Fact]
    public void SessionCredentialsCannotLeakThroughRecordFormatting()
    {
        var session = DesktopProtocol.NewSessionToken();
        Assert.DoesNotContain(session, new DesktopBootstrap(1234, session).ToString());
        Assert.DoesNotContain(session, new DesktopRequest(session, "status").ToString());
    }

    [Fact]
    public async Task ControlRejectsUnauthorizedClientsAndNeverAdmitsUnknownCommands()
    {
        var lifetime = new Lifetime();
        using var lifecycle = new ApplicationLifecycle(lifetime, TimeProvider.System);
        await using var server = new DesktopControlServer(lifecycle, lifetime, "http://127.0.0.1:9234/editor", DesktopProtocol.NewSessionToken());
        var bootstrap = server.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var bad = bootstrap with { SessionToken = DesktopProtocol.NewSessionToken() };
        await Assert.ThrowsAsync<EndOfStreamException>(() => DesktopProtocol.SendAsync(bad, "quit", deadline.Token));
        Assert.Null(lifecycle.Operation);
        Assert.Equal("unsupported", (await DesktopProtocol.SendAsync(bootstrap, "restore", deadline.Token)).State);
        Assert.Equal("busy", (await DesktopProtocol.SendAsync(bootstrap, "restart", deadline.Token)).State);
        Assert.Equal(0, lifetime.Stops);
        var starting = await DesktopProtocol.SendAsync(bootstrap, "status", deadline.Token);
        Assert.Equal("starting", starting.State);
        Assert.Equal("http://127.0.0.1:9234/editor", starting.EditorUrl);
    }

    [Fact]
    public async Task AcceptedRequestStopsOnceAndWaitReportsOnlyVerifiedCompletion()
    {
        var lifetime = new Lifetime();
        using var lifecycle = new ApplicationLifecycle(lifetime, TimeProvider.System);
        await using var server = new DesktopControlServer(lifecycle, lifetime, "http://127.0.0.1:9234/editor", DesktopProtocol.NewSessionToken());
        var bootstrap = server.Start();
        server.SetReady();
        server.RequestOpen();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var waiting = DesktopProtocol.SendAsync(bootstrap, "wait", deadline.Token);
        Assert.Equal(1, (await DesktopProtocol.SendAsync(bootstrap, "status", deadline.Token)).OpenRequests);
        var replies = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => DesktopProtocol.SendAsync(bootstrap, "restart", deadline.Token)));
        Assert.Single(replies, reply => reply.Accepted);
        Assert.Equal("restart", lifecycle.Operation?.Kind);
        Assert.False(waiting.IsCompleted);
        await server.CompleteAsync("restart-ready");
        Assert.Equal("restart-ready", (await waiting).State);
        Assert.Equal(1, lifetime.Stops);
    }

    [Theory]
    [InlineData("quit")]
    [InlineData("failed")]
    public async Task QuitOrFailedRestoreDoesNotBecomeRestartReady(string outcome)
    {
        var lifetime = new Lifetime();
        using var lifecycle = new ApplicationLifecycle(lifetime, TimeProvider.System);
        await using var server = new DesktopControlServer(lifecycle, lifetime, "http://127.0.0.1:9234/editor", DesktopProtocol.NewSessionToken());
        var bootstrap = server.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var waiting = DesktopProtocol.SendAsync(bootstrap, "wait", deadline.Token);
        await server.CompleteAsync(outcome);
        Assert.Equal(outcome, (await waiting).State);
    }

    [Fact]
    public async Task OversizedOrPartialFramesCannotHoldShutdown()
    {
        var lifetime = new Lifetime();
        using var lifecycle = new ApplicationLifecycle(lifetime, TimeProvider.System);
        await using var server = new DesktopControlServer(lifecycle, lifetime, "http://127.0.0.1:9234/editor", DesktopProtocol.NewSessionToken());
        var bootstrap = server.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var oversized = new TcpClient();
        await oversized.ConnectAsync(IPAddress.Loopback, bootstrap.Port, deadline.Token);
        await oversized.GetStream().WriteAsync(new byte[DesktopProtocol.MaximumFrameBytes], deadline.Token);
        Assert.Equal(0, await oversized.GetStream().ReadAsync(new byte[1], deadline.Token));
        using var partial = new TcpClient();
        await partial.ConnectAsync(IPAddress.Loopback, bootstrap.Port, deadline.Token);
        await partial.GetStream().WriteAsync(new byte[] { (byte)'{' }, deadline.Token);
        Assert.Null(lifecycle.Operation);
        // Disposal cancels authenticated waits and partial reads as well.
    }

    [Fact]
    public async Task ProfileOwnerRequestsOpenAndCanBeReacquiredAfterRelease()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-desktop-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            await using var first = DesktopProfileOwner.AcquireOrRequestOpen(directory);
            Assert.NotNull(first);
            var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            first.Watch(() => opened.TrySetResult());
            Assert.Null(DesktopProfileOwner.AcquireOrRequestOpen(directory));
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
            first.Dispose();
            await using var replacement = DesktopProfileOwner.AcquireOrRequestOpen(directory);
            Assert.NotNull(replacement);
            Assert.Equal(0, new FileInfo(Path.Combine(directory, ".desktop-owner.lock")).Length);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        private int stops;
        public int Stops => Volatile.Read(ref stops);
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => Interlocked.Increment(ref stops);
    }
}
