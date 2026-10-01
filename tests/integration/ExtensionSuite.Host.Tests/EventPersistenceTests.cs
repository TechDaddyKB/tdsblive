using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class EventPersistenceTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "tdsblive-tests", Guid.NewGuid().ToString());
    private PooledDbContextFactory<FoundationDbContext> factory = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(directory);
        factory = NewFactory();
        await using var db = factory.CreateDbContext();
        await DatabaseLifecycle.InitializeAsync(db);
    }

    private PooledDbContextFactory<FoundationDbContext> NewFactory() => new(new DbContextOptionsBuilder<FoundationDbContext>()
        .UseSqlite($"Data Source={Path.Combine(directory, "foundation.db")};Foreign Keys=True;Pooling=False").Options);

    private static CanonicalEvent Event(EventProvenance provenance = EventProvenance.Live) => new()
    {
        Source = "internal", Platform = "system", Type = "chat.message", NativeType = "test.chat",
        DedupeKey = "same-message", OccurredAt = DateTimeOffset.UtcNow, Provenance = provenance,
        Raw = JsonNode.Parse("""{"token":"synthetic-private-value","unknown":true}""")!.AsObject()
    };

    [Fact]
    public async Task MigrationRestartDedupeAndCheckpointAreDurable()
    {
        var item = Event();
        var store = new EventStore(factory);
        Assert.True(await store.AcceptAsync(item, "poll-1"));
        var reopened = NewFactory();
        await using var db = reopened.CreateDbContext();
        await DatabaseLifecycle.InitializeAsync(db);
        Assert.False(await new EventStore(reopened).AcceptAsync(item with { Id = Guid.CreateVersion7() }, "poll-2"));
        Assert.Single(await db.Events.ToArrayAsync());
        Assert.Single(await db.Outbox.ToArrayAsync());
        Assert.Equal("poll-2", (await db.Checkpoints.SingleAsync()).Value);
        Assert.DoesNotContain("synthetic-private-value", (await db.Events.SingleAsync()).Json);
        Assert.True((await store.ReadAsync())[0].Raw!["unknown"]!.GetValue<bool>());
        await DatabaseLifecycle.CheckpointAsync(db);
    }

    [Fact]
    public async Task TestPersistenceIsExplicitAndNeverSharesLiveDedupeNamespace()
    {
        var store = new EventStore(factory);
        Assert.False(await store.AcceptAsync(Event(EventProvenance.Simulation), "ignored"));
        Assert.Empty(await store.ReadAsync(provenance: EventProvenance.Simulation));
        Assert.True(await store.AcceptAsync(Event(), "live"));
        Assert.True(await store.AcceptAsync(Event(EventProvenance.Simulation), "sim", persistTest: true));
        Assert.True(await store.AcceptAsync(Event(EventProvenance.Replay), "replay", persistTest: true, retainRaw: false));
        Assert.Single(await store.ReadAsync());
        Assert.Null((await store.ReadAsync(provenance: EventProvenance.Replay))[0].Raw);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ReadAsync(501));
    }

    [Fact]
    public async Task CheckpointFailureRollsBackEventAndOutboxTogether()
    {
        await using var db = factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_checkpoint BEFORE INSERT ON Checkpoints
            BEGIN SELECT RAISE(ABORT, 'synthetic checkpoint failure'); END;
            """);
        await Assert.ThrowsAsync<DbUpdateException>(() => new EventStore(factory).AcceptAsync(Event(), "failed"));
        Assert.Empty(await db.Events.AsNoTracking().ToArrayAsync());
        Assert.Empty(await db.Outbox.AsNoTracking().ToArrayAsync());
        Assert.Empty(await db.Checkpoints.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task UncommittedTransactionLeavesNoEventOrCheckpointAfterReopen()
    {
        await using (var db = factory.CreateDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Checkpoints.Add(new SourceCheckpoint { Source = "failed", Provenance = "Live", Value = "uncommitted" });
            await db.SaveChangesAsync();
        }
        await using var reopened = NewFactory().CreateDbContext();
        Assert.Empty(await reopened.Checkpoints.ToArrayAsync());
        Assert.Empty(await reopened.Events.ToArrayAsync());
        Assert.Empty(await reopened.Outbox.ToArrayAsync());
    }

    public Task DisposeAsync()
    {
        Directory.Delete(directory, recursive: true);
        return Task.CompletedTask;
    }
}
