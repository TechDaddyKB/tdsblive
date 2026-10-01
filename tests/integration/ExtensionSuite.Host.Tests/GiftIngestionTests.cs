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

public sealed class GiftIngestionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task KickNormalizedEventsSurviveRawDisabledAndReorderedReconnect(bool individualFirst)
    {
        using var factory = new FoundationHostFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.RemoveAll<IIsolatedIntegration>()));
        using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var rules = new ValuationRuleStore(contexts);
        Assert.True(await rules.SetAsync("kick", "gift", "", 500m, 0));
        var normalizer = new StreamerBotEventNormalizer(new SensitiveValues());
        var individualEnvelope = JsonNode.Parse("""
            {"event":{"source":"Kick","type":"GiftSubscription"},"timeStamp":"2026-01-05T12:00:01Z",
             "data":{"user":{"id":"owned-giver","name":"Owned fixture"},"recipient":{"id":"owned-r1"},
             "subscribedAt":"2026-01-05T12:00:00Z","expiresAt":"2026-02-05T12:00:00Z","isAnonymous":false,"isTest":false}}
            """)!.AsObject();
        var batchEnvelope = JsonNode.Parse("""
            {"event":{"source":"Kick","type":"MassGiftSubscription"},"timeStamp":"2026-01-05T12:00:01Z",
             "data":{"user":{"id":"owned-giver","name":"Owned fixture"},"recipients":[{"id":"owned-r1"},{"id":"owned-r2"}],
             "subscribedAt":"2026-01-05T12:00:00Z","expiresAt":"2026-02-05T12:00:00Z","isAnonymous":false,"isTest":false}}
            """)!.AsObject();
        var events = app.Services.GetRequiredService<EventStore>();
        var projection = new FinancialProjection(contexts, new(contexts), new NoRates(), rules);
        var first = normalizer.Normalize(individualFirst ? individualEnvelope : batchEnvelope, DateTimeOffset.UtcNow).Event!;
        var second = normalizer.Normalize(individualFirst ? batchEnvelope : individualEnvelope, DateTimeOffset.UtcNow).Event!;
        Assert.True(await events.AcceptAsync(first, "1", retainRaw: false));
        await events.MarkDeliveredAsync(first.Id, default);
        Assert.Equal(1, await projection.ProcessBatchAsync());
        Assert.True(await events.AcceptAsync(second, "2", retainRaw: false));
        Assert.Equal(1, await new FinancialProjection(contexts, new(contexts), new NoRates(), rules).ProcessBatchAsync());
        batchEnvelope["timeStamp"] = "2026-01-05T12:10:00Z";
        batchEnvelope["data"]!["recipients"] = new JsonArray(new JsonObject { ["id"] = "owned-r2" }, new JsonObject { ["id"] = "owned-r1" });
        var repeated = normalizer.Normalize(batchEnvelope, DateTimeOffset.UtcNow).Event!;
        Assert.False(await events.AcceptAsync(repeated, "3", retainRaw: false));
        Assert.Equal(0, await projection.ProcessBatchAsync());
        Assert.Equal("1000", Assert.Single(await new FinancialStore(contexts).TotalsAsync(new(null, null))).UsdAmountMinor);
        Assert.All(await events.ReadAsync(), item => Assert.Null(item.Raw));
        await using var db = await contexts.CreateDbContextAsync();
        Assert.Equal(2, await db.FinancialProjectionReceipts.CountAsync());
        Assert.Equal(2, await db.GiftAccountingClaims.CountAsync());
    }

    private sealed class NoRates : ICurrencyRateProvider
    {
        public Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Gift facts must not cause an external rate lookup.");
    }
}
