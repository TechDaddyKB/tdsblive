using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    internal int ControlPort => server.Port;
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

    public static Task<DesktopSession?> StartAsync(WebApplication app, IConfiguration configuration) =>
        StartAsync(app, configuration, Console.OpenStandardInput(), Console.IsInputRedirected, Console.Out, IsOutputPipe());

    internal static async Task<DesktopSession?> StartAsync(WebApplication app, IConfiguration configuration,
        Stream bootstrapInput, bool redirected, TextWriter? bootstrapOutput = null, bool outputIsPipe = false)
    {
        var mode = configuration["TDSBLive:DesktopMode"] ?? "automatic";
        if (mode is not ("automatic" or "external" or "off")) throw new ArgumentException("Desktop mode must be automatic, external or off.");
        var companionPath = Path.Combine(AppContext.BaseDirectory, "desktop", "TDSBLive.Desktop.exe");
        var external = mode == "external";
        var bootstrapMode = configuration["TDSBLive:DesktopBootstrap"] ?? "input";
        if (bootstrapMode is not ("input" or "output") ||
            bootstrapMode == "output" && (!external || !outputIsPipe || bootstrapOutput is null))
            throw new ArgumentException("Output desktop bootstrap requires external mode and a private output pipe.");
        if (mode == "off" || !external && (!OperatingSystem.IsWindows() || !File.Exists(companionPath))) return null;
        var sessionToken = external && bootstrapMode == "input"
            ? await ReadExternalTokenAsync(bootstrapInput, redirected) : DesktopProtocol.NewSessionToken();
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
            if (bootstrapMode == "output")
            {
                // UMU replaces stdin with /dev/null. Deliver the credential only
                // through the private output pipe, never logs or configuration.
                await bootstrapOutput!.WriteLineAsync(DesktopProtocol.BootstrapPrefix + JsonSerializer.Serialize(bootstrapInfo));
                await bootstrapOutput.FlushAsync();
            }
            else
            {
                // Port is not a capability. The input-mode credential is never printed.
                await Console.Out.WriteLineAsync(DesktopProtocol.ReadyPrefix + bootstrapInfo.Port);
                await Console.Out.FlushAsync();
            }
        }
        else await session.StartCompanionAsync(companionPath, bootstrapInfo);
        return session;
    }

    private static bool IsOutputPipe()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var handle = NativeOutput.GetStdHandle(-11);
        var type = NativeOutput.GetFileType(handle);
        if (type == 3) return true; // Native Windows anonymous/named pipe.
        if (type != 2 || !Console.IsOutputRedirected) return false;
        var wine = NativeOutput.GetProcAddress(NativeOutput.GetModuleHandle("ntdll.dll"), "wine_get_version") != IntPtr.Zero;
        if (!wine) return false;
        var status = NativeOutput.NtQueryVolumeInformationFile(handle, out _, out var device, 8, 4);
        return IsWineOutputPipe(type, Console.IsOutputRedirected, wine, status, device.Type);
    }

    // Wine get_device_info maps Unix FIFOs to FILE_DEVICE_UNKNOWN, whereas
    // regular files are disk devices, /dev/null is FILE_DEVICE_NULL and terminal
    // handles are recognized by Console.IsOutputRedirected. Native Windows never
    // uses this compatibility case. A caller must also explicitly request the
    // external output bootstrap; ordinary host output contains no credential.
    internal static bool IsWineOutputPipe(uint fileType, bool redirected, bool wine, int status, uint deviceType)
        => fileType == 2 && redirected && wine && status == 0 && deviceType == 0x22;

    private static class NativeOutput
    {
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetStdHandle(int standardHandle);
        [DllImport("kernel32.dll")]
        internal static extern uint GetFileType(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string moduleName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("ntdll.dll")]
        internal static extern int NtQueryVolumeInformationFile(IntPtr handle, out IoStatus io,
            out DeviceInformation device, uint length, int informationClass);
        [StructLayout(LayoutKind.Sequential)]
        internal struct IoStatus { internal IntPtr Status; internal UIntPtr Information; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct DeviceInformation { internal uint Type; internal uint Characteristics; }
    }

    private static async Task<string> ReadExternalTokenAsync(Stream input, bool redirected)
    {
        if (!redirected) throw new ArgumentException("External desktop mode needs a launcher pipe.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var bootstrap = await DesktopProtocol.ReadAsync<DesktopBootstrap>(input, timeout.Token);
        if (!DesktopProtocol.ValidSessionToken(bootstrap.SessionToken) || bootstrap.Port != 0)
            throw new ArgumentException("Invalid external desktop bootstrap.");
        return bootstrap.SessionToken;
    }

    private async Task StartCompanionAsync(string companionPath, DesktopBootstrap bootstrapInfo)
    {
        var info = new ProcessStartInfo(companionPath)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
        };
        info.ArgumentList.Add("--attach");
        try
        {
            companion = Process.Start(info) ?? throw new InvalidOperationException();
            await DesktopProtocol.WriteAsync(companion.StandardInput.BaseStream, bootstrapInfo, CancellationToken.None);
            companion.StandardInput.Close();
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or IOException)
        {
            await Console.Error.WriteLineAsync("Desktop controls could not start. TDSBLive is still running; use Settings in the editor to restart or quit.");
        }
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
                var connectedState = server.LastContact is null ? "starting" : "running";
                health.Set("Desktop controls", new(missing ? "degraded" : connectedState));
                if (missing && !degraded)
                    logger.LogWarning("Desktop controls are unavailable. TDSBLive remains running; use Settings in the editor to restart or quit, or open the TDSBLive shortcut again.");
                degraded = missing;
                await Task.Delay(TimeSpan.FromSeconds(3), monitorStopping.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Monitoring is canceled before the host's services are disposed.
        }
        finally { health.Set("Desktop controls", new("stopped")); }
    }

    public async Task StopMonitoringAsync()
    {
        await monitorStopping.CancelAsync();
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
