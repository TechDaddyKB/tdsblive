using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class FoundationDbContext(DbContextOptions<FoundationDbContext> options) : DbContext(options)
{
    public DbSet<StoredEvent> Events => Set<StoredEvent>();
    public DbSet<SourceCheckpoint> Checkpoints => Set<SourceCheckpoint>();
    public DbSet<OutboxEntry> Outbox => Set<OutboxEntry>();
    public DbSet<StoredConfiguration> Configurations => Set<StoredConfiguration>();
    public DbSet<StoredLog> Logs => Set<StoredLog>();
    public DbSet<RumbleSnapshotState> RumbleStates => Set<RumbleSnapshotState>();
    public DbSet<RumbleTriggerDelivery> RumbleDeliveries => Set<RumbleTriggerDelivery>();
    public DbSet<StoredOverlay> Overlays => Set<StoredOverlay>();
    public DbSet<StoredAsset> Assets => Set<StoredAsset>();
    public DbSet<StoredOverlayToken> OverlayTokens => Set<StoredOverlayToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredOverlay>().HasKey(item => item.Id);
        modelBuilder.Entity<StoredAsset>().HasKey(item => item.Id);
        modelBuilder.Entity<StoredOverlayToken>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Hash).IsUnique();
            entity.HasOne<StoredOverlay>().WithMany().HasForeignKey(item => item.OverlayId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RumbleSnapshotState>().HasKey(item => new { item.Context, item.Provenance });
        modelBuilder.Entity<RumbleTriggerDelivery>(entity =>
        {
            entity.HasKey(item => item.EventId);
            entity.HasOne<StoredEvent>().WithOne().HasForeignKey<RumbleTriggerDelivery>(item => item.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.State, item.CreatedAtTicks });
        });
        modelBuilder.Entity<StoredEvent>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.Source, item.Provenance, item.DedupeKey }).IsUnique();
            entity.HasIndex(item => item.OccurredAtTicks);
            entity.HasIndex(item => item.Type);
        });
        modelBuilder.Entity<SourceCheckpoint>().HasKey(item => new { item.Source, item.Provenance });
        modelBuilder.Entity<StoredConfiguration>().HasKey(item => item.Name);
        modelBuilder.Entity<StoredLog>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TimestampTicks);
        });
        modelBuilder.Entity<OutboxEntry>(entity =>
        {
            entity.HasKey(item => item.EventId);
            entity.HasOne<StoredEvent>().WithOne().HasForeignKey<OutboxEntry>(item => item.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.DeliveredAtTicks, item.CreatedAtTicks });
        });
    }
}

public sealed class StoredOverlay
{
    public required string Id { get; set; }
    public required string Json { get; set; }
    public int Version { get; set; }
}
public sealed class StoredAsset
{
    public required string Id { get; set; }
    public required string Json { get; set; }
}
public sealed class StoredOverlayToken
{
    public Guid Id { get; set; }
    public required string OverlayId { get; set; }
    public required string Hash { get; set; }
    public long ExpiresAtTicks { get; set; }
    public bool Revoked { get; set; }
}

public sealed class RumbleSnapshotState
{
    public required string Context { get; set; }
    public required string Provenance { get; set; }
    public required string Json { get; set; }
}
public sealed class RumbleTriggerDelivery
{
    public Guid EventId { get; set; }
    public long CreatedAtTicks { get; set; }
    public required string State { get; set; }
}

public sealed class StoredConfiguration
{
    public required string Name { get; set; }
    public required string Json { get; set; }
}

public sealed class StoredLog
{
    public long Id { get; set; }
    public long TimestampTicks { get; set; }
    public required string Json { get; set; }
}

public sealed class StoredEvent
{
    public Guid Id { get; set; }
    public required string Source { get; set; }
    public required string Provenance { get; set; }
    public required string DedupeKey { get; set; }
    public required string Type { get; set; }
    public long OccurredAtTicks { get; set; }
    public required string Json { get; set; }
}

public sealed class SourceCheckpoint
{
    public required string Source { get; set; }
    public required string Provenance { get; set; }
    public required string Value { get; set; }
}

public sealed class OutboxEntry
{
    public Guid EventId { get; set; }
    public long CreatedAtTicks { get; set; }
    public long? DeliveredAtTicks { get; set; }
}
