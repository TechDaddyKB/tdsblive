using ExtensionSuite.Host;
using ExtensionSuite.Core;

if (await DeveloperCommands.RunAsync(args) is { } utilityExitCode)
{
    Environment.ExitCode = utilityExitCode;
    return;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
var desktopEnabled = DesktopSession.IsEnabled(builder.Configuration);
await using var profileOwner = desktopEnabled ? DesktopProfileOwner.AcquireOrRequestOpen(new ApplicationPaths(builder.Configuration).Root) : null;
if (desktopEnabled && profileOwner is null) return;
builder.AddFoundation();
builder.Services.AddSingleton<IEditorBrowserLauncher, EditorBrowserLauncher>();
var app = builder.Build();
await app.InitializeFoundationAsync();
// Close long-lived browser subscriptions before Kestrel's graceful-shutdown
// wait consumes the deadline needed by consumers and the final DB checkpoint.
app.Lifetime.ApplicationStopping.Register(app.Services.GetRequiredService<EditorEventHub>().BeginShutdown);
app.UseWebSockets();
app.UseMiddleware<RequestSecurity>();
app.UseRateLimiter();
app.UseStaticFiles();
app.MapFoundationEndpoints();
await using var desktop = await DesktopSession.StartAsync(app, builder.Configuration);
if (desktop?.External != true && (desktop?.OpenEditor ?? builder.Configuration.GetValue<bool>("TDSBLive:OpenEditor")))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var configuration = app.Services.GetRequiredService<ApplicationConfiguration>();
        app.Services.GetRequiredService<IEditorBrowserLauncher>().Open(desktop?.EditorServer ?? configuration.Server);
    });
}
var lifecycle = app.Services.GetRequiredService<ApplicationLifecycle>();
var paths = app.Services.GetRequiredService<ApplicationPaths>();
var restore = app.Services.GetRequiredService<RecoveryRestore>();
try
{
    await app.StartAsync();
    desktop?.SetReady();
    profileOwner?.Watch(() =>
    {
        if (desktop?.External == true) desktop.RequestOpen();
        else app.Services.GetRequiredService<IEditorBrowserLauncher>().Open(desktop?.EditorServer ?? app.Services.GetRequiredService<ApplicationConfiguration>().Server);
    });
    await app.WaitForShutdownAsync();
    if (profileOwner is not null) await profileOwner.StopWatchingAsync();
    if (desktop is not null) await desktop.StopMonitoringAsync();
    var operation = lifecycle.Operation;
    var mayRelaunch = true;
    if (operation?.Restore is { } prepared)
    {
        var shutdown = await RecoveryShutdown.DrainAsync(app);
        await using (prepared)
        {
            try { await restore.ApplyAsync(prepared, shutdown); }
            catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or InvalidOperationException or System.Data.Common.DbException)
            {
                mayRelaunch = false;
                Console.Error.WriteLine("Restore failed. The safety copy is retained; reopen TDSBLive and check local recovery information.");
                Environment.ExitCode = 1;
            }
        }
    }
    else await app.DisposeAsync();
    // Release the profile before a replacement process tries to acquire it.
    profileOwner?.Dispose();
    if (mayRelaunch && desktop?.External != true && operation?.Kind is "restart" or "restore" &&
        !ApplicationRelauncher.TryStart(Environment.ProcessPath!, typeof(Program).Assembly.Location, paths.Root,
            openEditor: builder.Configuration.GetValue<bool>("TDSBLive:OpenEditor"), launchArguments: args))
    {
        Console.Error.WriteLine("TDSBLive stopped. Open it again using its shortcut to continue.");
        Environment.ExitCode = 1;
    }
    if (desktop is not null)
        await desktop.CompleteAsync(!mayRelaunch || Environment.ExitCode != 0 ? "failed" :
            operation?.Kind is "restart" or "restore" ? desktop.External ? "restart-ready" : "relaunched" :
            operation?.Kind == "quit" ? "quit" : "stopped");
}
catch (IOException)
{
    Console.Error.WriteLine("TDSBLive could not bind its configured HTTP address. Check for a port conflict and change server.port in configuration.json.");
    Environment.ExitCode = 1;
}
finally { await app.DisposeAsync(); }
