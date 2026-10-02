using System.Globalization;

namespace ExtensionSuite.Core;

/// <summary>Persisted donor presentation and filtering settings. Money remains integer USD cents.</summary>
public sealed record DonorWidgetSettings
{
    public string Period { get; init; } = "all-time";
    public string[] Platforms { get; init; } = [];
    public string[] EventTypes { get; init; } = [];
    public string MinimumUsdMinor { get; init; } = "0";
    public int Count { get; init; } = 10;
    public DateOnly? CustomStart { get; init; }
    public DateOnly? CustomEndExclusive { get; init; }
    public bool ShowName { get; init; } = true;
    public bool ShowAvatar { get; init; } = true;
    public bool ShowPlatformBadges { get; init; } = true;
    public bool ShowAmount { get; init; } = true;
    public bool ShowCrown { get; init; } = true;
    public string Template { get; init; } = "{name} · {amount}";
    public string FontFamily { get; init; } = "sans-serif";
    public string Animation { get; init; } = "fade";
    public int TransitionMs { get; init; } = 300;
    public string? CrownAssetId { get; init; }
    public string? FontAssetId { get; init; }

    public void Validate()
    {
        if (Period is not ("all-time" or "current-stream" or "today" or "week" or "month" or "year" or "custom") ||
            Platforms is null || Platforms.Length > 16 || Platforms.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 64) ||
            EventTypes is null || EventTypes.Length > 64 || EventTypes.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 128) ||
            MinimumUsdMinor is null || !long.TryParse(MinimumUsdMinor, NumberStyles.None, CultureInfo.InvariantCulture, out var minimum) || minimum < 0 ||
            Count is < 1 or > 25 || Template is null || Template.Length > 4096 ||
            string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 128 ||
            Animation is not ("none" or "fade" or "slide") || TransitionMs is < 0 or > 10000 ||
            CrownAssetId is not null && !AssetIdentity.IsValid(CrownAssetId) ||
            FontAssetId is not null && !AssetIdentity.IsValid(FontAssetId) ||
            Period == "custom" && (CustomStart is null || CustomEndExclusive is null || CustomEndExclusive <= CustomStart))
            throw new ArgumentException("Invalid donor widget settings.");
    }
}
