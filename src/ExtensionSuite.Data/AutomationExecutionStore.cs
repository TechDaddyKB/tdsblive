using Microsoft.EntityFrameworkCore;
using ExtensionSuite.Core;
using System.Globalization;
using System.Text.Json;

namespace ExtensionSuite.Data;

public sealed class AutomationExecutionStore(IDbContextFactory<FoundationDbContext> factory, TimeProvider clock)
{
    public async Task<int> EnqueuePlanAsync(Guid eventId, AutomationPlannedAction[] plan, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow().UtcTicks;
        var count = 0;
        foreach (var group in plan.GroupBy(action => action.RuleId))
        {
            if (await db.AutomationExecutions.AnyAsync(item => item.EventId == eventId && item.RuleId == group.Key, ct)) continue;
            var key = "automation-cooldown-" + group.Key.ToString("N");
            var cooldown = await db.Configurations.SingleOrDefaultAsync(item => item.Name == key, ct);
            var until = cooldown is null ? 0 : long.Parse(cooldown.Json, CultureInfo.InvariantCulture);
            var rejected = now < until;
            var settings = group.First();
            if (!rejected && settings.QueuePolicy == "interrupt")
            {
                await db.AutomationExecutions.Where(item => item.QueueGroup == settings.QueueGroup &&
                    (item.State == "queued" || item.State == "moderation-pending" || item.State == "language-review"))
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, "cancelled")
                        .SetProperty(item => item.Detail, "interrupted-before-dispatch")
                        .SetProperty(item => item.Version, item => item.Version + 1), ct);
                await db.AutomationExecutions.Where(item => item.QueueGroup == settings.QueueGroup && item.State == "dispatching")
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.CancelRequested, true), ct);
            }
            var pendingRuns = await db.AutomationExecutions.Where(item => item.QueueGroup == settings.QueueGroup &&
                (item.State == "queued" || item.State == "moderation-pending" || item.State == "language-review" || item.State == "dispatching"))
                .Select(item => new { item.EventId, item.RuleId }).Distinct().CountAsync(ct);
            var reason = rejected ? "rule-cooldown" : settings.QueuePolicy != "interrupt" && pendingRuns >= settings.MaximumQueueLength ? "queue-full" :
                settings.QueuePolicy == "ignore" && pendingRuns > 0 ? "queue-busy" : null;
            rejected = reason is not null;
            var order = 0;
            foreach (var action in group)
            {
                db.AutomationExecutions.Add(new()
                {
                    Id = Guid.CreateVersion7(), EventId = eventId, RuleId = action.RuleId, ActionId = action.Action.Id,
                    ActionOrder = order++, QueueGroup = action.QueueGroup,
                    CreatedAtTicks = now, DueAtTicks = now, State = rejected ? "rejected" : action.State,
                    Detail = reason, Json = JsonSerializer.Serialize(action, EventStore.JsonOptions)
                });
                count++;
            }
            if (!rejected && group.Any(action => action.State is "queued" or "moderation-pending" or "language-review"))
            {
                var expiry = checked(now + TimeSpan.FromSeconds(group.First().CooldownSeconds).Ticks).ToString(CultureInfo.InvariantCulture);
                if (cooldown is null) db.Configurations.Add(new() { Name = key, Json = expiry });
                else cooldown.Json = expiry;
            }
            await db.SaveChangesAsync(ct);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return count;
    }
    public async Task<bool> EnqueueAsync(Guid eventId, Guid ruleId, Guid actionId, string json,
        bool moderationRequired = false, CancellationToken ct = default, string? initialState = null)
    {
        if (eventId == Guid.Empty || ruleId == Guid.Empty || actionId == Guid.Empty || string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Execution requires stable event/rule/action identifiers and a payload.");
        if (initialState is not (null or "queued" or "moderation-pending" or "language-review" or "empty"))
            throw new ArgumentException("Invalid initial execution state.");
        await using var db = await factory.CreateDbContextAsync(ct);
        db.AutomationExecutions.Add(new()
        {
            Id = Guid.CreateVersion7(), EventId = eventId, RuleId = ruleId, ActionId = actionId,
            CreatedAtTicks = clock.GetUtcNow().UtcTicks, DueAtTicks = clock.GetUtcNow().UtcTicks,
            State = initialState ?? (moderationRequired ? "moderation-pending" : "queued"), Json = json
        });
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 }) { return false; }
    }

    public async Task<AutomationExecution[]> ListAsync(int limit = 100, CancellationToken ct = default)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomationExecutions.AsNoTracking().OrderByDescending(item => item.CreatedAtTicks).ThenBy(item => item.Id).Take(limit).ToArrayAsync(ct);
    }

    public async Task<AutomationExecution[]> QueuedAsync(int limit = 100, CancellationToken ct = default)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcTicks;
        return await db.AutomationExecutions.AsNoTracking().Where(item => item.State == "queued" && item.DueAtTicks <= now &&
                !db.AutomationExecutions.Any(earlier => earlier.EventId == item.EventId && earlier.RuleId == item.RuleId &&
                    earlier.ActionOrder < item.ActionOrder && (earlier.State == "moderation-pending" || earlier.State == "language-review" || earlier.State == "dispatching")))
            .OrderBy(item => item.CreatedAtTicks).ThenBy(item => item.EventId).ThenBy(item => item.RuleId).ThenBy(item => item.ActionOrder).Take(limit).ToArrayAsync(ct);
    }

    public async Task<bool> CancellationRequestedAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomationExecutions.Where(item => item.Id == id).Select(item => item.CancelRequested).SingleAsync(ct);
    }

    public async Task<bool> ResolveLanguageAsync(Guid id, int version, string language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Length > 35)
            throw new ArgumentException("A bounded verified language is required.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var receipt = await db.AutomationExecutions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.Version == version && item.State == "language-review", ct);
        if (receipt is null) return false;
        var plan = JsonSerializer.Deserialize<AutomationPlannedAction>(receipt.Json, EventStore.JsonOptions)!;
        if (plan.Action.Speech is not { } settings || !settings.AllowedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
            return false;
        var eventJson = await db.Events.Where(item => item.Id == receipt.EventId && item.Provenance == "Live")
            .Select(item => item.Json).SingleOrDefaultAsync(ct);
        if (eventJson is null) return false;
        var accepted = JsonSerializer.Deserialize<CanonicalEvent>(eventJson, EventStore.JsonOptions)!;
        var prepared = AutomationSpeech.Prepare(settings, accepted, accepted.Automation?.Anonymous ?? true, language);
        var state = prepared.State == "ready" ? "queued" : prepared.State;
        var json = JsonSerializer.Serialize(plan with { State = state, SpeechText = prepared.Text }, EventStore.JsonOptions);
        return await db.AutomationExecutions.Where(item => item.Id == id && item.Version == version && item.State == "language-review")
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, state).SetProperty(item => item.Json, json)
                .SetProperty(item => item.Detail, "language-verified-by-operator")
                .SetProperty(item => item.Version, item => item.Version + 1), ct) == 1;
    }

    public async Task<bool> TransitionAsync(Guid id, int version, string from, string to, CancellationToken ct = default, string? detail = null)
    {
        if (!Allowed(from, to)) throw new ArgumentException("Invalid automation execution transition.");
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomationExecutions.Where(item => item.Id == id && item.Version == version && item.State == from)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, to).SetProperty(item => item.Detail, detail)
                .SetProperty(item => item.Version, item => item.Version + 1), ct) == 1;
    }

    // Call once during host startup, before any dispatcher starts. A dispatch intent
    // survives crashes; its side effect cannot be proven absent and must not be retried.
    public async Task<int> RecoverInterruptedAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomationExecutions.Where(item => item.State == "dispatching")
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, "uncertain")
                .SetProperty(item => item.Version, item => item.Version + 1), ct);
    }

    private static bool Allowed(string from, string to) => (from, to) switch
    {
        ("moderation-pending", "queued" or "rejected") => true,
        ("language-review", "rejected") => true,
        ("queued", "dispatching" or "cancelled" or "rejected" or "failed") => true,
        ("dispatching", "dispatched" or "uncertain" or "failed" or "rejected") => true,
        ("dispatched", "completed" or "uncertain") => true,
        _ => false
    };
}
