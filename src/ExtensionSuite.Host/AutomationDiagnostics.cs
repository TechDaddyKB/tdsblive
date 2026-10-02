using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.StreamerBot;

namespace ExtensionSuite.Host;

public sealed record AutomationCapabilityIssue(Guid RuleId, Guid ActionId, string Code, string Message);
public sealed record AutomationCapabilityReport(string StreamerBotState, string SpeakerBotState,
    bool VoiceDiscoverySupported, AutomationCapabilityIssue[] Issues);

public static class AutomationDiagnostics
{
    public static async Task<AutomationCapabilityReport> ReadAsync(AutomationRuleStore rules, ApplicationConfiguration configuration,
        StreamerBotConnection streamer, SpeakerBotConnection speaker, AssetStore assets, OverlayStore overlays, CancellationToken ct)
    {
        var issues = new List<AutomationCapabilityIssue>();
        var catalog = await assets.ListAsync(ct);
        var canvases = await overlays.ListAsync(ct);
        foreach (var rule in await rules.ListAsync(ct))
        foreach (var action in rule.Actions)
        {
            void Add(string code, string message) => issues.Add(new(rule.Id, action.Id, code, message));
            if (action.Kind == "speech")
            {
                if (speaker.State.State != "connected") Add("speaker-unavailable", "Speaker.bot is not connected.");
                Add("voice-alias-unverified", "The configured voice alias must exist in Speaker.bot. Its documented WebSocket API does not provide voice discovery; verify actual speech before relying on this rule.");
                if (rule.Condition.Platform.Equals("kofi", StringComparison.OrdinalIgnoreCase))
                    Add("kofi-metadata", "Ko-fi requires the documented forwarding contract. Message visibility, anonymity and language are independent; missing metadata can suppress message text or require review.");
            }
            if (action.Kind == "sound")
            {
                if (!canvases.Any(value => value.Id == action.OverlayId && value.CanvasEnabled)) Add("missing-canvas-overlay", "The target canvas overlay is unavailable.");
                if (action.SoundAssetIds.Any(id => !catalog.Any(value => value.Id == id && value.Mime.StartsWith("audio/", StringComparison.Ordinal))))
                    Add("missing-audio-asset", "A selected local audio asset is missing or is not audio.");
                Add("browser-playback-required", "Playback requires a connected live canvas browser source. A playback receipt is distinct from captured OBS audio.");
            }
            if (action.Kind == "streamerbot")
            {
                if (streamer.State.State != "connected") Add("streamer-unavailable", "Streamer.bot is not connected; cached discovery does not prove availability.");
                foreach (var id in new[] { action.StreamerBotActionId, action.RevertActionId }.OfType<Guid>().Distinct())
                {
                    if (!streamer.Discovery.Actions.Any(value => value.Id == id && value.Enabled)) Add("missing-action", $"Action {id} is missing or disabled in Streamer.bot.");
                    if (!configuration.StreamerBot.AllowedActionIds.Contains(id)) Add("action-not-selected", $"Action {id} is not in the integration's selected-action allowlist.");
                }
                Add("external-effect-unverified", "Streamer.bot acknowledgment proves dispatch. Verify the intended VTube Studio effect and its reversal separately.");
            }
        }
        return new(streamer.State.State, speaker.State.State, false, issues.ToArray());
    }
}
