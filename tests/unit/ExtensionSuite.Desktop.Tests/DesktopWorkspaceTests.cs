using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using ExtensionSuite.Desktop;
using ExtensionSuite.DesktopControl;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class DesktopWorkspaceTests
{
    private static async Task RunAsync(Func<Task> test)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await session.Dispatch(async () => { await test(); return true; }, deadline.Token);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!condition()) await Task.Delay(20, deadline.Token);
    }

    private static Button Button(DesktopApp app, string label) => app.Controls!.GetLogicalDescendants()
        .OfType<Button>().Single(button => Equals(button.Content, label));

    private static string Text(DesktopApp app) => string.Join("\n", app.Controls!.GetLogicalDescendants()
        .OfType<TextBlock>().Select(text => text.Text));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    [Theory]
    [InlineData(false, "", "needs to be started by TDSBLive")]
    [InlineData(true, "invalid\n", "lost their connection")]
    [InlineData(true, "", "lost their connection")]
    public Task MissingOrBrokenLauncherPipeProvidesClosableGuidance(bool attached, string input, string guidance) => RunAsync(async () =>
    {
        using var app = new DesktopApp(_ => throw new InvalidOperationException("No browser should open."), _ => false, _ => Task.FromResult(true));
        using var pipe = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(input));
        await app.StartAsync(pipe, attached);
        Assert.Contains(guidance, Text(app));
        Assert.False(Button(app, "Quit").IsEnabled);
        Click(Button(app, "Close desktop controls"));
        app.Controls!.Close();
    });

    [Fact]
    public Task RedirectedBootstrapConnectsWithoutSavingCredentials() => RunAsync(async () =>
    {
        await using var host = new OwnedControlHost();
        using var app = new DesktopApp(_ => { }, _ => false, _ => Task.FromResult(false));
        using var pipe = new MemoryStream();
        await DesktopProtocol.WriteAsync(pipe, host.Bootstrap, CancellationToken.None);
        pipe.Position = 0;
        var attached = app.StartAsync(pipe, true);
        await UntilAsync(() => app.Controls?.IsVisible == true && Button(app, "Quit").IsEnabled);
        Assert.DoesNotContain(host.Bootstrap.SessionToken, Text(app));
        host.Complete("quit");
        await attached;
        app.Controls!.Close();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingTrayOffersSameControlsAndClosingOnlyHidesThem(bool dark) => RunAsync(async () =>
    {
        await using var host = new OwnedControlHost();
        var opened = new List<Uri>();
        var available = false;
        using var app = new DesktopApp(opened.Add, _ => available, _ => Task.FromResult(false));
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        app.InitializeTray();
        var attached = app.AttachAsync(host.Bootstrap);
        await UntilAsync(() => app.Controls?.IsVisible == true && Button(app, "Restart").IsEnabled);
        Assert.Equal("TDSBLive is running", app.Controls!.Title);
        Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, app.Controls.ActualThemeVariant);
        Assert.Contains("tray is unavailable", Text(app));
        Assert.True(Button(app, "Quit").MinHeight >= 44);
        Click(Button(app, "Open editor"));
        Assert.Equal("http://127.0.0.1:23456/editor", Assert.Single(opened).AbsoluteUri);
        app.Controls.Close();
        Assert.False(app.Controls.IsVisible);
        Assert.Empty(host.Commands);
        await Task.Delay(1200);
        Assert.False(app.Controls.IsVisible);
        available = true;
        await Task.Delay(1200);
        available = false;
        await UntilAsync(() => app.Controls.IsVisible);
        host.Complete("quit");
        await attached;
        app.Controls.Close();
        Assert.False(app.Controls.IsVisible);
    });

    [Theory]
    [InlineData("Restart", "restart")]
    [InlineData("Quit", "quit")]
    public Task CancelAndRepeatedClicksSendNoUnconfirmedCommand(string label, string command) => RunAsync(async () =>
    {
        await using var host = new OwnedControlHost();
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmations = 0;
        using var app = new DesktopApp(_ => { }, _ => false, chosen =>
        {
            Assert.Equal(command, chosen);
            confirmations++;
            return answer.Task;
        });
        var attached = app.AttachAsync(host.Bootstrap);
        await UntilAsync(() => app.Controls?.IsVisible == true && Button(app, label).IsEnabled);
        var button = Button(app, label);
        Click(button);
        Assert.False(button.IsEnabled);
        Click(button);
        Assert.Equal(1, confirmations);
        Assert.Empty(host.Commands);
        answer.SetResult(false);
        await UntilAsync(() => button.IsEnabled);
        Assert.Empty(host.Commands);
        answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Click(button);
        answer.SetResult(true);
        await UntilAsync(() => host.Commands.Count == 1);
        Assert.Equal(command, Assert.Single(host.Commands));
        await UntilAsync(() => !Button(app, "Quit").IsEnabled && Text(app).Contains(command == "restart" ? "Restarting" : "Stopping"));
        host.Complete(command == "restart" ? "relaunched" : "quit");
        await attached;
        app.Controls!.Close();
    });

    [Fact]
    public Task ReturningTrayRetriesRegistrationWithoutReplacingSessionOrReopeningHiddenControls() => RunAsync(async () =>
    {
        await using var host = new OwnedControlHost();
        var available = false;
        var shellReturned = false;
        var attempts = new List<TrayIcon?>();
        using var app = new DesktopApp(_ => throw new InvalidOperationException("No browser should open."),
            _ => available, _ => Task.FromResult(false), icon =>
            {
                attempts.Add(icon);
                if (shellReturned) available = true;
            });
        app.InitializeTray();
        var attached = app.AttachAsync(host.Bootstrap);
        await UntilAsync(() => app.Controls?.IsVisible == true && Button(app, "Quit").IsEnabled);
        var initialIcon = Assert.Single(attempts);
        Assert.NotNull(initialIcon);
        app.Controls!.Close();
        shellReturned = true;
        await UntilAsync(() => available);
        Assert.False(app.Controls.IsVisible);
        Assert.All(attempts, icon => Assert.Same(initialIcon, icon));
        var count = attempts.Count;
        await Task.Delay(1200);
        Assert.Equal(count, attempts.Count);
        Assert.Empty(host.Commands);
        host.Complete("quit");
        await attached;
        app.Controls.Close();
    });

    [Theory]
    [InlineData("port-conflict", "Another app may be using")]
    [InlineData("failed", "safety copy is retained")]
    [InlineData("stopped", "Open it again using its shortcut")]
    public Task FailedOrUnexpectedCompletionShowsRecoveryWithoutRelaunching(string outcome, string guidance) => RunAsync(async () =>
    {
        await using var host = new OwnedControlHost();
        using var app = new DesktopApp(_ => { }, _ => false, _ => Task.FromResult(true));
        var attached = app.AttachAsync(host.Bootstrap);
        await UntilAsync(() => app.Controls?.IsVisible == true && Button(app, "Quit").IsEnabled);
        host.Complete(outcome);
        await attached;
        Assert.Equal("TDSBLive needs attention", app.Controls!.Title);
        Assert.Contains(guidance, Text(app));
        Assert.False(Button(app, "Restart").IsEnabled);
        Assert.Empty(host.Commands);
        Click(Button(app, "Close desktop controls"));
        app.Controls.Close();
    });

    [Theory]
    [InlineData("http://example.com/editor", false, "editor address is invalid")]
    [InlineData("https://127.0.0.1/editor", false, "editor address is invalid")]
    [InlineData("not a URL", false, "editor address is invalid")]
    [InlineData("http://127.0.0.1:23456/editor", true, "Enter this address in your browser")]
    public Task BrowserHandoffRejectsForeignAddressesAndExplainsLaunchFailure(string address, bool fail, string guidance) => RunAsync(async () =>
    {
        await using var host = new OwnedControlHost { EditorUrl = address };
        var launches = 0;
        using var app = new DesktopApp(_ => { launches++; if (fail) throw new Win32Exception(); }, _ => false, _ => Task.FromResult(false));
        var attached = app.AttachAsync(host.Bootstrap);
        await UntilAsync(() => app.Controls?.IsVisible == true && Button(app, "Open editor").IsEnabled);
        Click(Button(app, "Open editor"));
        Assert.Equal(fail ? 1 : 0, launches);
        Assert.Contains(guidance, Text(app));
        host.Complete("quit");
        await attached;
        app.Controls!.Close();
    });

    [Fact]
    public Task DuplicateLaunchRequestsOpenOnceAndBusyHostCannotAdmitCommands() => RunAsync(async () =>
    {
        await using var host = new OwnedControlHost { State = "starting" };
        var launches = 0;
        using var app = new DesktopApp(_ => launches++, _ => false, _ => Task.FromResult(true));
        var attached = app.AttachAsync(host.Bootstrap);
        await UntilAsync(() => app.Controls?.IsVisible == true);
        Assert.False(Button(app, "Open editor").IsEnabled);
        Click(Button(app, "Quit"));
        Assert.Empty(host.Commands);
        host.State = "running";
        host.OpenRequests = 1;
        await UntilAsync(() => launches == 1);
        await Task.Delay(1200);
        Assert.Equal(1, launches);
        host.AcceptCommands = false;
        Click(Button(app, "Restart"));
        await UntilAsync(() => Text(app).Contains("already busy"));
        Assert.Empty(host.Commands);
        host.Complete("quit");
        await attached;
        app.Controls!.Close();
    });

    [Fact]
    public Task KnownShutdownFailureIsNotOverwrittenBySimultaneousConnectionClosure() => RunAsync(async () =>
    {
        await using var host = new OwnedControlHost { PauseStatusResponses = true };
        using var app = new DesktopApp(_ => { }, _ => false, _ => Task.FromResult(true));
        var attached = app.AttachAsync(host.Bootstrap);
        await host.StatusSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        host.Complete("port-conflict");
        await UntilAsync(() => app.Controls?.IsVisible == true && Text(app).Contains("Another app may be using"));
        host.Disconnect();
        await attached;
        Assert.Contains("Another app may be using", Text(app));
        Assert.DoesNotContain("lost their connection", Text(app));
        Click(Button(app, "Close desktop controls"));
        app.Controls!.Close();
    });

    [Fact]
    public Task ConnectionLossDisablesCommandsAndLeavesRecoveryGuidance() => RunAsync(async () =>
    {
        await using var host = new OwnedControlHost();
        using var app = new DesktopApp(_ => { }, _ => false, _ => Task.FromResult(true));
        var attached = app.AttachAsync(host.Bootstrap);
        await UntilAsync(() => app.Controls?.IsVisible == true && Button(app, "Quit").IsEnabled);
        host.Disconnect();
        await attached;
        Assert.Equal("TDSBLive needs attention", app.Controls!.Title);
        Assert.Contains("use Settings", Text(app));
        Assert.False(Button(app, "Quit").IsEnabled);
        Assert.Empty(host.Commands);
        Click(Button(app, "Close desktop controls"));
        app.Controls.Close();
    });

    private sealed class OwnedControlHost : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stopping = new();
        private readonly TaskCompletionSource<string> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task accepting;
        private readonly List<Task> connections = [];
        public DesktopBootstrap Bootstrap { get; }
        public string EditorUrl { get; set; } = "http://127.0.0.1:23456/editor";
        public string State { get; set; } = "running";
        public int OpenRequests { get; set; }
        public bool AcceptCommands { get; set; } = true;
        public bool PauseStatusResponses { get; init; }
        public TaskCompletionSource StatusSeen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public System.Collections.Concurrent.ConcurrentQueue<string> Commands { get; } = new();

        public OwnedControlHost()
        {
            listener.Start();
            Bootstrap = new(((IPEndPoint)listener.LocalEndpoint).Port, DesktopProtocol.NewSessionToken());
            accepting = AcceptAsync();
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!stopping.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(stopping.Token);
                    connections.Add(ReplyAsync(client));
                }
            }
            catch (Exception error) when (error is SocketException or OperationCanceledException) { }
        }

        private async Task ReplyAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var request = await DesktopProtocol.ReadAsync<DesktopRequest>(client.GetStream(), stopping.Token);
                    Assert.True(DesktopProtocol.Authenticate(Bootstrap.SessionToken, request.SessionToken));
                    DesktopReply reply;
                    if (request.Command == "wait") reply = new(await completed.Task.WaitAsync(stopping.Token), EditorUrl);
                    else if (request.Command == "status")
                    {
                        StatusSeen.TrySetResult();
                        if (PauseStatusResponses) await Task.Delay(Timeout.Infinite, stopping.Token);
                        reply = new(State, EditorUrl, OpenRequests: OpenRequests);
                    }
                    else
                    {
                        var accepted = AcceptCommands && State == "running";
                        if (accepted) { Commands.Enqueue(request.Command); State = request.Command == "restart" ? "restarting" : "stopping"; }
                        reply = new(accepted ? "stopping" : "busy", EditorUrl, accepted);
                    }
                    await DesktopProtocol.WriteAsync(client.GetStream(), reply, stopping.Token);
                }
                catch (Exception error) when (error is IOException or SocketException or OperationCanceledException) { }
            }
        }

        public void Complete(string outcome) => completed.TrySetResult(outcome);
        public void Disconnect() { stopping.Cancel(); listener.Stop(); }
        public async ValueTask DisposeAsync()
        {
            Disconnect();
            await accepting;
            await Task.WhenAll(connections);
            stopping.Dispose();
        }
    }
}
