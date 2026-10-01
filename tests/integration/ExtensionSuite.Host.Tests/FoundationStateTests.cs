using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class FoundationStateTests
{
    [Fact]
    public async Task SqliteConfigurationWinsAfterRestartAndFailedFallbackRollsBack()
    {
        var root = Path.Combine(Path.GetTempPath(), "tdsblive-state-tests", Guid.NewGuid().ToString());
        var paths = new ApplicationPaths(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["TDSBLive:DataDirectory"] = root }).Build());
        try
        {
            await using var db = new FoundationDbContext(new DbContextOptionsBuilder<FoundationDbContext>().UseSqlite("Data Source=" + paths.Database).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261001085207_InitialFoundation");
            db.Events.Add(new StoredEvent { Id = Guid.CreateVersion7(), Source = "migration-test", Provenance = "Simulation",
                DedupeKey = "one", Type = "test", OccurredAtTicks = DateTimeOffset.UtcNow.UtcTicks, Json = "{}" });
            await db.SaveChangesAsync();
            await DatabaseLifecycle.InitializeAsync(db);
            Assert.Single(await db.Events.ToArrayAsync());
            var state = new FoundationStateStore(paths.Database);
            var store = new ConfigurationStore(paths, state);
            store.InitializePersistence(new ApplicationConfiguration());
            await store.SaveAsync(new ApplicationConfiguration { DisplayName = "Durable name" }, CancellationToken.None);
            File.Delete(paths.Configuration);
            var reopened = new ConfigurationStore(paths, new FoundationStateStore(paths.Database));
            Assert.Equal("Durable name", reopened.Load().DisplayName);
            Assert.Throws<IOException>(() => state.SaveConfiguration("{}", () => throw new IOException("Synthetic failure")));
            Assert.Equal("Durable name", reopened.Load().DisplayName);
            var sensitive = new SensitiveValues();
            sensitive.Set("test", "synthetic-private");
            using var provider = new RedactedFileLoggerProvider(paths, new ApplicationConfiguration(), sensitive, state);
            provider.CreateLogger("tests").LogInformation("Value {Value}", "synthetic-private");
            state.FlushLogs(CancellationToken.None);
            var log = Assert.Single(await db.Logs.ToArrayAsync());
            Assert.DoesNotContain("synthetic-private", log.Json);
            db.Logs.Add(new StoredLog { TimestampTicks = DateTime.UtcNow.AddDays(-15).Ticks, Json = "{}" });
            await db.SaveChangesAsync();
            provider.CreateLogger("tests").LogInformation("Retention test");
            state.FlushLogs(CancellationToken.None);
            Assert.Equal(2, await db.Logs.CountAsync());
            Assert.Equal(0, provider.WriteFailures);
            await db.Database.ExecuteSqlRawAsync("DROP TABLE Logs");
            provider.CreateLogger("tests").LogInformation("Database failure fallback");
            state.FlushLogs(CancellationToken.None);
            Assert.Equal(1, state.LogPersistenceFailures);
            Assert.Contains("Database failure fallback", File.ReadAllText(Path.Combine(paths.Logs, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl")));
            for (var index = 0; index < 1025; index++) state.AppendLog("{}", DateTimeOffset.UtcNow, 14);
            Assert.Equal(2, state.LogPersistenceFailures);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
