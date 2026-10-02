using ExtensionSuite.Host;
using ExtensionSuite.Core;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.AddFoundation();
builder.Services.AddSingleton<IEditorBrowserLauncher, EditorBrowserLauncher>();
var app = builder.Build();
await app.InitializeFoundationAsync();
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
try { await app.RunAsync(); }
catch (IOException)
{
    Console.Error.WriteLine("TDSBLive could not bind its configured HTTP address. Check for a port conflict and change server.port in configuration.json.");
    Environment.ExitCode = 1;
}
