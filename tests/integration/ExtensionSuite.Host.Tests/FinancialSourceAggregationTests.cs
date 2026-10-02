using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Finance;
using ExtensionSuite.Host;
using ExtensionSuite.StreamerBot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class FinancialSourceAggregationTests
{
    [Fact]
    public async Task AllRequiredSupportFamiliesCombineOnlyAfterExplicitIdentityLinking()
    {
        using var factory = new FoundationHostFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.RemoveAll<IIsolatedIntegration>()));
        using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var rules = new ValuationRuleStore(contexts);
        foreach (var rule in new[] { ("twitch", "bits", "", 1m), ("twitch", "subscription", "1000", 500m),
            ("twitch", "gift", "1000", 500m), ("youtube", "membership", "gold", 700m),
            ("youtube", "gift", "gold", 700m), ("kick", "subscription", "", 450m), ("kick", "gift", "", 450m) })
            Assert.True(await rules.SetAsync(rule.Item1, rule.Item2, rule.Item3, rule.Item4, 0));
        var normalizer = new StreamerBotEventNormalizer(new SensitiveValues());
        var timestamp = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);
        CanonicalEvent Normalize(string platform, string type, string fields)
        {
            var data = JsonNode.Parse(fields)!.AsObject();
            data["eventId"] = "owned-" + platform + "-" + type;
            data["user"] = new JsonObject { ["id"] = "owned-person", ["name"] = "Same name" };
            data["broadcaster"] = new JsonObject { ["id"] = "owned-channel" };
            return normalizer.Normalize(new JsonObject { ["event"] = new JsonObject { ["source"] = platform, ["type"] = type },
                ["data"] = data, ["timeStamp"] = timestamp.ToString("O") }, timestamp).Event!;
        }
        var items = new List<CanonicalEvent>
        {
            Normalize("Twitch", "Cheer", "{\"bits\":100,\"message\":\"Owned support message\"}"),
            Normalize("Twitch", "Sub", "{\"subTier\":\"1000\"}"),
            Normalize("Twitch", "GiftSub", "{\"subTier\":\"1000\",\"fromCommunitySubGift\":false}"),
            Normalize("YouTube", "SuperChat", "{\"microAmount\":10000000,\"currencyCode\":\"USD\"}"),
            Normalize("YouTube", "SuperSticker", "{\"microAmount\":20000000,\"currencyCode\":\"USD\"}"),
            Normalize("YouTube", "NewSponsor", "{\"tier\":\"gold\"}"),
            Normalize("YouTube", "MembershipGift", "{\"tier\":\"gold\",\"count\":2}"),
            Normalize("Kick", "Subscription", "{}"),
            Normalize("Kick", "GiftSubscription", "{\"recipient\":{\"id\":\"owned-recipient\"},\"subscribedAt\":\"2026-01-05T12:00:00Z\",\"expiresAt\":\"2026-02-05T12:00:00Z\"}")
        };
        var kofi = normalizer.Normalize(JsonNode.Parse("""
            {"event":{"source":"General","type":"Custom"},"data":{"tdsbliveForwardedSource":"Kofi","tdsbliveForwardedType":"Donation",
            "payload":{"messageId":"owned-kofi","amount":"12.50","currency":"USD","userId":"owned-person","userName":"Same name","timestamp":"2026-01-05T12:00:00Z"}}}
            """)!.AsObject(), timestamp).Event!;
        items.Add(kofi);
        // RumbleEngine's observed paid-Rant contract supplies native USD cents.
        items.Add(new CanonicalEvent { Source = "rumble", Platform = "rumble", Type = "support.donation", NativeType = "Rant",
            NativeId = "owned-rant", DedupeKey = "owned-rant", OccurredAt = timestamp, ReceivedAt = timestamp,
            User = new("owned-person", null, "Same name", null, [], false), Support = new("rant", 1, new(200, "USD", 2)) });
        var events = app.Services.GetRequiredService<EventStore>();
        foreach (var item in items) Assert.True(await events.AcceptAsync(item, "owned", retainRaw: false));
        var ledger = new FinancialStore(contexts);
        var projection = new FinancialProjection(contexts, ledger, new NoExternalRates(), rules);
        Assert.Equal(11, await projection.ProcessBatchAsync());
        Assert.Equal(5, (await ledger.TotalsAsync(new(null, null))).Length);
        var identities = await new FinancialReadStore(contexts).IdentitiesAsync();
        var target = identities.First(row => row.Platform == "twitch").SupporterId;
        foreach (var identity in identities.Where(row => row.SupporterId != target))
            await ledger.LinkIdentityAsync(identity.Id, target);
        var total = Assert.Single(await ledger.TotalsAsync(new(null, null)));
        Assert.Equal("8550", total.UsdAmountMinor);
        Assert.Equal("4450", total.ExactAmountMinor);
        Assert.Equal("4100", total.NominalAmountMinor);
        Assert.Equal(11, total.ContributionCount);
        Assert.Equal(0, total.UnknownCount);
        Assert.Equal(0, total.GatedCount);
        Assert.Equal(0, await projection.ProcessBatchAsync());
        await using var db = await contexts.CreateDbContextAsync();
        Assert.Contains("Owned support message", (await db.FinancialEvents.SingleAsync(row => row.Type == "bits")).MetadataJson);
    }

    private sealed class NoExternalRates : ICurrencyRateProvider
    {
        public Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("USD and configured nominal fixtures require no external request.");
    }
}
