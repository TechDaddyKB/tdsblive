using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class RecoveryEndpointTests
{
    [Theory]
    [InlineData("/api/recovery/backup")]
    [InlineData("/api/recovery/validate")]
    [InlineData("/api/recovery/restore")]
    [InlineData("/api/configuration/export")]
    [InlineData("/api/configuration/import")]
    [InlineData("/api/application/restart")]
    [InlineData("/api/application/quit")]
    public async Task AuthenticatedLanCannotRunOwnerOnlyOperations(string path)
    {
        using var factory = new FoundationHostFactory(remotePeer: true);
        using var client = await ClientAsync(factory);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.AdminCredential);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/status")).StatusCode);
        using var response = await client.PostAsJsonAsync(path, new { id = Guid.NewGuid(), confirm = true });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/status")).StatusCode);
    }

    private static async Task<HttpClient> ClientAsync(FoundationHostFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        using var csrf = await client.GetAsync("/api/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", document.RootElement.GetProperty("requestToken").GetString());
        client.DefaultRequestHeaders.Add("Origin", "http://127.0.0.1");
        return client;
    }

    [Fact]
    public async Task DownloadedBackupCanBeValidatedWithoutReplacingData()
    {
        using var factory = new FoundationHostFactory();
        using var client = await ClientAsync(factory);
        using var download = await client.PostAsync("/api/recovery/backup", null);
        download.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", download.Content.Headers.ContentType?.MediaType);
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
        using var validation = await client.PostAsync("/api/recovery/validate", new ByteArrayContent(bytes));
        validation.EnsureSuccessStatusCode();
        using var preview = JsonDocument.Parse(await validation.Content.ReadAsStringAsync());
        Assert.NotEqual(Guid.Empty, preview.RootElement.GetProperty("id").GetGuid());
        Assert.True(preview.RootElement.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
        using var status = await client.GetAsync("/api/application/status");
        using var state = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("operation").ValueKind);
    }

    [Fact]
    public async Task InvalidUploadAndUnconfirmedRestoreLeaveHostRunning()
    {
        using var factory = new FoundationHostFactory();
        using var client = await ClientAsync(factory);
        using var invalid = await client.PostAsync("/api/recovery/validate", new ByteArrayContent([1, 2, 3]));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var unconfirmed = await client.PostAsJsonAsync("/api/recovery/restore", new { id = Guid.NewGuid(), confirm = false });
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        using var unknown = await client.PostAsJsonAsync("/api/recovery/restore", new { id = Guid.NewGuid(), confirm = true });
        Assert.Equal(HttpStatusCode.Conflict, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/status")).StatusCode);
    }

    [Fact]
    public async Task BackupWriteRequiresRequestProtection()
    {
        using var factory = new FoundationHostFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        using var response = await client.PostAsync("/api/recovery/backup", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
