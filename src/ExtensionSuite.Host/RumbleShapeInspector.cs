using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExtensionSuite.Host;

public sealed class RumbleShapeInspector
{
    private HashSet<string> previous = [];
    public JsonObject Inspect(JsonObject payload)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        Visit(payload, "$", fields, 0);
        var added = fields.Except(previous).Order(StringComparer.Ordinal).ToArray();
        var removed = previous.Except(fields).Order(StringComparer.Ordinal).ToArray();
        var unknown = fields.Except(RumbleObservedShapes.Fields).Order(StringComparer.Ordinal).ToArray();
        previous = fields;
        return new JsonObject
        {
            ["observedFields"] = Array(fields.Order(StringComparer.Ordinal)), ["unknownFields"] = Array(unknown),
            ["addedFields"] = Array(added), ["removedFields"] = Array(removed),
            ["limitation"] = "Compared with the G00 capture field/type baseline; unknown fields are evidence, not authoritative subscription or gift events."
        };
    }

    private static JsonArray Array(IEnumerable<string> values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    private static void Visit(JsonNode? node, string path, HashSet<string> fields, int depth)
    {
        if (depth > 20 || fields.Count >= 512 || path.Length > 512) return;
        var kind = node?.GetValueKind() switch
        {
            JsonValueKind.Object => "object", JsonValueKind.Array => "array", JsonValueKind.String => "string",
            JsonValueKind.Number => "number", JsonValueKind.True or JsonValueKind.False => "boolean", _ => "null"
        };
        fields.Add(path + ":" + kind);
        if (node is JsonObject obj) foreach (var property in obj) Visit(property.Value, path + "[" + JsonSerializer.Serialize(property.Key) + "]", fields, depth + 1);
        if (node is JsonArray array) foreach (var item in array.Take(100)) Visit(item, path + "[]", fields, depth + 1);
    }
}
