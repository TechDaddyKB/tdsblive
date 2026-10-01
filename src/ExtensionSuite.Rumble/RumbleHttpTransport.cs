using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExtensionSuite.Rumble;

public sealed class RumbleHttpTransport(HttpClient client, TimeProvider clock, int timeoutSeconds = 15)
{
    public const int MaximumResponseBytes = 16 * 1024 * 1024;
    public static bool ValidCredential(string value) => value.Length <= 4096 && Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.Host.Equals("rumble.com", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort &&
        uri.AbsolutePath == "/-livestream-api/get-data" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) &&
        CredentialKey(value) is { Length: > 0 } && !value.Any(char.IsControl);

    public static string? CredentialKey(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        var keys = uri.Query.TrimStart('?').Split('&').Where(part => part.StartsWith("key=", StringComparison.Ordinal)).ToArray();
        return keys.Length == 1 ? Uri.UnescapeDataString(keys[0][4..]) : null;
    }

    public async Task<RumblePoll> PollAsync(string credential, CancellationToken cancellationToken)
    {
        if (!ValidCredential(credential)) return new(clock.GetUtcNow(), "invalidCredential");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            var upstream = new UriBuilder("https", "rumble.com") { Path = "/-livestream-api/get-data", Query = new Uri(credential).Query };
            using var request = new HttpRequestMessage(HttpMethod.Get, upstream.Uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            var now = clock.GetUtcNow();
            var retry = response.Headers.RetryAfter;
            var after = retry?.Delta ?? (retry?.Date is { } date ? date - now : null);
            if (!response.IsSuccessStatusCode) return new(now, "http_error", (int)response.StatusCode, RetryAfter: after);
            if (response.Content.Headers.ContentLength > MaximumResponseBytes) return new(now, "responseTooLarge", (int)response.StatusCode);
            await using var body = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var bytes = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count = await body.ReadAsync(buffer, deadline.Token)) != 0)
            {
                if (bytes.Length + count > MaximumResponseBytes) return new(now, "responseTooLarge", (int)response.StatusCode);
                bytes.Write(buffer, 0, count);
            }
            try
            {
                var parsed = JsonNode.Parse(bytes.ToArray(), documentOptions: new JsonDocumentOptions { MaxDepth = 32 });
                return parsed is JsonObject payload ? new(now, "ok", (int)response.StatusCode, payload) : new(now, "unexpected_json", (int)response.StatusCode);
            }
            catch (JsonException) { return new(now, "invalid_json", (int)response.StatusCode); }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(clock.GetUtcNow(), "timeout"); }
        catch (HttpRequestException) { return new(clock.GetUtcNow(), "network_error"); }
        catch (IOException) { return new(clock.GetUtcNow(), "network_error"); }
    }

    public static TimeSpan NextDelay(int intervalSeconds, int failures, TimeSpan? retryAfter, double jitter)
    {
        if (intervalSeconds < 5 || jitter is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        var normal = (double)intervalSeconds;
        var backoff = failures == 0 ? normal : Math.Max(normal, Math.Min(300, normal * Math.Pow(2, Math.Min(failures, 16))));
        var seconds = backoff * (1 + jitter * .1);
        return TimeSpan.FromSeconds(Math.Max(seconds, Math.Max(0, retryAfter?.TotalSeconds ?? 0)));
    }

    public static async Task WaitDelayAsync(TimeSpan delay, TimeProvider clock, CancellationToken cancellationToken)
    {
        // Task.Delay timers have a finite duration range. Preserve long Retry-After
        // values rather than failing and accidentally retrying the request early.
        while (delay > TimeSpan.Zero)
        {
            var slice = delay > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : delay;
            await Task.Delay(slice, clock, cancellationToken);
            delay -= slice;
        }
    }

    public static DateTimeOffset ScheduledAt(DateTimeOffset now, TimeSpan delay) =>
        delay >= DateTimeOffset.MaxValue - now ? DateTimeOffset.MaxValue : now + delay;
}
