using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using ExtensionSuite.Rumble;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class RumbleTransportTests
{
    private static string Credential => "https://rumble.com/-livestream-api/get-data?" + "key=synthetic-test";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    [Theory]
    [InlineData("http://rumble.com/-livestream-api/get-data?key=synthetic", false)]
    [InlineData("https://rumble.com.attacker.invalid/-livestream-api/get-data?key=synthetic", false)]
    [InlineData("https://rumble.com:8443/-livestream-api/get-data?key=synthetic", false)]
    [InlineData("https://rumble.com/account?key=synthetic", false)]
    [InlineData("https://rumble.com/-livestream-api/get-data?key=synthetic#fragment", false)]
    [InlineData("https://rumble.com/-livestream-api/get-data?key=synthetic", true)]
    [InlineData("https://rumble.com/-livestream-api/get-data?key=", false)]
    [InlineData("https://rumble.com/-livestream-api/get-data?key=synthetic&key=other", false)]
    public void CredentialDestinationIsConstrained(string value, bool expected) => Assert.Equal(expected, RumbleHttpTransport.ValidCredential(value));

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(302)]
    public async Task ErrorsAndRedirectsIgnoreBodiesAndHonorRetryAfter(int status)
    {
        using var client = new HttpClient(new Handler((_, _) =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new UnreadableContent() };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(600));
            return Task.FromResult(response);
        }));
        var poll = await new RumbleHttpTransport(client, new Clock()).PollAsync(Credential, default);
        Assert.Equal("http_error", poll.Outcome); Assert.Equal(status, poll.HttpStatus); Assert.Null(poll.Payload);
        Assert.True(RumbleHttpTransport.NextDelay(7, 20, poll.RetryAfter, 0) >= TimeSpan.FromSeconds(600));
    }

    [Fact]
    public async Task HttpDateRetryAfterUsesInjectedClock()
    {
        using var client = new HttpClient(new Handler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddMinutes(10)); return Task.FromResult(response);
        }));
        Assert.Equal(TimeSpan.FromMinutes(10), (await new RumbleHttpTransport(client, new Clock()).PollAsync(Credential, default)).RetryAfter);
    }

    [Theory]
    [InlineData("{invalid", "invalid_json")]
    [InlineData("[]", "unexpected_json")]
    [InlineData("{\"future\":true}", "ok")]
    public async Task ParsesOnlyBoundedJsonObjects(string body, string outcome)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })));
        var result = await new RumbleHttpTransport(client, new Clock()).PollAsync(Credential, default);
        Assert.Equal(outcome, result.Outcome); Assert.Equal(Now, result.ObservedAt);
    }

    [Fact]
    public async Task RejectsAdvertisedAndChunkedOversizedBodies()
    {
        using var declared = new HttpClient(new Handler((_, _) =>
        {
            var content = new UnreadableContent(); content.Headers.ContentLength = RumbleHttpTransport.MaximumResponseBytes + 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }));
        Assert.Equal("responseTooLarge", (await new RumbleHttpTransport(declared, new Clock()).PollAsync(Credential, default)).Outcome);
        using var chunked = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StreamContent(new MemoryStream(new byte[RumbleHttpTransport.MaximumResponseBytes + 1])) })));
        Assert.Equal("responseTooLarge", (await new RumbleHttpTransport(chunked, new Clock()).PollAsync(Credential, default)).Outcome);
    }

    [Fact]
    public async Task TimeoutNetworkFailureAndShutdownAreDistinguished()
    {
        using var hanging = new HttpClient(new Handler(async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new HttpResponseMessage(); }));
        Assert.Equal("timeout", (await new RumbleHttpTransport(hanging, new Clock(), 1).PollAsync(Credential, default)).Outcome);
        using var stop = new CancellationTokenSource(); await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RumbleHttpTransport(hanging, new Clock()).PollAsync(Credential, stop.Token));
        using var failed = new HttpClient(new Handler((_, _) => throw new HttpRequestException("synthetic failure")));
        Assert.Equal("network_error", (await new RumbleHttpTransport(failed, new Clock()).PollAsync(Credential, default)).Outcome);
        Assert.Equal("invalidCredential", (await new RumbleHttpTransport(failed, new Clock()).PollAsync("invalid", default)).Outcome);
    }

    [Fact]
    public void SchedulingHasPositiveJitterBoundedBackoffAndRecovery()
    {
        Assert.Equal(TimeSpan.FromSeconds(7), RumbleHttpTransport.NextDelay(7, 0, null, 0));
        Assert.Equal(TimeSpan.FromSeconds(7.7), RumbleHttpTransport.NextDelay(7, 0, null, 1));
        Assert.Equal(TimeSpan.FromSeconds(14), RumbleHttpTransport.NextDelay(7, 1, null, 0));
        Assert.Equal(TimeSpan.FromSeconds(300), RumbleHttpTransport.NextDelay(7, 20, null, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RumbleHttpTransport.NextDelay(4, 0, null, 0));
    }

    [Fact]
    public void ParserPreservesRedactedUnknownShapesAndRejectsAmbiguousStreamContainers()
    {
        var sensitive = new ExtensionSuite.Core.SensitiveValues(); sensitive.Set("test", "synthetic-sensitive-value");
        var payload = JsonNode.Parse("""{"type":"user","user_id":"synthetic-account","livestreams":[],"followers":false,"subscribers":7,"future":{"nested":"synthetic-sensitive-value","stream_key":"synthetic-private"}}""")!.AsObject();
        var snapshot = RumbleSnapshotParser.Parse(payload, "context", sensitive);
        Assert.Equal("[REDACTED]", snapshot.Raw["future"]!["nested"]!.GetValue<string>());
        Assert.Equal("[REDACTED]", snapshot.Raw["future"]!["stream_key"]!.GetValue<string>());
        payload["livestreams"] = new JsonArray(new JsonObject { ["id"] = "stream", ["is_live"] = "unknown" });
        Assert.Throws<RumbleShapeException>(() => RumbleSnapshotParser.Parse(payload, "context", sensitive));
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request, cancellationToken);
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class UnreadableContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new InvalidOperationException("Error body must not be read.");
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
