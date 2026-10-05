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
Environment.ExitCode = await HostApplicationRunner.RunAsync(app, builder.Configuration, args, desktop, profileOwner);
