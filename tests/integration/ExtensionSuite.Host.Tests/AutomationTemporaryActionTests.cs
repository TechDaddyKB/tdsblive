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
            using var service = new AutomationTemporaryActions(store, bot, clock);
            var action = new AutomationAction { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), DurationSeconds = 60, StackPolicy = "queue" };
            var planned = new AutomationPlannedAction(Guid.NewGuid(), 1, action, "queued", null, "main", "queue", 20, 0);
            var first = await service.ApplyAsync(Guid.NewGuid(), planned, default);
            if (failOnRevert)
            {
                Assert.Equal("dispatched", first.State);
                Assert.Equal("completed", (await service.ApplyAsync(Guid.NewGuid(), planned, default)).State);
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
            using var service = new AutomationTemporaryActions(store, bot, clock);
            var action = new AutomationAction { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), RevertActionId = Guid.NewGuid(), DurationSeconds = 60 };
            var planned = new AutomationPlannedAction(Guid.NewGuid(), 1, action, "queued", null, "main", "queue", 20, 0);
            Assert.Equal("dispatched", (await service.ApplyAsync(Guid.NewGuid(), planned, default)).State);
            clock.Now = clock.Now.AddSeconds(20);
            Assert.Equal("completed", (await service.ApplyAsync(Guid.NewGuid(), planned, default)).State);
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

    private sealed class OwnedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class OwnedBot : IAutomationTemporaryBot
    {
        public bool? ThrowOnRevert { get; init; }
        public List<(Guid ActionId, bool Revert)> Calls { get; } = [];
        public Task<BotExecution> ExecuteAsync(Guid actionId, Guid executionId, bool revert, CancellationToken ct)
        {
            Calls.Add((actionId, revert));
            if (ThrowOnRevert == revert) throw new IOException("Owned unknown dispatch result.");
            return Task.FromResult(new BotExecution(Guid.NewGuid(), "DoAction", "acknowledged", false, DateTimeOffset.UtcNow));
        }
    }
}
