namespace ExtensionSuite.Core;

// Wire amounts are decimal integer strings: JavaScript must never round authoritative cents.
public sealed record FinancialTotals(Guid SupporterId, string Name, string UsdAmountMinor,
    string ExactAmountMinor, string FxAmountMinor, string NominalAmountMinor,
    long ContributionCount, long UnknownCount, long EstimatedCount, long GatedCount);
