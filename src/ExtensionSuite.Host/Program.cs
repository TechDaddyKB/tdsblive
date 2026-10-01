using ExtensionSuite.Host;

var builder = WebApplication.CreateBuilder(args);
builder.AddFoundation();
await using var app = builder.Build();
await app.InitializeFoundationAsync();
app.UseWebSockets();
app.UseMiddleware<RequestSecurity>();
app.UseRateLimiter();
app.MapFoundationEndpoints();
try { await app.RunAsync(); }
catch (IOException)
{
    Console.Error.WriteLine("TDSBLive could not bind its configured HTTP address. Check for a port conflict and change server.port in configuration.json.");
    Environment.ExitCode = 1;
}
