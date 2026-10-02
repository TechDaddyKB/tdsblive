using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExtensionSuite.Host;
using ExtensionSuite.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace ExtensionSuite.Host.Tests;

public sealed class SetupEndpointTests
{
    [Fact]
    public async Task SessionPasswordIsProtectedAndNeverWrittenToConfiguration()
    {
        using var factory = new FoundationHostFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        var value = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/integrations/streamerbot/credential", new StreamerCredentialUpdate(value))).StatusCode);
        using var csrf = JsonDocument.Parse(await client.GetStringAsync("/api/auth/csrf"));
        client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf.RootElement.GetProperty("requestToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/integrations/streamerbot/credential", new StreamerCredentialUpdate(""))).StatusCode);
        using var response = await client.PostAsJsonAsync("/api/integrations/streamerbot/credential", new StreamerCredentialUpdate(value));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        Assert.Equal(value, factory.Services.GetRequiredService<SensitiveValues>().Get("streamerbot-password"));
        Assert.DoesNotContain(value, await client.GetStringAsync("/api/configuration"));
        Assert.False(File.Exists(Path.Combine(factory.DirectoryPath, "credentials", "streamerbot-password.dpapi")));
    }

    [Fact]
    public async Task ProgressPersistsAndRejectsStaleOrInvalidChanges()
    {
        using var factory = new FoundationHostFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        Assert.Equal(new SetupProgress(), await client.GetFromJsonAsync<SetupProgress>("/api/setup"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/setup", new SetupProgress(2))).StatusCode);
        using var csrf = JsonDocument.Parse(await client.GetStringAsync("/api/auth/csrf"));
        client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf.RootElement.GetProperty("requestToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/setup", new SetupProgress(6))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/setup", new SetupProgress(1, true))).StatusCode);
        using var saved = await client.PutAsJsonAsync("/api/setup", new SetupProgress(2));
        saved.EnsureSuccessStatusCode();
        Assert.Equal(new SetupProgress(2, false, 1), await saved.Content.ReadFromJsonAsync<SetupProgress>());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/setup", new SetupProgress(3))).StatusCode);
        using var reopened = factory.WithWebHostBuilder(_ => { });
        using var second = reopened.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        Assert.Equal(new SetupProgress(2, false, 1), await second.GetFromJsonAsync<SetupProgress>("/api/setup"));
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/setup", new SetupProgress(5, true, 1))).StatusCode);
        Assert.Equal(new SetupProgress(5, true, 2), await client.GetFromJsonAsync<SetupProgress>("/api/setup"));
    }
}
