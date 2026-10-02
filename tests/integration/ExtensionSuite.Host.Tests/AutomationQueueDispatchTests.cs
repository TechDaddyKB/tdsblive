using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationQueueDispatchTests
{
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
        public TaskCompletionSource FreeDispatched { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BlockedStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public System.Collections.Concurrent.ConcurrentQueue<Guid> BlockedCalls { get; } = new();
        public async Task<AutomationDispatchOutcome> DispatchAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct)
        {
            if (action.QueueGroup == "blocked") { BlockedCalls.Enqueue(action.Action.Id); BlockedStarted.TrySetResult(); await Release.Task.WaitAsync(ct); }
            else FreeDispatched.TrySetResult();
            return new("completed");
        }
    }
}
