using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Host;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class HostSecurityTests
{
    private static HttpClient Client(FoundationHostFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("http://127.0.0.1"), AllowAutoRedirect = false });

    private static async Task CsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", json.RootElement.GetProperty("requestToken").GetString());
    }

    [Fact]
    public async Task UntrustedHostAndCrossOriginAreRejected()
    {
        using var factory = new FoundationHostFactory();
        using var client = Client(factory);
        using var untrusted = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        untrusted.Headers.Host = "attacker.invalid";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(untrusted)).StatusCode);
        using var crossOrigin = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        crossOrigin.Headers.Add("Origin", "http://attacker.invalid");
        var response = await client.SendAsync(crossOrigin);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        using var crossSite = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        crossSite.Headers.Add("Sec-Fetch-Site", "cross-site");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(crossSite)).StatusCode);
    }

    [Fact]
    public async Task ConfigurationWritesRequireCsrfAndSurviveNewHost()
    {
        using var factory = new FoundationHostFactory();
        using var client = Client(factory);
        var config = new ApplicationConfiguration { DisplayName = "Configured streamer" };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/configuration", config)).StatusCode);
        await CsrfAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/configuration", config)).StatusCode);
        var configuration = await client.GetFromJsonAsync<ApplicationConfiguration>("/api/configuration");
        Assert.Equal("Configured streamer", configuration!.DisplayName);
        var invalid = config with { Server = new ServerConfiguration { Host = "0.0.0.0", EnableLan = false } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/configuration", invalid)).StatusCode);
        Assert.Equal("Configured streamer", (await client.GetFromJsonAsync<ApplicationConfiguration>("/api/configuration"))!.DisplayName);
        using var reopened = factory.WithWebHostBuilder(_ => { });
        using var reopenedClient = reopened.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        using var status = JsonDocument.Parse(await reopenedClient.GetStringAsync("/api/status"));
        Assert.Equal("Configured streamer", status.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task LanRequiresAuthenticationAndRevokesCookieSession()
    {
        using var factory = new FoundationHostFactory(remotePeer: true);
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/status")).StatusCode);
        await CsrfAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new AdminLogin("incorrect"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new AdminLogin(factory.AdminCredential))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/status")).StatusCode);
        // Antiforgery identity is unchanged; session authentication is explicit middleware state.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/status")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.AdminCredential);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/auth/provision", null)).StatusCode);
    }

    [Fact]
    public async Task TestEventDefaultsToIsolationAndCannotBecomeLive()
    {
        using var factory = new FoundationHostFactory();
        using var client = Client(factory);
        await CsrfAsync(client);
        var item = new CanonicalEvent { Source = "internal", Platform = "system", Type = "test", NativeType = "test",
            DedupeKey = "test", OccurredAt = DateTimeOffset.UtcNow, Provenance = EventProvenance.Live };
        using var response = await client.PostAsJsonAsync("/api/test-event", new TestEventRequest(item));
        response.EnsureSuccessStatusCode();
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(result.RootElement.GetProperty("persisted").GetBoolean());
        Assert.False(result.RootElement.GetProperty("liveActionsAllowed").GetBoolean());
        Assert.Equal("simulation", result.RootElement.GetProperty("provenance").GetString());
        Assert.Empty((await client.GetFromJsonAsync<CanonicalEvent[]>("/api/events"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/events?limit=501")).StatusCode);
    }
}
