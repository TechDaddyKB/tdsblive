using System.Text.RegularExpressions;

namespace ExtensionSuite.Core;

public sealed partial record ChatSettings
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
            !double.IsFinite(BackgroundOpacity) || BackgroundOpacity is < 0 or > 1 || Font is null || !FontPattern().IsMatch(Font) ||
            AnimationIn is not ("none" or "fade" or "slide") || AnimationOut is not ("none" or "fade" or "slide") ||
            FontAssetId is not null && !AssetIdentity.IsValid(FontAssetId)) throw new ArgumentException("Invalid chat settings.");
        if (PlatformColors is null || PlatformColors.Count != 4 || SupportedPlatforms.Any(p => !PlatformColors.TryGetValue(p, out var c) || c is null || !ColorPattern().IsMatch(c)))
            throw new ArgumentException("Invalid platform colors.");
        foreach (var values in new[] { IgnoredUsers, IgnoredPrefixes, BotUsers })
            if (values is null || values.Length > 128 || values.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 128)) throw new ArgumentException("Invalid chat filters.");
    }
    [GeneratedRegex(@"^[\w -]{1,64}\z", RegexOptions.NonBacktracking, 100)]
    private static partial Regex FontPattern();
    [GeneratedRegex(@"^#[0-9a-fA-F]{6}\z", RegexOptions.NonBacktracking, 100)]
    private static partial Regex ColorPattern();

    public bool Accepts(CanonicalEvent item)
    {
        if (item.Type != "chat.message" || item.Message?.Text is not { } text || !Platforms.Contains(item.Platform)) return false;
        var names = new[] { item.User?.PlatformUserId, item.User?.Login, item.User?.DisplayName }.Where(v => v is not null).Cast<string>();
        if (names.Any(name => IgnoredUsers.Contains(name, StringComparer.OrdinalIgnoreCase))) return false;
        if (IgnoredPrefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal))) return false;
        return !HideBotMessages || !(item.User?.IsBot == true || names.Any(name => BotUsers.Contains(name, StringComparer.OrdinalIgnoreCase)));
    }
}

public sealed partial record OverlayDefinition
{
    public string Id { get; init; } = "combined-chat";
    public string Name { get; init; } = "Combined Chat";
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public string Background { get; init; } = "transparent";
    public int Version { get; init; } = 1;
    public ChatSettings Chat { get; init; } = new();
    public bool CanvasEnabled { get; init; }
    public int RevisionLimit { get; init; } = 50;
    public OverlayWidget[] Widgets { get; init; } = [];
    public AlertSet[] AlertSets { get; init; } = [];
    public static bool ValidId(string id) => IdPattern().IsMatch(id);
    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,63}\z", RegexOptions.NonBacktracking, 100)]
    private static partial Regex IdPattern();
    public void Validate()
    {
        if (Id is null || !ValidId(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || Width is < 1 or > 7680 || Height is < 1 or > 7680 ||
            Background != "transparent" || Version < 1 || Version == int.MaxValue || Chat is null) throw new ArgumentException("Invalid overlay definition.");
        Chat.Validate();
        if (RevisionLimit is < 1 or > 200 || Widgets is null || Widgets.Length > 100 || Widgets.Any(w => w is null) ||
            Widgets.Select(w => w.Id).Distinct(StringComparer.Ordinal).Count() != Widgets.Length)
            throw new ArgumentException("Invalid overlay canvas.");
        foreach (var widget in Widgets) widget.Validate();
        if (AlertSets is null || AlertSets.Length > 100 || AlertSets.Any(s => s is null) || AlertSets.Select(s => s.Id).Distinct().Count() != AlertSets.Length)
            throw new ArgumentException("Invalid alert sets.");
        foreach (var set in AlertSets) set.Validate();
        var members = AlertSets.SelectMany(s => s.WidgetIds).ToArray();
        if (members.Distinct(StringComparer.Ordinal).Count() != members.Length || members.Any(id => !Widgets.Any(w => w.Id == id && w.Kind == "alert")))
            throw new ArgumentException("Alert sets require distinct existing alert widgets.");
        var groups = Widgets.Where(w => w.Kind == "alert").GroupBy(w => w.Alert.Group);
        if (groups.Any(g => g.Select(w => (w.Alert.Concurrency, w.Alert.MaximumQueueLength, w.Alert.OverflowPolicy)).Distinct().Count() > 1))
            throw new ArgumentException("Alert widgets in a queue group must share concurrency, queue limit and overflow policy.");
    }
}

public static partial class AssetIdentity
{
    public static bool IsValid(string id) => HashPattern().IsMatch(id);
    [GeneratedRegex(@"^[0-9a-f]{64}\z", RegexOptions.NonBacktracking, 100)]
    private static partial Regex HashPattern();
}

public sealed record AssetInfo(string Id, string Filename, string Mime, long Size, string Hash, DateTimeOffset UploadedAt, bool Sanitized, string? License);
public sealed record OverlayTokenInfo(Guid Id, string OverlayId, DateTimeOffset ExpiresAt, bool Revoked);
public sealed record CreateOverlayToken(int LifetimeDays = 30);
public sealed record CreatedOverlayToken(OverlayTokenInfo Info, string Token);
