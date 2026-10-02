using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using ExtensionSuite.StreamerBot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationTemporaryActionTests
{
    [Theory]
    [InlineData("cancelled")]
    [InlineData("untracked")]
    [InlineData("enable-failure")]
    public async Task QueuedRepeatCannotReplayCancelledOrUntrackedWorkAndReportsItsOwnFailure(string scenario)
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-queued-tracking", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "toggle.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var clock = new OwnedClock(); var bot = new OwnedBot { ThrowOnCall = scenario == "enable-failure" ? 3 : null };
            var effects = new AutomationTemporaryStore(factory, clock); var receipts = new AutomationExecutionStore(factory, clock);
            using var service = new AutomationTemporaryActions(effects, bot, clock, receipts);
            var plan = new AutomationPlannedAction(Guid.NewGuid(), 1,
                new() { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), DurationSeconds = 60, StackPolicy = "queue" },
                "queued", null, "main", "queue", 10, 0);
            Assert.Equal("dispatched", (await ApplyOwnedAsync(service, receipts, plan)).State);
            Assert.Equal("waiting-effect", (await ApplyOwnedAsync(service, receipts, plan)).State);
            var waiting = (await receipts.ListAsync()).Single(item => item.State == "waiting-effect");
            if (scenario == "cancelled")
                Assert.True(await receipts.TransitionCurrentAsync(waiting.Id, "waiting-effect", "cancelled", "owned-interrupt", default));
            if (scenario == "untracked")
            {
                var effect = (await effects.GetAsync(plan.Action.Id))!;
                var payload = System.Text.Json.JsonSerializer.Deserialize<TemporaryEffectPayload>(effect.Json, EventStore.JsonOptions)!;
                effect.Json = System.Text.Json.JsonSerializer.Serialize(payload with { Queue = [payload.Queue[0] with { ExecutionId = null }] }, EventStore.JsonOptions);
                Assert.True(await effects.SaveAsync(effect));
            }
            clock.Now = clock.Now.AddSeconds(60);
            await service.TickAsync(default);
            Assert.Equal(scenario == "enable-failure" ? 3 : 2, bot.Calls.Count);
            var current = (await effects.GetAsync(plan.Action.Id))!;
            Assert.Equal(scenario == "cancelled" ? "idle" : "uncertain", current.State);
            if (scenario == "enable-failure")
            {
                Assert.Equal(waiting.Id, bot.Calls[2].ExecutionId);
                Assert.Equal("uncertain", await receipts.StateAsync(waiting.Id, default));
            }
            var calls = bot.Calls.Count;
            await service.TickAsync(default);
            Assert.Equal(calls, bot.Calls.Count);
            if (scenario == "untracked")
            {
                Assert.True(await service.ResolveRestoredAsync(plan.Action.Id, current.Version, true, default));
                Assert.Equal("cancelled", await receipts.StateAsync(waiting.Id, default));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("extend", false, 120)]
    [InlineData("extend", true, 120)]
    [InlineData("restart", false, 80)]
    [InlineData("restart", true, 80)]
    [InlineData("ignore", false, 60)]
    [InlineData("ignore", true, 60)]
    [InlineData("queue", false, 60)]
    [InlineData("queue", true, 60)]
    public async Task StackingAndReversionSurviveServiceRecreation(string policy, bool separateRevert, int expirySeconds)
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-stack-restart", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "toggle.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var clock = new OwnedClock(); var initial = clock.Now; var bot = new OwnedBot();
            var store = new AutomationTemporaryStore(factory, clock);
            var receipts = new AutomationExecutionStore(factory, clock);
            var action = new AutomationAction { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(),
                RevertActionId = separateRevert ? Guid.NewGuid() : null, DurationSeconds = 60, StackPolicy = policy };
            var plan = new AutomationPlannedAction(Guid.NewGuid(), 1, action, "queued", null, "main", "queue", 1, 0);
            using (var service = new AutomationTemporaryActions(store, bot, clock, receipts))
            {
                Assert.Equal("dispatched", (await ApplyOwnedAsync(service, receipts, plan)).State);
                clock.Now = initial.AddSeconds(20);
                Assert.Equal(policy == "queue" ? "waiting-effect" : "completed", (await ApplyOwnedAsync(service, receipts, plan)).State);
                if (policy == "queue") Assert.Equal("rejected", (await ApplyOwnedAsync(service, receipts, plan)).State);
                Assert.Single(bot.Calls);
            }
            var reopened = new AutomationTemporaryStore(factory, clock);
            var waiting = (await receipts.ListAsync()).Where(item => item.State == "waiting-effect").ToArray();
            Assert.Equal(policy == "queue" ? 1 : 0, waiting.Length);
            Assert.Equal(0, await reopened.RecoverInterruptedAsync());
            Assert.Equal(initial.AddSeconds(expirySeconds).UtcTicks, (await reopened.GetAsync(action.Id))!.ExpiresAtTicks);
            using var recreated = new AutomationTemporaryActions(reopened, bot, clock, receipts);
            clock.Now = initial.AddSeconds(expirySeconds - 1);
            await recreated.TickAsync(default);
            Assert.Single(bot.Calls);
            clock.Now = initial.AddSeconds(expirySeconds);
            await recreated.TickAsync(default);
            Assert.True(bot.Calls[1].Revert);
            Assert.Equal(action.RevertActionId ?? action.StreamerBotActionId, bot.Calls[1].ActionId);
            if (policy == "queue")
            {
                Assert.Equal(3, bot.Calls.Count);
                Assert.False(bot.Calls[2].Revert);
                Assert.Equal(waiting[0].Id, bot.Calls[2].ExecutionId);
                Assert.Equal("dispatched", await receipts.StateAsync(waiting[0].Id, default));
                Assert.Equal("active", (await reopened.GetAsync(action.Id))!.State);
                clock.Now = clock.Now.AddSeconds(60);
                await recreated.TickAsync(default);
                Assert.Equal(4, bot.Calls.Count);
                Assert.Equal(waiting[0].Id, bot.Calls[3].ExecutionId);
                Assert.Equal("completed", await receipts.StateAsync(waiting[0].Id, default));
            }
            else Assert.Equal(2, bot.Calls.Count);
            Assert.Equal("idle", (await reopened.GetAsync(action.Id))!.State);
            var count = bot.Calls.Count;
            await recreated.TickAsync(default);
            Assert.Equal(count, bot.Calls.Count);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AmbiguousEnableOrRevertIsVisibleAndManualResolutionDoesNotRetry(bool failOnRevert)
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-toggle-failure", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "toggle.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var clock = new OwnedClock(); var bot = new OwnedBot { ThrowOnRevert = failOnRevert };
            var store = new AutomationTemporaryStore(factory, clock);
            var receipts = new AutomationExecutionStore(factory, clock);
            using var service = new AutomationTemporaryActions(store, bot, clock, receipts);
            var action = new AutomationAction { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), DurationSeconds = 60, StackPolicy = "queue" };
            var planned = new AutomationPlannedAction(Guid.NewGuid(), 1, action, "queued", null, "main", "queue", 20, 0);
            var first = await ApplyOwnedAsync(service, receipts, planned);
            if (failOnRevert)
            {
                Assert.Equal("dispatched", first.State);
                Assert.Equal("waiting-effect", (await ApplyOwnedAsync(service, receipts, planned)).State);
                clock.Now = clock.Now.AddSeconds(60);
                await service.TickAsync(default);
            }
            else Assert.Equal("uncertain", first.State);
            var effect = Assert.Single(await store.ListAsync());
            Assert.Equal("uncertain", effect.State);
            var calls = bot.Calls.Count;
            await service.TickAsync(default);
            Assert.Equal("uncertain", (await service.ApplyAsync(Guid.NewGuid(), planned, default)).State);
            Assert.Equal(calls, bot.Calls.Count);
            Assert.False(await service.ResolveRestoredAsync(action.Id, effect.Version, false, default));
            Assert.True(await service.ResolveRestoredAsync(action.Id, effect.Version, true, default));
            Assert.False(await service.ResolveRestoredAsync(action.Id, effect.Version, true, default));
            Assert.Equal(calls, bot.Calls.Count);
            var resolved = (await store.GetAsync(action.Id))!;
            Assert.Equal("idle", resolved.State);
            var payload = System.Text.Json.JsonSerializer.Deserialize<TemporaryEffectPayload>(resolved.Json, EventStore.JsonOptions)!;
            Assert.Empty(payload.Queue);
            Assert.DoesNotContain(await receipts.ListAsync(), item => item.State == "waiting-effect");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ExtendedEffectRevertsOnceAtPersistedDeadline()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-toggle", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new PooledDbContextFactory<FoundationDbContext>(new DbContextOptionsBuilder<FoundationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(directory, "toggle.db")};Pooling=False").Options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var clock = new OwnedClock(); var bot = new OwnedBot();
            var store = new AutomationTemporaryStore(factory, clock);
            var receipts = new AutomationExecutionStore(factory, clock);
            using var service = new AutomationTemporaryActions(store, bot, clock, receipts);
            var action = new AutomationAction { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), RevertActionId = Guid.NewGuid(), DurationSeconds = 60 };
            var planned = new AutomationPlannedAction(Guid.NewGuid(), 1, action, "queued", null, "main", "queue", 20, 0);
            Assert.Equal("dispatched", (await ApplyOwnedAsync(service, receipts, planned)).State);
            clock.Now = clock.Now.AddSeconds(20);
            Assert.Equal("completed", (await ApplyOwnedAsync(service, receipts, planned)).State);
            Assert.Single(bot.Calls);
            var persisted = (await store.GetAsync(action.Id))!;
            Assert.Equal(clock.Now.AddSeconds(100).UtcTicks, persisted.ExpiresAtTicks);
            clock.Now = new(persisted.ExpiresAtTicks, TimeSpan.Zero);
            await service.TickAsync(default);
            Assert.Equal(new[] { false, true }, bot.Calls.Select(call => call.Revert));
            Assert.Equal(action.RevertActionId, bot.Calls[1].ActionId);
            Assert.Equal("idle", (await store.GetAsync(action.Id))!.State);
            await service.TickAsync(default);
            Assert.Equal(2, bot.Calls.Count);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task<AutomationDispatchOutcome> ApplyOwnedAsync(AutomationTemporaryActions service,
        AutomationExecutionStore receipts, AutomationPlannedAction plan)
    {
        var eventId = Guid.CreateVersion7();
        await receipts.EnqueueAsync(eventId, plan.RuleId, plan.Action.Id, System.Text.Json.JsonSerializer.Serialize(plan, EventStore.JsonOptions));
        var receipt = (await receipts.ListAsync()).Single(item => item.EventId == eventId);
        Assert.True(await receipts.TransitionAsync(receipt.Id, receipt.Version, "queued", "dispatching"));
        var result = await service.ApplyAsync(receipt.Id, plan, default);
        if (result.State == "completed")
        {
            await receipts.TransitionCurrentAsync(receipt.Id, "dispatching", "dispatched", result.Detail!, default);
            await receipts.TransitionCurrentAsync(receipt.Id, "dispatched", "completed", result.Detail!, default);
        }
        else if (result.State != "waiting-effect")
            await receipts.TransitionCurrentAsync(receipt.Id, "dispatching", result.State, result.Detail!, default);
        return result;
    }

    private sealed class OwnedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class OwnedBot : IAutomationTemporaryBot
    {
        public bool? ThrowOnRevert { get; init; }
        public int? ThrowOnCall { get; init; }
        public List<(Guid ActionId, Guid ExecutionId, bool Revert)> Calls { get; } = [];
        public Task<BotExecution> ExecuteAsync(Guid actionId, Guid executionId, bool revert, CancellationToken ct)
        {
            Calls.Add((actionId, executionId, revert));
            if (ThrowOnRevert == revert || ThrowOnCall == Calls.Count) throw new IOException("Owned unknown dispatch result.");
            return Task.FromResult(new BotExecution(Guid.NewGuid(), "DoAction", "acknowledged", false, DateTimeOffset.UtcNow));
        }
    }
}
