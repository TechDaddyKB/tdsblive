using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed class FoundationDbContext(DbContextOptions<FoundationDbContext> options) : DbContext(options)
{
    public DbSet<StoredAutomationRule> AutomationRules => Set<StoredAutomationRule>();
    public DbSet<AutomationExecution> AutomationExecutions => Set<AutomationExecution>();
    public DbSet<AutomationTemporaryEffect> AutomationTemporaryEffects => Set<AutomationTemporaryEffect>();
    public DbSet<AutomationEventInbox> AutomationInbox => Set<AutomationEventInbox>();
    public DbSet<StoredEvent> Events => Set<StoredEvent>();
    public DbSet<SourceCheckpoint> Checkpoints => Set<SourceCheckpoint>();
    public DbSet<OutboxEntry> Outbox => Set<OutboxEntry>();
    public DbSet<StoredConfiguration> Configurations => Set<StoredConfiguration>();
    public DbSet<StoredLog> Logs => Set<StoredLog>();
    public DbSet<RumbleSnapshotState> RumbleStates => Set<RumbleSnapshotState>();
    public DbSet<RumbleTriggerDelivery> RumbleDeliveries => Set<RumbleTriggerDelivery>();
    public DbSet<StoredOverlayRevision> OverlayRevisions => Set<StoredOverlayRevision>();
    public DbSet<StoredOverlay> Overlays => Set<StoredOverlay>();
    public DbSet<StoredAsset> Assets => Set<StoredAsset>();
    public DbSet<StoredOverlayToken> OverlayTokens => Set<StoredOverlayToken>();
    public DbSet<Supporter> Supporters => Set<Supporter>();
    public DbSet<SupporterIdentity> SupporterIdentities => Set<SupporterIdentity>();
    public DbSet<FinancialContribution> FinancialEvents => Set<FinancialContribution>();
    public DbSet<StoredFxRate> FxRates => Set<StoredFxRate>();
    public DbSet<StoredValuationRule> ValuationRules => Set<StoredValuationRule>();
    public DbSet<FinancialAudit> FinancialAudits => Set<FinancialAudit>();
    public DbSet<FinancialProjectionReceipt> FinancialProjectionReceipts => Set<FinancialProjectionReceipt>();
    public DbSet<GiftAccountingClaim> GiftAccountingClaims => Set<GiftAccountingClaim>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AutomationEventInbox>(entity =>
        {
            entity.HasKey(item => item.Sequence);
            entity.HasIndex(item => item.EventId).IsUnique();
            entity.HasIndex(item => new { item.Processed, item.Sequence });
            entity.HasOne<StoredEvent>().WithMany().HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AutomationTemporaryEffect>(entity =>
        {
            entity.HasKey(item => item.ActionId);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.State, item.ExpiresAtTicks });
        });
        modelBuilder.Entity<StoredAutomationRule>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<AutomationExecution>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.EventId, item.RuleId, item.ActionId }).IsUnique();
            entity.HasIndex(item => new { item.State, item.DueAtTicks });
            entity.HasIndex(item => new { item.QueueGroup, item.State });
            entity.HasIndex(item => item.CreatedAtTicks);
            entity.Property(item => item.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<FinancialProjectionReceipt>(entity =>
        {
            entity.HasKey(item => item.EventId);
            entity.HasIndex(item => item.State);
            entity.HasOne<StoredEvent>().WithMany().HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<GiftAccountingClaim>(entity =>
        {
            entity.HasKey(item => new { item.Platform, item.KeyHash });
            entity.HasIndex(item => item.OwnerContributionId);
            entity.HasOne<FinancialContribution>().WithMany().HasForeignKey(item => item.OwnerContributionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Supporter>().HasKey(item => item.Id);
        modelBuilder.Entity<SupporterIdentity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.Platform, item.IdentityKey }).IsUnique();
            entity.HasIndex(item => item.SupporterId);
            entity.HasOne<Supporter>().WithMany().HasForeignKey(item => item.SupporterId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<FinancialContribution>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.EventId).IsUnique();
            entity.HasIndex(item => new { item.Platform, item.NativeEventId }).IsUnique().HasFilter("NativeEventId IS NOT NULL");
            entity.HasIndex(item => new { item.Platform, item.DedupeKey }).IsUnique();
            entity.HasIndex(item => item.OccurredAtTicks);
            entity.HasIndex(item => item.SupporterId);
            entity.HasIndex(item => item.Platform);
            entity.HasIndex(item => new { item.StreamId, item.OccurredAtTicks });
            entity.Property(item => item.GiftRole).HasDefaultValue("none");
            entity.HasIndex(item => new { item.Platform, item.GiftScopeKey, item.GiftCorrelationKey, item.GiftRole });
            entity.HasOne<Supporter>().WithMany().HasForeignKey(item => item.SupporterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupporterIdentity>().WithMany().HasForeignKey(item => item.IdentityId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StoredFxRate>().HasKey(item => new { item.Currency, item.RequestedDay, item.Origin });
        modelBuilder.Entity<StoredValuationRule>(entity =>
        {
            entity.HasKey(item => new { item.Platform, item.Type, item.Tier });
            entity.Property(item => item.Enabled).HasDefaultValue(true);
        });
        modelBuilder.Entity<FinancialAudit>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ContributionId);
            entity.HasIndex(item => item.CreatedAtTicks);
        });
        modelBuilder.Entity<StoredOverlay>().HasKey(item => item.Id);
        modelBuilder.Entity<StoredOverlayRevision>(entity =>
        {
            entity.HasKey(item => new { item.OverlayId, item.Version });
            entity.HasOne<StoredOverlay>().WithMany().HasForeignKey(item => item.OverlayId).OnDelete(DeleteBehavior.Cascade);
        });
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

public sealed class StoredAutomationRule
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public required string Json { get; set; }
}

public sealed class AutomationEventInbox
{
    public long Sequence { get; set; }
    public Guid EventId { get; set; }
    public bool Processed { get; set; }
}

public sealed class AutomationTemporaryEffect
{
    public Guid ActionId { get; set; }
    public int Version { get; set; }
    public long ExpiresAtTicks { get; set; }
    public required string State { get; set; }
    public required string Json { get; set; }
}

public sealed class AutomationExecution
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid RuleId { get; set; }
    public Guid ActionId { get; set; }
    public int ActionOrder { get; set; }
    public string QueueGroup { get; set; } = "main";
    public bool CancelRequested { get; set; }
    public int Version { get; set; }
    public long CreatedAtTicks { get; set; }
    public long DueAtTicks { get; set; }
    public required string State { get; set; }
    public string? Detail { get; set; }
    public required string Json { get; set; }
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

public sealed class StoredOverlayRevision
{
    public required string OverlayId { get; set; }
    public int Version { get; set; }
    public long SavedAtTicks { get; set; }
    public required string Json { get; set; }
}
