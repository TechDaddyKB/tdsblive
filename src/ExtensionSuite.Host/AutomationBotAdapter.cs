using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.StreamerBot;

namespace ExtensionSuite.Host;

public interface IAutomationOverlaySound
{
    Task<AutomationDispatchOutcome> PlayAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct);
}

public interface IAutomationTemporaryActions
{
    Task<AutomationDispatchOutcome> ApplyAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct);
}

public sealed class AutomationBotAdapter(SpeakerBotConnection speaker, StreamerBotConnection streamer,
    IAutomationOverlaySound sound, IAutomationTemporaryActions temporary) : IAutomationActionDispatcher
{
    public async Task<AutomationDispatchOutcome> DispatchAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct)
    {
        action.Action.Validate();
        if (action.State != "queued" && action.State != "moderation-pending")
            return new("rejected", "action-not-approved");
        if (action.Action.Kind == "sound") return await sound.PlayAsync(executionId, action, ct);
        if (action.Action.DurationSeconds > 0) return await temporary.ApplyAsync(executionId, action, ct);
        BotExecution result;
        if (action.Action.Kind == "speech")
        {
            if (string.IsNullOrWhiteSpace(action.SpeechText)) return new("rejected", "empty-speech");
            var settings = action.Action.Speech!;
            result = await speaker.SpeakAsync(settings.Voice, action.SpeechText, settings.SpeakerBadWordFilter, true, ct);
        }
        else
        {
            result = await streamer.ExecuteActionAsync(action.Action.StreamerBotActionId!.Value,
                new JsonObject { ["tdsbliveExecutionId"] = executionId.ToString(), ["tdsbliveRuleId"] = action.RuleId.ToString() }, true, ct);
        }
        return Outcome(result);
    }

    public static AutomationDispatchOutcome Outcome(BotExecution result) =>
        result.MayHaveExecuted || result.State == "uncertain" ? new("uncertain", result.State) :
        result.State == "acknowledged" ? new("dispatched", "acknowledged-not-playback-confirmed") :
        result.State is "missingAction" or "actionNotSelected" or "unsupportedCapability" ? new("rejected", result.State) :
        new("failed", result.State);
}
