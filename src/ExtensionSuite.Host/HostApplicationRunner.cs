using ExtensionSuite.Core;

namespace ExtensionSuite.Host;

internal static class HostApplicationRunner
{
    public static async Task<int> RunAsync(WebApplication app, IConfiguration configuration, string[] arguments,
        DesktopSession? desktop, DesktopProfileOwner? profileOwner)
    {
        var started = false;
        try
        {
            await app.StartAsync();
            started = true;
            desktop?.SetReady();
            profileOwner?.Watch(() => OpenEditor(app, desktop));
            await app.WaitForShutdownAsync();
            if (profileOwner is not null) await profileOwner.StopWatchingAsync();
            if (desktop is not null) await desktop.StopMonitoringAsync();
            return await FinishAsync(app, configuration, arguments, desktop, profileOwner);
        }
        catch (IOException)
        {
            await Console.Error.WriteLineAsync(started ?
                "TDSBLive could not close its local data safely. Reopen it and check local recovery information." :
                "TDSBLive could not bind its configured HTTP address. Check for a port conflict and change server.port in configuration.json.");
            if (desktop is not null)
            {
                await desktop.StopMonitoringAsync();
                await desktop.CompleteAsync(started ? "failed" : "port-conflict");
            }
            return 1;
        }
        finally { await app.DisposeAsync(); }
    }

    private static void OpenEditor(WebApplication app, DesktopSession? desktop)
    {
        if (desktop?.External == true) { desktop.RequestOpen(); return; }
        var server = desktop?.EditorServer ?? app.Services.GetRequiredService<ApplicationConfiguration>().Server;
        app.Services.GetRequiredService<IEditorBrowserLauncher>().Open(server);
    }

    private static async Task<int> FinishAsync(WebApplication app, IConfiguration configuration, string[] arguments,
        DesktopSession? desktop, DesktopProfileOwner? profileOwner)
    {
        var operation = app.Services.GetRequiredService<ApplicationLifecycle>().Operation;
        var paths = app.Services.GetRequiredService<ApplicationPaths>();
        var restore = app.Services.GetRequiredService<RecoveryRestore>();
        var safe = await CloseAndRestoreAsync(app, operation, restore);
        // Recovery replaces the profile while ownership is held; release only
        // after that work, and before a replacement process can start.
        profileOwner?.ReleaseOwnership();
        if (safe && desktop?.External != true && operation?.Kind is "restart" or "restore")
        {
            safe = ApplicationRelauncher.TryStart(Environment.ProcessPath!, typeof(HostApplicationRunner).Assembly.Location,
                paths.Root, openEditor: configuration.GetValue<bool>("TDSBLive:OpenEditor"), launchArguments: arguments);
            if (!safe) await Console.Error.WriteLineAsync("TDSBLive stopped. Open it again using its shortcut to continue.");
        }
        if (desktop is not null) await desktop.CompleteAsync(CompletionState(safe, operation?.Kind, desktop.External));
        return safe ? 0 : 1;
    }

    private static async Task<bool> CloseAndRestoreAsync(WebApplication app, ApplicationOperation? operation, RecoveryRestore restore)
    {
        if (operation?.Restore is not { } prepared) { await app.DisposeAsync(); return true; }
        var shutdown = await RecoveryShutdown.DrainAsync(app);
        await using (prepared)
        {
            try { await restore.ApplyAsync(prepared, shutdown); return true; }
            catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or InvalidOperationException or System.Data.Common.DbException)
            {
                await Console.Error.WriteLineAsync("Restore failed. The safety copy is retained; reopen TDSBLive and check local recovery information.");
                return false;
            }
        }
    }

    internal static string CompletionState(bool safe, string? operation, bool external)
    {
        if (!safe) return "failed";
        if (operation is "restart" or "restore") return external ? "restart-ready" : "relaunched";
        return operation == "quit" ? "quit" : "stopped";
    }
}
