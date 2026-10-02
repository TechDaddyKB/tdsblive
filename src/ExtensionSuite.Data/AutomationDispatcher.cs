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

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            var queued = await store.QueuedAsync(ct: ct);
            var results = await Task.WhenAll(queued.GroupBy(item => item.QueueGroup)
                .Select(group => DispatchGroupAsync(group, ct)));
            return results.Sum();
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
