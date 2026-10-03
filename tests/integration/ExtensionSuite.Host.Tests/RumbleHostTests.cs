using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using ExtensionSuite.Rumble;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class RumbleHostTests
{
    private static string Credential => "https://rumble.com/-livestream-api/get-data?" + "key=synthetic-host-test";
    [Fact]
    public async Task CredentialEndpointsAreProtectedSessionOnlyAndNeverEchoCredentials()
    {
        using var transport = new HttpClient(new Handler(() => new JsonObject { ["type"] = "user", ["user_id"] = "synthetic-account", ["livestreams"] = new JsonArray() }));
        await using var factory = new FoundationHostFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton(new RumbleHttpTransport(transport, TimeProvider.System))));
        using var client = configured.CreateClient();
        Assert.Equal("disabled", (await client.GetFromJsonAsync<RumbleStatus>("/api/rumble/status"))!.State);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/rumble/credential", new RumbleCredentialRequest(Credential))).StatusCode);
        await Protect(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/rumble/credential", new RumbleCredentialRequest("invalid"))).StatusCode);
        if (!OperatingSystem.IsWindows()) Assert.Equal(HttpStatusCode.NotImplemented, (await client.PostAsJsonAsync("/api/rumble/credential", new RumbleCredentialRequest(Credential, false))).StatusCode);
        else
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/rumble/credential", new RumbleCredentialRequest(Credential, false))).StatusCode);
            Assert.Equal(Credential, await configured.Services.GetRequiredService<WindowsSecretVault>().GetAsync("rumble-url"));
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/rumble/credential", new RumbleCredentialRequest(Credential))).StatusCode);
        Assert.DoesNotContain(Credential, await client.GetStringAsync("/api/rumble/status"));
        Assert.True(configured.Services.GetRequiredService<RumbleIntegration>().Status.CredentialPresent);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/rumble/reset-baseline", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/rumble/deliveries")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/rumble/credential")).StatusCode);
        Assert.Null(configured.Services.GetRequiredService<SensitiveValues>().Get("rumble-url"));
    }

    [Fact]
    public async Task HostedPollingPersistsNewEventsRedactsSnapshotsAndResetsWithoutAutomation()
    {
        var snapshotCount = 1;
        using var transport = new HttpClient(new Handler(() =>
        {
            // Polling does not itself create new chat. Advance the owned snapshot explicitly,
            // so a concurrent background poll cannot invent an event during reset assertions.
            var index = Volatile.Read(ref snapshotCount);
            var messages = new JsonArray();
            for (var count = 1; count <= index; count++) messages.Add(new JsonObject { ["username"] = "synthetic-user", ["created_on"] = $"2026-01-01T00:00:{count:00}Z", ["text"] = "synthetic", ["stream_key"] = "synthetic-private" });
            return new JsonObject { ["type"] = "user", ["user_id"] = "synthetic-account", ["livestreams"] = new JsonArray(new JsonObject {
                ["id"] = "synthetic-stream", ["is_live"] = true, ["chat"] = new JsonObject { ["recent_messages"] = messages, ["recent_rants"] = new JsonArray() } }) };
        }));
        await using var factory = new FoundationHostFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton(new RumbleHttpTransport(transport, TimeProvider.System))));
        using var client = configured.CreateClient(); await Protect(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/rumble/credential", new RumbleCredentialRequest(Credential))).StatusCode);
        var rumble = configured.Services.GetRequiredService<RumbleIntegration>();
        var events = configured.Services.GetRequiredService<EventStore>();
        await rumble.PollOnceAsync(default); // Background worker may have completed the baseline already.
        Volatile.Write(ref snapshotCount, 2);
        await rumble.PollOnceAsync(default);
        Assert.True(rumble.Status.BaselineEstablished); Assert.Equal("healthy", rumble.Status.State);
        Assert.True(rumble.Status.PollLatencyMilliseconds >= 0);
        Assert.Equal(7, rumble.Status.PollIntervalSeconds);
        Assert.True(rumble.Status.AcceptedEventsThisProcess > 0);
        Assert.True(rumble.Status.DuplicateRecords > 0);
        Assert.Null(rumble.Status.Viewers);
        Assert.Contains(await events.ReadAsync(), item => item.Type == "chat.message");
        var before = (await events.ReadAsync()).Count(item => item.Type == "chat.message");
        await rumble.ResetBaselineAsync(default); await rumble.PollOnceAsync(default);
        Assert.Equal(before, (await events.ReadAsync()).Count(item => item.Type == "chat.message"));
        Assert.Empty(await configured.Services.GetRequiredService<RumbleStore>().PendingTriggersAsync(default));
        Assert.DoesNotContain("synthetic-private", System.Text.Json.JsonSerializer.Serialize(configured.Services.GetRequiredService<EventInspectorStore>().Read()));
        var inspector = configured.Services.GetRequiredService<EventInspectorStore>();
        var snapshot = inspector.Read().First(item => item.Classification == "rumble.snapshot");
        var sanitized = await client.GetStringAsync($"/api/inspector/{snapshot.Id}/sanitized-fixture");
        Assert.Contains("shapeOnly", sanitized); Assert.DoesNotContain("synthetic-user", sanitized);
        Assert.DoesNotContain("synthetic-stream", sanitized); Assert.DoesNotContain(Credential, sanitized);
        await rumble.DisconnectAsync(default); await rumble.PollOnceAsync(default); Assert.False(rumble.Status.CredentialPresent);
    }

    [Theory]
    [InlineData(4, 15, 2)]
    [InlineData(7, 0, 2)]
    [InlineData(7, 61, 2)]
    [InlineData(7, 15, 1)]
    [InlineData(86401, 15, 2)]
    public void RejectsUnsafePollingConfiguration(int interval, int timeout, int confirmations)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ApplicationConfiguration { Rumble = new() { PollIntervalSeconds = interval,
            AdvancedSlowerPolling = true, RequestTimeoutSeconds = timeout, OfflineConfirmationPolls = confirmations } }.Validate());
    }

    [Fact]
    public async Task FailedCommitReportsHealthAndKeepsBaselinePending()
    {
        using var transport = new HttpClient(new Handler(() => new JsonObject { ["type"] = "user", ["user_id"] = "synthetic-account", ["livestreams"] = new JsonArray() }));
        var config = new ApplicationConfiguration(); var sensitive = new SensitiveValues(); var inspector = new EventInspectorStore(config);
        var rumble = new RumbleIntegration(new FailedStore(), new RumbleHttpTransport(transport, TimeProvider.System), config, sensitive, inspector, TimeProvider.System);
        using var services = new ServiceCollection().BuildServiceProvider();
        await rumble.SetCredentialAsync(Credential, true, services, default);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = rumble.RunAsync(lifetime.Token);
        while (rumble.Status.State != "processingFailure") await Task.Delay(20, lifetime.Token);
        Assert.False(rumble.Status.BaselineEstablished); Assert.Contains(inspector.Read(), item => item.Classification == "rumble.processingFailure");
        await lifetime.CancelAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(3)));
    }
    private sealed class FailedStore : IRumbleStore
    {
        public Task<RumbleState> LoadAsync(string credentialContext, EventProvenance provenance, CancellationToken cancellationToken) => Task.FromResult(new RumbleState());
        public Task<CanonicalEvent[]> CommitAsync(string credentialContext, EventProvenance provenance, RumbleBatch batch, bool retainRaw, bool forwardTriggers, CancellationToken cancellationToken) =>
            throw new IOException("synthetic commit failure");
    }
    private static async Task Protect(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"); client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf!.RequestToken);
    }
    private sealed class Handler(Func<JsonObject> snapshot) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(snapshot().ToJsonString()) });
    }
}
