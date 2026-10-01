using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;

namespace ExtensionSuite.Rumble;

public static class RumbleSnapshotParser
{
    public static RumbleSnapshot Parse(JsonObject payload, string credentialContext, SensitiveValues sensitive)
    {
        var raw = CredentialRedactor.Json(payload, sensitive.Snapshot())!.AsObject();
        if (raw["livestreams"] is not JsonArray livestreams || Text(raw["type"]) is not { Length: > 0 } accountType ||
            Text(raw["user_id"]) is not { Length: > 0 } userId || livestreams.Count > 100)
            throw new RumbleShapeException();
        var financialScope = Hash(accountType, userId, raw["channel_id"]?.ToJsonString());
        var scope = Hash(credentialContext, financialScope);
        var diagnostics = new List<RumbleDiagnostic>();
        var streams = new List<RumbleStream>();
        foreach (var entry in livestreams)
        {
            if (entry is not JsonObject stream || Text(stream["id"]) is not { Length: > 0 } id ||
                stream["is_live"] is not JsonValue flag || !flag.TryGetValue<bool>(out var live))
                throw new RumbleShapeException(); // A malformed stream cannot prove disappearance.
            if (streams.Any(item => item.Id == id)) throw new RumbleShapeException();
            var chat = stream["chat"] as JsonObject;
            var messages = Records(chat?["recent_messages"], "chat.message", scope, id, diagnostics);
            var rants = Records(chat?["recent_rants"], "support.rant", financialScope, id, diagnostics);
            streams.Add(new(id, Text(stream["title"]), live, Nonnegative(stream["watching_now"]), Nonnegative(stream["likes"]),
                messages, rants, ValidWindow(chat?["recent_messages"], messages), ValidWindow(chat?["recent_rants"], rants)));
        }
        var followers = Records((raw["followers"] as JsonObject)?["recent_followers"], "community.follow", scope, null, diagnostics);
        var subscriptions = Records((raw["subscribers"] as JsonObject)?["recent_subscribers"], "support.subscription", scope, null, diagnostics);
        if (raw["gifted_subs"] is JsonObject gifts && (gifts["recent_gifted_subs"] is JsonArray { Count: > 0 } || gifts["latest_gifted_sub"] is JsonObject))
            diagnostics.Add(new("giftIdentityUnverified", new() { ["authoritativeAutomationEnabled"] = false, ["financialIngestionEnabled"] = false }));
        return new(scope, (int)Math.Clamp(Nonnegative(raw["max_num_results"]) ?? 50, 1, 10000), streams.ToArray(), followers, subscriptions, raw, diagnostics.ToArray(),
            ValidWindow((raw["followers"] as JsonObject)?["recent_followers"], followers), ValidWindow((raw["subscribers"] as JsonObject)?["recent_subscribers"], subscriptions));
    }

    private static bool ValidWindow(JsonNode? node, RumbleRecord[] records) => node is JsonArray array && array.Count <= 10000 && array.Count == records.Length;

    private static RumbleRecord[] Records(JsonNode? node, string kind, string scope, string? stream, List<RumbleDiagnostic> diagnostics)
    {
        if (node is null) return [];
        if (node is not JsonArray array || array.Count > 10000) { diagnostics.Add(new("invalidRecentArray", new() { ["kind"] = kind })); return []; }
        var result = new List<RumbleRecord>();
        foreach (var element in array)
        {
            if (element is not JsonObject item || !TryRecord(item, kind, scope, stream, diagnostics, out var record))
                diagnostics.Add(new("invalidRecentRecord", new() { ["kind"] = kind }));
            else result.Add(record!);
        }
        return result.ToArray();
    }

    private static bool TryRecord(JsonObject raw, string kind, string scope, string? stream, List<RumbleDiagnostic> diagnostics, out RumbleRecord? record)
    {
        record = null;
        var user = Text(raw["username"]) ?? Text(raw["user"]);
        var dateKey = kind switch { "community.follow" => "followed_on", "support.subscription" => "subscribed_on", _ => "created_on" };
        if (string.IsNullOrWhiteSpace(user) || !TryUtc(Text(raw[dateKey]), out var created)) return false;
        var text = Text(raw["text"]);
        if (kind is "chat.message" or "support.rant" && text is null) return false;
        var badges = (raw["badges"] as JsonArray ?? []).Select(value => value?.ToJsonString() ?? "null").Order(StringComparer.Ordinal).ToArray();
        var cents = kind is "support.rant" or "support.subscription" ? Nonnegative(raw["amount_cents"]) : null;
        if (kind == "support.rant" && cents is null) return false;
        string? expiry = null;
        if (kind == "support.rant")
        {
            if (!TryUtc(Text(raw["expires_on"]), out var expires)) return false;
            expiry = expires.ToString("O");
            if (raw["amount_dollars"] is JsonValue dollars && dollars.TryGetValue<decimal>(out var display) && display != cents / 100m)
                diagnostics.Add(new("rantAmountConflict", new() { ["canonicalCents"] = cents }));
        }
        var core = Hash(kind, scope, stream, created.ToString("O"), user, text, cents?.ToString(CultureInfo.InvariantCulture), expiry);
        var fingerprint = kind == "chat.message" ? Hash(core, JsonSerializer.Serialize(badges)) : core;
        record = new(kind, fingerprint, core, created, user, text, Text(raw["profile_pic_url"]),
            (raw["badges"] as JsonArray ?? []).Select(Text).OfType<string>().Order(StringComparer.Ordinal).ToArray(), cents, stream, raw);
        return true;
    }

    public static string Hash(params string?[] parts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
    public static string? Text(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : null;
    public static long? Nonnegative(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<long>(out var number) && number >= 0 ? number : null;
    private static bool TryUtc(string? text, out DateTimeOffset value)
    {
        value = default;
        if (text is null || !(text.EndsWith('Z') || text.Length >= 6 && (text[^6] is '+' or '-') && text[^3] == ':')) return false;
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        value = parsed.ToUniversalTime(); return true;
    }
}

public sealed class RumbleShapeException : Exception;
