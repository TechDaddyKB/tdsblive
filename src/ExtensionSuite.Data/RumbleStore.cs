using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Rumble;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class RumbleStore(IDbContextFactory<FoundationDbContext> factory, SensitiveValues sensitive) : IRumbleStore
{
    public async Task<RumbleState> LoadAsync(string credentialContext, EventProvenance provenance, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = await db.RumbleStates.FindAsync([credentialContext, provenance.ToString()], cancellationToken);
        return row is null ? new() : JsonSerializer.Deserialize<RumbleState>(row.Json, EventStore.JsonOptions)!;
    }

    public async Task<CanonicalEvent[]> CommitAsync(string credentialContext, EventProvenance provenance, RumbleBatch batch,
        bool retainRaw, bool forwardTriggers, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var accepted = new List<CanonicalEvent>();
        var sourceScope = provenance.ToString();
        foreach (var item in batch.Events)
        {
            item.Validate();
            if (item.Provenance != provenance || item.Source != "rumble") throw new ArgumentException("Snapshot provenance mismatch.");
            if (await db.Events.AnyAsync(row => row.Source == "rumble" && row.Provenance == sourceScope && row.DedupeKey == item.DedupeKey, cancellationToken)) continue;
            var clean = JsonSerializer.SerializeToNode(item with { Raw = retainRaw ? item.Raw : null }, EventStore.JsonOptions);
            var json = CredentialRedactor.Json(clean, sensitive.Snapshot())!.ToJsonString(EventStore.JsonOptions);
            db.Events.Add(new() { Id = item.Id, Source = "rumble", Provenance = sourceScope, DedupeKey = item.DedupeKey,
                Type = item.Type, OccurredAtTicks = item.OccurredAt.UtcTicks, Json = json });
            db.Outbox.Add(new() { EventId = item.Id, CreatedAtTicks = item.ReceivedAt.UtcTicks });
            if (forwardTriggers && provenance == EventProvenance.Live && item.Type != "rumble.subscription.candidate")
                db.RumbleDeliveries.Add(new() { EventId = item.Id, CreatedAtTicks = item.ReceivedAt.UtcTicks, State = "pending" });
            accepted.Add(JsonSerializer.Deserialize<CanonicalEvent>(json, EventStore.JsonOptions)!);
        }
        var row = await db.RumbleStates.FindAsync([credentialContext, sourceScope], cancellationToken);
        var stateJson = JsonSerializer.Serialize(batch.State, EventStore.JsonOptions);
        if (row is null) db.RumbleStates.Add(new() { Context = credentialContext, Provenance = sourceScope, Json = stateJson });
        else row.Json = stateJson;
        var checkpoint = await db.Checkpoints.FindAsync(["rumble", sourceScope], cancellationToken);
        var value = batch.State.PollSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (checkpoint is null) db.Checkpoints.Add(new() { Source = "rumble", Provenance = sourceScope, Value = value });
        else checkpoint.Value = value;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return accepted.ToArray();
    }

    public async Task RecoverUncertainAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.RumbleDeliveries.Where(item => item.State == "dispatching").ExecuteUpdateAsync(setters => setters.SetProperty(item => item.State, "uncertain"), cancellationToken);
    }
    public async Task<CanonicalEvent[]> PendingTriggersAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await (from delivery in db.RumbleDeliveries.AsNoTracking()
                          join item in db.Events.AsNoTracking() on delivery.EventId equals item.Id
                          where delivery.State == "pending" orderby delivery.CreatedAtTicks select item.Json).Take(64).ToArrayAsync(cancellationToken);
        return rows.Select(json => JsonSerializer.Deserialize<CanonicalEvent>(json, EventStore.JsonOptions)!).ToArray();
    }
    public async Task<bool> ClaimAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.RumbleDeliveries.Where(item => item.EventId == eventId && item.State == "pending")
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.State, "dispatching"), cancellationToken) == 1;
    }
    public async Task FinishAsync(Guid eventId, string outcome, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.RumbleDeliveries.Where(item => item.EventId == eventId && item.State == "dispatching")
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.State, outcome), cancellationToken);
    }
    public async Task<Dictionary<string, int>> DeliveryStatusAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.RumbleDeliveries.AsNoTracking().GroupBy(item => item.State).Select(group => new { State = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.State, item => item.Count, cancellationToken);
    }
}
