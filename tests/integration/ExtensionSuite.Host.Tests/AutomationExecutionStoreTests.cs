using ExtensionSuite.Data;
using ExtensionSuite.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationExecutionStoreTests
{
    [Fact]
    public async Task LanguageReviewReservesCapacityAndRequiresAllowedLanguageThenSeparateModeration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-language", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "review.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var rule = new AutomationRule { Enabled = true, MaximumQueueLength = 1, Condition = new("kofi", "support.donation"),
                Actions = [new() { Speech = new() { Voice = "owned", Template = "{message}", AllowedLanguages = ["en"], ManualModeration = true } }] };
            var item = new CanonicalEvent { Source = "owned", Platform = "kofi", Type = "support.donation", NativeType = "owned",
                OccurredAt = DateTimeOffset.UtcNow, DedupeKey = "language-review", Message = new("Owned review text"),
                Automation = new(false, true), Support = new("donation", 1, new(1000, "USD", 2)) };
            Assert.True(await new EventStore(factory).AcceptAsync(item, "owned"));
            var store = new AutomationExecutionStore(factory, TimeProvider.System);
            Assert.Equal(1, await store.EnqueuePlanAsync(item.Id, AutomationPlanner.Evaluate([rule], item)));
            var receipt = Assert.Single(await store.ListAsync());
            Assert.Equal("language-review", receipt.State);
            Assert.Contains("Owned review text", receipt.Json);
            await using (var db = factory.CreateDbContext())
            {
                db.AutomationExecutions.Add(new() { Id = Guid.CreateVersion7(), EventId = item.Id, RuleId = receipt.RuleId,
                    ActionId = Guid.NewGuid(), ActionOrder = 1, QueueGroup = receipt.QueueGroup,
                    State = "queued", Json = "{}", CreatedAtTicks = receipt.CreatedAtTicks, DueAtTicks = receipt.DueAtTicks });
                await db.SaveChangesAsync();
            }
            Assert.Empty(await store.QueuedAsync());
            var overflow = Guid.CreateVersion7();
            await store.EnqueuePlanAsync(overflow, AutomationPlanner.Evaluate([rule], item));
            Assert.Equal("queue-full", (await store.ListAsync()).Single(value => value.EventId == overflow).Detail);
            Assert.False(await store.ResolveLanguageAsync(receipt.Id, receipt.Version, "fr"));
            Assert.True(await store.ResolveLanguageAsync(receipt.Id, receipt.Version, "en"));
            Assert.False(await store.ResolveLanguageAsync(receipt.Id, receipt.Version, "en"));
            var reviewed = (await store.ListAsync()).Single(value => value.Id == receipt.Id);
            Assert.Equal("moderation-pending", reviewed.State);
            Assert.Empty(await store.QueuedAsync());
            Assert.True(await store.TransitionAsync(reviewed.Id, reviewed.Version, reviewed.State, "queued"));
            var released = await store.QueuedAsync();
            Assert.Equal(2, released.Length);
            Assert.Equal(receipt.Id, released[0].Id);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task DedupeAndRestartNeverReplayAnInterruptedExternalDispatch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-executions", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<FoundationDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "executions.db")};Pooling=False").Options;
            var factory = new PooledDbContextFactory<FoundationDbContext>(options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var store = new AutomationExecutionStore(factory, TimeProvider.System);
            var eventId = Guid.NewGuid(); var ruleId = Guid.NewGuid(); var actionId = Guid.NewGuid();
            Assert.True(await store.EnqueueAsync(eventId, ruleId, actionId, "{}"));
            Assert.False(await store.EnqueueAsync(eventId, ruleId, actionId, "{}"));
            var receipt = Assert.Single(await store.ListAsync());
            Assert.True(await store.TransitionAsync(receipt.Id, 0, "queued", "dispatching"));
            Assert.False(await store.TransitionAsync(receipt.Id, 0, "queued", "dispatching"));
            var reopened = new AutomationExecutionStore(new PooledDbContextFactory<FoundationDbContext>(options), TimeProvider.System);
            Assert.Equal(1, await reopened.RecoverInterruptedAsync());
            var uncertain = Assert.Single(await reopened.ListAsync());
            Assert.Equal("uncertain", uncertain.State);
            Assert.Equal(2, uncertain.Version);
            Assert.Equal(0, await reopened.RecoverInterruptedAsync());
            await Assert.ThrowsAsync<ArgumentException>(() => reopened.TransitionAsync(uncertain.Id, uncertain.Version, "uncertain", "queued"));
            Assert.False(await reopened.EnqueueAsync(eventId, ruleId, actionId, "{}"));

            var moderatedAction = Guid.NewGuid();
            Assert.True(await store.EnqueueAsync(eventId, ruleId, moderatedAction, "{}", moderationRequired: true));
            var pending = (await store.ListAsync()).Single(item => item.ActionId == moderatedAction);
            await Assert.ThrowsAsync<ArgumentException>(() => store.TransitionAsync(pending.Id, 0, "moderation-pending", "dispatching"));
            Assert.True(await store.TransitionAsync(pending.Id, 0, "moderation-pending", "rejected"));
            await Assert.ThrowsAsync<ArgumentException>(() => store.TransitionAsync(pending.Id, 1, "rejected", "queued"));
            var firstAction = new AutomationAction { Speech = new() { Voice = "owned" } };
            var secondAction = firstAction with { Id = Guid.NewGuid() };
            var queueRule = Guid.NewGuid();
            var plan = new[]
            {
                new AutomationPlannedAction(queueRule, 1, firstAction, "queued", "owned", "bounded", "queue", 1, 0),
                new AutomationPlannedAction(queueRule, 1, secondAction, "queued", "owned", "bounded", "queue", 1, 0)
            };
            var acceptedEvent = Guid.NewGuid();
            Assert.Equal(2, await store.EnqueuePlanAsync(acceptedEvent, plan));
            Assert.Equal(new[] { firstAction.Id, secondAction.Id }, (await store.QueuedAsync()).Where(item => item.EventId == acceptedEvent).Select(item => item.ActionId));
            var overflowEvent = Guid.NewGuid();
            Assert.Equal(2, await store.EnqueuePlanAsync(overflowEvent, plan));
            var overflow = (await store.ListAsync()).Where(item => item.EventId == overflowEvent).ToArray();
            Assert.Equal(2, overflow.Length);
            Assert.All(overflow, item => { Assert.Equal("rejected", item.State); Assert.Equal("queue-full", item.Detail); });
            var interruptEvent = Guid.NewGuid();
            Assert.Equal(1, await store.EnqueuePlanAsync(interruptEvent, [plan[0] with { QueuePolicy = "interrupt" }]));
            var cancelled = (await store.ListAsync()).Where(item => item.EventId == acceptedEvent).ToArray();
            Assert.All(cancelled, item => { Assert.Equal("cancelled", item.State); Assert.Equal("interrupted-before-dispatch", item.Detail); });
            Assert.Equal(interruptEvent, Assert.Single(await store.QueuedAsync()).EventId);
        }
        finally { Directory.Delete(directory, true); }
    }
}
