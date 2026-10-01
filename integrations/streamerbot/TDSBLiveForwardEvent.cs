using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    public bool Execute()
    {
        string origin;
        if (CPH.TryGetArg<string>("tdsbliveOrigin", out origin) && origin == "tdsblive") return true;
        string source;
        string type;
        if (!CPH.TryGetArg<string>("tdsbliveForwardedSource", out source) ||
            !CPH.TryGetArg<string>("tdsbliveForwardedType", out type)) return false;
        var payload = new JObject();
        // Explicitly selected fields prevent forwarding arbitrary action arguments/credentials.
        foreach (var field in new[] { "messageId", "eventId", "userId", "user", "userLogin", "userName", "avatarUrl", "badges", "isBot", "from", "message", "text", "amount", "currency", "createdAt", "publishedAt", "timestamp", "isTest" })
        {
            object value;
            if (CPH.TryGetArg<object>(field, out value) && value != null) payload[field] = JToken.FromObject(value);
        }
        var envelope = new JObject
        {
            ["tdsbliveForwardedSource"] = source, ["tdsbliveForwardedType"] = type,
            ["tdsbliveOrigin"] = "streamerbot", ["tdsbliveBridgePath"] = new JArray("streamerbot"),
            ["payload"] = payload
        };
        CPH.WebsocketBroadcastJson(envelope.ToString(Formatting.None));
        return true;
    }
}
