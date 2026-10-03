using System.Text.Json.Nodes;

namespace ExtensionSuite.Core;

// Shape exports deliberately discard values, not just recognized credentials.
// Unknown property names can themselves be private IDs or credentials.
public static class DiagnosticSanitizer
{
    private static readonly HashSet<string> PublicFields = new(StringComparer.Ordinal)
    {
        "data", "event", "events", "user", "username", "display_name", "displayName", "id", "type", "name",
        "message", "text", "timestamp", "created_on", "amount", "currency", "cents", "user_id", "avatar",
        "livestreams", "livestream", "followers", "subscribers", "subscriptions", "gifted_subscriptions",
        "chat", "messages", "rants", "latest_followers", "latest_subscribers", "recent_messages", "recent_rants",
        "is_live", "viewers", "likes", "title", "url", "raw", "payload", "classification", "outcome", "status",
        "level", "category", "eventId", "exceptionType", "receivedAt", "occurredAt", "monetary", "stream", "support"
    };

    public static JsonNode? Shape(JsonNode? value, int depth = 0)
    {
        if (depth > 32) return JsonValue.Create("[depth limit]");
        if (value is JsonObject obj)
        {
            var result = new JsonObject();
            var ordinal = 0;
            foreach (var field in obj)
            {
                var key = PublicFields.Contains(field.Key) ? field.Key : "unknownField" + ++ordinal;
                while (result.ContainsKey(key) || obj.ContainsKey(key) && key != field.Key) key = "unknownField" + ++ordinal;
                result[key] = Shape(field.Value, depth + 1);
            }
            return result;
        }
        if (value is JsonArray array) return new JsonArray(array.Take(100).Select(item => Shape(item, depth + 1)).ToArray());
        if (value is null) return null;
        if (value is JsonValue scalar)
        {
            if (scalar.GetValueKind() == System.Text.Json.JsonValueKind.String) return JsonValue.Create("sample");
            if (scalar.GetValueKind() == System.Text.Json.JsonValueKind.Number) return JsonValue.Create(0);
            if (scalar.GetValueKind() is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False) return JsonValue.Create(false);
        }
        return null;
    }
}
