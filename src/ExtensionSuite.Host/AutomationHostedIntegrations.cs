using ExtensionSuite.Data;

namespace ExtensionSuite.Host;

public sealed class AutomationReaderIntegration(AutomationEventReader reader) : IIsolatedIntegration
{
    public string Name => "automation-events";
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
            if (await reader.ProcessAsync(ct) == 0) await Task.Delay(200, ct);
    }
}

public sealed class AutomationDispatchIntegration(AutomationDispatcher dispatcher) : IIsolatedIntegration
{
    public string Name => "automation-dispatch";
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await dispatcher.PumpAsync(ct);
                await Task.Delay(100, ct);
            }
        }
        finally
        {
            if (ct.IsCancellationRequested) await dispatcher.WaitForIdleAsync(ct);
        }
    }
}

public sealed class AutomationTimerIntegration(AutomationTemporaryActions temporary) : IIsolatedIntegration
{
    public string Name => "automation-timers";
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await temporary.TickAsync(ct);
            await Task.Delay(100, ct);
        }
    }
}
