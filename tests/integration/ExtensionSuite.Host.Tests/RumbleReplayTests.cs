using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Rumble;
using ExtensionSuite.StreamerBot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class RumbleReplayTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "tdsblive-rumble-tests", Guid.NewGuid().ToString());
    private PooledDbContextFactory<FoundationDbContext> factory = null!;
    private RumbleStore store = null!;
    private readonly RumbleSnapshotEngine engine = new(new());
    private static string Root
    {
        get
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "TDSBLive.slnx"))) folder = folder.Parent;
            return folder?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(directory);
        factory = NewFactory(); store = new(factory, new());
        await using var db = factory.CreateDbContext(); await DatabaseLifecycle.InitializeAsync(db);
    }
    private PooledDbContextFactory<FoundationDbContext> NewFactory() => new(new DbContextOptionsBuilder<FoundationDbContext>()
        .UseSqlite($"Data Source={Path.Combine(directory, "rumble.db")};Foreign Keys=True;Pooling=False").Options);
    public static IEnumerable<object[]> Scenarios => Directory.EnumerateFiles(Path.Combine(Root, "tests/fixtures/rumble/synthetic"), "*.json")
        .Where(path => Path.GetFileName(path) != "index.json").Select(path => new object[] { Path.GetFileName(path) });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task SyntheticFixtureRunsThroughActualEngineAndTransactionalStore(string file)
    {
        var scenario = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Root, "tests/fixtures/rumble/synthetic", file)))!.AsObject();
        var output = new List<CanonicalEvent>(); var diagnostics = new List<RumbleDiagnostic>(); var offlineSteps = new List<int>();
        var context = "synthetic-a"; var baseline = true; var stepNumber = 0;
        foreach (var step in scenario["steps"]!.AsArray().OfType<JsonObject>())
        {
            stepNumber++;
            if (step["kind"]!.GetValue<string>() == "control")
            {
                baseline = true;
                if (step["op"]!.GetValue<string>() == "credential-change") context = "synthetic-b";
                store = new(NewFactory(), new()); // Restart is an actual SQLite reopen, not a cleared dictionary.
                continue;
            }
            var observed = DateTimeOffset.Parse(step["observed_at"]!.GetValue<string>(), CultureInfo.InvariantCulture).ToUniversalTime();
            var after = step["retry_after"]?.GetValue<string>();
            var retry = after is null ? (TimeSpan?)null : double.TryParse(after, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                ? TimeSpan.FromSeconds(seconds) : DateTimeOffset.Parse(after, CultureInfo.InvariantCulture) - observed;
            var poll = new RumblePoll(observed, step["outcome"]!.GetValue<string>(), step["http_status"]?.GetValue<int>(), step["payload"] as JsonObject, retry);
            var prior = await store.LoadAsync(context, EventProvenance.Replay, default);
            var batch = engine.Reconcile(prior, poll, context, baseline, EventProvenance.Replay);
            var accepted = await store.CommitAsync(context, EventProvenance.Replay, batch, true, true, default);
            if (batch.RawSnapshot is not null) baseline = false;
            if (accepted.Any(item => item.Type == "stream.offline")) offlineSteps.Add(stepNumber);
            if (stepNumber == scenario["expected"]?["noHistoricalAlertsAtStep"]?.GetValue<int>()) Assert.DoesNotContain(accepted, item => item.Type == "chat.message");
            if (batch.Baseline) Assert.DoesNotContain(accepted, item => item.Type is "chat.message" or "support.rant" or "community.follow" or "rumble.subscription.candidate");
            if (retry is { } minimum) Assert.True(RumbleHttpTransport.NextDelay(7, batch.State.ConsecutiveFailures, retry, 0) >= minimum);
            output.AddRange(accepted); diagnostics.AddRange(batch.Diagnostics);
            if (scenario["expected"]?["preserveUnknown"]?.GetValue<bool>() == true && batch.RawSnapshot?["future_data"] is { } unknown)
                Assert.True(JsonNode.DeepEquals(unknown, step["payload"]!["future_data"]));
        }
        var expected = scenario["expected"]!.AsObject();
        foreach (var (key, type) in new[] { ("chat", "chat.message"), ("rants", "support.rant"), ("follows", "community.follow"),
            ("online", "stream.online"), ("offline", "stream.offline"), ("subscriptions", "rumble.subscription.candidate"),
            ("viewerChanges", "stream.viewers"), ("likeChanges", "stream.likes"), ("authoritativeGifts", "support.gift") })
            if (expected[key] is { } count) Assert.Equal(count.GetValue<int>(), output.Count(item => item.Type == type));
        if (expected["possibleGaps"] is { } gaps) Assert.Equal(gaps.GetValue<int>(), diagnostics.Count(item => item.Code == "rumble.chat.possible_gap"));
        if (expected["offlineAtStep"] is { } offline) Assert.Equal(offline.GetValue<int>(), Assert.Single(offlineSteps));
        if (expected["oldStreamOfflineAtStep"] is { } oldOffline) Assert.Equal(oldOffline.GetValue<int>(), Assert.Single(offlineSteps));
        if (expected["usdAmountMinor"] is { } cents) Assert.Equal(cents.GetValue<long>(), Assert.Single(output, item => item.Type == "support.rant").Monetary!.MinorUnits);
        if (expected["amountConflictDiagnostic"]?.GetValue<bool>() == true) Assert.Contains(diagnostics, item => item.Code == "rantAmountConflict");
        if (expected["requireExplicitMutationPolicy"]?.GetValue<bool>() == true)
        {
            Assert.Contains(diagnostics, item => item.Code == "chatBadgeMutationAmbiguous"); Assert.DoesNotContain(output, item => item.Type == "chat.message");
        }
        if (expected["financialEntries"] is { } financial) Assert.Equal(financial.GetValue<int>(), output.Count(item => item.Type == "support.rant")); // Idempotent support input; ledger is G07.
        Assert.Equal(output.Count, output.Select(item => item.DedupeKey).Distinct().Count());
        Assert.All(output, item => { item.Validate(); Assert.Equal(EventProvenance.Replay, item.Provenance); });
        Assert.Empty(await store.PendingTriggersAsync(default)); // Replay never queues live automation.
    }

    [Fact]
    public async Task All785CapturedPollsReconcileWithoutHistoricalAlertsOrDuplicateEvents()
    {
        var path = Path.Combine(Root, "artifacts/rumble-replay/captured.jsonl");
        Assert.True(File.Exists(path), "Run python tools/prepare_rumble_replay.py before .NET tests.");
        var output = new List<CanonicalEvent>(); var diagnostics = new List<RumbleDiagnostic>(); var count = 0; var baseline = true;
        foreach (var line in await File.ReadAllLinesAsync(path))
        {
            var row = JsonNode.Parse(line)!.AsObject(); count++;
            Assert.Equal(count, row["sourcePollId"]!.GetValue<int>());
            var poll = new RumblePoll(DateTimeOffset.Parse(row["observed_at"]!.GetValue<string>(), CultureInfo.InvariantCulture).ToUniversalTime(), "ok", 200, row["payload"]!.AsObject());
            var batch = engine.Reconcile(await store.LoadAsync("capture", EventProvenance.Replay, default), poll, "capture", baseline, EventProvenance.Replay);
            var accepted = await store.CommitAsync("capture", EventProvenance.Replay, batch, true, true, default);
            if (batch.RawSnapshot is not null) baseline = false;
            output.AddRange(accepted); diagnostics.AddRange(batch.Diagnostics);
        }
        Assert.Equal(785, count); Assert.Equal(97, output.Count(item => item.Type == "chat.message"));
        Assert.Equal(4, output.Count(item => item.Type == "community.follow")); Assert.DoesNotContain(output, item => item.Type == "support.rant");
        Assert.Equal(1, output.Count(item => item.Type == "stream.online")); Assert.Equal(1, output.Count(item => item.Type == "stream.offline"));
        Assert.DoesNotContain(diagnostics, item => item.Code == "rumble.chat.possible_gap");
        Assert.Equal(output.Count, output.Select(item => item.Id).Distinct().Count());
        await using var db = factory.CreateDbContext(); Assert.Equal(output.Count, await db.Events.CountAsync()); Assert.Equal(output.Count, await db.Outbox.CountAsync());
        Assert.Equal("785", (await db.Checkpoints.SingleAsync()).Value); Assert.Empty(await db.RumbleDeliveries.ToArrayAsync());
    }

    [Fact]
    public async Task SnapshotCommitRollsBackStateEventsCheckpointAndBothOutboxesTogether()
    {
        await using var db = factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_rumble BEFORE INSERT ON RumbleStates BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END;");
        var poll = new RumblePoll(DateTimeOffset.UtcNow, "timeout");
        var batch = engine.Reconcile(new(), poll, "rollback", false);
        await Assert.ThrowsAsync<DbUpdateException>(() => store.CommitAsync("rollback", EventProvenance.Live, batch, true, true, default));
        Assert.Empty(await db.Events.ToArrayAsync()); Assert.Empty(await db.Outbox.ToArrayAsync()); Assert.Empty(await db.RumbleDeliveries.ToArrayAsync());
        Assert.Empty(await db.Checkpoints.ToArrayAsync()); Assert.Empty(await db.RumbleStates.ToArrayAsync());
    }

    [Fact]
    public async Task TriggerClaimCrashRecoveryDoesNotReissueUncertainAutomation()
    {
        var batch = engine.Reconcile(new(), new(DateTimeOffset.UtcNow, "timeout"), "delivery", false);
        var item = Assert.Single(await store.CommitAsync("delivery", EventProvenance.Live, batch, true, true, default));
        Assert.True(await store.ClaimAsync(item.Id, default)); Assert.False(await store.ClaimAsync(item.Id, default));
        await new RumbleStore(NewFactory(), new()).RecoverUncertainAsync(default);
        Assert.Empty(await store.PendingTriggersAsync(default)); Assert.Equal(1, (await store.DeliveryStatusAsync(default))["uncertain"]);
    }

    [Fact]
    public async Task DispatcherRefreshesLateRegistrationAndDeliversOneCanonicalTrigger()
    {
        var triggerDiscovery = 0; var executions = 0;
        var batch = engine.Reconcile(new(), new(DateTimeOffset.UtcNow, "timeout"), "late-bootstrap", false);
        var waiting = Enumerable.Range(0, 64).Select(index => new CanonicalEvent {
            Source = "rumble", Platform = "rumble", Type = "stream.viewers", NativeType = "viewers",
            OccurredAt = DateTimeOffset.UtcNow, ReceivedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            DedupeKey = $"waiting-viewer-{index}", Metrics = new(index), Stream = new("synthetic-stream", "Test") }).ToArray();
        await store.CommitAsync("waiting", EventProvenance.Live, new(new(), waiting, [], false, null), true, true, default);
        var item = Assert.Single(await store.CommitAsync("late-bootstrap", EventProvenance.Live, batch, true, true, default));
        await using var server = await FakeBot.Start(async socket =>
        {
            await FakeBot.Send(socket, new() { ["request"] = "Hello" });
            while (socket.State == WebSocketState.Open)
            {
                var request = await FakeBot.Read(socket); var response = new JsonObject();
                if (request["request"]!.GetValue<string>() == "GetCodeTriggers")
                {
                    response["triggers"] = ++triggerDiscovery == 1 ? new JsonArray() : new JsonArray(new JsonObject {
                        ["eventName"] = "tdsblive.rumble.health", ["name"] = "Health", ["category"] = "Tests" });
                }
                if (request["request"]!.GetValue<string>() == "ExecuteCodeTrigger")
                {
                    Interlocked.Increment(ref executions);
                    Assert.Equal("tdsblive.rumble.health", request["triggerName"]!.GetValue<string>());
                    Assert.Equal(item.Id.ToString(), request["args"]!["tdsbliveEventId"]!.GetValue<string>());
                    Assert.Equal("timeout", request["args"]!["health"]!.GetValue<string>());
                    Assert.Null(request["args"]!["raw"]);
                }
                await FakeBot.Reply(socket, request, response);
            }
        });
        var config = new ApplicationConfiguration { StreamerBot = server.Configuration with { ForwardLiveEvents = true }, Rumble = new() { ForwardTriggers = true } };
        var adapter = new StreamerBotConnection(config.StreamerBot, () => null, new());
        // This exercises 64 individual SQLite park transactions before delivery.
        // Windows coverage instrumentation can exceed ten seconds without a protocol failure.
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var run = adapter.RunAsync((_, _) => Task.CompletedTask, lifetime.Token);
        var dispatch = Task.CompletedTask;
        try
        {
            while (adapter.State.State != "connected") await Task.Delay(20, lifetime.Token);
            dispatch = new RumbleTriggerDispatcher(store, adapter, config, new EventInspectorStore(config), TimeProvider.System).RunAsync(lifetime.Token);
            while (!(await store.DeliveryStatusAsync(lifetime.Token)).ContainsKey("acknowledged")) await Task.Delay(20, lifetime.Token);
            Assert.Equal(1, executions); Assert.True(triggerDiscovery >= 2); Assert.Empty(await store.PendingTriggersAsync(default));
            Assert.Equal(64, (await store.DeliveryStatusAsync(default))["waitingForTrigger"]);
            await lifetime.CancelAsync(); await run.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatch.WaitAsync(TimeSpan.FromSeconds(3)));
        }
        finally
        {
            // Assertions/timeouts must also stop database users before fixture deletion on Windows.
            await lifetime.CancelAsync();
            try { await Task.WhenAll(run, dispatch); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { /* Expected shutdown. */ }
        }
        // A later registration resumes only its parked rows, including after reopening SQLite.
        var reopened = new RumbleStore(NewFactory(), new());
        await reopened.ResumeRegisteredAsync(["integration.health"], default);
        Assert.Empty(await reopened.PendingTriggersAsync(default));
        await reopened.ResumeRegisteredAsync(["stream.viewers"], default);
        Assert.Equal(64, (await reopened.PendingTriggersAsync(default)).Length);
        Assert.Equal(1, (await reopened.DeliveryStatusAsync(default))["acknowledged"]);
    }

    [Fact]
    public void ChatRetentionKeepsNewest10000AndRecent24HoursWhileFinancialIdentityNeverExpires()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = JsonNode.Parse("""{"type":"user","user_id":"synthetic-account","livestreams":[]}""")!.AsObject();
        var snapshot = RumbleSnapshotParser.Parse(payload, "retention", new());
        var scope = new RumbleScopeState();
        for (var index = 0; index < 10002; index++) scope.History[index.ToString(CultureInfo.InvariantCulture)] = new() {
            Kind = "chat.message", Fingerprint = index.ToString(CultureInfo.InvariantCulture), MaximumCount = 1,
            LastSeen = index == 0 ? now : now.AddHours(-25), Sequence = index };
        scope.History["financial"] = new() { Kind = "support.rant", Fingerprint = "financial", MaximumCount = 1, LastSeen = now.AddYears(-1) };
        var state = new RumbleState { ActiveScope = snapshot.Scope, Scopes = new() { [snapshot.Scope] = scope } };
        var batch = engine.Reconcile(state, new(now, "ok", 200, payload), "retention", false);
        Assert.Equal(10002, batch.State.Scopes[snapshot.Scope].History.Count);
        Assert.Contains("0", batch.State.Scopes[snapshot.Scope].History.Keys); Assert.DoesNotContain("1", batch.State.Scopes[snapshot.Scope].History.Keys);
        Assert.Contains("financial", batch.State.Scopes[snapshot.Scope].History.Keys); Assert.Equal(10003, scope.History.Count); // Input state remains unchanged until commit.
    }

    [Fact]
    public async Task CredentialRotationDoesNotDuplicatePreviouslyAcceptedRantMoney()
    {
        var path = Path.Combine(Root, "tests/fixtures/rumble/synthetic/rant-repeat-expiry.json");
        var scenario = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        var empty = scenario["steps"]![0]!["payload"]!.AsObject(); var withRant = scenario["steps"]![1]!["payload"]!.AsObject();
        var now = DateTimeOffset.UtcNow;
        foreach (var context in new[] { "credential-a", "credential-b" })
        {
            var first = engine.Reconcile(await store.LoadAsync(context, EventProvenance.Live, default), new(now, "ok", 200, empty), context, true);
            await store.CommitAsync(context, EventProvenance.Live, first, true, false, default);
            var second = engine.Reconcile(await store.LoadAsync(context, EventProvenance.Live, default), new(now.AddSeconds(7), "ok", 200, withRant), context, false);
            await store.CommitAsync(context, EventProvenance.Live, second, true, false, default);
        }
        Assert.Single(await new EventStore(factory).ReadAsync(), item => item.Type == "support.rant");
    }

    [Fact]
    public void MissingCollectionGetsItsOwnBaselineIncludingAfterRestart()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = JsonNode.Parse("""{"type":"user","user_id":"synthetic-account","livestreams":[{"id":"stream","is_live":true}]}""")!.AsObject();
        var initial = engine.Reconcile(new(), new(now, "ok", 200, payload), "late-array", true);
        var stream = payload["livestreams"]![0]!.AsObject();
        var messages = new JsonArray(new JsonObject { ["username"] = "user", ["text"] = "historical", ["created_on"] = "2026-01-01T00:00:01Z" });
        stream["chat"] = new JsonObject { ["recent_messages"] = messages };
        var late = engine.Reconcile(initial.State, new(now.AddSeconds(7), "ok", 200, payload), "late-array", false);
        Assert.DoesNotContain(late.Events, item => item.Type == "chat.message");
        messages.Add(new JsonObject { ["username"] = "user", ["text"] = "new", ["created_on"] = "2026-01-01T00:00:02Z" });
        var fresh = engine.Reconcile(late.State, new(now.AddSeconds(14), "ok", 200, payload), "late-array", false);
        Assert.Single(fresh.Events, item => item.Type == "chat.message");
        stream.Remove("chat");
        var restarted = engine.Reconcile(fresh.State, new(now.AddSeconds(21), "ok", 200, payload), "late-array", true);
        messages.Add(new JsonObject { ["username"] = "user", ["text"] = "downtime", ["created_on"] = "2026-01-01T00:00:03Z" });
        stream["chat"] = new JsonObject { ["recent_messages"] = messages.DeepClone() };
        var recovered = engine.Reconcile(restarted.State, new(now.AddSeconds(28), "ok", 200, payload), "late-array", false);
        Assert.DoesNotContain(recovered.Events, item => item.Type == "chat.message");
    }
    public Task DisposeAsync() { Directory.Delete(directory, true); return Task.CompletedTask; }
}
