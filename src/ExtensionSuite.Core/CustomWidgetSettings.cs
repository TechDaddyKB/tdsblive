using System.Text.Json.Nodes;
using System.Net;

namespace ExtensionSuite.Core;

public sealed record CustomWidgetSettings
{
    public int ManifestVersion { get; init; } = 1;
    public string PackageVersion { get; init; } = "1.0.0";
    public string Author { get; init; } = "";
    public string Html { get; init; } = "<div id=\"message\">Custom widget</div>";
    public string Css { get; init; } = "body { color: white; background: transparent; }";
    public string JavaScript { get; init; } = "SBX.on('chat.message', e => { document.getElementById('message').textContent = e.message?.text ?? ''; });";
    public JsonArray Fields { get; init; } = [];
    public JsonObject Config { get; init; } = [];
    public string[] Subscriptions { get; init; } = ["chat.message"];
    public string[] Permissions { get; init; } = [];
    public string[] NetworkDomains { get; init; } = [];
    public string[] AssetIds { get; init; } = [];
    public static readonly string[] SupportedPermissions = ["storage", "chat", "financial", "raw", "audio", "network"];

    public void Validate()
    {
        if (ManifestVersion != 1 || !System.Text.RegularExpressions.Regex.IsMatch(PackageVersion ?? "", @"^\d{1,6}\.\d{1,6}\.\d{1,6}\z", System.Text.RegularExpressions.RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100)) ||
            Author is null || Author.Length > 128 || Html is null || Html.Length > 131072 || Css is null || Css.Length > 65536 ||
            JavaScript is null || JavaScript.Length > 131072 || Fields is null || Fields.Count > 100 || Fields.ToJsonString().Length > 65536 ||
            Config is null || Config.ToJsonString().Length > 65536 || Subscriptions is null || Subscriptions.Length > 128 ||
            Subscriptions.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 128) || Permissions is null || Permissions.Length > 6 ||
            Permissions.Distinct().Count() != Permissions.Length || Permissions.Any(p => !SupportedPermissions.Contains(p)) ||
            NetworkDomains is null || NetworkDomains.Length > 16 || NetworkDomains.Any(d => !ValidDomain(d)) ||
            NetworkDomains.Length > 0 && !Permissions.Contains("network") || AssetIds is null || AssetIds.Length > 100 || AssetIds.Any(id => !AssetIdentity.IsValid(id)))
            throw new ArgumentException("Invalid custom widget manifest, source or permissions.");
        ValidateFields(Fields, 0, new HashSet<string>(StringComparer.Ordinal));
    }

    private static void ValidateFields(JsonArray fields, int depth, HashSet<string> keys)
    {
        string[] types = ["text", "textarea", "number", "slider", "checkbox", "dropdown", "multiselect", "color", "font", "image", "audio", "video", "duration", "event", "action", "user", "platform", "button", "hidden", "group"];
        if (depth > 4) throw new ArgumentException("Settings groups exceed nesting limit.");
        foreach (var field in fields)
        {
            if (field is not JsonObject obj || obj["key"] is not JsonValue keyValue || !keyValue.TryGetValue<string>(out var key) ||
                string.IsNullOrWhiteSpace(key) || key.Length > 64 || key is "__proto__" or "constructor" or "prototype" || !keys.Add(key) || keys.Count > 100 ||
                obj["label"] is not JsonValue labelValue || !labelValue.TryGetValue<string>(out var label) || label.Length > 128 ||
                obj["type"] is not JsonValue typeValue || !typeValue.TryGetValue<string>(out var type) || !types.Contains(type))
                throw new ArgumentException("Invalid custom settings schema.");
            if (obj["children"] is JsonArray children) ValidateFields(children, depth + 1, keys);
            else if (obj.ContainsKey("children")) throw new ArgumentException("Invalid settings children.");
            foreach (var name in new[] { "min", "max", "step", "maxLength" })
                if (obj.ContainsKey(name) && (obj[name] is not JsonValue number || !number.TryGetValue<double>(out var numeric) || !double.IsFinite(numeric)))
                    throw new ArgumentException("Invalid numeric field constraints.");
            if (obj.ContainsKey("multiple") && (obj["multiple"] is not JsonValue multiple || !multiple.TryGetValue<bool>(out _)))
                throw new ArgumentException("Invalid multiselect constraint.");
            if (obj.ContainsKey("options"))
            {
                if (obj["options"] is not JsonArray options || options.Count > 256 || options.Any(option => option is not JsonObject pair ||
                    pair["value"] is not JsonValue optionValue || !optionValue.TryGetValue<string>(out var optionText) || optionText.Length > 128 ||
                    pair["label"] is not JsonValue optionLabel || !optionLabel.TryGetValue<string>(out var optionName) || optionName.Length > 128))
                    throw new ArgumentException("Invalid settings options.");
            }
        }
    }

    public static bool ValidDomain(string? domain) => domain is not null && domain.Length is > 3 and <= 253 &&
        domain == domain.ToLowerInvariant() && domain.Contains('.') && !domain.EndsWith('.') &&
        domain.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.') &&
        Uri.CheckHostName(domain) == UriHostNameType.Dns && !IPAddress.TryParse(domain, out _) &&
        domain != "localhost" && !domain.EndsWith(".localhost") && !domain.EndsWith(".local") && !domain.EndsWith(".internal");

    public bool Accepts(CanonicalEvent item) => (Subscriptions.Contains(item.Type) || Subscriptions.Contains("*")) &&
        (item.Type != "chat.message" || Permissions.Contains("chat")) &&
        (item.Monetary is null && item.Support is null && !item.Type.StartsWith("financial.", StringComparison.Ordinal) && !item.Type.StartsWith("support.", StringComparison.Ordinal) || Permissions.Contains("financial"));
}
