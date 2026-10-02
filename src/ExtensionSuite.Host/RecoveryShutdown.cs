namespace ExtensionSuite.Host;

// A restore permit can only be produced by stopping and disposing the actual host.
// This closes consumers, request processing, logger handles and database services.
public sealed class RecoveryShutdown
{
    public string DataDirectory { get; }
    private RecoveryShutdown(string directory) => DataDirectory = directory;
    public static async Task<RecoveryShutdown> DrainAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        var directory = app.Services.GetRequiredService<ApplicationPaths>().Root;
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        await app.StopAsync(cancellationToken);
        if (!lifetime.ApplicationStopped.IsCancellationRequested) throw new InvalidOperationException("Application shutdown is incomplete.");
        await app.DisposeAsync();
        return new(directory);
    }
}
