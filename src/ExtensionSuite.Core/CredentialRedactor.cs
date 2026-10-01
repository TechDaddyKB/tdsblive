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

    public static string Text(string text, IEnumerable<string>? knownSecrets = null)
    {
        foreach (var secret in knownSecrets ?? [])
            if (!string.IsNullOrEmpty(secret)) text = text.Replace(secret, Replacement, StringComparison.Ordinal);
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
                result[item.Key] = SensitiveKey().IsMatch(item.Key) ? JsonValue.Create(Replacement) : Json(item.Value, knownSecrets);
            return result;
        }
        if (value is JsonArray array) return new JsonArray(array.Select(item => Json(item, knownSecrets)).ToArray());
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text)) return JsonValue.Create(Text(text, knownSecrets));
        return value?.DeepClone();
    }
}
