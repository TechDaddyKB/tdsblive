using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Rumble;
using ExtensionSuite.StreamerBot;

namespace ExtensionSuite.Host;

public sealed record RumbleStatus(string State, bool Enabled, bool CredentialPresent, DateTimeOffset? LastPollAt,
    DateTimeOffset? NextPollAt, int ConsecutiveFailures, bool BaselineEstablished, long PollSequence,
    bool ForwardTriggers, string[] LiveStreams, bool SubscriptionsLiveVerified = false, bool GiftsAuthoritative = false);

public sealed class RumbleIntegration(IRumbleStore store, RumbleHttpTransport transport, ApplicationConfiguration configuration,
    SensitiveValues sensitive, EventInspectorStore inspector, TimeProvider clock) : IIsolatedIntegration
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly RumbleSnapshotEngine engine = new(sensitive, configuration.Rumble.OfflineConfirmationPolls);
    private bool baseline = true;
    private bool sessionEnabled;
    private string? lastCredential;
    private RumbleStatus status = new("disabled", false, false, null, null, 0, false, 0, configuration.Rumble.ForwardTriggers && configuration.StreamerBot.ForwardLiveEvents, []);
    public string Name => "rumble";
    public RumbleStatus Status => Volatile.Read(ref status);

    public async Task SetCredentialAsync(string value, bool sessionOnly, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!RumbleHttpTransport.ValidCredential(value)) throw new ArgumentException("Use the official Rumble HTTPS Live API URL.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            var context = sensitive.Get("rumble-url") == value ? sensitive.Get("rumble-context") : Guid.NewGuid().ToString();
            context ??= Guid.NewGuid().ToString();
            if (!sessionOnly)
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                var vault = services.GetRequiredService<WindowsSecretVault>();
                await vault.SetAsync("rumble-url", value, cancellationToken);
                await vault.SetAsync("rumble-context", context, cancellationToken);
            }
            sensitive.Set("rumble-url", value); sensitive.Set("rumble-api-key", RumbleHttpTransport.CredentialKey(value)!);
            sensitive.Set("rumble-context", context); lastCredential = value;
            sessionEnabled = true; baseline = true;
            Volatile.Write(ref status, Status with { State = "waiting", Enabled = true, CredentialPresent = true, BaselineEstablished = false });
        }
        finally { gate.Release(); }
    }

    public async Task ResetBaselineAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try { baseline = true; Volatile.Write(ref status, Status with { BaselineEstablished = false }); }
        finally { gate.Release(); }
    }
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            sensitive.Remove("rumble-url"); sensitive.Remove("rumble-api-key"); sensitive.Remove("rumble-context"); lastCredential = null; sessionEnabled = false; baseline = true;
            Volatile.Write(ref status, Status with { State = "missingCredential", CredentialPresent = false, Enabled = false, NextPollAt = null });
        }
        finally { gate.Release(); }
    }

    public async Task<TimeSpan> PollOnceAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var credential = sensitive.Get("rumble-url");
            if (!(configuration.Rumble.Enabled || sessionEnabled) || credential is null)
            {
                Volatile.Write(ref status, Status with { State = configuration.Rumble.Enabled ? "missingCredential" : "disabled", CredentialPresent = credential is not null });
                return TimeSpan.FromSeconds(1);
            }
            var context = sensitive.Get("rumble-context");
            if (lastCredential != credential)
            {
                if (lastCredential is not null) context = null;
                baseline = true; lastCredential = credential;
            }
            if (RumbleHttpTransport.CredentialKey(credential) is { Length: > 0 } apiKey) sensitive.Set("rumble-api-key", apiKey);
            if (context is null) { context = Guid.NewGuid().ToString(); sensitive.Set("rumble-context", context); }
            var state = await store.LoadAsync(context, EventProvenance.Live, cancellationToken);
            var poll = await transport.PollAsync(credential, cancellationToken);
            var batch = engine.Reconcile(state, poll, context, baseline);
            var accepted = await store.CommitAsync(context, EventProvenance.Live, batch, configuration.RetainRawEvents,
                configuration.Rumble.ForwardTriggers && configuration.StreamerBot.ForwardLiveEvents, cancellationToken);
            if (batch.RawSnapshot is not null) baseline = false;
            foreach (var item in accepted) inspector.Add(new(item, "rumble", item.Type == "rumble.subscription.candidate" ? "liveUnverified" : null), item.Raw ?? new());
            foreach (var diagnostic in batch.Diagnostics) inspector.Add(new(null, diagnostic.Code), diagnostic.Details);
            if (batch.RawSnapshot is not null) inspector.Add(new(null, "rumble.snapshot", "unknownFieldsRetained"), batch.RawSnapshot);
            var delay = RumbleHttpTransport.NextDelay(configuration.Rumble.PollIntervalSeconds, batch.State.ConsecutiveFailures, poll.RetryAfter, Random.Shared.NextDouble());
            var scope = batch.State.ActiveScope is { } key ? batch.State.Scopes[key] : null;
            Volatile.Write(ref status, new(batch.State.Health, true, true, poll.ObservedAt, RumbleHttpTransport.ScheduledAt(clock.GetUtcNow(), delay),
                batch.State.ConsecutiveFailures, !baseline, batch.State.PollSequence, configuration.Rumble.ForwardTriggers && configuration.StreamerBot.ForwardLiveEvents,
                scope?.Streams.Where(item => item.Value.Live).Select(item => item.Key).ToArray() ?? []));
            return delay;
        }
        finally { gate.Release(); }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try { delay = await PollOnceAsync(cancellationToken); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // State acceptance failed; leave its checkpoint/baseline unchanged and retry independently.
                delay = TimeSpan.FromSeconds(5);
                Volatile.Write(ref status, Status with { State = "processingFailure", NextPollAt = clock.GetUtcNow() + delay });
                inspector.Add(new(null, "rumble.processingFailure"), new() { ["errorType"] = error.GetType().Name });
            }
            await RumbleHttpTransport.WaitDelayAsync(delay, clock, cancellationToken);
        }
    }
}

