using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;

namespace ExtensionSuite.StreamerBot;

public sealed record EventNormalization(CanonicalEvent? Event, string Classification, string? Limitation = null);

public sealed class StreamerBotEventNormalizer(SensitiveValues sensitive)
{
    public EventNormalization Normalize(JsonObject envelope, DateTimeOffset receivedAt)
    {
        var raw = CredentialRedactor.Json(envelope, sensitive.Snapshot()) as JsonObject ?? new JsonObject();
        var routing = raw["event"] as JsonObject;
        var category = String(routing?["source"]);
        var nativeType = String(routing?["type"]);
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(nativeType)) return new(null, "invalid", "missingEventRouting");
        var data = raw["data"] as JsonObject ?? new JsonObject();
        // General.Custom can wrap WebsocketBroadcastJson content in a JSON string.
        if (category.Equals("General", StringComparison.OrdinalIgnoreCase) && nativeType == "Custom" && String(data["data"]) is { } broadcast)
        {
            try { data = JsonNode.Parse(broadcast, documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 32 }) as JsonObject ?? data; }
            catch (System.Text.Json.JsonException) { return new(null, "invalid", "invalidCustomBroadcast"); }
        }
        var bridgeData = data;
        var path = Strings(data["tdsbliveBridgePath"]);
        if (path.Contains("tdsblive", StringComparer.OrdinalIgnoreCase) || string.Equals(String(data["tdsbliveOrigin"]), "tdsblive", StringComparison.OrdinalIgnoreCase))
            return new(null, "bridgeLoop", "returnedToOrigin");
        if (path.Length >= 16) return new(null, "bridgeLoop", "bridgePathLimit");
        if (category.Equals("General", StringComparison.OrdinalIgnoreCase) && nativeType == "Custom" && String(data["tdsbliveForwardedSource"]) is { } forwarded)
        {
            if (string.IsNullOrWhiteSpace(forwarded) || string.IsNullOrWhiteSpace(String(data["tdsbliveForwardedType"])))
                return new(null, "invalid", "missingForwardedRouting");
            category = forwarded;
            nativeType = String(data["tdsbliveForwardedType"]) ?? "Unknown";
            data = data["payload"] as JsonObject ?? new JsonObject();
        }
        var platform = category.ToLowerInvariant();
        var type = Classify(platform, nativeType);
        var known = type != "integration.unknown";
        var legacyMessage = data["message"] as JsonObject;
        var nativeId = String(data["messageId"]) ?? String(data["eventId"]) ?? String(data["id"]) ?? String(legacyMessage?["msgId"]);
        var user = data["user"] as JsonObject;
        var text = String(data["text"]) ?? String(data["message"]) ?? String(legacyMessage?["message"]);
        var timestamp = String(data["createdAt"]) ?? String(data["publishedAt"]) ?? String(data["timestamp"]) ?? String(raw["timeStamp"]);
        var occurredAt = DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date.ToUniversalTime() : receivedAt.ToUniversalTime();
        var dedupe = nativeId is not null ? $"{platform}:{nativeType}:{nativeId}" : timestamp is not null
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw.ToJsonString()))) : Guid.CreateVersion7().ToString();
        var provenance = EventProvenance.Live;
        if (Enum.TryParse<EventProvenance>(String(bridgeData["tdsbliveProvenance"]), true, out var declared) && Enum.IsDefined(declared)) provenance = declared;
        if (Boolean(data["isTest"]) || Boolean(bridgeData["tdsbliveTest"]) || Boolean((data["meta"] as JsonObject)?["isTest"])) provenance = EventProvenance.Simulation;
        var eventUser = user is null && platform != "kofi" && String(data["userId"]) is null && String(data["userName"]) is null ? null : new EventUser(String(user?["id"]) ?? String(data["userId"]), String(user?["login"]) ?? String(data["userLogin"]) ?? String(data["user"]),
            String(user?["name"]) ?? String(user?["displayName"]) ?? String(data["userName"]) ?? String(data["from"]),
            String(user?["avatarUrl"]) ?? String(user?["profileImageUrl"]) ?? String(user?["profilePicture"]) ?? String(data["avatarUrl"]), BadgeNames(user?["badges"] ?? data["badges"]), Boolean(user?["isBot"]) || Boolean(data["isBot"]));
        var currency = String(data["currency"]);
        EventMoney? money = null;
        if (nativeType is "Cheer" && Integer(data["bits"]) is { } bits)
            money = new(bits, "BITS", "nominal");
        else if (type.StartsWith("support.", StringComparison.Ordinal)) money = new(null, currency, "unknown");
        var item = new CanonicalEvent
        {
            OccurredAt = occurredAt, ReceivedAt = receivedAt.ToUniversalTime(), Source = "streamerbot", Platform = platform,
            Type = type, NativeType = category + "." + nativeType, NativeId = nativeId, DedupeKey = dedupe,
            User = eventUser, Message = text is null ? null : new(text), Monetary = money, Raw = raw, Provenance = provenance,
            CorrelationId = Guid.TryParse(String(bridgeData["tdsbliveCorrelationId"]), out var correlation) ? correlation : null,
            BridgePath = [.. path, "tdsblive"]
        };
        return new(item, known ? "normalized" : "unknown", known ? null : "payloadRetainedWithoutAssumingPlatformSemantics");
    }

    private static string Classify(string platform, string nativeType) => (platform, nativeType) switch
    {
        ("twitch", "ChatMessage") or ("kick", "ChatMessage") or ("youtube", "Message") => "chat.message",
        ("twitch", "Follow") or ("kick", "Follow") or ("youtube", "NewSubscriber") => "community.follow",
        ("twitch", "Cheer") => "support.bits",
        ("twitch", "Sub" or "ReSub") or ("kick", "Subscription" or "Resubscription") or ("youtube", "NewSponsor" or "MemberMileStone") => "support.subscription",
        ("twitch", "GiftSub" or "GiftBomb") or ("kick", "GiftSubscription" or "MassGiftSubscription") or ("youtube", "MembershipGift" or "GiftMembershipReceived") => "support.gift",
        ("youtube", "SuperChat" or "SuperSticker" or "JewelsGifted") or ("kick", "KicksGifted") or ("kofi", "Donation" or "ShopOrder" or "Commission") => "support.donation",
        ("kofi", "Subscription" or "Resubscription") => "support.subscription",
        ("twitch" or "kick", "StreamOnline") or ("youtube", "BroadcastStarted") => "stream.online",
        ("twitch" or "kick", "StreamOffline") or ("youtube", "BroadcastEnded") => "stream.offline",
        ("general", "Custom") or ("custom", "Event" or "CodeEvent") => "integration.custom",
        ("tdsblive-test", _) => "integration.test",
        _ => "integration.unknown"
    };

    private static string? String(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length <= 16384 ? text : null;
    private static bool Boolean(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
    private static long? Integer(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<long>(out var number) && number >= 0) return number;
        return value.TryGetValue<int>(out var integer) && integer >= 0 ? integer : null;
    }
    private static string[] Strings(JsonNode? node) => node is JsonArray array ? array.Select(String).Where(value => value is not null).Cast<string>().ToArray() : [];
    private static string[] BadgeNames(JsonNode? node) => node is JsonArray array ? array.OfType<JsonObject>().Select(item => String(item["name"])).Where(value => value is not null).Cast<string>().ToArray() : [];
}
