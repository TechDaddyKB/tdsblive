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
    TimeProvider clock) : IAutomationTemporaryActions, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<AutomationDispatchOutcome> ApplyAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct)
    {
        action.Action.Validate();
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
                if (payload.Current.Action.StreamerBotActionId != action.Action.StreamerBotActionId ||
                    payload.Current.Action.RevertActionId != action.Action.RevertActionId)
                    return new("rejected", "active-effect-action-changed");
                var decision = AutomationTemporaryPolicy.Apply(new(new(effect.ExpiresAtTicks, TimeSpan.Zero), payload.Queue.Length),
                    clock.GetUtcNow(), action.Action.DurationSeconds, action.Action.StackPolicy, action.MaximumQueueLength);
                if (decision.Operation == "ignored") return new("completed", "temporary-repeat-ignored");
                if (decision.Operation == "queue-full") return new("rejected", "temporary-queue-full");
                if (decision.Operation == "queued") payload = payload with { Queue = [.. payload.Queue, action] };
                effect.ExpiresAtTicks = decision.State!.ExpiresAt.UtcTicks;
                effect.Json = JsonSerializer.Serialize(payload, EventStore.JsonOptions);
                return await store.SaveAsync(effect, ct) ? new("completed", "temporary-" + decision.Operation) : new("failed", "temporary-version-conflict");
            }
            return await EnableAsync(executionId, action, [], effect, ct);
        }
        finally { gate.Release(); }
    }

    public async Task TickAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { foreach (var effect in await store.DueAsync(ct)) await RevertAsync(effect, ct); }
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
            var payload = Read(effect) with { Queue = [] };
            return await store.ResolveRestoredAsync(actionId, version, JsonSerializer.Serialize(payload, EventStore.JsonOptions), ct);
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
        if (effect.State == "idle" && payload.Queue.Length > 0)
            await EnableAsync(Guid.CreateVersion7(), payload.Queue[0], payload.Queue[1..], effect, ct);
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
