using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Finance;
using ExtensionSuite.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class FinancialProjectionTests
{
    private static CanonicalEvent Paid(string key, EventProvenance provenance = EventProvenance.Live) => new()
    {
        Source = "owned-fixture", Platform = "youtube", Type = "support.donation", NativeType = "Fixture.SuperChat",
        NativeId = key, DedupeKey = key, OccurredAt = new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero),
        User = new("owned-user"), Support = new("donation", 1, new(1000, "EUR", 2)), Provenance = provenance
    };

    [Fact]
    public async Task AcknowledgedEventsCatchUpWithoutRawAndRestartCannotDoubleCount()
    {
        using var factory = new FoundationHostFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.RemoveAll<IIsolatedIntegration>()));
        using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var events = app.Services.GetRequiredService<EventStore>();
        var item = Paid("owned-1");
        Assert.True(await events.AcceptAsync(item, "1", retainRaw: false));
        await events.MarkDeliveredAsync(item.Id, default);
        var rates = new FixtureRates();
        var projection = new FinancialProjection(contexts, new(contexts), rates, new(contexts));
        Assert.Equal(1, await projection.ProcessBatchAsync());
        var reopened = new FinancialProjection(contexts, new(contexts), rates, new(contexts));
        Assert.Equal(0, await reopened.ProcessBatchAsync());
        await using var db = await contexts.CreateDbContextAsync();
        var row = Assert.Single(await db.FinancialEvents.ToArrayAsync());
        Assert.Equal(1170, row.UsdAmountMinor);
        Assert.Equal("fx", row.ValuationMethod);
        Assert.Equal("processed", Assert.Single(await db.FinancialProjectionReceipts.ToArrayAsync()).State);
        Assert.Equal(1, rates.Calls);
    }

    [Fact]
    public async Task MissingTypedFactsAndCorruptRowsDoNotBlockLiveSupportOrProcessReplay()
    {
        using var factory = new FoundationHostFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.RemoveAll<IIsolatedIntegration>()));
        using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var events = app.Services.GetRequiredService<EventStore>();
        await events.AcceptAsync(Paid("legacy") with { Support = null }, "1");
        await events.AcceptAsync(Paid("replay", EventProvenance.Replay), "2", persistTest: true);
        await events.AcceptAsync(Paid("valid"), "3");
        await using var db = await contexts.CreateDbContextAsync();
        db.Events.Add(new StoredEvent { Id = Guid.CreateVersion7(), Source = "owned-fixture", Provenance = "Live",
            DedupeKey = "corrupt", Type = "support.donation", OccurredAtTicks = 1, Json = "{" });
        await db.SaveChangesAsync();
        var projection = new FinancialProjection(contexts, new(contexts), new FixtureRates(), new(contexts));
        Assert.Equal(3, await projection.ProcessBatchAsync());
        Assert.Equal(1, await db.FinancialEvents.CountAsync());
        var states = await db.FinancialProjectionReceipts.Select(row => row.State).ToArrayAsync();
        Assert.Contains("quarantined", states);
        Assert.Contains("unsupported", states);
        Assert.Contains("processed", states);
        Assert.Equal(0, await projection.ProcessBatchAsync());
    }

    [Fact]
    public async Task ReceiptFailureRetriesTheFrozenLedgerWriteInsteadOfRepricing()
    {
        using var factory = new FoundationHostFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.RemoveAll<IIsolatedIntegration>()));
        using var client = app.CreateClient();
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        await app.Services.GetRequiredService<EventStore>().AcceptAsync(Paid("receipt-failure"), "1");
        await using var db = await contexts.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_projection BEFORE INSERT ON FinancialProjectionReceipts BEGIN SELECT RAISE(ABORT, 'owned receipt failure'); END;");
        var rates = new FixtureRates();
        var projection = new FinancialProjection(contexts, new(contexts), rates, new(contexts));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => projection.ProcessBatchAsync());
        Assert.Equal(1, await db.FinancialEvents.CountAsync());
        Assert.Empty(await db.FinancialProjectionReceipts.ToArrayAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_projection;");
        rates.Value = 2m;
        Assert.Equal(1, await projection.ProcessBatchAsync());
        Assert.Equal(1170, (await db.FinancialEvents.SingleAsync()).UsdAmountMinor);
        Assert.Equal(1, rates.Calls);
    }

    private sealed class FixtureRates : ICurrencyRateProvider
    {
        public int Calls { get; private set; }
        public decimal Value { get; set; } = 1.17m;
        public Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<CurrencyRate?>(new(currency, date, date, Value, "owned-fixture", false));
        }
    }
}
