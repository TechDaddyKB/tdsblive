using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class RecoveryRestoreTests
{
    [Fact]
    public async Task RestoreStopsRealConsumersKeepsSafetyCopyAndStartsWithAutomationDisabled()
    {
        var parent = Path.Combine(Path.GetTempPath(), "tdsblive-restore-tests", Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "live");
        var app = await StartAsync(root);
        var paths = app.Services.GetRequiredService<ApplicationPaths>();
        try
        {
            var store = app.Services.GetRequiredService<ConfigurationStore>();
            var original = store.Load();
            await store.SaveAsync(original with
            {
                Server = original.Server with { EnableLan = true },
                StreamerBot = original.StreamerBot with { Enabled = true, ForwardLiveEvents = true },
                SpeakerBot = original.SpeakerBot with { Enabled = true },
                Rumble = original.Rumble with { Enabled = true, ForwardTriggers = true }
            }, default); // Persists for the next restart; live test services remain disabled.
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            var shutdown = await RecoveryShutdown.DrainAsync(app);
            Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
            Directory.CreateDirectory(paths.Secrets);
            var encryptedMarker = new byte[] { 1, 2, 3, 4 }; // Owned opaque marker, never a real credential.
            await File.WriteAllBytesAsync(Path.Combine(paths.Secrets, "owned-marker.dpapi"), encryptedMarker);
            var eventId = Guid.CreateVersion7();
            var item = new CanonicalEvent { Id = eventId, Source = "owned-test", Platform = "twitch", Type = "chat.message",
                NativeType = "OwnedRestoreExample", DedupeKey = "owned", OccurredAt = DateTimeOffset.UtcNow,
                Message = new("Owned restore example"), Provenance = EventProvenance.Live };
            var executionId = Guid.CreateVersion7();
            var action = new AutomationAction { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), RevertActionId = Guid.NewGuid(), DurationSeconds = 5 };
            var rule = new AutomationRule { Version = 1, Enabled = true, Condition = new("twitch", "chat.message"), Actions = [action] };
            await using (var db = Context(paths.Database))
            {
                db.AutomationRules.Add(new() { Id = rule.Id, Version = rule.Version, Json = JsonSerializer.Serialize(rule, EventStore.JsonOptions) });
                db.Events.Add(new() { Id = eventId, Source = "owned-test", Provenance = "Live", DedupeKey = "owned", Type = "chat.message",
                    Json = JsonSerializer.Serialize(item, EventStore.JsonOptions), OccurredAtTicks = item.OccurredAt.UtcTicks });
                db.AutomationInbox.Add(new() { EventId = eventId });
                db.Outbox.Add(new() { EventId = eventId });
                db.RumbleDeliveries.Add(new() { EventId = eventId, State = "pending" });
                db.AutomationExecutions.Add(new() { Id = executionId, EventId = eventId, RuleId = rule.Id, ActionId = action.Id,
                    State = "queued", DueAtTicks = DateTimeOffset.MaxValue.UtcTicks, Json = "{}" });
                var plan = new AutomationPlannedAction(rule.Id, rule.Version, action, "queued", "owned", "main", "queue", 20, 0);
                db.AutomationTemporaryEffects.Add(new() { ActionId = action.Id, Version = 1, State = "active", ExpiresAtTicks = 1,
                    Json = JsonSerializer.Serialize(new TemporaryEffectPayload(executionId, plan, []), EventStore.JsonOptions) });
                db.OverlayTokens.Add(new() { Id = Guid.NewGuid(), OverlayId = "combined-chat", Hash = new string('0', 64), ExpiresAtTicks = DateTimeOffset.MaxValue.UtcTicks });
                await db.SaveChangesAsync();
            }
            var archive = new RecoveryArchive(paths);
            using var bytes = new MemoryStream();
            await archive.CreateAsync(bytes);
            bytes.Position = 0;
            await using var prepared = await archive.PrepareAsync(bytes);
            await using (var db = Context(paths.Database))
            {
                db.Configurations.Add(new() { Name = "after-backup", Json = "{}" });
                await db.SaveChangesAsync();
            }
            var receipt = await new RecoveryRestore(paths, TimeProvider.System).ApplyAsync(prepared, shutdown);
            Assert.True(Directory.Exists(receipt.SafetyDirectory));
            Assert.Equal(encryptedMarker, await File.ReadAllBytesAsync(Path.Combine(paths.Secrets, "owned-marker.dpapi")));
            await using (var safety = Context(Path.Combine(receipt.SafetyDirectory, "tdsblive.db")))
                Assert.True(await safety.Configurations.AnyAsync(item => item.Name == "after-backup"));
            await using (var restored = Context(paths.Database))
            {
                Assert.False(await restored.Configurations.AnyAsync(item => item.Name == "after-backup"));
                Assert.Equal("cancelled", (await restored.AutomationExecutions.SingleAsync()).State);
                Assert.Equal("uncertain", (await restored.AutomationTemporaryEffects.SingleAsync()).State);
                Assert.True((await restored.AutomationInbox.SingleAsync()).Processed);
                Assert.NotNull((await restored.Outbox.SingleAsync()).DeliveredAtTicks);
                Assert.Equal("restored-suppressed", (await restored.RumbleDeliveries.SingleAsync()).State);
                Assert.True((await restored.OverlayTokens.SingleAsync()).Revoked);
                var restoredRule = JsonSerializer.Deserialize<AutomationRule>((await restored.AutomationRules.SingleAsync()).Json, EventStore.JsonOptions)!;
                Assert.False(restoredRule.Enabled);
                Assert.Equal(2, restoredRule.Version);
            }
            var restoredConfiguration = new ConfigurationStore(paths, new FoundationStateStore(paths.Database)).Load();
            Assert.False(restoredConfiguration.Server.EnableLan);
            Assert.False(restoredConfiguration.StreamerBot.Enabled);
            Assert.False(restoredConfiguration.StreamerBot.ForwardLiveEvents);
            Assert.False(restoredConfiguration.SpeakerBot.Enabled);
            Assert.False(restoredConfiguration.Rumble.Enabled);
            Assert.False(restoredConfiguration.Rumble.ForwardTriggers);
            // Linux test hosts have no vault loader. Remove the opaque marker before
            // Windows restart; valid DPAPI roundtrips are qualified by vault tests.
            File.Delete(Path.Combine(paths.Secrets, "owned-marker.dpapi"));
            await app.DisposeAsync();
            app = await StartAsync(root);
            Assert.Equal("disabled", app.Services.GetRequiredService<RumbleIntegration>().Status.State);
            await RecoveryShutdown.DrainAsync(app);
        }
        finally
        {
            await app.DisposeAsync();
            ClearPools(paths.Database);
            FoundationHostFactory.DeleteTemporaryDirectory(parent);
        }
    }

    [Fact]
    public async Task AShutdownPermitFromAnotherHostCannotReplaceLiveData()
    {
        var parent = Path.Combine(Path.GetTempPath(), "tdsblive-restore-tests", Guid.NewGuid().ToString("N"));
        var live = await StartAsync(Path.Combine(parent, "live"));
        var other = await StartAsync(Path.Combine(parent, "other"));
        var paths = live.Services.GetRequiredService<ApplicationPaths>();
        var otherPaths = other.Services.GetRequiredService<ApplicationPaths>();
        try
        {
            var archive = new RecoveryArchive(paths);
            using var bytes = new MemoryStream();
            await archive.CreateAsync(bytes);
            bytes.Position = 0;
            await using var prepared = await archive.PrepareAsync(bytes);
            var wrong = await RecoveryShutdown.DrainAsync(other);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new RecoveryRestore(paths, TimeProvider.System).ApplyAsync(prepared, wrong));
            Assert.True(File.Exists(paths.Database));
            Assert.False(live.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.IsCancellationRequested);
        }
        finally
        {
            await RecoveryShutdown.DrainAsync(live);
            await live.DisposeAsync();
            await other.DisposeAsync();
            ClearPools(paths.Database);
            ClearPools(otherPaths.Database);
            FoundationHostFactory.DeleteTemporaryDirectory(parent);
        }
    }

    private static async Task<WebApplication> StartAsync(string root)
    {
        var builder = WebApplication.CreateBuilder(new[] { "--TDSBLive:DataDirectory", root });
        builder.WebHost.UseTestServer();
        builder.AddFoundation();
        var app = builder.Build();
        await app.InitializeFoundationAsync();
        await app.StartAsync();
        return app;
    }
    private static FoundationDbContext Context(string database) => new(new DbContextOptionsBuilder<FoundationDbContext>()
        .UseSqlite(new SqliteConnectionStringBuilder { DataSource = database, ForeignKeys = true, Pooling = false }.ToString()).Options);
    private static void ClearPools(string database)
    {
        foreach (var timeout in new int?[] { null, 2 })
        {
            var options = new SqliteConnectionStringBuilder { DataSource = database, ForeignKeys = true };
            if (timeout is { } value) options.DefaultTimeout = value;
            using var connection = new SqliteConnection(options.ToString());
            SqliteConnection.ClearPool(connection);
        }
    }
}
