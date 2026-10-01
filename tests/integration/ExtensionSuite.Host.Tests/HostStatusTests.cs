using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class HostStatusTests(FoundationHostFactory factory)
    : IClassFixture<FoundationHostFactory>
{
    [Fact]
    public async Task EditorAndLoginServeBuiltReactAssetsOverHttp()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        foreach (var route in new[] { "/editor", "/login" })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            var script = System.Text.RegularExpressions.Regex.Match(html, "src=\"([^\"]+\\.js)\"").Groups[1].Value;
            Assert.StartsWith("/editor/assets/", script);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(script)).StatusCode);
        }
    }

    [Fact]
    public async Task StatusIsAvailableOverHttpWithoutCertificateOrRedirect()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://127.0.0.1"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/api/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using var status = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("TDSBLive", status.RootElement.GetProperty("name").GetString());
        Assert.True(status.RootElement.GetProperty("httpSupported").GetBoolean());
    }
}
