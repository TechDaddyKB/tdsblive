using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationRuleStoreTests
{
    [Fact]
    public async Task DisableAndDeleteCancelPendingWorkAndRejectStalePlanningWithoutCancellingReversion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-rule-stop", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "rules.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var rules = new AutomationRuleStore(factory);
            var saved = (await rules.SaveAsync(new() { Enabled = true, Condition = new("twitch", "support.bits"),
                Actions = [new() { Speech = new() { Voice = "owned" } }] }))!;
            await using (var db = factory.CreateDbContext())
            {
                foreach (var state in new[] { "queued", "moderation-pending", "language-review", "waiting-effect", "dispatching", "dispatched" })
                    db.AutomationExecutions.Add(new() { Id = Guid.CreateVersion7(), EventId = Guid.CreateVersion7(), RuleId = saved.Id,
                        ActionId = Guid.NewGuid(), State = state, Json = "{}", CreatedAtTicks = 1, DueAtTicks = 1 });
                await db.SaveChangesAsync();
            }
            var receipts = new AutomationExecutionStore(factory, TimeProvider.System);
            Assert.Null(await rules.SaveAsync(saved with { Enabled = false, Version = 0 }));
            Assert.Contains(await receipts.ListAsync(), item => item.State == "queued");
            var disabled = (await rules.SaveAsync(saved with { Enabled = false }))!;
            var stopped = await receipts.ListAsync();
            Assert.Equal(4, stopped.Count(item => item.State == "cancelled"));
            Assert.True(stopped.Single(item => item.State == "dispatching").CancelRequested);
            Assert.False(stopped.Single(item => item.State == "dispatched").CancelRequested);
            var plan = new AutomationPlannedAction(saved.Id, saved.Version, saved.Actions[0], "queued", "owned", "main", "queue", 20, 0);
            var stale = Guid.CreateVersion7();
            await receipts.EnqueuePlanAsync(stale, [plan], requireCurrentRules: true);
            Assert.Equal("rule-disabled-or-changed", (await receipts.ListAsync()).Single(item => item.EventId == stale).Detail);
            await receipts.EnqueueAsync(Guid.CreateVersion7(), saved.Id, Guid.NewGuid(), "{}");
            Assert.False(await rules.DeleteAsync(saved.Id, saved.Version));
            Assert.True(await rules.DeleteAsync(saved.Id, disabled.Version));
            Assert.DoesNotContain(await receipts.ListAsync(), item => item.State == "queued");
            var deleted = Guid.CreateVersion7();
            await receipts.EnqueuePlanAsync(deleted, [plan], requireCurrentRules: true);
            Assert.Equal("rule-deleted", (await receipts.ListAsync()).Single(item => item.EventId == deleted).Detail);
            Assert.Empty(await receipts.QueuedAsync());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task SavesReopensAndRejectsStaleEditsAndDeletes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-automation", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<FoundationDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "rules.db")};Pooling=False").Options;
            var factory = new PooledDbContextFactory<FoundationDbContext>(options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var store = new AutomationRuleStore(factory);
            var rule = new AutomationRule { Condition = new("kofi", "donation"), Actions = [new() { Speech = new() { Voice = "owned" } }] };
            var saved = await store.SaveAsync(rule);
            Assert.NotNull(saved);
            Assert.Equal(1, saved.Version);
            var reopened = new AutomationRuleStore(new PooledDbContextFactory<FoundationDbContext>(options));
            Assert.Equal(rule.Id, Assert.Single(await reopened.ListAsync()).Id);
            Assert.Null(await reopened.SaveAsync(rule));
            var updated = await reopened.SaveAsync(saved with { Name = "Updated" });
            Assert.Equal(2, updated!.Version);
            Assert.False(await store.DeleteAsync(rule.Id, 1));
            Assert.True(await store.DeleteAsync(rule.Id, 2));
            Assert.Empty(await reopened.ListAsync());
        }
        finally { Directory.Delete(directory, true); }
    }
}
