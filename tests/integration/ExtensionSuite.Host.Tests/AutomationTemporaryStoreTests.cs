using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationTemporaryStoreTests
{
    [Fact]
    public async Task DeadlinesSurviveReopenAndInterruptedTogglesAreNotAutomaticallyDue()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-temporary", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<FoundationDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "effects.db")};Pooling=False").Options;
            var factory = new PooledDbContextFactory<FoundationDbContext>(options);
            await using (var db = factory.CreateDbContext()) await DatabaseLifecycle.InitializeAsync(db);
            var store = new AutomationTemporaryStore(factory, TimeProvider.System);
            var action = Guid.NewGuid();
            var expiry = DateTimeOffset.UtcNow.AddSeconds(-1).UtcTicks;
            Assert.True(await store.SaveAsync(new() { ActionId = action, ExpiresAtTicks = expiry, State = "active", Json = "{}" }));
            var reopened = new AutomationTemporaryStore(new PooledDbContextFactory<FoundationDbContext>(options), TimeProvider.System);
            var due = Assert.Single(await reopened.DueAsync());
            Assert.Equal(expiry, due.ExpiresAtTicks);
            Assert.False(await reopened.SaveAsync(new() { ActionId = action, Version = 0, ExpiresAtTicks = expiry, State = "idle", Json = "{}" }));
            due.State = "reverting";
            Assert.True(await reopened.SaveAsync(due));
            Assert.Equal(1, await store.RecoverInterruptedAsync());
            Assert.Equal("uncertain", (await store.GetAsync(action))!.State);
            Assert.Empty(await store.DueAsync());
            Assert.Equal(0, await store.RecoverInterruptedAsync());
        }
        finally { Directory.Delete(directory, true); }
    }
}
