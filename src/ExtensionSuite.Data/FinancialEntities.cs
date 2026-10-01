namespace ExtensionSuite.Data;

public sealed class FinancialProjectionReceipt
{
    public Guid EventId { get; set; }
    public required string State { get; set; }
    public string? Reason { get; set; }
    public long ProcessedAtTicks { get; set; }
}

public sealed class Supporter
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}

public sealed class SupporterIdentity
{
    public Guid Id { get; set; }
    public Guid SupporterId { get; set; }
    public required string Platform { get; set; }
    // Platform ID when available; otherwise an explicitly typed source key.
    public required string IdentityKey { get; set; }
    public required string DisplayName { get; set; }
}

public sealed class FinancialContribution
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid SupporterId { get; set; }
    public Guid IdentityId { get; set; }
    public required string Source { get; set; }
    public required string Platform { get; set; }
    public required string Type { get; set; }
    public string? NativeEventId { get; set; }
    public required string DedupeKey { get; set; }
    public long OccurredAtTicks { get; set; }
    public long Quantity { get; set; }
    public long? NativeAmountMinor { get; set; }
    public string? NativeCurrency { get; set; }
    public int? NativeMinorUnitDigits { get; set; }
    public long? UsdAmountMinor { get; set; }
    public required string ValuationMethod { get; set; }
    public bool Estimated { get; set; }
    // Decimal values stored as invariant strings, never binary floats.
    public string? FxRate { get; set; }
    public int? FxRateDay { get; set; }
    public string? FxProvider { get; set; }
    public string? PendingReason { get; set; }
    public string? StreamId { get; set; }
    public string? GiftCorrelationKey { get; set; }
    public required string AccountingState { get; set; }
    public required string MetadataJson { get; set; }
    public int Version { get; set; }
}

public sealed class StoredFxRate
{
    public required string Currency { get; set; }
    public int RequestedDay { get; set; }
    public required string Origin { get; set; }
    public int RateDay { get; set; }
    public required string UsdPerNativeUnit { get; set; }
    public required string Provider { get; set; }
    public bool Estimated { get; set; }
}

public sealed class StoredValuationRule
{
    public required string Platform { get; set; }
    public required string Type { get; set; }
    public required string Tier { get; set; }
    public required string UsdMinorPerUnit { get; set; }
    public int Version { get; set; }
}

public sealed class FinancialAudit
{
    public Guid Id { get; set; }
    public Guid? ContributionId { get; set; }
    public long CreatedAtTicks { get; set; }
    public required string Operation { get; set; }
    public required string BeforeJson { get; set; }
    public required string AfterJson { get; set; }
}
