using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Finance;
using ExtensionSuite.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class FinancialEndpointTests
{
    private static readonly DateTimeOffset Occurred = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private static CanonicalEvent Paid(string key, string platform = "twitch", SupportDetails? support = null) => new()
    {
        Source = "owned-fixture", Platform = platform, Type = "support.donation", NativeType = "Fixture.Donation",
        NativeId = key, DedupeKey = key, OccurredAt = Occurred, User = new("owned-id", DisplayName: "Same label"),
        Support = support ?? new("donation", 1, new(500, "USD", 2))
    };
    private static async Task Csrf(HttpClient http) => http.DefaultRequestHeaders.Add("X-TDSBLive-CSRF",
        (await http.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"))!.RequestToken);

    [Fact]
    public async Task LedgerHttpPreservesIntegerPrecisionAndLinkUnlinkChangesAttribution()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient(); await Csrf(http);
        var ledger = app.Services.GetRequiredService<FinancialStore>();
        await ledger.AcceptAsync(Paid("large", support: new("donation", 1, new(long.MaxValue, "USD", 2))));
        await ledger.AcceptAsync(Paid("second", "youtube"));
        var json = await http.GetFromJsonAsync<JsonElement>("/api/financial/ledger");
        var large = json.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("nativeEventId").GetString() == "large");
        Assert.Equal(JsonValueKind.String, large.GetProperty("usdAmountMinor").ValueKind);
        Assert.Equal("9223372036854775807", large.GetProperty("usdAmountMinor").GetString());
        Assert.Equal("1", large.GetProperty("quantity").GetString());
        var identities = (await http.GetFromJsonAsync<FinancialIdentity[]>("/api/financial/identities"))!;
        Assert.Equal(2, identities.Length); Assert.NotEqual(identities[0].SupporterId, identities[1].SupporterId);
        var source = identities.Single(row => row.Platform == "youtube"); var target = identities.Single(row => row.Platform == "twitch");
        var linked = await http.PostAsJsonAsync($"/api/financial/identities/{source.Id}/link", new IdentityLinkRequest(source.SupporterId, target.SupporterId));
        linked.EnsureSuccessStatusCode();
        var totals = (await http.GetFromJsonAsync<FinancialTotalsResponse>("/api/financial/totals"))!;
        Assert.Equal("9223372036854776307", Assert.Single(totals.Supporters).UsdAmountMinor);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/financial/identities/{source.Id}/unlink", new IdentityUnlinkRequest(source.SupporterId))).StatusCode);
        var unlinked = await http.PostAsJsonAsync($"/api/financial/identities/{source.Id}/unlink", new IdentityUnlinkRequest(target.SupporterId));
        unlinked.EnsureSuccessStatusCode();
        Assert.Equal(2, (await http.GetFromJsonAsync<FinancialTotalsResponse>("/api/financial/totals"))!.Supporters.Length);
        Assert.Single((await http.GetFromJsonAsync<FinancialIdentity[]>("/api/financial/identities?search=owned-id&limit=1"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/financial/ledger?limit=501")).StatusCode);
    }

    [Fact]
    public async Task SettingsPersistImmediatelyAndCustomDatesUseTheConfiguredTimezone()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient(); await Csrf(http);
        await app.Services.GetRequiredService<FinancialStore>().AcceptAsync(Paid("timezone"));
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/financial/totals?period=current-stream")).StatusCode);
        var saved = await http.PutAsJsonAsync("/api/financial/settings", new FinancialSettings("America/Chicago", Occurred, 0));
        saved.EnsureSuccessStatusCode();
        Assert.Equal(1, (await saved.Content.ReadFromJsonAsync<FinancialSettings>())!.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync("/api/financial/settings", new FinancialSettings("UTC", null, 0))).StatusCode);
        var contexts = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        Assert.Equal("America/Chicago", (await new FinancialSettingsStore(contexts).GetAsync()).TimeZone);
        var yesterday = (await http.GetFromJsonAsync<FinancialTotalsResponse>("/api/financial/totals?period=custom&start=2026-01-04&endExclusive=2026-01-05"))!;
        Assert.Equal("500", Assert.Single(yesterday.Supporters).UsdAmountMinor);
        var nextDay = (await http.GetFromJsonAsync<FinancialTotalsResponse>("/api/financial/totals?period=custom&start=2026-01-05&endExclusive=2026-01-06"))!;
        Assert.Empty(nextDay.Supporters);
        Assert.Equal("500", Assert.Single((await http.GetFromJsonAsync<FinancialTotalsResponse>("/api/financial/totals?period=current-stream"))!.Supporters).UsdAmountMinor);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync("/api/financial/settings", new FinancialSettings("not/a-zone", null, 1))).StatusCode);
    }

    [Fact]
    public async Task BulkReconciliationRequiresSelectedVersionsAndCannotBypassGiftGating()
    {
        using var app = new FoundationHostFactory(); using var http = app.CreateClient(); await Csrf(http);
        var ledger = app.Services.GetRequiredService<FinancialStore>();
        await ledger.AcceptAsync(Paid("fx", support: new("donation", 1, new(1000, "EUR", 2))));
        await ledger.AcceptAsync(Paid("bits", support: new("bits", 500)));
        await ledger.AcceptAsync(Paid("gift", support: new("gift", 5)));
        var page = (await http.GetFromJsonAsync<FinancialLedgerPage>("/api/financial/ledger"))!;
        (await http.PutAsJsonAsync("/api/financial/rates/override", new ManualRateUpdate("EUR", new(2026, 1, 5), "1.25"))).EnsureSuccessStatusCode();
        (await http.PutAsJsonAsync("/api/financial/rules", new NominalRuleUpdate("twitch", "bits", "", "1", 0))).EnsureSuccessStatusCode();
        var request = new FinancialReconcileRequest(page.Items.Select(row => new FinancialVersionRef(row.Id, row.Version)).ToArray());
        var response = await http.PostAsJsonAsync("/api/financial/reconcile", request); response.EnsureSuccessStatusCode();
        var results = (await response.Content.ReadFromJsonAsync<FinancialReconcileResult[]>())!;
        Assert.Equal(2, results.Count(row => row.Outcome == "reconciled")); Assert.Single(results, row => row.Outcome == "gated");
        Assert.Equal("1750", Assert.Single((await http.GetFromJsonAsync<FinancialTotalsResponse>("/api/financial/totals"))!.Supporters).UsdAmountMinor);
        var stale = await http.PostAsJsonAsync("/api/financial/reconcile", request);
        Assert.Equal(2, (await stale.Content.ReadFromJsonAsync<FinancialReconcileResult[]>())!.Count(row => row.Outcome == "conflict"));
        (await http.PutAsJsonAsync("/api/financial/rates/override", new ManualRateUpdate("EUR", new(2026, 1, 5), "2"))).EnsureSuccessStatusCode();
        Assert.Equal("1750", Assert.Single((await http.GetFromJsonAsync<FinancialTotalsResponse>("/api/financial/totals"))!.Supporters).UsdAmountMinor);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/financial/reconcile", new FinancialReconcileRequest([]))).StatusCode);
        var diagnostic = (await http.GetFromJsonAsync<FinancialProjectionDiagnostics>("/api/financial/diagnostics"))!;
        Assert.Equal(0, diagnostic.PendingConversions); Assert.Equal(1, diagnostic.GatedContributions);
    }

    [Fact]
    public async Task RateAndRuleMaintenanceHttpContractsAreExplicitAndRejectInvalidDecimals()
    {
        using var factory = new FoundationHostFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIsolatedIntegration>();
            services.AddSingleton(provider => new CachedCurrencyRates(new OwnedRates(), provider.GetRequiredService<IDbContextFactory<FoundationDbContext>>()));
        }));
        using var http = app.CreateClient(); await Csrf(http);
        var lookup = await http.PostAsJsonAsync("/api/financial/rates/lookup", new RateRequest("EUR", new(2026, 1, 5)));
        Assert.Equal("1.17", (await lookup.Content.ReadFromJsonAsync<RateLookupResponse>())!.Rate!.UsdPerNativeUnit);
        var refreshed = await http.PostAsJsonAsync("/api/financial/rates/refresh", new RateRequest("EUR", new(2026, 1, 5)));
        Assert.True((await refreshed.Content.ReadFromJsonAsync<RateRefreshResponse>())!.Refreshed);
        Assert.Single((await http.GetFromJsonAsync<CachedRateEntry[]>("/api/financial/rates"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync("/api/financial/rates/override", new ManualRateUpdate("EUR", new(2026, 1, 5), "NaN"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync("/api/financial/rules", new NominalRuleUpdate("twitch", "bits", "", "1e2", 0))).StatusCode);
        (await http.PutAsJsonAsync("/api/financial/rules", new NominalRuleUpdate("twitch", "bits", "", "0", 0))).EnsureSuccessStatusCode();
        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/financial/rules") { Content = JsonContent.Create(new NominalRuleRemoval("twitch", "bits", "", 1)) };
        (await http.SendAsync(delete)).EnsureSuccessStatusCode();
        var removed = Assert.Single((await http.GetFromJsonAsync<NominalValuationRule[]>("/api/financial/rules"))!);
        Assert.False(removed.Enabled); Assert.Equal(2, removed.Version);
    }

    [Fact]
    public async Task ScopedOverlayTokenCannotReadFinanceAndWritesRequireCsrf()
    {
        using var app = new FoundationHostFactory(true); using var admin = app.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", app.AdminCredential);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync("/api/financial/settings", new FinancialSettings())).StatusCode);
        await Csrf(admin);
        (await admin.PostAsJsonAsync("/api/overlays", new OverlayDefinition { Id = "owned-finance-scope" })).EnsureSuccessStatusCode();
        var token = (await (await admin.PostAsJsonAsync("/api/overlays/owned-finance-scope/tokens", new CreateOverlayToken())).Content.ReadFromJsonAsync<CreatedOverlayToken>())!;
        using var viewer = app.CreateClient(); viewer.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        viewer.DefaultRequestHeaders.Add("X-TDSBLive-Overlay", "owned-finance-scope");
        foreach (var route in new[] { "ledger", "totals", "identities", "settings", "rules", "rates", "diagnostics" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/financial/" + route)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/financial/ledger")).StatusCode);
    }

    private sealed class OwnedRates : ICurrencyRateProvider
    {
        public Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult<CurrencyRate?>(new(currency, date, date, 1.17m, "owned-fixture", false));
    }
}
