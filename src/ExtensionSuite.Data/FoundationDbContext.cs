using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class FoundationDbContext(DbContextOptions<FoundationDbContext> options) : DbContext(options)
{
    public DbSet<StoredEvent> Events => Set<StoredEvent>();
    public DbSet<SourceCheckpoint> Checkpoints => Set<SourceCheckpoint>();
    public DbSet<OutboxEntry> Outbox => Set<OutboxEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredEvent>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.Source, item.Provenance, item.DedupeKey }).IsUnique();
            entity.HasIndex(item => item.OccurredAtTicks);
            entity.HasIndex(item => item.Type);
        });
        modelBuilder.Entity<SourceCheckpoint>().HasKey(item => new { item.Source, item.Provenance });
        modelBuilder.Entity<OutboxEntry>(entity =>
        {
            entity.HasKey(item => item.EventId);
            entity.HasOne<StoredEvent>().WithOne().HasForeignKey<OutboxEntry>(item => item.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.DeliveredAtTicks, item.CreatedAtTicks });
        });
    }
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
