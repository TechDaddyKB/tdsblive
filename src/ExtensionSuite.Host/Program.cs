using ExtensionSuite.Host;
using ExtensionSuite.Core;

if (await DeveloperCommands.RunAsync(args) is { } utilityExitCode)
{
    Environment.ExitCode = utilityExitCode;
    return;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
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
if (builder.Configuration.GetValue<bool>("TDSBLive:OpenEditor"))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var configuration = app.Services.GetRequiredService<ApplicationConfiguration>();
        app.Services.GetRequiredService<IEditorBrowserLauncher>().Open(configuration.Server);
    });
}
var lifecycle = app.Services.GetRequiredService<ApplicationLifecycle>();
var paths = app.Services.GetRequiredService<ApplicationPaths>();
var restore = app.Services.GetRequiredService<RecoveryRestore>();
try
{
    await app.StartAsync();
    await app.WaitForShutdownAsync();
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
    if (mayRelaunch && operation?.Kind is "restart" or "restore" &&
        !ApplicationRelauncher.TryStart(Environment.ProcessPath!, typeof(Program).Assembly.Location, paths.Root,
            openEditor: builder.Configuration.GetValue<bool>("TDSBLive:OpenEditor")))
    {
        Console.Error.WriteLine("TDSBLive stopped. Open it again using its shortcut to continue.");
        Environment.ExitCode = 1;
    }
}
catch (IOException)
{
    Console.Error.WriteLine("TDSBLive could not bind its configured HTTP address. Check for a port conflict and change server.port in configuration.json.");
    Environment.ExitCode = 1;
}
finally { await app.DisposeAsync(); }
