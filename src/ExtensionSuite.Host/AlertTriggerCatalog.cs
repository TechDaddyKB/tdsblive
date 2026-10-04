using ExtensionSuite.Core;
using ExtensionSuite.StreamerBot;
using System.Text.Json.Nodes;
namespace ExtensionSuite.Host;
public sealed record AlertTriggerChoice(string Id, string Label, string Platform, string EventType,
    string Description, string Availability, string[] Units, string? NativeType = null, string? CustomTriggerKey = null);
public static class AlertTriggerCatalog
{
    private static readonly (string Source, string Native, string Label)[] Maintained = [
        ("Twitch", "Sub", "New subscription"), ("Twitch", "ReSub", "Renewed subscription"),
        ("Twitch", "GiftSub", "Individual gift subscription"), ("Twitch", "GiftBomb", "Gift subscription bundle"),
        ("YouTube", "SuperChat", "Super Chat"), ("YouTube", "SuperSticker", "Super Sticker"), ("YouTube", "JewelsGifted", "Jewels gift"),
        ("YouTube", "NewSponsor", "New membership"), ("YouTube", "MemberMileStone", "Membership milestone"),
        ("YouTube", "MembershipGift", "Gift membership bundle"), ("YouTube", "GiftMembershipReceived", "Received gift membership"),
        ("Kick", "Subscription", "New subscription"), ("Kick", "Resubscription", "Renewed subscription"),
        ("Kick", "GiftSubscription", "Individual gift subscription"), ("Kick", "MassGiftSubscription", "Gift subscription bundle"), ("Kick", "KicksGifted", "Kicks gift"),
        ("Kofi", "Donation", "Donation only"), ("Kofi", "ShopOrder", "Shop order"), ("Kofi", "Commission", "Commission"),
        ("Kofi", "Subscription", "New subscription"), ("Kofi", "Resubscription", "Renewed subscription"),
        ("Twitch", "StreamOnline", "Stream started"), ("Twitch", "StreamOffline", "Stream ended"),
        ("Kick", "StreamOnline", "Stream started"), ("Kick", "StreamOffline", "Stream ended"),
        ("YouTube", "BroadcastStarted", "Broadcast started"), ("YouTube", "BroadcastEnded", "Broadcast ended")
    ];
    private static readonly (string Platform, string Type, string Label)[] Known = [
        ("twitch", "community.follow", "Follow"), ("twitch", "support.subscription", "Subscription"),
        ("twitch", "support.gift", "Gift subscription"), ("twitch", "support.bits", "Bits"),
        ("youtube", "community.follow", "New subscriber"), ("youtube", "support.donation", "Super Chat / paid support"),
        ("youtube", "support.subscription", "Membership"), ("youtube", "support.gift", "Gift membership"),
        ("kick", "community.follow", "Follow"), ("kick", "support.subscription", "Subscription"), ("kick", "support.gift", "Gift subscription"),
        ("kofi", "support.donation", "Donation"), ("kofi", "support.subscription", "Subscription"),
        ("rumble", "community.follow", "Follow"), ("rumble", "support.rant", "Rant"),
        ("rumble", "support.subscription", "Subscription (unverified)"), ("rumble", "support.gift", "Gift (unverified)"),
        ("general", "integration.custom", "Custom event"), ("custom", "integration.custom", "Custom event")
    ];
    public static AlertTriggerChoice[] Build(BotDiscovery discovery, InspectorEntry[] observed, StreamerBotEventNormalizer normalizer)
    {
        var items = Known.Select(k => new AlertTriggerChoice(k.Platform + ":" + k.Type, PlatformLabel(k.Platform) + " · " + k.Label,
            k.Platform, k.Type, "Incoming event; platform delivery must be verified separately.",
            k.Label.Contains("unverified") ? "unverified" : "configured", Units(k.Type))).ToList();
        foreach (var k in Maintained)
        {
            var item = normalizer.Normalize(new JsonObject { ["event"] = new JsonObject { ["source"] = k.Source, ["type"] = k.Native }, ["data"] = new JsonObject() }, DateTimeOffset.UtcNow).Event!;
            items.Add(new("native:" + item.NativeType, PlatformLabel(item.Platform) + " · " + k.Label, item.Platform, item.Type,
                "Maintained incoming event identity. Connection and live delivery must be verified separately.", "maintained", Units(item.Type), item.NativeType));
        }
        foreach (var (source, names) in discovery.Events)
            foreach (var name in names)
            {
                var item = normalizer.Normalize(new JsonObject { ["event"] = new JsonObject { ["source"] = source, ["type"] = name }, ["data"] = new JsonObject() }, DateTimeOffset.UtcNow).Event;
                if (item is null) continue;
                items.Add(new("native:" + item.NativeType, source + " · " + name, item.Platform, item.Type,
                    "Discovered incoming event. Discovery does not establish live delivery.", "discovered", Units(item.Type), item.NativeType));
            }
        foreach (var item in observed.Select(e => e.Event).OfType<CanonicalEvent>().Where(e => e.Type == "integration.custom" && e.AlertTriggerKey is not null))
            items.Add(new("custom:" + item.Platform + ":" + item.AlertTriggerKey, "Custom · " + item.AlertTriggerKey, item.Platform, item.Type,
                "Observed incoming tdsbliveAlertTrigger identity.", "observed", [], CustomTriggerKey: item.AlertTriggerKey));
        return items.DistinctBy(c => c.Id).OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static string[] Units(string type) => type.StartsWith("support.", StringComparison.Ordinal) ? ["quantity", "native-money"] : [];
    private static string PlatformLabel(string platform) => platform switch {
        "twitch" => "Twitch", "youtube" => "YouTube", "kick" => "Kick", "kofi" => "Ko-fi",
        "rumble" => "Rumble", "general" => "Streamer.bot general", "custom" => "Custom", _ => platform
    };
}
