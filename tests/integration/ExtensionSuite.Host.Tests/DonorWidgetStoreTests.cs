using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class DonorWidgetStoreTests
{
    [Fact]
    public async Task SourceAndMinimumFiltersApplyBeforeRankLimitAndStreamTotal()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var ledger = new FinancialStore(factory); var widgets = new DonorWidgetStore(factory);
        await ledger.AcceptAsync(Paid("large", "twitch", 1000));
        await ledger.AcceptAsync(Paid("small", "youtube", 100));
        await ledger.AcceptAsync(Paid("nominal", "twitch", 0) with { Support = new("bits", 100), Type = "support.bits" }, nominalUsdMinorPerUnit: 1);
        var filtered = new OverlayWidget { Kind = "current-stream-total", Donor = new() { MinimumUsdMinor = "500" } };
        var snapshot = await widgets.SnapshotAsync(filtered, new(Now.AddHours(-1), Now.AddHours(1)), Now);
        Assert.Equal("1000", snapshot.TotalUsdMinor); Assert.Single(snapshot.Rows);
        var bits = await widgets.SnapshotAsync(filtered with { Donor = new() { EventTypes = ["bits"] } }, new(null, null), Now);
        Assert.Equal("100", bits.TotalUsdMinor); Assert.Equal(1, bits.EstimatedCount);
        var unvalued = Paid("unvalued", "kick", 0) with { OccurredAt = Now.AddSeconds(1), Support = new("subscription", 1), Type = "support.subscription" };
        await ledger.AcceptAsync(unvalued);
        var latest = await widgets.SnapshotAsync(new() { Kind = "latest-supporter" }, new(null, null), Now);
        var row = Assert.Single(latest.Rows); Assert.Equal("unvalued", row.Name); Assert.False(row.HasKnownAmount);
    }

    [Theory]
    [InlineData("https://example.invalid/avatar.png", true)]
    [InlineData("https://example.invalid/avatar.png?token=owned-sensitive-fixture", false)]
    [InlineData("https://user:password@example.invalid/avatar.png", false)]
    [InlineData("javascript:alert(1)", false)]
    public async Task AvatarProjectionDoesNotExposeCredentialBearingUrls(string url, bool accepted)
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var item = Paid("avatar", "twitch", 100) with { User = new("owned-avatar", DisplayName: "Avatar donor", AvatarUrl: url) };
        await new FinancialStore(factory).AcceptAsync(item);
        var snapshot = await new DonorWidgetStore(factory).SnapshotAsync(new() { Kind = "donor-crown" }, new(null, null), Now);
        Assert.Equal(accepted ? url : null, Assert.Single(snapshot.Rows).AvatarUrl);
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static CanonicalEvent Paid(string id, string platform, long amount) => new()
    {
        Source = "owned-donor-fixture", Platform = platform, Type = "support.donation", NativeType = "Fixture.Donation",
        NativeId = id, DedupeKey = id, OccurredAt = Now, User = new(id, DisplayName: id),
        Support = new("donation", 1, new(amount, "USD", 2))
    };

    [Fact]
    public async Task LinkedRankingsUpdateAndFilteredTotalsKeepIntegerPrecision()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var ledger = new FinancialStore(factory); var widgets = new DonorWidgetStore(factory);
        await ledger.AcceptAsync(Paid("alice", "twitch", long.MaxValue));
        await ledger.AcceptAsync(Paid("bob", "youtube", 1));
        var widget = new OverlayWidget { Kind = "donor-leaderboard" };
        var before = await widgets.SnapshotAsync(widget, new(null, null), Now);
        Assert.Equal("9223372036854775808", before.TotalUsdMinor); Assert.Equal(2, before.Rows.Length);
        await using var db = await factory.CreateDbContextAsync();
        var identities = await db.SupporterIdentities.OrderBy(row => row.Platform).ToArrayAsync();
        await ledger.LinkIdentityAsync(identities[1].Id, identities[0].SupporterId);
        var after = await widgets.SnapshotAsync(widget, new(null, null), Now);
        var linked = Assert.Single(after.Rows);
        Assert.Equal("9223372036854775808", linked.UsdAmountMinor);
        Assert.Equal(new[] { "twitch", "youtube" }, linked.Platforms);
        var filtered = await widgets.SnapshotAsync(widget with { Donor = new() { Platforms = ["youtube"] } }, new(null, null), Now);
        Assert.Equal("1", filtered.TotalUsdMinor);
        Assert.Empty((await widgets.SnapshotAsync(widget, new(Now.AddTicks(1), Now.AddDays(1)), Now)).Rows);
    }

    [Fact]
    public async Task UnknownAndGatedSupportCannotWinAndReplayCannotAlterSnapshot()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var ledger = new FinancialStore(factory); var widgets = new DonorWidgetStore(factory);
        await ledger.AcceptAsync(Paid("unknown", "twitch", 0) with { Support = new("subscription", 1) });
        await ledger.AcceptAsync(Paid("gift", "rumble", 0) with { Support = new("gift", 100), Type = "support.gift" });
        Assert.False(await ledger.AcceptAsync(Paid("replay", "twitch", 99999) with { Provenance = EventProvenance.Replay }));
        var snapshot = await widgets.SnapshotAsync(new() { Kind = "donor-crown" }, new(null, null), Now);
        Assert.Equal("pending", snapshot.State); Assert.Empty(snapshot.Rows);
        Assert.Equal("0", snapshot.TotalUsdMinor); Assert.Equal(1, snapshot.UnknownCount); Assert.Equal(1, snapshot.GatedCount);
    }
}
