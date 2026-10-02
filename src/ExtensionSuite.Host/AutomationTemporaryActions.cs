using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.StreamerBot;

namespace ExtensionSuite.Host;

public sealed record TemporaryEffectPayload(Guid ExecutionId, AutomationPlannedAction Current,
    AutomationPlannedAction[] Queue);

public interface IAutomationTemporaryBot
{
    Task<BotExecution> ExecuteAsync(Guid actionId, Guid executionId, bool revert, CancellationToken ct);
}

public sealed class AutomationTemporaryBot(StreamerBotConnection bot) : IAutomationTemporaryBot
{
    public Task<BotExecution> ExecuteAsync(Guid actionId, Guid executionId, bool revert, CancellationToken ct) =>
        bot.ExecuteActionAsync(actionId, new JsonObject { ["tdsbliveExecutionId"] = executionId.ToString(), ["tdsbliveTemporaryRevert"] = revert }, true, ct);
}

public sealed class AutomationTemporaryActions(AutomationTemporaryStore store, IAutomationTemporaryBot bot,
    TimeProvider clock, AutomationExecutionStore receipts) : IAutomationTemporaryActions, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<AutomationDispatchOutcome> ApplyAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct)
    {
        action.Action.Validate();
        action = action with { ExecutionId = executionId };
        if (action.Action.Kind != "streamerbot" || action.Action.DurationSeconds <= 0) return new("rejected", "not-temporary");
        await gate.WaitAsync(ct);
        try
        {
            var effect = await store.GetAsync(action.Action.Id, ct);
            if (effect is { State: "active" } && effect.ExpiresAtTicks <= clock.GetUtcNow().UtcTicks)
            {
                await RevertAsync(effect, ct);
                effect = await store.GetAsync(action.Action.Id, ct);
            }
            if (effect is not null && effect.State != "idle" && effect.State != "active")
                return new("uncertain", "temporary-effect-needs-resolution");
            if (effect is { State: "active" })
            {
                var payload = Read(effect);
                if (payload.Current.RuleId != action.RuleId || payload.Current.Action.StreamerBotActionId != action.Action.StreamerBotActionId ||
                    payload.Current.Action.RevertActionId != action.Action.RevertActionId)
                    return new("rejected", "active-effect-action-changed");
                var decision = AutomationTemporaryPolicy.Apply(new(new(effect.ExpiresAtTicks, TimeSpan.Zero), payload.Queue.Length),
                    clock.GetUtcNow(), action.Action.DurationSeconds, action.Action.StackPolicy, action.MaximumQueueLength);
                if (decision.Operation == "ignored") return new("completed", "temporary-repeat-ignored");
                if (decision.Operation == "queue-full") return new("rejected", "temporary-queue-full");
                if (decision.Operation == "queued") payload = payload with { Queue = [.. payload.Queue, action] };
                effect.ExpiresAtTicks = decision.State!.ExpiresAt.UtcTicks;
                effect.Json = JsonSerializer.Serialize(payload, EventStore.JsonOptions);
                if (decision.Operation == "queued")
                    return await store.SaveQueuedAsync(effect, executionId, ct)
                        ? new("waiting-effect", "temporary-queue-accepted") : new("failed", "temporary-queue-not-persisted");
                return await store.SaveAsync(effect, ct) ? new("completed", "temporary-" + decision.Operation) : new("failed", "temporary-version-conflict");
            }
            return await EnableAsync(executionId, action, [], effect, ct);
        }
        finally { gate.Release(); }
    }

    public async Task TickAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            // No enable/revert operation can be in flight while this gate is held.
            // Leftover intents therefore belong to an interrupted operation, not a live worker.
            await store.RecoverInterruptedAsync(ct);
            foreach (var uncertain in (await store.ListAsync(ct)).Where(item => item.State == "uncertain"))
            {
                var id = Read(uncertain).ExecutionId;
                await receipts.TransitionCurrentAsync(id, "dispatching", "uncertain", "temporary-interrupted-intent", ct);
                await receipts.TransitionCurrentAsync(id, "dispatched", "uncertain", "temporary-interrupted-intent", ct);
            }
            foreach (var effect in await store.DueAsync(ct)) await RevertAsync(effect, ct);
        }
        finally { gate.Release(); }
    }

    public async Task<bool> ResolveRestoredAsync(Guid actionId, int version, bool restored, CancellationToken ct)
    {
        if (!restored) return false;
        await gate.WaitAsync(ct);
        try
        {
            var effect = await store.GetAsync(actionId, ct);
            if (effect is null || effect.Version != version || effect.State != "uncertain") return false;
            // Operator confirmation is an observation, never permission to repeat a toggle.
            // Discard pending repetitions after manually restoring the external state.
            var original = Read(effect);
            var payload = original with { Queue = [] };
            return await store.ResolveRestoredAsync(actionId, version, JsonSerializer.Serialize(payload, EventStore.JsonOptions),
                original.Current.RuleId, ct);
        }
        finally { gate.Release(); }
    }

    private async Task<AutomationDispatchOutcome> EnableAsync(Guid executionId, AutomationPlannedAction action,
        AutomationPlannedAction[] queue, AutomationTemporaryEffect? previous, CancellationToken ct)
    {
        var effect = new AutomationTemporaryEffect { ActionId = action.Action.Id, Version = previous?.Version ?? 0,
            ExpiresAtTicks = clock.GetUtcNow().AddSeconds(action.Action.DurationSeconds).UtcTicks, State = "enabling",
            Json = JsonSerializer.Serialize(new TemporaryEffectPayload(executionId, action, queue), EventStore.JsonOptions) };
        if (!await store.SaveAsync(effect, ct)) return new("failed", "temporary-version-conflict");
        effect.Version++;
        var result = AutomationBotAdapter.Outcome(await ExecuteAsync(action.Action.StreamerBotActionId!.Value, executionId, false, ct));
        effect.State = result.State == "dispatched" ? "active" : result.State == "uncertain" ? "uncertain" : "idle";
        effect.ExpiresAtTicks = clock.GetUtcNow().AddSeconds(action.Action.DurationSeconds).UtcTicks;
        if (!await store.SaveAsync(effect, ct)) return new("uncertain", "temporary-result-not-persisted");
        return result;
    }

    private async Task RevertAsync(AutomationTemporaryEffect effect, CancellationToken ct)
    {
        var payload = Read(effect);
        effect.State = "reverting";
        if (!await store.SaveAsync(effect, ct)) return;
        effect.Version++;
        var id = payload.Current.Action.RevertActionId ?? payload.Current.Action.StreamerBotActionId!.Value;
        var result = AutomationBotAdapter.Outcome(await ExecuteAsync(id, payload.ExecutionId, true, ct));
        // A rejected reversion leaves the external effect active, so it also needs resolution.
        effect.State = result.State == "dispatched" ? "idle" : "uncertain";
        if (!await store.SaveAsync(effect, ct)) return;
        effect.Version++;
        await receipts.TransitionCurrentAsync(payload.ExecutionId, "dispatched", effect.State == "idle" ? "completed" : "uncertain",
            effect.State == "idle" ? "temporary-revert-acknowledged" : "temporary-revert-outcome-unknown", ct);
        if (effect.State == "idle") await StartQueuedAsync(effect, payload, ct);
    }

    private async Task StartQueuedAsync(AutomationTemporaryEffect effect, TemporaryEffectPayload payload, CancellationToken ct)
    {
        for (var index = 0; index < payload.Queue.Length; index++)
        {
            var action = payload.Queue[index];
            if (action.ExecutionId is { } id)
            {
                var state = await receipts.StateAsync(id, ct);
                if (state is "cancelled" or "rejected" or "failed") continue;
                if (await receipts.TransitionCurrentAsync(id, "waiting-effect", "dispatching", "temporary-queued-enable-intent", ct))
                {
                    var result = await EnableAsync(id, action, payload.Queue[(index + 1)..], effect, ct);
                    await receipts.TransitionCurrentAsync(id, "dispatching", result.State, result.Detail ?? "temporary-queued-enable", ct);
                    if (result.State is "failed" or "rejected" && index + 1 < payload.Queue.Length)
                    {
                        var halted = await store.GetAsync(effect.ActionId, ct);
                        if (halted is not null)
                        {
                            halted.State = "uncertain";
                            await store.SaveAsync(halted, ct);
                        }
                    }
                    return;
                }
            }
            // Legacy/untracked queue entries cannot safely issue an action with invented identity.
            effect.State = "uncertain";
            await store.SaveAsync(effect, ct);
            return;
        }
        effect.Json = JsonSerializer.Serialize(payload with { Queue = [] }, EventStore.JsonOptions);
        await store.SaveAsync(effect, ct);
    }

    private async Task<BotExecution> ExecuteAsync(Guid actionId, Guid executionId, bool revert, CancellationToken ct)
    {
        try { return await bot.ExecuteAsync(actionId, executionId, revert, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Transport failures after dispatch intent cannot establish that a toggle did not execute.
            return new(Guid.CreateVersion7(), "DoAction", "uncertain", true, clock.GetUtcNow());
        }
    }
    private static TemporaryEffectPayload Read(AutomationTemporaryEffect effect) =>
        JsonSerializer.Deserialize<TemporaryEffectPayload>(effect.Json, EventStore.JsonOptions) ?? throw new JsonException("Invalid temporary payload.");
    public void Dispose() => gate.Dispose();
}
