var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/status", () => new { name = "TDSBLive", httpSupported = true });
app.Run("http://127.0.0.1:17474");

public partial class Program;
