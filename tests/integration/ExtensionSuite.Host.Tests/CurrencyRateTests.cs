using System.Net;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class CurrencyRateTests
{
    private static readonly DateOnly Date = new(2026, 1, 5);
    private const string Historical = "{\"date\":\"2026-01-02\",\"base\":\"EUR\",\"quote\":\"USD\",\"rate\":1.23456789}";

    [Fact]
    public async Task HistoricalDecimalObservationAndLatestFallbackRetainDatesAndEstimation()
    {
        var requests = new List<Uri>();
        using var client = new HttpClient(new Handler((request, _) =>
        { requests.Add(request.RequestUri!); return Task.FromResult(Response(HttpStatusCode.OK, Historical)); }));
        var provider = new FrankfurterRateProvider(client); var rate = await provider.GetRateAsync("EUR", Date);
        Assert.NotNull(rate); Assert.Equal(1.23456789m, rate.UsdPerNativeUnit);
        Assert.Equal(new DateOnly(2026, 1, 2), rate.RateDate); Assert.Equal(Date, rate.RequestedDate); Assert.False(rate.Estimated);
        Assert.Equal("https://api.frankfurter.dev/v2/rate/eur/usd?date=2026-01-05", Assert.Single(requests).AbsoluteUri);
        Assert.Equal(1m, (await provider.GetRateAsync("USD", Date))!.UsdPerNativeUnit); Assert.Single(requests);
        var calls = 0;
        using var fallbackClient = new HttpClient(new Handler((request, _) =>
        {
            calls++; return Task.FromResult(request.RequestUri!.Query.Length > 0 ? Response(HttpStatusCode.NotFound, "{}") :
                Response(HttpStatusCode.OK, Historical.Replace("2026-01-02", "2026-01-06")));
        }));
        var latest = await new FrankfurterRateProvider(fallbackClient).GetRateAsync("EUR", Date);
        Assert.NotNull(latest); Assert.True(latest.Estimated); Assert.Equal(new DateOnly(2026, 1, 6), latest.RateDate); Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task TemporaryOrInvalidRequestsRemainPendingWithoutLatestRetry(HttpStatusCode status)
    {
        var calls = 0;
        using var client = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(Response(status, "{}")); }));
        Assert.Null(await new FrankfurterRateProvider(client).GetRateAsync("EUR", Date)); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("{\"date\":\"2026-01-02\",\"base\":\"JPY\",\"quote\":\"USD\",\"rate\":1}")]
    [InlineData("{\"date\":\"2026-01-06\",\"base\":\"EUR\",\"quote\":\"USD\",\"rate\":1}")]
    [InlineData("{\"date\":\"2026-01-02\",\"base\":\"EUR\",\"quote\":\"USD\",\"rate\":-1}")]
    [InlineData("{\"date\":\"2026-01-02\",\"base\":\"EUR\",\"quote\":\"USD\",\"rate\":\"1\"}")]
    public async Task MalformedMismatchedOrUntrustedRatesCannotBeAccepted(string json)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, json))));
        Assert.Null(await new FrankfurterRateProvider(client).GetRateAsync("EUR", Date));
    }

    [Fact]
    public async Task OversizedResponsesTransportFailureAndCallerCancellationAreBounded()
    {
        using var oversized = new HttpClient(new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, new string(' ', 65537)))));
        Assert.Null(await new FrankfurterRateProvider(oversized).GetRateAsync("EUR", Date));
        using var failed = new HttpClient(new Handler((_, _) => throw new HttpRequestException("owned transport failure")));
        Assert.Null(await new FrankfurterRateProvider(failed).GetRateAsync("EUR", Date));
        using var timedOut = new HttpClient(new Handler((_, _) => throw new TaskCanceledException()));
        Assert.Null(await new FrankfurterRateProvider(timedOut).GetRateAsync("EUR", Date));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var waiting = new HttpClient(new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Response(HttpStatusCode.OK, Historical); }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FrankfurterRateProvider(waiting).GetRateAsync("EUR", Date, cancelled.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => new FrankfurterRateProvider(waiting).GetRateAsync("eur", Date));
    }

    [Fact]
    public async Task PersistedCacheAndDatedOverridesDoNotRewriteAcceptedFinancialHistory()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>(); var remote = new FakeRates();
        var cache = new CachedCurrencyRates(remote, factory);
        Assert.Equal(1.25m, (await cache.GetRateAsync("EUR", Date))!.UsdPerNativeUnit); Assert.Equal(1, remote.Calls);
        var reopened = new CachedCurrencyRates(remote, factory);
        Assert.Equal(1.25m, (await reopened.GetRateAsync("EUR", Date))!.UsdPerNativeUnit); Assert.Equal(1, remote.Calls);
        await reopened.SetManualAsync("EUR", Date, 2m);
        var manual = await reopened.GetRateAsync("EUR", Date); Assert.Equal("manual", manual!.Provider); Assert.Equal(2m, manual.UsdPerNativeUnit);
        var ledger = new FinancialStore(factory); var item = new CanonicalEvent
        {
            Source = "owned-fx-fixture", Platform = "kofi", Type = "support.donation", NativeType = "Fixture.Donation", DedupeKey = "owned-fx-key",
            OccurredAt = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), Support = new("donation", 1, new(1000, "EUR", 2))
        };
        await ledger.AcceptAsync(item, manual);
        await reopened.SetManualAsync("EUR", Date, 3m);
        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(2000, (await db.FinancialEvents.SingleAsync()).UsdAmountMinor);
        Assert.True(await reopened.RemoveManualAsync("EUR", Date)); Assert.False(await reopened.RemoveManualAsync("EUR", Date));
        Assert.Equal(1.25m, (await reopened.GetRateAsync("EUR", Date))!.UsdPerNativeUnit); Assert.Equal(1, remote.Calls);
        Assert.Equal(3, await db.FinancialAudits.CountAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => reopened.SetManualAsync("USD", Date, 2m));
        await Assert.ThrowsAsync<ArgumentException>(() => reopened.SetManualAsync("EUR", Date, -1m));
        await Assert.ThrowsAsync<ArgumentException>(() => reopened.GetRateAsync("bad-code", Date));
    }

    [Fact]
    public async Task UnavailableRatesRemainUncachedAndOverrideWinsOverOverlappingNetworkLookup()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        var unavailable = new CachedCurrencyRates(new EmptyRates(), factory);
        Assert.Null(await unavailable.GetRateAsync("EUR", Date));
        await using var db = await factory.CreateDbContextAsync(); Assert.Equal(0, await db.FxRates.CountAsync());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new CachedCurrencyRates(new DelayedRates(entered, release), factory);
        var pending = cache.GetRateAsync("EUR", Date); await entered.Task;
        await cache.SetManualAsync("EUR", Date, 9m); release.SetResult();
        Assert.Equal(9m, (await pending)!.UsdPerNativeUnit);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string json) => new(status) { Content = new StringContent(json) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private sealed class FakeRates : ICurrencyRateProvider
    {
        public int Calls;
        public Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult<CurrencyRate?>(new(currency, date, date, 1.25m, "owned-provider", false)); }
    }
    private sealed class EmptyRates : ICurrencyRateProvider
    {
        public Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult<CurrencyRate?>(null);
    }
    private sealed class DelayedRates(TaskCompletionSource entered, TaskCompletionSource release) : ICurrencyRateProvider
    {
        public async Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default)
        { entered.SetResult(); await release.Task.WaitAsync(cancellationToken); return new(currency, date, date, 1m, "owned-provider", false); }
    }
}
