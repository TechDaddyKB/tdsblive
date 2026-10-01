using System.Text.Json.Nodes;
using ExtensionSuite.Core;

namespace ExtensionSuite.StreamerBot;

public static class ChatMediaNormalizer
{
    public static EventMessage? Normalize(string? text, JsonObject data, string platform)
    {
        var parts = ReadParts(data["parts"] as JsonArray, platform);
        if (parts.Length == 1 && parts[0].Kind == "gif" && parts[0].Text.Length == 0)
            return new(text ?? "", [parts[0] with { Text = text ?? "[GIF]" }]);
        if (parts.Length > 0 && (text is null || string.Concat(parts.Select(p => p.Text)) == text))
            return new(text ?? string.Concat(parts.Select(p => p.Text)), parts);
        if (text is null) return null;
        var spans = new List<(int Start, int End, EventMessagePart Part)>();
        if (data["emotes"] is JsonArray emotes)
            foreach (var item in emotes.OfType<JsonObject>().Take(128))
            {
                var start = Integer(item, "startIndex", "StartIndex"); var end = Integer(item, "endIndex", "EndIndex");
                var image = Image(String(item, "imageUrl", "ImageUrl"));
                if (start is null || end is null || start < 0 || end < start || end >= text.Length || image is null) continue;
                var original = text[start.Value..(end.Value + 1)]; var name = String(item, "name", "Name");
                if (name is not null && name != original || char.IsLowSurrogate(text[start.Value]) || char.IsHighSurrogate(text[end.Value])) continue;
                spans.Add((start.Value, end.Value + 1, new("emote", original, image, String(item, "type", "Type") ?? platform, Flag(item, "zeroWidth", "ZeroWidth"))));
            }
        var result = new List<EventMessagePart>(); var position = 0;
        foreach (var span in spans.OrderBy(s => s.Start))
        {
            if (span.Start < position) continue;
            if (span.Start > position) result.Add(new("text", text[position..span.Start]));
            result.Add(span.Part); position = span.End;
        }
        if (position < text.Length) result.Add(new("text", text[position..]));
        // A GIF attachment must be explicitly typed; ordinary links never become media.
        result.AddRange(parts.Where(p => p.Kind == "gif"));
        return new(text, result.Any(p => p.Kind != "text") ? result.ToArray() : null);
    }

    private static EventMessagePart[] ReadParts(JsonArray? items, string platform)
    {
        if (items is null || items.Count > 256) return [];
        var result = new List<EventMessagePart>();
        foreach (var item in items.OfType<JsonObject>())
        {
            var kind = String(item, "type", "Type")?.ToLowerInvariant();
            var text = String(item, "text", "Text") ?? String(item, "name", "Name") ?? "";
            if (kind == "text") { result.Add(new("text", text)); continue; }
            var gif = kind == "gif" || String(item, "gifId", "GifId") is not null;
            var image = Image(String(item, "imageUrl", "ImageUrl") ?? (gif ? String(item, "url", "Url") : null));
            if (image is not null) result.Add(new(gif ? "gif" : "emote", text, image, String(item, "source", "Source") ?? platform, Flag(item, "zeroWidth", "ZeroWidth")));
            else if (text.Length > 0) result.Add(new("text", text));
        }
        return result.ToArray();
    }
    private static string? Image(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) ? value : null;
    private static string? String(JsonObject item, string camel, string pascal) => (item[camel] ?? item[pascal]) is JsonValue value && value.TryGetValue<string>(out var text) && text.Length <= 16384 ? text : null;
    private static int? Integer(JsonObject item, string camel, string pascal) => (item[camel] ?? item[pascal]) is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;
    private static bool Flag(JsonObject item, string camel, string pascal) => (item[camel] ?? item[pascal]) is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
}
