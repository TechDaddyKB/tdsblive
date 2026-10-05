using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using ExtensionSuite.DesktopControl;

namespace ExtensionSuite.Desktop;

public sealed class DesktopApp : Application, IDisposable
{
    private const string RunningLabel = "TDSBLive is running";
    private readonly CancellationTokenSource stopping = new();
    private readonly CancellationToken stopped;
    private readonly NativeMenuItem open = new("Open editor");
    private readonly NativeMenuItem restart = new("Restart");
    private readonly NativeMenuItem quit = new("Quit");
    private TrayIcon? tray;
    private Window? controls;
    private TextBlock? statusText;
    private Button? openButton, restartButton, quitButton;
    private DesktopBootstrap? bootstrap;
    private IClassicDesktopStyleApplicationLifetime? lifetime;
    private bool running, busy;
    private string? editorUrl;
    private int openRequests;
    private bool trayUnavailable;
    private bool exiting;
    private bool completionReceived;
    private string statusLabel = "Starting TDSBLive…";
    private readonly Action<Uri> launchBrowser;
    private readonly Func<TrayIcon?, bool> trayRegistered;
    private readonly Func<string, Task<bool>> confirmation;
    internal Window? Controls => controls;

    public DesktopApp() : this(LaunchBrowser, IsTrayRegistered, ConfirmAsync) { }

    internal DesktopApp(Action<Uri> launchBrowser, Func<TrayIcon?, bool> trayRegistered,
        Func<string, Task<bool>> confirmation)
    {
        stopped = stopping.Token;
        (this.launchBrowser, this.trayRegistered, this.confirmation) = (launchBrowser, trayRegistered, confirmation);
    }

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            lifetime = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => Dispose();
            InitializeTray();
            _ = StartAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private Task StartAsync() => StartAsync(Console.OpenStandardInput(),
        Program.Arguments.SequenceEqual(new[] { "--attach" }) && Console.IsInputRedirected);

    internal async Task StartAsync(Stream input, bool attached)
    {
        try
        {
            if (!attached)
            {
                ShowControls("This desktop companion needs to be started by TDSBLive. Open TDSBLive using its shortcut.");
                return;
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var session = await DesktopProtocol.ReadAsync<DesktopBootstrap>(input, timeout.Token);
            await AttachAsync(session);
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ArgumentException or System.Text.Json.JsonException)
        {
            ConnectionFailed();
        }
    }

    internal void InitializeTray()
    {
        var menu = new NativeMenu();
        menu.Items.Add(open);
        menu.Items.Add(restart);
        menu.Items.Add(quit);
        open.Click += (_, _) => OpenEditor();
        restart.Click += async (_, _) => await RequestAsync("restart");
        quit.Click += async (_, _) => await RequestAsync("quit");
        tray = new TrayIcon { Icon = CreateIcon(), ToolTipText = "TDSBLive — Starting", Menu = menu, IsVisible = true };
        tray.Clicked += (_, _) => OpenEditor();
        TrayIcon.SetIcons(this, new TrayIcons { tray });
        SetState("Starting TDSBLive…", false);
    }

    internal async Task AttachAsync(DesktopBootstrap session)
    {
        bootstrap = session;
        var waiting = WaitForCompletionAsync();
        try
        {
            while (!stopped.IsCancellationRequested)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped);
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                var reply = await DesktopProtocol.SendAsync(bootstrap, "status", deadline.Token);
                editorUrl = reply.EditorUrl;
                SetState(reply.State switch
                {
                    "running" => RunningLabel, "restarting" => "Restarting TDSBLive…",
                    "stopping" => "Stopping TDSBLive…", "starting" => "Starting TDSBLive…",
                    _ => "TDSBLive needs attention"
                }, reply.State == "running");
                if (running && reply.OpenRequests != openRequests)
                {
                    openRequests = reply.OpenRequests;
                    OpenEditor();
                }
                var available = trayRegistered(tray);
                if (!available && !trayUnavailable)
                    ShowControls("TDSBLive is running. Use these controls while your desktop tray is unavailable.");
                trayUnavailable = !available;
                await Task.Delay(TimeSpan.FromSeconds(1), stopped);
                if (waiting.IsCompleted) { await waiting; return; }
            }
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ArgumentException or System.Text.Json.JsonException)
        {
            ConnectionFailed();
        }
        finally
        {
            // A connection failure can end polling before the completion waiter
            // settles. Observe its fault without killing or restarting the host.
            _ = waiting.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private void ConnectionFailed()
    {
        if (stopped.IsCancellationRequested || completionReceived) return;
        SetState("Stopped unexpectedly", false);
        ShowControls("TDSBLive stopped or desktop controls lost their connection. If the editor still works, use Settings to restart or quit. Otherwise open TDSBLive again.");
    }

    private static bool IsTrayRegistered(TrayIcon? icon) => OperatingSystem.IsWindows()
        ? WindowsTrayRegistration.IsAvailable() : icon?.NativeMenuExporter is not null;

    private static void LaunchBrowser(Uri address)
    {
        using var browser = Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
    }

    private async Task WaitForCompletionAsync()
    {
        if (bootstrap is null) return;
        var result = await DesktopProtocol.SendAsync(bootstrap, "wait", stopped);
        completionReceived = true;
        if (result.State is "quit" or "relaunched") ExitCompanion();
        else if (result.State == "port-conflict")
        {
            SetState("Could not start TDSBLive", false);
            ShowControls("Another app may be using TDSBLive's address. Close the other app or choose a different port. TDSBLive has not stopped or taken control of the other app.");
        }
        else
        {
            SetState("Stopped unexpectedly", false);
            ShowControls("TDSBLive has stopped. Open it again using its shortcut. If a restore failed, your safety copy is retained; check recovery guidance in the editor.");
        }
    }

    private void OpenEditor()
    {
        if (!running || editorUrl is null) return;
        if (!Uri.TryCreate(editorUrl, UriKind.Absolute, out var address) || address.Scheme != "http" || !address.IsLoopback)
        {
            ShowControls("The editor address is invalid. Open the editor from your TDSBLive shortcut.");
            return;
        }
        try { launchBrowser(address); }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            ShowControls("Your browser could not open. Enter this address in your browser: " + address.AbsoluteUri);
        }
    }

