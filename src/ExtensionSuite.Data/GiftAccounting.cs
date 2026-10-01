using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExtensionSuite.Core;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

internal sealed record GiftAccountingDecision(string State, string? Reason = null);

/// <summary>Called inside ledger acceptance's transaction. Claims use source identities, never linked supporter identities.</summary>
internal static class GiftAccounting
{
    public static string? SenderKey(CanonicalEvent item) => item.User?.PlatformUserId is { Length: > 0 } id ? "id:" + id : null;

    public static async Task<GiftAccountingDecision> DecideAsync(FoundationDbContext db, CanonicalEvent item,
        Guid contributionId, CancellationToken cancellationToken)
    {
        var support = item.Support!;
        if (support.Kind == "gift" && item.Platform == "rumble") return new("gated", "rumble_gift_unverified");
        if (support.GatedReason is { } reason) return new("gated", reason);
        if (support.Kind != "gift") return new("counted");
        if (support.GiftRole == "recipient") return new("excluded", "gift_recipient_notification");
        if (item.Platform == "twitch" && support.GiftRole == "standalone") return support.Quantity == 1 && support.GiftCorrelationKey is null
            ? new("counted") : new("gated", "gift_evidence_conflict");
        if (string.IsNullOrWhiteSpace(support.GiftScopeKey)) return new("gated", "gift_scope_unavailable");
        return item.Platform switch
        {
            "twitch" => await TwitchAsync(db, item, contributionId, cancellationToken),
            "youtube" => await YoutubeAsync(db, item, contributionId, cancellationToken),
            "kick" => await KickAsync(db, item, contributionId, cancellationToken),
            _ => new("gated", "gift_correlation_pending")
        };
    }

    private static async Task<GiftAccountingDecision> TwitchAsync(FoundationDbContext db, CanonicalEvent item,
        Guid contributionId, CancellationToken cancellationToken)
    {
        var support = item.Support!;
        if (support.GiftRole is not ("batch" or "individual") || string.IsNullOrWhiteSpace(support.GiftCorrelationKey))
            return new("gated", "gift_correlation_pending");
        if (support.GiftRole == "individual" && support.Quantity != 1) return new("gated", "gift_evidence_conflict");
        var hash = Hash(support.GiftScopeKey, support.GiftCorrelationKey);
        var claim = await db.GiftAccountingClaims.FindAsync([item.Platform, hash], cancellationToken);
        if (support.GiftRole == "individual")
            return claim is null ? new("gated", "gift_batch_pending") : Compatible(claim, support, SenderKey(item))
                ? new("excluded", "gift_batch_notification") : new("gated", "gift_evidence_conflict");
        if (claim is not null)
            return Compatible(claim, support, SenderKey(item)) && claim.Quantity == support.Quantity
                ? new("excluded", "gift_duplicate_batch") : new("gated", "gift_evidence_conflict");
        Claim(db, item, contributionId, hash);
        var pending = await db.FinancialEvents.Where(row => row.Platform == item.Platform && row.Type == "gift" &&
            row.GiftScopeKey == support.GiftScopeKey && row.GiftCorrelationKey == support.GiftCorrelationKey &&
            row.GiftRole == "individual" && row.AccountingState == "gated" && row.PendingReason == "gift_batch_pending")
            .ToArrayAsync(cancellationToken);
        foreach (var row in pending)
        {
            if (row.GiftTier == support.Tier && SendersCompatible(row.GiftSenderKey, SenderKey(item)))
                Exclude(db, row, "gift_batch_notification", contributionId);
            else Conflict(db, row, contributionId);
        }
        return new("counted");
    }

    private static async Task<GiftAccountingDecision> YoutubeAsync(FoundationDbContext db, CanonicalEvent item,
        Guid contributionId, CancellationToken cancellationToken)
    {
        var support = item.Support!;
        if (support.GiftRole != "batch" || string.IsNullOrWhiteSpace(support.GiftCorrelationKey))
            return new("gated", "gift_correlation_pending");
        var hash = Hash(support.GiftScopeKey, support.GiftCorrelationKey);
        var claim = await db.GiftAccountingClaims.FindAsync([item.Platform, hash], cancellationToken);
        if (claim is not null) return Compatible(claim, support, SenderKey(item)) && claim.Quantity == support.Quantity
            ? new("excluded", "gift_duplicate_batch") : new("gated", "gift_evidence_conflict");
        Claim(db, item, contributionId, hash);
        return new("counted");
    }

