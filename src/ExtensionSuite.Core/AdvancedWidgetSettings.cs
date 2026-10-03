namespace ExtensionSuite.Core;

public sealed record EventListSettings
{
    public string[] EventTypes { get; init; } = ["community.follow", "support.subscription", "support.gift", "support.bits", "support.donation", "support.rant"];
    public string[] Platforms { get; init; } = [];
    public string[] IgnoredUsers { get; init; } = [];
    public int Count { get; init; } = 10;
    public int DurationMs { get; init; } = 60000;
    public bool Persistent { get; init; } = true;
    public bool NewestOnTop { get; init; } = true;
    public string Template { get; init; } = "{user} · {type}";
    public string Font { get; init; } = "Arial";
    public void Validate()
    {
        if (EventTypes is null || EventTypes.Length is < 1 or > 64 || EventTypes.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 128) ||
            Platforms is null || Platforms.Length > 16 || Platforms.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 64) ||
            IgnoredUsers is null || IgnoredUsers.Length > 128 || IgnoredUsers.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 128) ||
            Count is < 1 or > 100 || DurationMs is < 100 or > 86400000 || Template is null || Template.Length > 4096)
            throw new ArgumentException("Invalid event list settings.");
        (new ChatSettings { Font = Font }).Validate();
    }
    public bool Accepts(CanonicalEvent item) => (EventTypes.Contains("*") || EventTypes.Contains(item.Type)) &&
        (Platforms.Length == 0 || Platforms.Contains(item.Platform)) &&
        !new[] { item.User?.PlatformUserId, item.User?.Login, item.User?.DisplayName }.Any(name => name is not null && IgnoredUsers.Contains(name, StringComparer.OrdinalIgnoreCase));
}

public sealed record ProgressSettings
{
    public string Source { get; init; } = "manual";
    public string Label { get; init; } = "Stream goal";
    public double Value { get; init; }
    public double Target { get; init; } = 100;
    public string FillColor { get; init; } = "#60baff";
    public string TrackColor { get; init; } = "#24262c";
    public bool ShowValue { get; init; } = true;
    public bool ShowPercent { get; init; } = true;
    public string Orientation { get; init; } = "horizontal";
    public void Validate()
    {
        if (Source is not ("manual" or "ledger-usd") || Label is null || Label.Length > 256 ||
            !double.IsFinite(Value) || Value is < 0 or > 1e12 || !double.IsFinite(Target) || Target is <= 0 or > 1e12 ||
            Orientation is not ("horizontal" or "vertical")) throw new ArgumentException("Invalid progress settings.");
        foreach (var color in new[] { FillColor, TrackColor })
            if (color is null || color.Length != 7 || color[0] != '#' || color[1..].Any(c => !Uri.IsHexDigit(c)))
                throw new ArgumentException("Invalid progress color.");
    }
}
