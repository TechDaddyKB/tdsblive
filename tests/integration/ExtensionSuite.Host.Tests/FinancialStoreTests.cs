using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class FinancialStoreTests
{
    private static CanonicalEvent Paid(string platform = "twitch", string nativeId = "owned-purchase") => new()
    {
        Source = "synthetic-fixture", Platform = platform, Type = "support.donation", NativeType = "Fixture.Donation",
        NativeId = nativeId, DedupeKey = "fixture:" + nativeId, OccurredAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        User = new("user-1", DisplayName: "Same display name"), Support = new("donation", 1, new(500, "USD", 2))
    };

    [Fact]
    public async Task UniqueSourceKeysSurviveFreshRepositoryAndRawCaptureIsUnnecessary()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var store = new FinancialStore(factory); var item = Paid();
        Assert.Null(item.Raw); Assert.True(await store.AcceptAsync(item));
        Assert.False(await store.AcceptAsync(item));
        await using var db = await factory.CreateDbContextAsync();
        var reopened = new FinancialStore(new ReopenedFactory(new DbContextOptionsBuilder<FoundationDbContext>()
            .UseSqlite(db.Database.GetConnectionString()).Options));
        Assert.False(await reopened.AcceptAsync(item with { Id = Guid.CreateVersion7(), DedupeKey = "different-key" }));
        Assert.False(await reopened.AcceptAsync(item with { Id = Guid.CreateVersion7(), NativeId = "different-id" }));
        Assert.True(await reopened.AcceptAsync(Paid(nativeId: "owned-purchase-2")));
        var rows = await db.FinancialEvents.AsNoTracking().ToArrayAsync();
        Assert.Equal(2, rows.Length); Assert.All(rows, row => Assert.Equal(500, row.UsdAmountMinor));
        Assert.Equal(1, await db.SupporterIdentities.CountAsync());
    }

    [Fact]
    public async Task SimulationReplayAndUnverifiedGiftCannotEnterValuedTotals()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(factory);
        foreach (var provenance in new[] { EventProvenance.Simulation, EventProvenance.Replay })
            Assert.False(await store.AcceptAsync(Paid() with { Provenance = provenance }));
        Assert.True(await store.AcceptAsync(Paid("rumble") with { Support = new("gift", 10), Type = "support.gift" }, nominalUsdMinorPerUnit: 500));
        Assert.True(await store.AcceptAsync(Paid("youtube") with { Support = new("gift", 10, GiftRole: "recipient"), Type = "support.gift" }, nominalUsdMinorPerUnit: 500));
        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(2, await db.FinancialEvents.CountAsync());
        Assert.All(await db.FinancialEvents.ToArrayAsync(), row => { Assert.Null(row.UsdAmountMinor); Assert.NotEqual("counted", row.AccountingState); });
        Assert.Contains(await db.FinancialEvents.ToArrayAsync(), row => row.PendingReason == "rumble_gift_unverified");
    }

    [Fact]
    public async Task IdenticalDisplayNamesStaySeparateUntilTransactionalManualLink()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(factory);
        await store.AcceptAsync(Paid()); await store.AcceptAsync(Paid("youtube"));
        Assert.Equal(2, (await store.TotalsAsync(new(null, null))).Length);
        await using var db = await factory.CreateDbContextAsync();
        var identities = await db.SupporterIdentities.AsNoTracking().OrderBy(row => row.Platform).ToArrayAsync();
        Assert.Equal(2, identities.Select(row => row.SupporterId).Distinct().Count());
        await store.LinkIdentityAsync(identities[1].Id, identities[0].SupporterId);
        await store.LinkIdentityAsync(identities[1].Id, identities[0].SupporterId);
        var rows = await db.FinancialEvents.AsNoTracking().ToArrayAsync();
        Assert.All(rows, row => Assert.Equal(identities[0].SupporterId, row.SupporterId));
        Assert.Equal(1000, rows.Sum(row => row.UsdAmountMinor));
        var total = Assert.Single(await store.TotalsAsync(new(null, null)));
        Assert.Equal("1000", total.UsdAmountMinor); Assert.Equal("1000", total.ExactAmountMinor);
        Assert.Equal(1, await db.FinancialAudits.CountAsync());
        Assert.Equal(2, rows.Select(row => row.Platform).Distinct().Count());
        Assert.All(rows, row => Assert.Equal(1, row.Quantity));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.LinkIdentityAsync(identities[1].Id, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task DuplicateDeliveryCannotRevalueAcceptedHistoricalSpend()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(factory);
        var item = Paid() with { Support = new("donation", 1, new(1000, "EUR", 2)) };
        var date = new DateOnly(2026, 1, 1);
        Assert.True(await store.AcceptAsync(item, new("EUR", date, date, 1.25m, "manual", false)));
        Assert.False(await store.AcceptAsync(item with { Id = Guid.CreateVersion7() }, new("EUR", date, date, 2m, "changed-provider", true)));
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.FinancialEvents.SingleAsync();
        Assert.Equal(1250, row.UsdAmountMinor); Assert.Equal(1000, row.NativeAmountMinor);
        Assert.Equal("1.25", row.FxRate); Assert.Equal("manual", row.FxProvider); Assert.False(row.Estimated);
        Assert.Equal(1, row.Version);
    }

    [Fact]
    public async Task PeriodTotalsAreHalfOpenAndCannotLosePrecisionOrIncludePendingGiftValues()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(factory);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); var end = start.AddDays(1);
        await store.AcceptAsync(Paid(nativeId: "before") with { OccurredAt = start.AddTicks(-1) });
        await store.AcceptAsync(Paid(nativeId: "start") with { Support = new("donation", 1, new(long.MaxValue, "USD", 2)) });
        await store.AcceptAsync(Paid(nativeId: "second") with { Support = new("donation", 1, new(1, "USD", 2)) });
        await store.AcceptAsync(Paid(nativeId: "end") with { OccurredAt = end });
        await store.AcceptAsync(Paid(nativeId: "unknown") with { Support = new("subscription", 1) });
        await store.AcceptAsync(Paid(nativeId: "nominal") with { Support = new("bits", 10) }, nominalUsdMinorPerUnit: .5m);
        await store.AcceptAsync(Paid(nativeId: "gated") with { Support = new("gift", 100) }, nominalUsdMinorPerUnit: 500m);
        var total = Assert.Single(await store.TotalsAsync(new(start, end)));
        Assert.Equal("9223372036854775813", total.UsdAmountMinor);
        Assert.Equal("9223372036854775808", total.ExactAmountMinor);
        Assert.Equal("5", total.NominalAmountMinor); Assert.Equal("0", total.FxAmountMinor);
        Assert.Equal(5, total.ContributionCount); Assert.Equal(1, total.UnknownCount);
        Assert.Equal(1, total.EstimatedCount); Assert.Equal(1, total.GatedCount);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.TotalsAsync(new(null, null), 0));
    }

    [Fact]
    public async Task IdentityWriteAndContributionInsertRollbackTogether()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(factory);
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_financial BEFORE INSERT ON FinancialEvents BEGIN SELECT RAISE(ABORT, 'owned rollback test'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => store.AcceptAsync(Paid()));
        Assert.Equal(0, await db.Supporters.CountAsync()); Assert.Equal(0, await db.SupporterIdentities.CountAsync());
        Assert.Equal(0, await db.FinancialEvents.CountAsync());
    }

    private sealed class ReopenedFactory(DbContextOptions<FoundationDbContext> options) : IDbContextFactory<FoundationDbContext>
    {
        public FoundationDbContext CreateDbContext() => new(options);
    }
}
