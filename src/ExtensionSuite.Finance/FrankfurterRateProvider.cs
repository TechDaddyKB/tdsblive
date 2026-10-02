using System.Globalization;
using System.Text.Json;
using ExtensionSuite.Core;

namespace ExtensionSuite.Finance;

public sealed class FrankfurterRateProvider(HttpClient http) : ICurrencyRateProvider
{
    private const int MaximumResponseBytes = 65536;

    public async Task<CurrencyRate?> GetRateAsync(string currency, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (!CurrencyCode.IsValid(currency)) throw new ArgumentException("Invalid currency code.");
        if (currency == "USD") return new("USD", date, date, 1m, "USD", false);
        var historical = await FetchAsync(currency, date, false, cancellationToken);
        return historical.Rate ?? (historical.HistoricalUnavailable
            ? (await FetchAsync(currency, date, true, cancellationToken)).Rate : null);
    }

    private async Task<(CurrencyRate? Rate, bool HistoricalUnavailable)> FetchAsync(string currency, DateOnly date,
        bool latest, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var suffix = latest ? "" : "?date=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = new Uri("https://api.frankfurter.dev/v2/rate/" + currency.ToLowerInvariant() + "/usd" + suffix);
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return (null, true);
            if (!response.IsSuccessStatusCode) return (null, false);
            if (response.RequestMessage?.RequestUri is { } actual && (actual.Scheme != "https" || actual.Host != "api.frankfurter.dev"))
                return (null, false);
            if (response.Content.Headers.ContentLength > MaximumResponseBytes) return (null, false);
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream(); var chunk = new byte[4096];
            while (true)
            {
                var read = await stream.ReadAsync(chunk, deadline.Token);
                if (read == 0) break;
                if (buffer.Length + read > MaximumResponseBytes) return (null, false);
                await buffer.WriteAsync(chunk.AsMemory(0, read), deadline.Token);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.GetProperty("base").GetString() != currency ||
                root.GetProperty("quote").GetString() != "USD" || !root.GetProperty("rate").TryGetDecimal(out var rate) || rate <= 0 ||
                !DateOnly.TryParseExact(root.GetProperty("date").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var observed))
                return (null, false);
            if (!latest && observed > date) return (null, false);
            return (new(currency, date, observed, rate, "frankfurter:v2:blended", latest), false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return (null, false); }
        catch (HttpRequestException) { return (null, false); }
        catch (IOException) { return (null, false); }
        catch (JsonException) { return (null, false); }
        catch (KeyNotFoundException) { return (null, false); }
        catch (InvalidOperationException) { return (null, false); }
    }
}
