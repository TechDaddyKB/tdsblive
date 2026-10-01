using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class HostStatusTests(FoundationHostFactory factory)
    : IClassFixture<FoundationHostFactory>
{
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
