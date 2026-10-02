using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationQueueDispatchTests
{
    [Fact]
    public async Task InterruptCancelsActiveDispatchBeforeReplacementRunsAndRetainsPlaybackDetail()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-group-interrupt", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "groups.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var store = new AutomationExecutionStore(factory, TimeProvider.System);
            var plan = new AutomationPlannedAction(Guid.NewGuid(), 1, new() { Speech = new() { Voice = "owned" } },
                "queued", "owned", "blocked", "queue", 20, 0);
            var firstEvent = Guid.CreateVersion7();
            await store.EnqueuePlanAsync(firstEvent, [plan]);
            var adapter = new ControlledAdapter { BlockOnlyFirst = true };
            using var dispatcher = new AutomationDispatcher(store, adapter);
            using var stop = new CancellationTokenSource();
            try
            {
                await dispatcher.PumpAsync(stop.Token);
                await adapter.BlockedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var replacement = Guid.CreateVersion7();
                await store.EnqueuePlanAsync(replacement, [plan with { QueuePolicy = "interrupt" }]);
                for (var attempts = 0; attempts < 100; attempts++)
                {
                    if ((await store.ListAsync()).Single(item => item.EventId == firstEvent).State == "uncertain") break;
                    await Task.Delay(20);
                }
                Assert.Equal("uncertain", (await store.ListAsync()).Single(item => item.EventId == firstEvent).State);
                Assert.Single(adapter.BlockedCalls);
                await dispatcher.DrainAsync(stop.Token);
                var completed = (await store.ListAsync()).Single(item => item.EventId == replacement);
                Assert.Equal("completed", completed.State);
                Assert.Equal("owned-playback-completed", completed.Detail);
                Assert.Equal(2, adapter.BlockedCalls.Count);
            }
            finally { await stop.CancelAsync(); await dispatcher.WaitForIdleAsync(stop.Token); }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task NewlyArrivingGroupRunsWhileAnotherGroupHasMoreThanOneBatchWaiting()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-group-arrival", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "groups.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var store = new AutomationExecutionStore(factory, TimeProvider.System);
            var action = new AutomationAction { Speech = new() { Voice = "owned" } };
            var plan = new AutomationPlannedAction(Guid.NewGuid(), 1, action, "queued", "owned", "blocked", "queue", 100, 0);
            await using (var db = factory.CreateDbContext())
            {
                for (var index = 0; index < 110; index++)
                    db.AutomationExecutions.Add(new() { Id = Guid.CreateVersion7(), EventId = Guid.CreateVersion7(), RuleId = plan.RuleId,
                        ActionId = action.Id, QueueGroup = "blocked", State = "queued", CreatedAtTicks = index + 1, DueAtTicks = index + 1,
                        Json = System.Text.Json.JsonSerializer.Serialize(plan, EventStore.JsonOptions) });
                await db.SaveChangesAsync();
            }
            var adapter = new ControlledAdapter();
            using var dispatcher = new AutomationDispatcher(store, adapter);
            using var stop = new CancellationTokenSource();
            try
            {
                Assert.Equal(100, await dispatcher.PumpAsync(stop.Token));
                await adapter.BlockedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var arriving = Guid.CreateVersion7();
                await store.EnqueuePlanAsync(arriving, [plan with { RuleId = Guid.NewGuid(), QueueGroup = "free" }]);
                Assert.Equal(1, await dispatcher.PumpAsync(stop.Token));
                await adapter.FreeDispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Single(adapter.BlockedCalls);
                Assert.False(adapter.Release.Task.IsCompleted);
            }
            finally
            {
                await stop.CancelAsync();
                await dispatcher.WaitForIdleAsync(stop.Token);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task BlockedGroupDoesNotPreventAnotherGroupAndKeepsItsOwnOrder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-groups", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "groups.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var store = new AutomationExecutionStore(factory, TimeProvider.System);
            var first = new AutomationAction { Speech = new() { Voice = "owned" } };
            var second = first with { Id = Guid.NewGuid() };
            var rule = Guid.NewGuid();
            await store.EnqueuePlanAsync(Guid.NewGuid(), [new(rule, 1, first, "queued", "owned", "blocked", "queue", 20, 0),
                new(rule, 1, second, "queued", "owned", "blocked", "queue", 20, 0)]);
            await store.EnqueuePlanAsync(Guid.NewGuid(), [new(Guid.NewGuid(), 1, first with { Id = Guid.NewGuid() }, "queued", "owned", "free", "queue", 20, 0)]);
            var adapter = new ControlledAdapter();
            using var dispatcher = new AutomationDispatcher(store, adapter);
            var running = dispatcher.DrainAsync();
            try
            {
                await adapter.FreeDispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await adapter.BlockedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(running.IsCompleted);
                Assert.Equal(new[] { first.Id }, adapter.BlockedCalls.ToArray());
            }
            finally { adapter.Release.TrySetResult(); }
            Assert.Equal(3, await running);
            Assert.Equal(new[] { first.Id, second.Id }, adapter.BlockedCalls.ToArray());
            Assert.All(await store.ListAsync(), item => Assert.Equal("completed", item.State));
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class ControlledAdapter : IAutomationActionDispatcher
    {
        public bool BlockOnlyFirst { get; init; }
        public TaskCompletionSource FreeDispatched { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BlockedStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public System.Collections.Concurrent.ConcurrentQueue<Guid> BlockedCalls { get; } = new();
        public async Task<AutomationDispatchOutcome> DispatchAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct)
        {
            if (action.QueueGroup == "blocked")
            {
                BlockedCalls.Enqueue(action.Action.Id); BlockedStarted.TrySetResult();
                if (!BlockOnlyFirst || BlockedCalls.Count == 1) await Release.Task.WaitAsync(ct);
            }
            else FreeDispatched.TrySetResult();
            return new("completed", "owned-playback-completed");
        }
    }
}
