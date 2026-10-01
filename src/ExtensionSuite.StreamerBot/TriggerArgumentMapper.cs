using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;

namespace ExtensionSuite.StreamerBot;

public static class TriggerArgumentMapper
{
    public static IReadOnlyDictionary<string, string> EventNames { get; } = new Dictionary<string, string>
    {
        ["chat.message"] = "tdsblive.rumble.chat", ["support.rant"] = "tdsblive.rumble.rant",
        ["community.follow"] = "tdsblive.rumble.follow", ["support.subscription"] = "tdsblive.rumble.subscription",
        ["support.gift"] = "tdsblive.rumble.gift", ["stream.online"] = "tdsblive.rumble.online",
        ["stream.offline"] = "tdsblive.rumble.offline", ["stream.viewers"] = "tdsblive.rumble.viewers",
        ["stream.likes"] = "tdsblive.rumble.likes", ["integration.health"] = "tdsblive.rumble.health",
        ["finance.changed"] = "tdsblive.finance.changed", ["overlay.changed"] = "tdsblive.overlay.changed"
    };

    public static JsonObject Map(CanonicalEvent item)
    {
        item.Validate();
        if (item.BridgePath.Length >= 16 && !item.BridgePath.Contains("tdsblive", StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("The bridge path cannot accept another hop.");
        return new JsonObject
        {
            ["tdsbliveEventId"] = item.Id.ToString(), ["tdsbliveCorrelationId"] = (item.CorrelationId ?? item.Id).ToString(),
            ["tdsbliveOrigin"] = "tdsblive", ["tdsbliveBridgePath"] = JsonSerializer.Serialize(item.BridgePath.Append("tdsblive").Distinct(StringComparer.OrdinalIgnoreCase).ToArray()),
            ["tdsbliveProvenance"] = item.Provenance.ToString().ToLowerInvariant(), ["tdsblivePlatform"] = item.Platform,
            ["tdsbliveType"] = item.Type, ["tdsbliveNativeType"] = item.NativeType,
            ["tdsbliveOccurredAt"] = item.OccurredAt.ToString("O"), ["tdsbliveNativeId"] = item.NativeId,
            ["userId"] = item.User?.PlatformUserId, ["user"] = item.User?.Login, ["userName"] = item.User?.DisplayName,
            ["message"] = item.Message?.Text, ["amountMinorUnits"] = item.Monetary?.MinorUnits,
            ["currency"] = item.Monetary?.Currency, ["valuationKind"] = item.Monetary?.ValuationKind,
            ["streamId"] = item.Stream?.Id, ["streamTitle"] = item.Stream?.Title,
            ["badges"] = JsonSerializer.Serialize(item.User?.Badges ?? []), ["avatarUrl"] = item.User?.AvatarUrl,
            ["health"] = item.Metrics?.Health, ["value"] = item.Metrics?.Value
        };
    }
}
