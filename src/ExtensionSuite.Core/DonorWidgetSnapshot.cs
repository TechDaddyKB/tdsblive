namespace ExtensionSuite.Core;

/// <summary>Overlay-safe aggregate facts; excludes ledger rows, source payloads and identity keys.</summary>
public sealed record DonorWidgetSnapshot(
    string WidgetId,
    string State,
    DateTimeOffset GeneratedAt,
    DonorSupporterRow[] Rows,
    string TotalUsdMinor,
    long UnknownCount,
    long GatedCount,
    long EstimatedCount);

public sealed record DonorSupporterRow(
    Guid SupporterId,
    string Name,
    string UsdAmountMinor,
    string[] Platforms,
    long UnknownCount,
    long EstimatedCount,
    DateTimeOffset LatestAt,
    bool HasKnownAmount = true,
    string? AvatarUrl = null);
