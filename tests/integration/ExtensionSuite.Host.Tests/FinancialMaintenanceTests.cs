using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ExtensionSuite.Host;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class FinancialMaintenanceTests
{
    [Fact]
    public async Task DowngradeCannotReactivateRemovedRulesOrEraseFinancialHistory()
    {
        using var factory = new FoundationHostFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.RemoveAll<IIsolatedIntegration>()));
        using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var rules = new ValuationRuleStore(contexts);
        var ledger = new FinancialStore(contexts);
        Assert.True(await rules.SetAsync("twitch", "bits", "", 1m, 0));
        await ledger.AcceptAsync(Contribution(new("bits", 500)), nominalUsdMinorPerUnit: 1m);
        Assert.True(await rules.RemoveAsync("twitch", "bits", "", 1));
        await using var db = await contexts.CreateDbContextAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261001222806_GiftAccountingClaims");
        await db.Database.MigrateAsync();
        Assert.Empty(await rules.ListAsync());
        Assert.Equal("500", Assert.Single(await ledger.TotalsAsync(new(null, null))).UsdAmountMinor);
        Assert.Equal(1, await db.FinancialAudits.CountAsync(row => row.Operation == "nominal_rule_remove"));
    }
    private static readonly DateOnly Day = new(2026, 1, 5);
    private static CanonicalEvent Contribution(SupportDetails support) => new()
    {
        Source = "owned-fixture", Platform = "twitch", Type = "support.donation", NativeType = "Fixture.Support",
        DedupeKey = "owned-maintenance", OccurredAt = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), Support = support
    };

    [Fact]
    public async Task RuleRemovalIsUnconfiguredAndReactivationCannotReuseAStaleVersion()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var rules = new ValuationRuleStore(contexts); var ledger = new FinancialStore(contexts);
        Assert.True(await rules.SetAsync("twitch", "bits", "", 1m, 0));
        await ledger.AcceptAsync(Contribution(new("bits", 500)), nominalUsdMinorPerUnit: await rules.FindAsync("twitch", "bits", ""));
        Assert.False(await rules.RemoveAsync("twitch", "bits", "", 2));
        Assert.True(await rules.RemoveAsync("twitch", "bits", "", 1));
        Assert.Null(await rules.FindAsync("twitch", "bits", ""));
        var removed = Assert.Single(await rules.ListAsync()); Assert.False(removed.Enabled); Assert.Equal(2, removed.Version);
        Assert.False(await rules.SetAsync("twitch", "bits", "", 2m, 0));
        Assert.False(await rules.SetAsync("twitch", "bits", "", 2m, 1));
        Assert.True(await rules.SetAsync("twitch", "bits", "", 0m, 2));
        var reactivated = Assert.Single(await new ValuationRuleStore(contexts).ListAsync());
        Assert.True(reactivated.Enabled); Assert.Equal(3, reactivated.Version); Assert.Equal(0m, await rules.FindAsync("twitch", "bits", ""));
        Assert.Equal("500", Assert.Single(await ledger.TotalsAsync(new(null, null))).UsdAmountMinor);
        await using var db = await contexts.CreateDbContextAsync();
        Assert.Equal(1, await db.FinancialAudits.CountAsync(row => row.Operation == "nominal_rule_remove"));
    }

    [Fact]
    public async Task FailedRemovalAuditDoesNotDisableTheRule()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var rules = new ValuationRuleStore(contexts);
        await rules.SetAsync("twitch", "subscription", "prime", 300m, 0);
        await using var db = await contexts.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_maintenance_audit BEFORE INSERT ON FinancialAudits BEGIN SELECT RAISE(ABORT, 'owned maintenance audit failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => rules.RemoveAsync("twitch", "subscription", "prime", 1));
        Assert.Equal(300m, await rules.FindAsync("twitch", "subscription", "prime"));
        Assert.Equal(1, Assert.Single(await rules.ListAsync()).Version);
    }

    [Fact]
    public async Task ExplicitCacheRefreshKeepsManualPrecedenceAndFrozenHistory()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var provider = new MutableRates();
        var rates = new CachedCurrencyRates(provider, contexts); var ledger = new FinancialStore(contexts);
        var original = await rates.GetRateAsync("EUR", Day);
        await ledger.AcceptAsync(Contribution(new("donation", 1, new(1000, "EUR", 2))), original);
        await rates.SetManualAsync("EUR", Day, 1.5m);
        provider.Value = 2m;
        Assert.True(await rates.RefreshAsync("EUR", Day));
        Assert.Equal(1.5m, (await rates.GetRateAsync("EUR", Day))!.UsdPerNativeUnit);
        var entries = await rates.ListAsync();
        Assert.Equal("2", entries.Single(row => row.Origin == "frankfurter").UsdPerNativeUnit);
        Assert.Equal("1.5", entries.Single(row => row.Origin == "manual").UsdPerNativeUnit);
        Assert.True(await rates.RemoveManualAsync("EUR", Day));
        Assert.Equal(2m, (await new CachedCurrencyRates(provider, contexts).GetRateAsync("EUR", Day))!.UsdPerNativeUnit);
        Assert.Equal("1170", Assert.Single(await ledger.TotalsAsync(new(null, null))).UsdAmountMinor);
        await using var db = await contexts.CreateDbContextAsync();
        Assert.Equal(1, await db.FinancialAudits.CountAsync(row => row.Operation == "fx_cache_refresh"));
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task UnavailableOrMismatchedRefreshCannotEraseTheCache()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var provider = new MutableRates();
        var rates = new CachedCurrencyRates(provider, contexts);
        await rates.GetRateAsync("EUR", Day);
        provider.Value = null;
        Assert.False(await rates.RefreshAsync("EUR", Day));
        Assert.Equal(1.17m, (await rates.GetRateAsync("EUR", Day))!.UsdPerNativeUnit);
        provider.Value = 2m; provider.Currency = "GBP";
        await Assert.ThrowsAsync<ArgumentException>(() => rates.RefreshAsync("EUR", Day));
        Assert.Equal("1.17", Assert.Single(await rates.ListAsync()).UsdPerNativeUnit);
        await using var db = await contexts.CreateDbContextAsync(); Assert.Equal(0, await db.FinancialAudits.CountAsync());
    }

    [Fact]
    public async Task FailedRefreshAuditRollsBackTheProviderObservation()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var provider = new MutableRates();
        var rates = new CachedCurrencyRates(provider, contexts);
        await rates.GetRateAsync("EUR", Day); provider.Value = 2m;
        await using var db = await contexts.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_refresh_audit BEFORE INSERT ON FinancialAudits BEGIN SELECT RAISE(ABORT, 'owned refresh audit failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => rates.RefreshAsync("EUR", Day));
        Assert.Equal("1.17", Assert.Single(await rates.ListAsync()).UsdPerNativeUnit);
    }

    private sealed class MutableRates : ICurrencyRateProvider
    {
        public int Calls { get; private set; }
        public decimal? Value { get; set; } = 1.17m;
        public string? Currency { get; set; }
        public Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<CurrencyRate?>(Value is { } value ? new(Currency ?? currency, date, date, value, "owned-provider", false) : null);
        }
    }
}
