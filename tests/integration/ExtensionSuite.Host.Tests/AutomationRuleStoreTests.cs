using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationRuleStoreTests
{
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
