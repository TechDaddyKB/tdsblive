using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using ExtensionSuite.Core;
using ExtensionSuite.DesktopControl;

namespace ExtensionSuite.Host;

public sealed class DesktopSession : IAsyncDisposable
{
    private readonly DesktopControlServer server;
    private Process? companion;
    private readonly CancellationTokenSource monitorStopping = new();
    private Task? monitoring;
    public bool External { get; }
    public bool OpenEditor { get; }
    public ServerConfiguration EditorServer { get; private init; } = new();
    private DesktopSession(DesktopControlServer server, bool external, bool openEditor)
        => (this.server, External, OpenEditor) = (server, external, openEditor);

    public static bool IsEnabled(IConfiguration configuration) => configuration["TDSBLive:DesktopMode"] switch
    {
        "off" => false,
        "external" => true,
        null or "automatic" => OperatingSystem.IsWindows() && File.Exists(Path.Combine(AppContext.BaseDirectory, "desktop", "TDSBLive.Desktop.exe")),
        _ => throw new ArgumentException("Desktop mode must be automatic, external or off.")
    };

    public static ServerConfiguration LocalEditorServer(ServerConfiguration configured)
    {
        configured.Validate();
        return configured.Host is "0.0.0.0" or "::" || IPAddress.IsLoopback(IPAddress.Parse(configured.Host))
            ? configured : configured with { Host = "127.0.0.1" };
    }

    public static async Task<DesktopSession?> StartAsync(WebApplication app, IConfiguration configuration)
    {
        var mode = configuration["TDSBLive:DesktopMode"] ?? "automatic";
        if (mode is not ("automatic" or "external" or "off")) throw new ArgumentException("Desktop mode must be automatic, external or off.");
        var companionPath = Path.Combine(AppContext.BaseDirectory, "desktop", "TDSBLive.Desktop.exe");
        var external = mode == "external";
        if (mode == "off" || !external && (!OperatingSystem.IsWindows() || !File.Exists(companionPath))) return null;
        var sessionToken = DesktopProtocol.NewSessionToken();
        if (external)
        {
            if (!Console.IsInputRedirected) throw new ArgumentException("External desktop mode needs a launcher pipe.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var bootstrap = await DesktopProtocol.ReadAsync<DesktopBootstrap>(Console.OpenStandardInput(), timeout.Token);
            if (!DesktopProtocol.ValidSessionToken(bootstrap.SessionToken) || bootstrap.Port != 0)
                throw new ArgumentException("Invalid external desktop bootstrap.");
            sessionToken = bootstrap.SessionToken;
        }
        var configured = app.Services.GetRequiredService<ApplicationConfiguration>().Server;
        var editorServer = LocalEditorServer(configured);
        // An explicit LAN interface does not also listen on loopback. Add a local
        // editor binding without changing the saved LAN address or its authority.
        if (configured.Host != editorServer.Host)
            app.Urls.Add($"http://127.0.0.1:{configured.Port}");
        var server = new DesktopControlServer(app.Services.GetRequiredService<ApplicationLifecycle>(), app.Lifetime,
            EditorBrowserLauncher.EditorUri(editorServer).AbsoluteUri, sessionToken);
        var bootstrapInfo = server.Start();
        var session = new DesktopSession(server, external, configuration.GetValue("TDSBLive:OpenEditor", true)) { EditorServer = editorServer };
        session.monitoring = session.MonitorAsync(app.Services.GetRequiredService<IntegrationHealthRegistry>(),
            app.Services.GetRequiredService<ILogger<DesktopSession>>());
        if (external)
        {
            // Port is not a capability. The session credential is never printed.
            Console.WriteLine(DesktopProtocol.ReadyPrefix + bootstrapInfo.Port);
            Console.Out.Flush();
        }
        else
        {
            var info = new ProcessStartInfo(companionPath)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
            };
            info.ArgumentList.Add("--attach");
            try
            {
                session.companion = Process.Start(info) ?? throw new InvalidOperationException();
                await DesktopProtocol.WriteAsync(session.companion.StandardInput.BaseStream, bootstrapInfo, CancellationToken.None);
                session.companion.StandardInput.Close();
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException or IOException)
            {
                Console.Error.WriteLine("Desktop controls could not start. TDSBLive is still running; use Settings in the editor to restart or quit.");
            }
        }
        return session;
    }

    public void SetReady() => server.SetReady();
    public void RequestOpen() => server.RequestOpen();
    public Task CompleteAsync(string state) => server.CompleteAsync(state);
    private async Task MonitorAsync(IntegrationHealthRegistry health, ILogger<DesktopSession> logger)
    {
        var started = DateTimeOffset.UtcNow;
        var degraded = false;
        try
        {
            while (!monitorStopping.IsCancellationRequested)
            {
                var missing = DateTimeOffset.UtcNow - (server.LastContact ?? started) > TimeSpan.FromSeconds(12);
                health.Set("Desktop controls", new(missing ? "degraded" : server.LastContact is null ? "starting" : "running"));
                if (missing && !degraded)
                    logger.LogWarning("Desktop controls are unavailable. TDSBLive remains running; use Settings in the editor to restart or quit, or open the TDSBLive shortcut again.");
                degraded = missing;
                await Task.Delay(TimeSpan.FromSeconds(3), monitorStopping.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally { health.Set("Desktop controls", new("stopped")); }
    }

    public async Task StopMonitoringAsync()
    {
        monitorStopping.Cancel();
        if (monitoring is not null) await monitoring;
    }

    public async ValueTask DisposeAsync()
    {
        await StopMonitoringAsync();
        await server.DisposeAsync();
        companion?.Dispose(); // Never kill the companion or the streaming backend.
        monitorStopping.Dispose();
    }
}
