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
        if (value is JsonObject obj) return ObjectShape(obj, depth);
        if (value is JsonArray array) return new JsonArray(array.Take(100).Select(item => Shape(item, depth + 1)).ToArray());
        if (value is null) return null;
        return value?.GetValueKind() switch
        {
            System.Text.Json.JsonValueKind.String => JsonValue.Create("sample"),
            System.Text.Json.JsonValueKind.Number => JsonValue.Create(0),
            System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => JsonValue.Create(false),
            _ => null
        };
    }

    private static JsonObject ObjectShape(JsonObject source, int depth)
    {
        var result = new JsonObject();
        var ordinal = 0;
        foreach (var field in source)
        {
            var key = PublicFields.Contains(field.Key) ? field.Key : "unknownField" + ++ordinal;
            while (result.ContainsKey(key) || source.ContainsKey(key) && key != field.Key) key = "unknownField" + ++ordinal;
            result[key] = Shape(field.Value, depth + 1);
        }
        return result;
    }
}
