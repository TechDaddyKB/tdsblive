using System.Collections.Concurrent;
using System.Security.Cryptography;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Overlays;

namespace ExtensionSuite.Host;

public sealed class AutomationOverlaySound(AssetStore assets, OverlayStore overlays, EditorEventHub hub,
    TimeProvider clock) : IAutomationOverlaySound
{
    private sealed record Lease(string OverlayId, string AssetId, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<Guid, Lease> leases = new();
    private readonly SemaphoreSlim slots = new(128, 128);

    public bool Authorizes(string overlayId, string assetId) => leases.Values.Any(lease =>
        lease.OverlayId == overlayId && lease.AssetId == assetId && lease.ExpiresAt > clock.GetUtcNow());

    public async Task<AutomationDispatchOutcome> PlayAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct)
    {
        action.Action.Validate();
        var settings = action.Action;
        if (settings.Kind != "sound" || executionId == Guid.Empty) return new("rejected", "invalid-sound-action");
        var definition = await overlays.GetAsync(settings.OverlayId!, ct);
        if (definition is null || !definition.CanvasEnabled) return new("rejected", "missing-canvas-overlay");
        foreach (var asset in settings.SoundAssetIds)
            if (!AssetIdentity.IsValid(asset) || (await assets.GetAsync(asset, ct))?.Mime.StartsWith("audio/", StringComparison.Ordinal) != true)
                return new("rejected", "missing-audio-asset");
        if (!await slots.WaitAsync(0, ct)) return new("rejected", "sound-capacity-full");
        var selected = settings.SoundAssetIds[RandomNumberGenerator.GetInt32(settings.SoundAssetIds.Length)];
        var added = false;
        try
        {
            added = leases.TryAdd(executionId, new(settings.OverlayId!, selected,
                clock.GetUtcNow().AddSeconds(settings.PlaybackTimeoutSeconds + 5)));
            if (!added) return new("rejected", "duplicate-sound-execution");
            return await hub.PlaySoundAsync(settings.OverlayId!, new
            {
                executionId, assetId = selected, volume = settings.Volume,
                timeoutSeconds = settings.PlaybackTimeoutSeconds, duckingVolume = settings.DuckingVolume
            }, executionId, settings.PlaybackTimeoutSeconds, ct);
        }
        finally
        {
            if (added) leases.TryRemove(executionId, out _);
            slots.Release();
        }
    }
}