public sealed class RumbleTriggerDispatcher(RumbleStore store, StreamerBotConnection streamer,
    ApplicationConfiguration configuration, EventInspectorStore inspector, TimeProvider clock) : IIsolatedIntegration
{
    private DateTimeOffset lastDiscoveryRefresh = DateTimeOffset.MinValue;
    public string Name => "rumble-trigger-delivery";
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await store.RecoverUncertainAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (configuration.Rumble.ForwardTriggers && configuration.StreamerBot.ForwardLiveEvents && streamer.State.State == "connected")
            {
                if (clock.GetUtcNow() - lastDiscoveryRefresh >= TimeSpan.FromSeconds(30))
                {
                    lastDiscoveryRefresh = clock.GetUtcNow(); await streamer.RefreshDiscoveryAsync(cancellationToken);
                    var registered = TriggerArgumentMapper.EventNames.Where(pair => streamer.Discovery.CodeTriggers.Any(trigger => trigger.EventName == pair.Value))
                        .Select(pair => pair.Key).ToArray();
                    await store.ResumeRegisteredAsync(registered, cancellationToken);
                }
                foreach (var item in await store.PendingTriggersAsync(cancellationToken))
                {
                    if (!TriggerArgumentMapper.EventNames.TryGetValue(item.Type, out var name))
                    {
                        await store.ParkPendingAsync(item.Id, "unmapped", cancellationToken); continue;
                    }
                    if (!streamer.Discovery.CodeTriggers.Any(trigger => trigger.EventName == name))
                    {
                        await store.ParkPendingAsync(item.Id, "waitingForTrigger", cancellationToken); continue;
                    }
                    if (!await store.ClaimAsync(item.Id, cancellationToken)) continue;
                    var result = await streamer.ExecuteCodeTriggerAsync(name, TriggerArgumentMapper.Map(item), true, cancellationToken);
                    await store.FinishAsync(item.Id, result.State, cancellationToken);
                    inspector.Add(new(null, "rumble.triggerDelivery", result.State), new() { ["eventId"] = item.Id.ToString(), ["triggerName"] = name,
                        ["outcome"] = result.State, ["mayHaveExecuted"] = result.MayHaveExecuted });
                }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }
}
