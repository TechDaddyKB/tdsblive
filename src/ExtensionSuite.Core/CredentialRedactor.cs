using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ExtensionSuite.Core;

public static partial class CredentialRedactor
{
    public const string Replacement = "[REDACTED]";

    [GeneratedRegex(@"(?i)(?:password|passwd|secret|token|authorization|cookie|stream[_-]?key|api[_-]?key|rumble[_-]?(?:api[_-]?)?url)")]
    private static partial Regex SensitiveKey();

    [GeneratedRegex(@"(?i)https?://[^\s<>""']+")]
    private static partial Regex HttpUrl();

    [GeneratedRegex(@"(?i)(?:password|passwd|secret|token|stream[_-]?key|api[_-]?key)\s*[=:]\s*[^\s,;]+|Bearer\s+[^\s,;]+")]
    private static partial Regex CredentialAssignment();

    public static string Text(string text, IEnumerable<string>? knownSecrets = null)
    {
        foreach (var secret in knownSecrets ?? [])
            if (!string.IsNullOrEmpty(secret)) text = text.Replace(secret, Replacement, StringComparison.Ordinal);
        text = CredentialAssignment().Replace(text, Replacement);
        return HttpUrl().Replace(text, match =>
        {
            if (!Uri.TryCreate(match.Value, UriKind.Absolute, out var uri)) return Replacement;
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.Host.EndsWith("rumble.com", StringComparison.OrdinalIgnoreCase)) return Replacement;
            return match.Value;
        });
    }

    public static JsonNode? Json(JsonNode? value, IEnumerable<string>? knownSecrets = null)
    {
        if (value is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var item in obj)
            {
                if (SensitiveKey().IsMatch(item.Key)) result[item.Key] = JsonValue.Create(Replacement);
                else if (item.Key is "url" or "imageUrl" && IsGif(obj) && item.Value is JsonValue urlValue && urlValue.TryGetValue<string>(out var url) && IsPublicGifUrl(url, knownSecrets))
                    result[item.Key] = JsonValue.Create(url);
                else result[item.Key] = Json(item.Value, knownSecrets);
            }
            return result;
        }
        if (value is JsonArray array) return new JsonArray(array.Select(item => Json(item, knownSecrets)).ToArray());
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text)) return JsonValue.Create(Text(text, knownSecrets));
        return value?.DeepClone();
    }

    private static bool IsGif(JsonObject value) => (value["kind"] ?? value["type"]) is JsonValue scalar && scalar.TryGetValue<string>(out var kind) && kind == "gif";
    private static bool IsPublicGifUrl(string value, IEnumerable<string>? knownSecrets)
    {
        if (value.Length > 2048 || CredentialAssignment().IsMatch(value) || (knownSecrets ?? []).Any(secret => !string.IsNullOrEmpty(secret) && value.Contains(secret, StringComparison.Ordinal))) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0) return false;
        if (!new[] { "media.giphy.com", "media0.giphy.com", "media1.giphy.com", "media2.giphy.com", "media3.giphy.com", "media4.giphy.com", "i.giphy.com" }.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)) return false;
        if (!uri.AbsolutePath.StartsWith("/media/", StringComparison.Ordinal) || !uri.AbsolutePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)) return false;
        return uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).All(pair =>
            new[] { "cid", "ep", "rid", "ct" }.Contains(Uri.UnescapeDataString(pair.Split('=', 2)[0]), StringComparer.Ordinal));
    }
}
