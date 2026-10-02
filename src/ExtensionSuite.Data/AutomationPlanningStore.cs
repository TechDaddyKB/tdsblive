using System.Text.Json;
using ExtensionSuite.Core;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class AutomationPlanningStore(IDbContextFactory<FoundationDbContext> factory,
    AutomationRuleStore rules, AutomationExecutionStore executions)
{
    public async Task<AutomationPlannedAction[]> PreviewAsync(CanonicalEvent item,
        bool? anonymous = null, string? language = null, CancellationToken ct = default) =>
        AutomationPlanner.Evaluate(await rules.ListAsync(ct), item, anonymous, language);

    public async Task<int> PlanAcceptedAsync(Guid eventId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var json = await db.Events.Where(item => item.Id == eventId && item.Provenance == "Live")
            .Select(item => item.Json).SingleOrDefaultAsync(ct);
        if (json is null) return 0;
        var item = JsonSerializer.Deserialize<CanonicalEvent>(json, EventStore.JsonOptions)!;
        var planned = await PreviewAsync(item, ct: ct);
        return await executions.EnqueuePlanAsync(item.Id, planned, ct);
    }
}
