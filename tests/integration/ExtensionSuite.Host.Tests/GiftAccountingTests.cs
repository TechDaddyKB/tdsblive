using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class GiftAccountingTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);
    private static CanonicalEvent Gift(string platform, string key, string role, long quantity = 1,
        string? group = "owned-batch", string[]? recipients = null, string sender = "owned-giver") => new()
    {
        Source = "owned-fixture", Platform = platform, Type = "support.gift", NativeType = "Fixture.Gift",
        DedupeKey = key, NativeId = key, OccurredAt = Start, User = new(sender),
        Support = new("gift", quantity, Tier: "owned-tier", GiftRole: role, GiftCorrelationKey: group,
            GiftScopeKey: "owned-channel", GiftRecipientKeys: recipients, GiftPeriodStart: Start, GiftPeriodEnd: Start.AddMonths(1))
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TwitchBatchAndIndividualArrivalOrdersCountOnlyThePurchase(bool individualFirst)
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var store = new FinancialStore(contexts);
        var batch = Gift("twitch", "batch", "batch", 3);
        var individual = Gift("twitch", "individual", "individual");
        if (individualFirst)
        {
            await store.AcceptAsync(individual, nominalUsdMinorPerUnit: 500m);
            var pending = Assert.Single(await store.TotalsAsync(new(null, null)));
            Assert.Equal("0", pending.UsdAmountMinor); Assert.Equal(1, pending.GatedCount);
        }
        await store.AcceptAsync(batch, nominalUsdMinorPerUnit: 500m);
        store = new(contexts); // Correlation state is durable across a new reader.
        if (!individualFirst) await store.AcceptAsync(individual, nominalUsdMinorPerUnit: 900m);
        await store.AcceptAsync(Gift("twitch", "individual-2", "individual"), nominalUsdMinorPerUnit: 900m);
        await store.AcceptAsync(Gift("twitch", "batch-alternate-notice", "batch", 3), nominalUsdMinorPerUnit: 900m);
        var total = Assert.Single(await store.TotalsAsync(new(null, null)));
        Assert.Equal("1500", total.UsdAmountMinor); Assert.Equal(1, total.ContributionCount);
        await using var db = await contexts.CreateDbContextAsync();
        Assert.Single(await db.GiftAccountingClaims.ToArrayAsync());
        Assert.Equal(3, await db.FinancialEvents.CountAsync(row => row.AccountingState == "excluded"));
        Assert.Equal(individualFirst ? 1 : 0, await db.FinancialAudits.CountAsync(row => row.Operation == "gift_correlation"));
    }

    [Fact]
    public async Task TwitchStandaloneAndDistinctBatchesCountWhileConflictingEvidenceStaysGated()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(contexts);
        await store.AcceptAsync(Gift("twitch", "standalone", "standalone", group: null), nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("twitch", "batch", "batch", 2), nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("twitch", "another-channel", "batch", 2) with
            { Support = Gift("twitch", "unused", "batch", 2).Support! with { GiftScopeKey = "another-channel" } }, nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("twitch", "conflicting-quantity", "batch", 3), nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("twitch", "conflicting-giver", "individual", sender: "another-giver"), nominalUsdMinorPerUnit: 500m);
        var totals = await store.TotalsAsync(new(null, null));
        Assert.Equal("2500", totals.Single(row => row.UsdAmountMinor != "0").UsdAmountMinor);
        await using var db = await contexts.CreateDbContextAsync();
        Assert.Equal(2, await db.GiftAccountingClaims.CountAsync());
        Assert.Equal(2, await db.FinancialEvents.CountAsync(row => row.PendingReason == "gift_evidence_conflict"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task KickExactRecipientPeriodsCorrelateInBothArrivalOrders(bool individualFirst)
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(contexts);
        var individual = Gift("kick", "individual", "individual", recipients: ["id:recipient-1"]);
        var batch = Gift("kick", "batch", "batch", 2, recipients: ["id:recipient-1", "id:recipient-2"]);
        if (individualFirst) await store.AcceptAsync(individual, nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(batch, nominalUsdMinorPerUnit: 600m);
        if (!individualFirst) await new FinancialStore(contexts).AcceptAsync(individual, nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("kick", "another-individual", "individual", recipients: ["id:recipient-2"]), nominalUsdMinorPerUnit: 800m);
        await store.AcceptAsync(Gift("kick", "duplicate-batch", "batch", 2, recipients: ["id:recipient-2", "id:recipient-1"]), nominalUsdMinorPerUnit: 800m);
        var total = Assert.Single(await store.TotalsAsync(new(null, null)));
        Assert.Equal("1200", total.UsdAmountMinor); Assert.Equal(1, total.ContributionCount);
        await using var db = await contexts.CreateDbContextAsync();
        var row = await db.FinancialEvents.SingleAsync(row => row.NativeEventId == "individual");
        Assert.Equal("excluded", row.AccountingState);
        Assert.Equal(individualFirst ? 500L : null, row.UsdAmountMinor); // Historical valuation remains available, but cannot count twice.
        Assert.Equal(2, await db.GiftAccountingClaims.CountAsync());
    }

    [Fact]
    public async Task KickPartialOverlapIsVisibleAndCannotInflateTheAcceptedBatch()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(contexts);
        await store.AcceptAsync(Gift("kick", "first", "batch", 2, recipients: ["id:r1", "id:r2"]), nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("kick", "partial", "batch", 2, recipients: ["id:r2", "id:r3"]), nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("kick", "next-period", "individual", recipients: ["id:r2"]) with
            { Support = Gift("kick", "unused", "individual", recipients: ["id:r2"]).Support! with { GiftPeriodStart = Start.AddMonths(1), GiftPeriodEnd = Start.AddMonths(2) } }, nominalUsdMinorPerUnit: 500m);
        var total = Assert.Single(await store.TotalsAsync(new(null, null)));
        Assert.Equal("1500", total.UsdAmountMinor); Assert.Equal(1, total.GatedCount);
        await using var db = await contexts.CreateDbContextAsync();
        Assert.Equal("gift_partial_overlap", (await db.FinancialEvents.SingleAsync(row => row.NativeEventId == "partial")).PendingReason);
        Assert.Equal(3, await db.GiftAccountingClaims.CountAsync());
    }

    [Fact]
    public async Task MissingKickEvidenceAndRumbleRemainGatedButYoutubeRecipientsAreExcluded()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(contexts);
        var missing = Gift("kick", "missing", "batch", 2, recipients: ["id:one"]);
        await store.AcceptAsync(missing, nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("kick", "anonymous", "individual", recipients: ["id:one"]) with { User = null }, nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("rumble", "rumble", "batch", 2), nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("youtube", "batch", "batch", 2), nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("youtube", "recipient", "recipient"), nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("youtube", "alternate-batch", "batch", 2), nominalUsdMinorPerUnit: 800m);
        await using var db = await contexts.CreateDbContextAsync();
        Assert.Equal(3, await db.FinancialEvents.CountAsync(row => row.AccountingState == "gated"));
        Assert.Equal(2, await db.FinancialEvents.CountAsync(row => row.AccountingState == "excluded"));
        Assert.Equal(1000, (await db.FinancialEvents.SingleAsync(row => row.AccountingState == "counted")).UsdAmountMinor);
        var gated = await db.FinancialEvents.AsNoTracking().FirstAsync(row => row.AccountingState == "gated");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReconcileAsync(gated.Id, gated.Version, nominalUsdMinorPerUnit: 500m));
    }

    [Fact]
    public async Task CorrelationAuditFailureRollsBackClaimsAndTheReplacementContribution()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(contexts);
        await store.AcceptAsync(Gift("kick", "individual", "individual", recipients: ["id:r1"]), nominalUsdMinorPerUnit: 500m);
        await using var db = await contexts.CreateDbContextAsync();
        var original = await db.FinancialEvents.AsNoTracking().SingleAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_gift_audit BEFORE INSERT ON FinancialAudits BEGIN SELECT RAISE(ABORT, 'owned gift audit failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => store.AcceptAsync(Gift("kick", "batch", "batch", 2, recipients: ["id:r1", "id:r2"]), nominalUsdMinorPerUnit: 500m));
        var unchanged = await db.FinancialEvents.AsNoTracking().SingleAsync();
        Assert.Equal("counted", unchanged.AccountingState); Assert.Equal(1, unchanged.Version);
        Assert.Equal(500, unchanged.UsdAmountMinor);
        Assert.Equal(original.Id, (await db.GiftAccountingClaims.SingleAsync()).OwnerContributionId);
    }

    [Fact]
    public async Task ExactPeriodAndSenderFactsAreRequiredWithoutNameBasedMatching()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(contexts);
        var item = Gift("kick", "valid", "individual", recipients: ["id:r1"]);
        await store.AcceptAsync(item, nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("kick", "wrong-sender", "individual", recipients: ["id:r1"], sender: "other-giver"), nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("kick", "no-period", "individual", recipients: ["id:r2"]) with
            { Support = item.Support! with { GiftPeriodStart = null, GiftRecipientKeys = ["id:r2"] } }, nominalUsdMinorPerUnit: 500m);
        await store.AcceptAsync(Gift("kick", "no-id", "individual", recipients: ["login:label-only"]), nominalUsdMinorPerUnit: 500m);
        await using var db = await contexts.CreateDbContextAsync();
        Assert.Equal(1, await db.GiftAccountingClaims.CountAsync());
        var reasons = await db.FinancialEvents.Where(row => row.AccountingState == "gated").Select(row => row.PendingReason).ToArrayAsync();
        Assert.Contains("gift_evidence_conflict", reasons); Assert.Contains("gift_period_unavailable", reasons); Assert.Contains("gift_recipients_unavailable", reasons);
        Assert.Equal("500", (await store.TotalsAsync(new(null, null))).Single(row => row.UsdAmountMinor != "0").UsdAmountMinor);
    }
}
