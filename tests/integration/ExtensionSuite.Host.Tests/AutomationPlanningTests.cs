using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationPlanningTests
{
    [Fact]
    public async Task PreviewAndReplayNeverEnqueueAndLivePlanningIsIdempotent()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-planning", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "planning.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var rules = new AutomationRuleStore(factory);
            var receipts = new AutomationExecutionStore(factory, TimeProvider.System);
            var planner = new AutomationPlanningStore(factory, rules, receipts);
            var saved = await rules.SaveAsync(new()
            {
                Enabled = true, CooldownSeconds = 30, Condition = new("kofi", "donation"),
                Actions = [new() { Speech = new() { Voice = "owned", ManualModeration = true } }]
            });
            Assert.NotNull(saved);
            var item = new CanonicalEvent
            {
                Source = "owned", Platform = "kofi", Type = "donation", NativeType = "owned",
                OccurredAt = DateTimeOffset.UtcNow, DedupeKey = "planning", Support = new("donation", 1, new(1000, "USD", 2)),
                Message = new("private anonymous message")
            };
            var preview = Assert.Single(await planner.PreviewAsync(item));
            Assert.Equal("moderation-pending", preview.State);
            Assert.DoesNotContain("private anonymous message", preview.SpeechText!);
            Assert.Empty(await receipts.ListAsync());
            Assert.Equal(0, await planner.PlanAcceptedAsync(item.Id));
            var replay = item with { Id = Guid.CreateVersion7(), Provenance = EventProvenance.Replay };
            Assert.True(await new EventStore(factory).AcceptAsync(replay, "replay", persistTest: true));
            Assert.Equal(0, await planner.PlanAcceptedAsync(replay.Id));
            Assert.True(await new EventStore(factory).AcceptAsync(item, "live"));
            Assert.Equal(1, await planner.PlanAcceptedAsync(item.Id));
            Assert.Equal(0, await planner.PlanAcceptedAsync(item.Id));
            var pending = Assert.Single(await receipts.ListAsync());
            using (var reader = new AutomationEventReader(factory, planner))
            {
                Assert.Equal(1, await reader.ProcessAsync());
                Assert.Equal(0, await reader.ProcessAsync());
            }
            using (var reopenedReader = new AutomationEventReader(new PooledDbContextFactory<FoundationDbContext>(
                new DbContextOptionsBuilder<FoundationDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "planning.db")};Pooling=False").Options), planner))
                Assert.Equal(0, await reopenedReader.ProcessAsync());
            Assert.Equal("moderation-pending", pending.State);
            var adapter = new AmbiguousAdapter();
            using var dispatcher = new AutomationDispatcher(receipts, adapter);
            Assert.Equal(0, await dispatcher.DrainAsync());
            Assert.Equal(0, adapter.Calls);
            Assert.True(await receipts.TransitionAsync(pending.Id, pending.Version, "moderation-pending", "queued"));
            Assert.Equal(1, await dispatcher.DrainAsync());
            Assert.Equal("uncertain", Assert.Single(await receipts.ListAsync()).State);
            Assert.Equal(0, await dispatcher.DrainAsync());
            Assert.Equal(1, adapter.Calls);
            var repeated = item with { Id = Guid.CreateVersion7(), DedupeKey = "cooldown-repeat" };
            Assert.True(await new EventStore(factory).AcceptAsync(repeated, "live-repeat"));
            var reopenedReceipts = new AutomationExecutionStore(new PooledDbContextFactory<FoundationDbContext>(
                new DbContextOptionsBuilder<FoundationDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "planning.db")};Pooling=False").Options), TimeProvider.System);
            var reopenedPlanner = new AutomationPlanningStore(factory, rules, reopenedReceipts);
            Assert.Equal(1, await reopenedPlanner.PlanAcceptedAsync(repeated.Id));
            var rejected = (await reopenedReceipts.ListAsync()).Single(receipt => receipt.EventId == repeated.Id);
            Assert.Equal("rejected", rejected.State);
            Assert.Equal("rule-cooldown", rejected.Detail);
            Assert.Equal(0, await dispatcher.DrainAsync());
            Assert.Equal(1, adapter.Calls);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class AmbiguousAdapter : IAutomationActionDispatcher
    {
        public int Calls { get; private set; }
        public Task<AutomationDispatchOutcome> DispatchAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct)
        {
            Calls++;
            throw new IOException("Owned transport outcome is unknown.");
        }
    }
}
