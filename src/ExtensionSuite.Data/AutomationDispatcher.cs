using System.Text.Json;
using ExtensionSuite.Core;

namespace ExtensionSuite.Data;

public sealed record AutomationDispatchOutcome(string State, string? Detail = null);
public interface IAutomationActionDispatcher
{
    Task<AutomationDispatchOutcome> DispatchAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct);
}

/// <summary>One worker per host. Atomic receipt claims also prevent another worker
/// from issuing the same external operation. No adapter exception authorizes retry.</summary>
public sealed class AutomationDispatcher(AutomationExecutionStore store, IAutomationActionDispatcher adapter) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, Task<int>> groups = new(StringComparer.Ordinal);
    private const int MaximumConcurrentGroups = 16;

    private sealed record ScheduledWork(Task<int>[] Running, int Started, int Completed);

    public async Task<int> PumpAsync(CancellationToken ct = default)
    {
        var work = await ScheduleAsync(ct);
        return work.Started + work.Completed;
    }

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        var work = await ScheduleAsync(ct);
        try { return work.Completed + (await Task.WhenAll(work.Running)).Sum(); }
        finally
        {
            await gate.WaitAsync(CancellationToken.None);
            try
            {
                foreach (var name in groups.Where(item => item.Value.IsCompleted && work.Running.Contains(item.Value)).Select(item => item.Key).ToArray())
                    groups.Remove(name);
            }
            finally { gate.Release(); }
        }
    }

    public async Task WaitForIdleAsync(CancellationToken ct)
    {
        Task<int>[] running;
        await gate.WaitAsync(CancellationToken.None);
        try { running = groups.Values.ToArray(); }
        finally { gate.Release(); }
        try { await Task.WhenAll(running); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task<ScheduledWork> ScheduleAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var completed = 0;
            foreach (var name in groups.Where(item => item.Value.IsCompleted).Select(item => item.Key).ToArray())
            {
                var finished = groups[name];
                groups.Remove(name);
                completed += await finished;
            }
            var started = 0;
            if (groups.Count < MaximumConcurrentGroups)
            {
                // Exclude occupied groups before applying the database batch limit, so a large
                // backlog behind one blocked operation cannot hide a newly available group.
                var queued = await store.QueuedAsync(ct: ct, excludedGroups: groups.Keys.ToArray());
                foreach (var group in queued.GroupBy(item => item.QueueGroup).Take(MaximumConcurrentGroups - groups.Count))
                {
                    groups.Add(group.Key, DispatchGroupAsync(group, ct));
                    started += group.Count();
                }
            }
            return new(groups.Values.ToArray(), started, completed);
        }
        finally { gate.Release(); }
    }

    private async Task<int> DispatchGroupAsync(IEnumerable<AutomationExecution> receipts, CancellationToken ct)
    {
        var dispatched = 0;
        foreach (var receipt in receipts)
        {
            AutomationPlannedAction? action;
            try
            {
                action = JsonSerializer.Deserialize<AutomationPlannedAction>(receipt.Json, EventStore.JsonOptions);
                if (action is null) throw new JsonException("Missing action payload.");
                action.Action.Validate();
            }
            catch (Exception error) when (error is JsonException or ArgumentException)
            {
                await store.TransitionAsync(receipt.Id, receipt.Version, "queued", "failed", ct);
                continue;
            }
            if (!await store.TransitionAsync(receipt.Id, receipt.Version, "queued", "dispatching", ct)) continue;
            string outcome;
            string? detail = null;
            using var dispatchStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var monitoringStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var monitoring = MonitorCancellationAsync(receipt.Id, dispatchStop, monitoringStop.Token);
            try
            {
                var result = await adapter.DispatchAsync(receipt.Id, action, dispatchStop.Token);
                detail = result.Detail;
                outcome = result.State is "dispatched" or "completed" or "rejected" or "failed" or "uncertain" ? result.State : "uncertain";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Leave the durable dispatch intent for startup recovery.
                throw;
            }
            catch (Exception)
            {
                outcome = "uncertain";
                detail = "adapter-exception-outcome-unknown";
            }
            finally
            {
                await monitoringStop.CancelAsync();
                await monitoring;
            }
            // A complete synchronous operation still records confirmed dispatch first.
            await store.TransitionAsync(receipt.Id, receipt.Version + 1, "dispatching", outcome == "completed" ? "dispatched" : outcome, ct, detail);
            if (outcome == "completed")
                await store.TransitionAsync(receipt.Id, receipt.Version + 2, "dispatched", "completed", ct, detail);
            dispatched++;
        }
        return dispatched;
    }

    public void Dispose() => gate.Dispose();

    private async Task MonitorCancellationAsync(Guid id, CancellationTokenSource dispatchStop, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (await store.CancellationRequestedAsync(id, ct)) { await dispatchStop.CancelAsync(); return; }
                await Task.Delay(100, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception)
        {
            // Losing interruption monitoring must not leave an unbounded external operation running.
            // The adapter/receipt path records an interrupted operation as uncertain rather than retrying.
            await dispatchStop.CancelAsync();
        }
    }
}
