using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

/// <summary>New live events enter the inbox in their acceptance transaction.
/// Historical events are not backfilled. Explicit sequences remain stable
/// across SQLite maintenance, unlike implicit row identifiers.</summary>
public sealed class AutomationEventReader(IDbContextFactory<FoundationDbContext> factory,
    AutomationPlanningStore planner) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<int> ProcessAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var batch = await db.AutomationInbox.Where(item => !item.Processed).OrderBy(item => item.Sequence).Take(100).ToArrayAsync(ct);
            foreach (var pointer in batch)
            {
                await planner.PlanAcceptedAsync(pointer.EventId, ct);
                pointer.Processed = true;
                await db.SaveChangesAsync(ct);
            }
            return batch.Length;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
