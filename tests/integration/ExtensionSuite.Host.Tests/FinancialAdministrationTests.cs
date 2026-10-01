using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class FinancialAdministrationTests
{
    [Fact]
    public async Task FailedSettingsAuditLeavesThePreviousTimezoneAndVersion()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var settings = new FinancialSettingsStore(contexts);
        await settings.SaveAsync(new("America/Chicago"));
        await using var db = await contexts.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_settings_audit BEFORE INSERT ON FinancialAudits BEGIN SELECT RAISE(ABORT, 'owned settings failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => settings.SaveAsync(new("UTC", Version: 1)));
        Assert.Equal(new FinancialSettings("America/Chicago", Version: 1), await new FinancialSettingsStore(contexts).GetAsync());
    }

    [Fact]
    public async Task FailedUnlinkAuditRollsBackNewSupporterAndAllAttribution()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var ledger = new FinancialStore(contexts);
        await ledger.AcceptAsync(new CanonicalEvent { Source = "owned-fixture", Platform = "twitch", Type = "support.donation", NativeType = "Fixture.Support",
            DedupeKey = "owned-unlink", User = new("owned-id"), Support = new("donation", 1, new(500, "USD", 2)) });
        await using var db = await contexts.CreateDbContextAsync(); var identity = await db.SupporterIdentities.AsNoTracking().SingleAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_unlink_audit BEFORE INSERT ON FinancialAudits BEGIN SELECT RAISE(ABORT, 'owned unlink failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => ledger.TransferIdentityAsync(identity.Id, identity.SupporterId, null));
        Assert.Equal(1, await db.Supporters.CountAsync());
        Assert.Equal(identity.SupporterId, (await db.SupporterIdentities.AsNoTracking().SingleAsync()).SupporterId);
        Assert.Equal(identity.SupporterId, (await db.FinancialEvents.AsNoTracking().SingleAsync()).SupporterId);
    }

    [Fact]
    public async Task MetadataMigrationPreservesTierAndDoesNotChangeMoneyOrUnverifiedGiftState()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var ledger = new FinancialStore(contexts);
        var item = new CanonicalEvent { Source = "owned-fixture", Platform = "twitch", Type = "support.subscription", NativeType = "Fixture.Support",
            DedupeKey = "owned-backfill", User = new("owned-user"), Support = new("subscription", 1, Tier: "1000") };
        await ledger.AcceptAsync(item, nominalUsdMinorPerUnit: 500m);
        await ledger.AcceptAsync(item with { Id = Guid.CreateVersion7(), DedupeKey = "owned-invalid-json", Support = new("gift", 5) });
        await using var db = await contexts.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE FinancialEvents SET GiftTier = '', GiftRole = 'none', GiftScopeKey = NULL, GiftSenderKey = NULL;");
        var malformed = "{";
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE FinancialEvents SET MetadataJson = {malformed} WHERE DedupeKey = {"owned-invalid-json"};");
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261001223937_ValuationRuleLifecycle");
        await migrator.MigrateAsync();
        var subscription = await db.FinancialEvents.AsNoTracking().SingleAsync(row => row.Type == "subscription");
        Assert.Equal("1000", subscription.GiftTier); Assert.Equal("id:owned-user", subscription.GiftSenderKey);
        Assert.Equal(500, subscription.UsdAmountMinor); Assert.Equal(1, subscription.Version);
        var gift = await db.FinancialEvents.AsNoTracking().SingleAsync(row => row.Type == "gift");
        Assert.Equal("gated", gift.AccountingState); Assert.Null(gift.UsdAmountMinor);
        Assert.Equal("none", gift.GiftRole);
    }
}
