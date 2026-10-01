using System.Text.Json;
using System.Text.Json.Serialization;
using ExtensionSuite.Core;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class EventStore(IDbContextFactory<FoundationDbContext> factory, SensitiveValues? sensitive = null)
{
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<EventProvenance>(JsonNamingPolicy.CamelCase));
        return options;
    }

    public async Task<bool> AcceptAsync(CanonicalEvent item, string checkpoint,
        bool persistTest = false, bool retainRaw = true, CancellationToken cancellationToken = default)
    {
        item.Validate();
        if (item.Provenance != EventProvenance.Live && !persistTest) return false;
        var sanitized = item with
        {
            Raw = retainRaw ? CredentialRedactor.Json(item.Raw, sensitive?.Snapshot()) as System.Text.Json.Nodes.JsonObject : null,
            Message = item.Message is null ? null : new EventMessage(CredentialRedactor.Text(item.Message.Text ?? "", sensitive?.Snapshot())),
            User = item.User is null ? null : item.User with { AvatarUrl = item.User.AvatarUrl is null ? null : CredentialRedactor.Text(item.User.AvatarUrl, sensitive?.Snapshot()) }
        };
        var provenance = item.Provenance.ToString();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var exists = await db.Events.AnyAsync(record => record.Source == item.Source &&
            record.Provenance == provenance && record.DedupeKey == item.DedupeKey, cancellationToken);
        if (!exists)
        {
            db.Events.Add(new StoredEvent { Id = item.Id, Source = item.Source, Provenance = provenance,
                DedupeKey = item.DedupeKey, Type = item.Type, OccurredAtTicks = item.OccurredAt.UtcTicks,
                Json = CredentialRedactor.Json(JsonSerializer.SerializeToNode(sanitized, JsonOptions), sensitive?.Snapshot())!.ToJsonString(JsonOptions) });
            db.Outbox.Add(new OutboxEntry { EventId = item.Id, CreatedAtTicks = item.ReceivedAt.UtcTicks });
        }
        var position = await db.Checkpoints.FindAsync([item.Source, provenance], cancellationToken);
        if (position is null) db.Checkpoints.Add(new SourceCheckpoint { Source = item.Source, Provenance = provenance, Value = checkpoint });
        else position.Value = checkpoint;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return !exists;
    }

    public async Task<CanonicalEvent[]> ReadAsync(int limit = 100, EventProvenance provenance = EventProvenance.Live,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var scope = provenance.ToString();
        var rows = await db.Events.AsNoTracking().Where(item => item.Provenance == scope)
            .OrderByDescending(item => item.OccurredAtTicks).Take(limit).Select(item => item.Json).ToArrayAsync(cancellationToken);
        return rows.Select(json => JsonSerializer.Deserialize<CanonicalEvent>(json, JsonOptions)!).ToArray();
    }
}
