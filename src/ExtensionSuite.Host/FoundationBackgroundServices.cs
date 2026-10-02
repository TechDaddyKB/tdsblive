using System.Collections.Concurrent;
using ExtensionSuite.Data;

namespace ExtensionSuite.Host;

public sealed class FoundationLogWriter(FoundationStateStore state) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await state.RunLogsAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { /* Expected shutdown. */ }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        state.FlushLogs(cancellationToken);
    }
}

public sealed record IntegrationHealth(string State, string? ErrorType = null);
public sealed class IntegrationHealthRegistry
{
    private readonly ConcurrentDictionary<string, IntegrationHealth> states = new();
    public void Set(string name, IntegrationHealth state) => states[name] = state;
    public IReadOnlyDictionary<string, IntegrationHealth> Snapshot() => new Dictionary<string, IntegrationHealth>(states);
}

public interface IIsolatedIntegration
{
    string Name { get; }
    Task RunAsync(CancellationToken cancellationToken);
}

public sealed class IsolatedIntegrations(IEnumerable<IIsolatedIntegration> integrations,
    IntegrationHealthRegistry health, ILogger<IsolatedIntegrations> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(integrations.Select(integration => RunAsync(integration, stoppingToken)));

    private async Task RunAsync(IIsolatedIntegration integration, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            health.Set(integration.Name, new("running"));
            try
            {
                await integration.RunAsync(stoppingToken);
                health.Set(integration.Name, new("stopped"));
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                health.Set(integration.Name, new("degraded", error.GetType().Name));
                logger.LogWarning("Integration {Name} failed with {ErrorType}; retrying independently.", integration.Name, error.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}

public sealed class DurableOutboxWorker(EventStore store, EditorEventHub hub, ILogger<DurableOutboxWorker> logger) : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await hub.ShutdownAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var item in await store.PendingAsync(stoppingToken))
                {
                    hub.Publish(item);
                    await store.MarkDeliveredAsync(item.Id, stoppingToken);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogWarning("Outbox delivery unavailable ({ErrorType}); durable rows remain pending.", error.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            }
        }
    }
}
