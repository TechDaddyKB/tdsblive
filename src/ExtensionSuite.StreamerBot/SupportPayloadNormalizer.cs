using System.Globalization;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;

namespace ExtensionSuite.StreamerBot;

public static class SupportPayloadNormalizer
{
    private static readonly Dictionary<string, Func<JsonObject, SupportDetails>> Strategies = new(StringComparer.Ordinal)
    {
        ["twitch:Cheer"] = data => Quantity("bits", Integer(data["bits"])),
        ["twitch:Sub"] = TwitchSubscription,
        ["twitch:ReSub"] = TwitchSubscription,
        ["twitch:GiftBomb"] = TwitchBatch,
        ["twitch:GiftSub"] = TwitchGift,
        ["youtube:SuperChat"] = YoutubeMoney,
        ["youtube:SuperSticker"] = YoutubeMoney,
        ["youtube:NewSponsor"] = data => new("membership", 1, Tier: Text(data["tier"]) ?? ""),
        ["youtube:MembershipGift"] = data => Quantity("gift", Integer(data["count"])) with
            { Tier = Text(data["tier"]) ?? "", GiftRole = "batch", GiftCorrelationKey = Text(data["eventId"]) },
        ["youtube:GiftMembershipReceived"] = data => new("gift", 1, Tier: Text(data["tier"]) ?? "", GiftRole: "recipient"),
        ["kick:Subscription"] = _ => new("subscription", 1),
        ["kick:Resubscription"] = _ => new("subscription", 1),
        ["kick:GiftSubscription"] = data => new("gift", 1, GiftRole: "individual", GiftRecipientKeys: RecipientKeys(data["recipient"])),
        ["kick:MassGiftSubscription"] = data => Quantity("gift", data["recipients"] is JsonArray recipients ? recipients.Count : null) with
            { GiftRole = "batch", GiftRecipientKeys = RecipientKeys(data["recipients"]) }
    };

    public static SupportDetails? Normalize(string platform, string nativeType, JsonObject data, bool forwarded)
    {
        SupportDetails? result;
        if (platform == "kofi" && new[] { "Donation", "ShopOrder", "Commission", "Subscription", "Resubscription" }.Contains(nativeType))
        {
            var kind = nativeType is "Subscription" or "Resubscription" ? "subscription" : "donation";
            result = Money(kind, Decimal(data["amount"]), Text(data["currency"]));
            if (!forwarded) result = result with { NativeMoney = null, GatedReason = "kofi_websocket_schema_unverified" };
        }
        else result = Strategies.TryGetValue(platform + ":" + nativeType, out var strategy) ? strategy(data) : null;
        if (result?.Kind == "gift")
        {
            var broadcaster = (data["broadcaster"] as JsonObject)?["id"] ?? (data["broadcast"] as JsonObject)?["id"];
            result = result with { GiftScopeKey = Text(broadcaster) is { } id ? "channel:" + id : "streamerbot:connected-channel" };
            if (platform == "kick") result = result with { GiftPeriodStart = Date(data["subscribedAt"]), GiftPeriodEnd = Date(data["expiresAt"]) };
        }
        result?.Validate(); return result;
    }

    private static SupportDetails TwitchSubscription(JsonObject data) => Boolean(data["isGift"]) == true
        ? new("gift", 1, GiftRole: "recipient")
        : new("subscription", 1, Tier: Boolean(data["isPrime"]) == true ? "prime" : Text(data["subTier"]) ?? "");

    private static SupportDetails TwitchBatch(JsonObject data) => Quantity("gift", Integer(data["total"])) with
    { Tier = Text(data["subTier"]) ?? "", GiftRole = "batch", GiftCorrelationKey = Text(data["id"]), GiftRecipientKeys = RecipientKeys(data["recipients"]) };

    private static SupportDetails TwitchGift(JsonObject data)
    {
        var community = Boolean(data["fromCommunitySubGift"]);
        var correlation = Text(data["communityGiftId"]);
        var reason = community is null ? "gift_origin_unavailable" : community == false && correlation is not null ? "gift_origin_conflict" : null;
        return new("gift", 1, Tier: Text(data["subTier"]) ?? "", GiftCorrelationKey: correlation,
            GiftRole: community == false ? "standalone" : "individual", GatedReason: reason,
            GiftRecipientKeys: RecipientKeys(data["recipient"]));
    }

    private static SupportDetails YoutubeMoney(JsonObject data)
    {
        var major = Integer(data["microAmount"]) is { } micros ? micros / 1000000m : Decimal(data["decimalAmount"]);
        var result = Money("donation", major, Text(data["currencyCode"]));
        if (major is { } amount && Decimal(data["decimalAmount"]) is { } other && other != amount)
            result = result with { NativeMoney = null, GatedReason = "conflicting_native_amounts" };
        return result;
    }

    private static SupportDetails Money(string kind, decimal? major, string? currency)
    {
        var result = new SupportDetails(kind, 1, ReportedAmountMajor: major?.ToString(CultureInfo.InvariantCulture), ReportedCurrency: currency);
        if (major is null || major < 0) return result with { GatedReason = "native_amount_unavailable" };
        if (CurrencyMinorUnits.Find(currency) is not { } digits) return result with { GatedReason = "currency_scale_unverified" };
        decimal scale = digits switch { 0 => 1m, 2 => 100m, 3 => 1000m, 4 => 10000m, _ => 10m };
        try
        {
            var minor = checked(major.Value * scale);
            if (minor != decimal.Truncate(minor)) return result with { GatedReason = "native_precision_unrepresentable" };
            return result with { NativeMoney = new(checked((long)minor), currency!, digits) };
        }
        catch (OverflowException) { return result with { GatedReason = "native_amount_out_of_range" }; }
    }

    private static SupportDetails Quantity(string kind, long? quantity) => quantity is > 0
        ? new(kind, quantity.Value) : new(kind, 0, GatedReason: "quantity_unavailable");
    private static string[]? RecipientKeys(JsonNode? node)
    {
        IEnumerable<JsonObject> users = node is JsonArray array ? array.OfType<JsonObject>() : node is JsonObject user ? [user] : [];
        var keys = users.Select(user => Text(user["id"]) is { } id ? "id:" + id : Text(user["login"]) is { } login ? "login:" + login.ToLowerInvariant() : null)
            .OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(1000).ToArray();
        return keys.Length == 0 ? null : keys;
    }
    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) && text.Length <= 128 && !text.Any(char.IsControl) ? text : null;
    private static bool? Boolean(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;
    private static DateTimeOffset? Date(JsonNode? node)
    {
        var text = Text(node);
        if (text is null) return null;
        var separator = text.IndexOf('T');
        if (separator < 0 ||
            (!text.EndsWith('Z') && text.LastIndexOf('+') < separator && text.LastIndexOf('-') < separator)) return null;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date.ToUniversalTime() : null;
    }
    private static long? Integer(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<long>(out var number)) return number >= 0 ? number : null;
        return value.TryGetValue<int>(out var integer) && integer >= 0 ? integer : null;
    }
    private static decimal? Decimal(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<decimal>(out var number)) return number;
        return value.TryGetValue<string>(out var text) && decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out number) ? number : null;
    }
}