    private async Task RequestAsync(string command)
    {
        if (!running || busy || bootstrap is null) return;
        busy = true;
        UpdateCommands();
        try
        {
            if (!await confirmation(command)) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopped);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var reply = await DesktopProtocol.SendAsync(bootstrap, command, timeout.Token);
            if (reply.Accepted) SetState(command == "restart" ? "Restarting TDSBLive…" : "Stopping TDSBLive…", false);
            else ShowControls("TDSBLive is already busy starting or stopping. Please wait.");
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException)
        {
            ShowControls("Desktop controls could not reach TDSBLive. If the editor still works, use Settings to restart or quit.");
        }
        finally { busy = false; UpdateCommands(); }
    }

    private static Task<bool> ConfirmAsync(string command) => new DesktopConfirmation(command).ShowAsync();

    private void SetState(string text, bool isRunning)
    {
        statusLabel = text;
        running = isRunning;
        if (tray is not null) tray.ToolTipText = "TDSBLive — " + (text == RunningLabel ? "Running" : text);
        if (statusText is not null) statusText.Text = text;
        if (controls is not null) controls.Title = running ? RunningLabel : "TDSBLive needs attention";
        UpdateCommands();
    }

    private void UpdateCommands()
    {
        open.IsEnabled = running && !busy;
        restart.IsEnabled = quit.IsEnabled = running && !busy;
        if (openButton is not null) openButton.IsEnabled = open.IsEnabled;
        if (restartButton is not null) restartButton.IsEnabled = restart.IsEnabled;
        if (quitButton is not null) quitButton.IsEnabled = quit.IsEnabled;
    }

    private void ShowControls(string explanation)
    {
        if (controls is null)
        {
            statusText = new TextBlock { FontSize = 20, FontWeight = FontWeight.SemiBold };
            openButton = new Button { Content = "Open editor", MinHeight = 44 };
            restartButton = new Button { Content = "Restart", MinHeight = 44 };
            quitButton = new Button { Content = "Quit", MinHeight = 44 };
            openButton.Click += (_, _) => OpenEditor();
            restartButton.Click += async (_, _) => await RequestAsync("restart");
            quitButton.Click += async (_, _) => await RequestAsync("quit");
            controls = new Window { Title = running ? RunningLabel : "TDSBLive needs attention", Width = 440, MinWidth = 320,
                SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterScreen, Icon = CreateIcon() };
            controls.Closing += (_, eventArgs) =>
            {
                if (exiting) return;
                eventArgs.Cancel = true;
                controls.Hide();
            };
        }
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
        // Detach the existing controls before reusing them in the refreshed panel.
        if (controls.Content is Panel old) old.Children.Clear();
        panel.Children.Add(statusText!);
        panel.Children.Add(new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(openButton!);
        panel.Children.Add(restartButton!);
        panel.Children.Add(quitButton!);
        if (!running)
        {
            var dismiss = new Button { Content = "Close desktop controls", MinHeight = 44 };
            dismiss.Click += (_, _) => ExitCompanion();
            panel.Children.Add(dismiss);
            panel.Children.Add(new TextBlock { Text = "Closing desktop controls does not stop a running TDSBLive backend. If the editor still works, use Settings to quit TDSBLive.", TextWrapping = TextWrapping.Wrap });
        }
        else panel.Children.Add(new TextBlock { Text = "Closing this window keeps TDSBLive running. Choose Quit when you finish streaming.", TextWrapping = TextWrapping.Wrap });
        controls.Content = panel;
        statusText!.Text = statusLabel;
        UpdateCommands();
        if (!controls.IsVisible) controls.Show();
    }

    private void ExitCompanion()
    {
        exiting = true;
        stopping.Cancel();
        lifetime?.Shutdown();
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping.Cancel();
        tray?.Dispose();
        stopping.Dispose();
    }

    private static WindowIcon CreateIcon()
    {
        using var bitmap = new WriteableBitmap(new PixelSize(32, 32), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var frame = bitmap.Lock())
        {
            var bytes = new byte[frame.RowBytes * 32];
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++) DrawIconPixel(bytes, frame.RowBytes, x, y);
            }
            Marshal.Copy(bytes, 0, frame.Address, bytes.Length);
        }
        using var image = new MemoryStream();
        bitmap.Save(image, PngBitmapEncoderOptions.Default);
        image.Position = 0;
        return new WindowIcon(image);
    }

    private static void DrawIconPixel(byte[] bytes, int rowBytes, int x, int y)
    {
        var index = y * rowBytes + x * 4;
        var visible = Math.Pow(x - 15.5, 2) + Math.Pow(y - 15.5, 2) <= 225;
        var letter = y is >= 8 and <= 12 && x is >= 7 and <= 24 || x is >= 13 and <= 18 && y is >= 8 and <= 24;
        bytes[index] = letter ? (byte)255 : (byte)235;
        bytes[index + 1] = letter ? (byte)255 : (byte)99;
        bytes[index + 2] = letter ? (byte)255 : (byte)37;
        bytes[index + 3] = visible ? (byte)255 : (byte)0;
    }
}
