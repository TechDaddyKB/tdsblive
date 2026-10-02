using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class FinancialReconciliationTests
{
    private static CanonicalEvent Event(SupportDetails support, string key = "owned-reconcile") => new()
    {
        Source = "owned-fixture", Platform = "twitch", NativeType = "Fixture.Support", Type = "support.donation",
        DedupeKey = key, OccurredAt = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), Support = support
    };

    [Fact]
    public async Task PendingConversionReconcilesExplicitlyWithAuditAndStaleVersionProtection()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(factory);
        await store.AcceptAsync(Event(new("donation", 1, new(1000, "EUR", 2))));
        await using var db = await factory.CreateDbContextAsync(); var row = await db.FinancialEvents.AsNoTracking().SingleAsync();
        Assert.Null(row.UsdAmountMinor); var date = new DateOnly(2026, 1, 5);
        Assert.True(await store.ReconcileAsync(row.Id, 1, new("EUR", date, date, 1.25m, "manual", false)));
        Assert.False(await store.ReconcileAsync(row.Id, 1, new("EUR", date, date, 2m, "changed", false)));
        Assert.True(await store.ReconcileAsync(row.Id, 2)); // Failed lookup cannot erase accepted history.
        var valued = await db.FinancialEvents.AsNoTracking().SingleAsync(); Assert.Equal(1250, valued.UsdAmountMinor); Assert.Equal(2, valued.Version);
        Assert.Equal(1, await db.FinancialAudits.CountAsync()); Assert.Null(valued.PendingReason);
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReconcileAsync(row.Id, 2, new("EUR", date.AddDays(1), date, 2m, "manual", false)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ReconcileAsync(row.Id, 0));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.ReconcileAsync(Guid.CreateVersion7(), 1));
        Assert.True(await store.ReconcileAsync(row.Id, 2, new("EUR", date, date, 2m, "manual", false)));
        Assert.Equal("2000", Assert.Single(await store.TotalsAsync(new(null, null))).UsdAmountMinor);
    }

    [Fact]
    public async Task UnconfiguredRulesStayUnknownUntilExplicitlyChosenAndReconciled()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var rules = new ValuationRuleStore(factory);
        Assert.Null(await rules.FindAsync("twitch", "bits", ""));
        var store = new FinancialStore(factory); await store.AcceptAsync(Event(new("bits", 500)));
        Assert.True(await rules.SetAsync("twitch", "bits", "", 1m, 0));
        Assert.False(await rules.SetAsync("twitch", "bits", "", 2m, 0));
        Assert.Equal(1m, await new ValuationRuleStore(factory).FindAsync("twitch", "bits", ""));
        await using var db = await factory.CreateDbContextAsync(); var row = await db.FinancialEvents.AsNoTracking().SingleAsync();
        Assert.Null(row.UsdAmountMinor); // Saving a rule does not automatically alter history.
        Assert.True(await store.ReconcileAsync(row.Id, 1, nominalUsdMinorPerUnit: await rules.FindAsync("twitch", "bits", "")));
        var valued = await db.FinancialEvents.AsNoTracking().SingleAsync();
        Assert.Equal(500, valued.UsdAmountMinor); Assert.Equal("configured_nominal", valued.ValuationMethod); Assert.True(valued.Estimated);
        Assert.True(await rules.SetAsync("twitch", "bits", "", 0m, 1));
        Assert.Equal(500, (await db.FinancialEvents.AsNoTracking().SingleAsync()).UsdAmountMinor);
        await store.AcceptAsync(Event(new("gift", 5), "owned-gift"));
        var gift = await db.FinancialEvents.AsNoTracking().SingleAsync(item => item.Type == "gift");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReconcileAsync(gift.Id, 1, nominalUsdMinorPerUnit: 500m));
        Assert.Null((await db.FinancialEvents.AsNoTracking().SingleAsync(item => item.Id == gift.Id)).UsdAmountMinor);
        await Assert.ThrowsAsync<ArgumentException>(() => rules.SetAsync("twitch", "bits", "", -1m, 2));
    }

    [Fact]
    public async Task FailedAuditInsertionRollsBackValuationAndVersion()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var store = new FinancialStore(factory);
        await store.AcceptAsync(Event(new("bits", 10)));
        await using var db = await factory.CreateDbContextAsync(); var row = await db.FinancialEvents.AsNoTracking().SingleAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_audit BEFORE INSERT ON FinancialAudits BEGIN SELECT RAISE(ABORT, 'owned audit rollback'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => store.ReconcileAsync(row.Id, 1, nominalUsdMinorPerUnit: 1m));
        var unchanged = await db.FinancialEvents.AsNoTracking().SingleAsync(); Assert.Null(unchanged.UsdAmountMinor); Assert.Equal(1, unchanged.Version);
        Assert.Equal(0, await db.FinancialAudits.CountAsync());
    }
}
