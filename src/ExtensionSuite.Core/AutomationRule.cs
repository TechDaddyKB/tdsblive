namespace ExtensionSuite.Core;

public sealed record AutomationAction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Kind { get; init; } = "speech";
    public AutomationSpeechSettings? Speech { get; init; }
    public Guid? StreamerBotActionId { get; init; }
    public Guid? RevertActionId { get; init; }
    public int DurationSeconds { get; init; }
    public string StackPolicy { get; init; } = "extend";
    public string? OverlayId { get; init; }
    public string[] SoundAssetIds { get; init; } = [];
    public decimal Volume { get; init; } = 1;
    public decimal DuckingVolume { get; init; } = 1;
    public int PlaybackTimeoutSeconds { get; init; } = 120;

    public void Validate()
    {
        if (Id == Guid.Empty || Kind is not ("speech" or "sound" or "streamerbot") ||
            DurationSeconds is < 0 or > 86400 ||
            StackPolicy is not ("extend" or "restart" or "ignore" or "queue") ||
            Volume is < 0 or > 1 || DuckingVolume is < 0 or > 1 ||
            PlaybackTimeoutSeconds is < 1 or > 3600 || SoundAssetIds is null ||
            SoundAssetIds.Length > 32 || SoundAssetIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 128))
            throw new ArgumentException("Invalid automation action.");
        if (Kind == "speech")
        {
            if (Speech is null || string.IsNullOrWhiteSpace(Speech.Voice))
                throw new ArgumentException("Speech actions require a selected voice alias.");
            Speech.Validate();
        }
        else if (Speech is not null) throw new ArgumentException("Speech settings require a speech action.");
        if (Kind == "sound")
        {
            if (string.IsNullOrWhiteSpace(OverlayId) || OverlayId.Length > 128 || SoundAssetIds.Length == 0)
                throw new ArgumentException("Sound actions require an overlay and local sound assets.");
        }
        else if (OverlayId is not null || SoundAssetIds.Length > 0)
            throw new ArgumentException("Sound settings require a sound action.");
        if (Kind == "streamerbot")
        {
            if (StreamerBotActionId is null || StreamerBotActionId == Guid.Empty || RevertActionId == Guid.Empty)
                throw new ArgumentException("Select a Streamer.bot action.");
        }
        else if (StreamerBotActionId is not null || RevertActionId is not null || DurationSeconds != 0)
            throw new ArgumentException("Temporary actions require Streamer.bot execution.");
        if (DurationSeconds == 0 && RevertActionId is not null)
            throw new ArgumentException("Reversion requires a positive duration.");
    }
}

public sealed record AutomationRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "New rule";
    public bool Enabled { get; init; }
    public int Version { get; init; }
    public required AutomationCondition Condition { get; init; }
    public AutomationAction[] Actions { get; init; } = [];
    public string QueueGroup { get; init; } = "main";
    public string QueuePolicy { get; init; } = "queue";
    public int MaximumQueueLength { get; init; } = 20;
    public int CooldownSeconds { get; init; }

    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || Version < 0 ||
            Condition is null || Actions is null || Actions.Length is < 1 or > 16 ||
            Actions.Any(action => action is null) || Actions.Select(action => action.Id).Distinct().Count() != Actions.Length ||
            string.IsNullOrWhiteSpace(QueueGroup) || QueueGroup.Length > 64 ||
            QueuePolicy is not ("queue" or "interrupt" or "ignore") || MaximumQueueLength is < 1 or > 100 ||
            CooldownSeconds is < 0 or > 86400)
            throw new ArgumentException("Invalid automation rule.");
        Condition.Validate();
        foreach (var action in Actions) action.Validate();
    }
}