    private static async Task<GiftAccountingDecision> KickAsync(FoundationDbContext db, CanonicalEvent item,
        Guid contributionId, CancellationToken cancellationToken)
    {
        var support = item.Support!;
        if (support.GiftRole is not ("batch" or "individual")) return new("gated", "gift_correlation_pending");
        if (SenderKey(item) is null) return new("gated", "gift_sender_unavailable");
        if (support.GiftPeriodStart is not { } start || support.GiftPeriodEnd is not { } end || end <= start)
            return new("gated", "gift_period_unavailable");
        if (support.GiftRecipientKeys is not { Length: > 0 } recipients || recipients.Length != support.Quantity ||
            recipients.Distinct(StringComparer.Ordinal).Count() != recipients.Length || recipients.Any(key => key.Length <= 3 || !key.StartsWith("id:", StringComparison.Ordinal)) ||
            (support.GiftRole == "individual" && support.Quantity != 1)) return new("gated", "gift_recipients_unavailable");
        var hashes = recipients.Select(recipient => Hash(support.GiftScopeKey, start.UtcTicks, end.UtcTicks, recipient)).ToArray();
        var existing = await db.GiftAccountingClaims.Where(row => row.Platform == item.Platform && hashes.Contains(row.KeyHash)).ToArrayAsync(cancellationToken);
        if (existing.Any(claim => !Compatible(claim, support, SenderKey(item)))) return new("gated", "gift_evidence_conflict");
        if (support.GiftRole == "individual" && existing.Length != 0) return new("excluded", "gift_duplicate_recipient");
        var batches = existing.Where(claim => claim.Role == "batch").ToArray();
        if (batches.Length != 0)
            return existing.Length == hashes.Length && batches.Length == hashes.Length && batches.Select(claim => claim.OwnerContributionId).Distinct().Count() == 1 &&
                batches.All(claim => claim.Quantity == support.Quantity)
                ? new("excluded", "gift_duplicate_batch") : new("gated", "gift_partial_overlap");
        // Exact individual subscription evidence can be replaced by its full batch in either arrival order.
        // Preserve each old valuation, but exclude it from accounting and audit the structural correction.
        var owners = existing.Select(claim => claim.OwnerContributionId).Distinct().ToArray();
        var individuals = await db.FinancialEvents.Where(row => owners.Contains(row.Id)).ToArrayAsync(cancellationToken);
        if (individuals.Any(row => row.GiftRole != "individual" || row.Quantity != 1 || row.AccountingState != "counted"))
            return new("gated", "gift_evidence_conflict");
        foreach (var individual in individuals) Exclude(db, individual, "gift_batch_notification", contributionId);
        foreach (var claim in existing) { claim.OwnerContributionId = contributionId; claim.Role = "batch"; claim.Quantity = support.Quantity; }
        var occupied = existing.Select(claim => claim.KeyHash).ToHashSet(StringComparer.Ordinal);
        foreach (var hash in hashes.Where(hash => !occupied.Contains(hash))) Claim(db, item, contributionId, hash);
        return new("counted");
    }

    private static bool Compatible(GiftAccountingClaim claim, SupportDetails support, string? sender) =>
        claim.Tier == support.Tier && SendersCompatible(claim.SenderKey, sender);
    private static bool SendersCompatible(string? first, string? second) => first is null || second is null || first == second;
    private static string Hash(params object?[] parts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
    private static void Claim(FoundationDbContext db, CanonicalEvent item, Guid contributionId, string hash) =>
        db.GiftAccountingClaims.Add(new() { Platform = item.Platform, KeyHash = hash, OwnerContributionId = contributionId,
            Role = item.Support!.GiftRole, Quantity = item.Support.Quantity, Tier = item.Support.Tier, SenderKey = SenderKey(item) });

    private static void Exclude(FoundationDbContext db, FinancialContribution row, string reason, Guid owner) => Change(db, row, "excluded", reason, owner);
    private static void Conflict(FoundationDbContext db, FinancialContribution row, Guid owner) => Change(db, row, "gated", "gift_evidence_conflict", owner);
    private static void Change(FoundationDbContext db, FinancialContribution row, string state, string reason, Guid owner)
    {
        var before = JsonSerializer.Serialize(new { row.AccountingState, row.PendingReason, row.Version, row.UsdAmountMinor });
        row.AccountingState = state; row.PendingReason = reason; row.Version++;
        db.FinancialAudits.Add(new() { Id = Guid.CreateVersion7(), ContributionId = row.Id, CreatedAtTicks = DateTimeOffset.UtcNow.UtcTicks,
            Operation = "gift_correlation", BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new { row.AccountingState, row.PendingReason, row.Version, row.UsdAmountMinor, owner }) });
    }
}
