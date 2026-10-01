using System.Text.RegularExpressions;

namespace ExtensionSuite.Core;

public sealed record ChatSettings
{
    public static readonly string[] SupportedPlatforms = ["twitch", "youtube", "kick", "rumble"];
    public string[] Platforms { get; init; } = [.. SupportedPlatforms];
    public bool ShowPlatformIcon { get; init; } = true;
    public bool ShowAvatar { get; init; } = true;
    public bool ShowBadges { get; init; } = true;
    public bool ShowUsername { get; init; } = true;
    public bool ShowMessage { get; init; } = true;
    public bool ShowTimestamp { get; init; }
    public int MessageDurationSeconds { get; init; } = 30;
    public int MaximumMessages { get; init; } = 100;
    public bool Persistent { get; init; }
    public bool NewestOnTop { get; init; }
    public string AnimationIn { get; init; } = "fade";
    public string AnimationOut { get; init; } = "fade";
    public double BackgroundOpacity { get; init; } = .35;
    public string Font { get; init; } = "Arial";
    public string? FontAssetId { get; init; }
    public int FontSize { get; init; } = 24;
    public Dictionary<string, string> PlatformColors { get; init; } = new() {
        ["twitch"] = "#c4a1ff", ["youtube"] = "#ff6b6b", ["kick"] = "#72e346", ["rumble"] = "#b8df5c" };
    public string[] IgnoredUsers { get; init; } = [];
    public string[] IgnoredPrefixes { get; init; } = [];
    public bool HideBotMessages { get; init; }
    public string[] BotUsers { get; init; } = ["nightbot", "moobot", "streamelements", "streamlabs"];

    public void Validate()
    {
        if (Platforms is null || Platforms.Length > 4 || Platforms.Distinct().Count() != Platforms.Length || Platforms.Any(p => !SupportedPlatforms.Contains(p)) ||
            MessageDurationSeconds is < 1 or > 86400 || MaximumMessages is < 1 or > 500 || FontSize is < 8 or > 120 ||
            !double.IsFinite(BackgroundOpacity) || BackgroundOpacity is < 0 or > 1 || Font is null || !Regex.IsMatch(Font, @"^[\w -]{1,64}$") ||
            AnimationIn is not ("none" or "fade" or "slide") || AnimationOut is not ("none" or "fade" or "slide") ||
            FontAssetId is not null && !AssetIdentity.IsValid(FontAssetId)) throw new ArgumentException("Invalid chat settings.");
        if (PlatformColors is null || PlatformColors.Count != 4 || SupportedPlatforms.Any(p => !PlatformColors.TryGetValue(p, out var c) || c is null || !Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$")))
            throw new ArgumentException("Invalid platform colors.");
        foreach (var values in new[] { IgnoredUsers, IgnoredPrefixes, BotUsers })
            if (values is null || values.Length > 128 || values.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 128)) throw new ArgumentException("Invalid chat filters.");
    }

    public bool Accepts(CanonicalEvent item)
    {
        if (item.Type != "chat.message" || item.Message?.Text is not { } text || !Platforms.Contains(item.Platform)) return false;
        var names = new[] { item.User?.PlatformUserId, item.User?.Login, item.User?.DisplayName }.Where(v => v is not null).Cast<string>();
        if (names.Any(name => IgnoredUsers.Contains(name, StringComparer.OrdinalIgnoreCase))) return false;
        if (IgnoredPrefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal))) return false;
        return !HideBotMessages || !(item.User?.IsBot == true || names.Any(name => BotUsers.Contains(name, StringComparer.OrdinalIgnoreCase)));
    }
}

public sealed record OverlayDefinition
{
    public string Id { get; init; } = "combined-chat";
    public string Name { get; init; } = "Combined Chat";
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public string Background { get; init; } = "transparent";
    public int Version { get; init; } = 1;
    public ChatSettings Chat { get; init; } = new();
    public static bool ValidId(string id) => Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{0,63}$");
    public void Validate()
    {
        if (Id is null || !ValidId(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || Width is < 1 or > 7680 || Height is < 1 or > 7680 ||
            Background != "transparent" || Version < 1 || Version == int.MaxValue || Chat is null) throw new ArgumentException("Invalid overlay definition.");
        Chat.Validate();
    }
}

public static class AssetIdentity
{
    public static bool IsValid(string id) => Regex.IsMatch(id, "^[0-9a-f]{64}$");
}

public sealed record AssetInfo(string Id, string Filename, string Mime, long Size, string Hash, DateTimeOffset UploadedAt, bool Sanitized, string? License);
public sealed record OverlayTokenInfo(Guid Id, string OverlayId, DateTimeOffset ExpiresAt, bool Revoked);
public sealed record CreateOverlayToken(int LifetimeDays = 30);
public sealed record CreatedOverlayToken(OverlayTokenInfo Info, string Token);
