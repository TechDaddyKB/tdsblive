namespace ExtensionSuite.Core;

public sealed record OverlayWidget
{
    public string Id { get; init; } = Guid.CreateVersion7().ToString();
    public string Name { get; init; } = "Text";
    public string Kind { get; init; } = "text";
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 400;
    public double Height { get; init; } = 200;
    public string? GroupId { get; init; }
    public double Rotation { get; init; }
    public bool Locked { get; init; }
    public bool Hidden { get; init; }
    public string Text { get; init; } = "Hello, stream!";
    public string Color { get; init; } = "#ffffff";
    public int FontSize { get; init; } = 32;
    public string? AssetId { get; init; }
    public double Volume { get; init; } = .75;
    public bool Loop { get; init; } = true;
    public bool Muted { get; init; } = true;
    public ChatSettings Chat { get; init; } = new();
    public AlertSettings Alert { get; init; } = new();
    public EventListSettings EventList { get; init; } = new();
    public ProgressSettings Progress { get; init; } = new();
    public CustomWidgetSettings Custom { get; init; } = new();
    public DonorWidgetSettings Donor { get; init; } = new();

    public void Validate()
    {
        if (!Guid.TryParseExact(Id, "D", out _) || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 ||
            Kind is not ("text" or "image" or "video" or "audio" or "chat" or "alert" or "donor-crown" or "donor-leaderboard" or "latest-supporter" or "current-stream-leader" or "current-stream-total" or "event-list" or "goal-bar" or "progress-bar" or "custom") ||
            !Finite(X, -7680, 7680) || !Finite(Y, -7680, 7680) || !Finite(Width, 1, 7680) || !Finite(Height, 1, 7680) ||
            GroupId is not null && !Guid.TryParseExact(GroupId, "D", out _) ||
            !Finite(Rotation, -360, 360) || Text is null || Text.Length > 4096 ||
            Color is null || Color.Length != 7 || Color[0] != '#' || Color[1..].Any(c => !Uri.IsHexDigit(c)) ||
            FontSize is < 8 or > 200 || !Finite(Volume, 0, 1) || AssetId is not null && !AssetIdentity.IsValid(AssetId) || Chat is null || Alert is null || Donor is null || EventList is null || Progress is null || Custom is null)
            throw new ArgumentException("Invalid overlay widget.");
        Chat.Validate(); Alert.Validate(); Donor.Validate(); EventList.Validate(); Progress.Validate(); Custom.Validate();
    }
    private static bool Finite(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
}

public sealed record AlertSettings
{
    public string[] EventTypes { get; init; } = ["community.follow"];
    public string[] Platforms { get; init; } = [.. ChatSettings.SupportedPlatforms];
    public string Template { get; init; } = "{user} · {type}";
    public string Group { get; init; } = "main-alerts";
    public int Priority { get; init; }
    public int DurationMs { get; init; } = 5000;
    public int CooldownMs { get; init; }
    public int Concurrency { get; init; } = 1;
    public int MaximumQueueLength { get; init; } = 50;
    public bool Interruptible { get; init; } = true;
    public string InterruptPolicy { get; init; } = "never";
    public string OverflowPolicy { get; init; } = "drop-oldest";
    public string Animation { get; init; } = "fade";
    public string? MediaAssetId { get; init; }
    public string? SoundAssetId { get; init; }
    public void Validate()
    {
        if (EventTypes is null || EventTypes.Length is < 1 or > 64 || EventTypes.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 128) ||
            Platforms is null || Platforms.Length > 16 || Platforms.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 64) ||
            Template is null || Template.Length > 4096 || string.IsNullOrWhiteSpace(Group) || Group.Length > 64 ||
            Priority is < -100 or > 100 || DurationMs is < 100 or > 300000 || CooldownMs is < 0 or > 3600000 ||
            Concurrency is < 1 or > 8 || MaximumQueueLength is < 1 or > 200 ||
            InterruptPolicy is not ("never" or "higher-priority") || OverflowPolicy is not ("drop-oldest" or "drop-newest") ||
            Animation is not ("none" or "fade" or "slide") || MediaAssetId is not null && !AssetIdentity.IsValid(MediaAssetId) ||
            SoundAssetId is not null && !AssetIdentity.IsValid(SoundAssetId)) throw new ArgumentException("Invalid alert settings.");
    }
}

public sealed record OverlayRevision(int Version, DateTimeOffset SavedAt, string Name);
public sealed record RestoreOverlayRevision(int ExpectedVersion);
