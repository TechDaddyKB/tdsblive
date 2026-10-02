using ExtensionSuite.Finance;

namespace ExtensionSuite.Host;

public sealed class FinancialHostedIntegration(FinancialProjection projection) : IIsolatedIntegration
{
    public string Name => "financial-ledger";
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await projection.ProcessBatchAsync(cancellationToken) == 0)
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }
}
