using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;

namespace ExtensionSuite.StreamerBot;

public sealed record BotConnectionState(string State, string? Version = null, bool AuthenticationRequired = false, string? FailureKind = null);
public sealed record BotAction(Guid Id, string Name, bool Enabled, string? Group);
public sealed record BotCodeTrigger(string Name, string EventName, string Category);
public sealed record BotDiscovery(IReadOnlyDictionary<string, string[]> Events, BotAction[] Actions, BotCodeTrigger[] CodeTriggers,
    bool EventsSupported, bool ActionsSupported, bool CodeTriggersSupported);
public sealed record BotExecution(Guid Id, string Operation, string State, bool MayHaveExecuted, DateTimeOffset CreatedAt);

public sealed class StreamerBotConnection(IntegrationConfiguration configuration, Func<string?> credential, SensitiveValues sensitive)
{
    private BotProtocolSession? session;
    private BotConnectionState state = new(configuration.Enabled ? "disconnected" : "disabled");
    private BotDiscovery discovery = new(new Dictionary<string, string[]>(), [], [], false, false, false);
    private readonly object executionsSync = new();
    private readonly Queue<BotExecution> executions = new();
    public BotConnectionState State => Volatile.Read(ref state);
    public BotDiscovery Discovery => Volatile.Read(ref discovery);
    public BotExecution[] Executions { get { lock (executionsSync) return executions.ToArray(); } }

    public static string AuthenticationResponse(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    public async Task RunAsync(Func<JsonObject, CancellationToken, Task> onEvent, CancellationToken cancellationToken)
    {
        if (!configuration.Enabled) return;
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            using var connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var current = new BotProtocolSession();
            try
            {
                Volatile.Write(ref state, new("connecting"));
                using var connectDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectDeadline.CancelAfter(TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds));
                var uri = new UriBuilder("ws", configuration.Host, configuration.Port, configuration.Endpoint).Uri;
                var hello = await current.ConnectAsync(uri, true, connectDeadline.Token) ?? throw new BotRequestException("invalidHello");
                var version = hello["info"]?["version"]?.GetValue<string>();
                var authRequired = hello["authentication"] is JsonObject;
                Volatile.Write(ref session, current);
                await AuthenticateAsync(current, hello, version, authRequired, cancellationToken);
                Volatile.Write(ref state, new("discovering", version, authRequired));
                await RefreshDiscoveryAsync(cancellationToken);
                await SubscribeAsync(current, cancellationToken);
                Volatile.Write(ref state, new("connected", version, authRequired));
                attempt = 0;
                await RunConnectedAsync(current, onEvent, connectionLifetime);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception error) when (error is BotRequestException or WebSocketException or JsonException or InvalidOperationException or OperationCanceledException)
            {
                var kind = error is BotRequestException requestError ? requestError.Kind : "connectionFailure";
                var observed = State;
                Volatile.Write(ref state, new(kind == "authenticationFailed" || kind == "missingCredential" ? "authenticationFailed" : "reconnecting",
                    observed.Version, observed.AuthenticationRequired, kind));
            }
            finally { Volatile.Write(ref session, null); await current.DisposeAsync(); }
            var seconds = Math.Min(configuration.MaximumReconnectDelaySeconds, configuration.ReconnectDelaySeconds * Math.Pow(2, Math.Min(attempt++, 8)));
            try { await Task.Delay(TimeSpan.FromSeconds(seconds + Random.Shared.NextDouble() * seconds * 0.1), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
        }
        Volatile.Write(ref state, new("disconnected"));
    }

    private async Task RunConnectedAsync(BotProtocolSession current, Func<JsonObject, CancellationToken, Task> onEvent,
        CancellationTokenSource connectionLifetime)
    {
        var consuming = ConsumeAsync(current, onEvent, connectionLifetime.Token);
        try
        {
            var finished = await Task.WhenAny(current.Completion, consuming);
            await finished;
            throw new BotRequestException("disconnected");
        }
        finally
        {
            await connectionLifetime.CancelAsync();
            try { await consuming; }
            catch (OperationCanceledException) when (connectionLifetime.IsCancellationRequested) { /* Expected reconnect/shutdown. */ }
        }
    }

    private async Task AuthenticateAsync(BotProtocolSession current, JsonObject hello, string? version, bool required, CancellationToken cancellationToken)
    {
        if (!required) return;
        Volatile.Write(ref state, new("authenticating", version, true));
        var password = credential();
        if (string.IsNullOrEmpty(password)) throw new BotRequestException("missingCredential");
        var salt = hello["authentication"]?["salt"]?.GetValue<string>() ?? throw new BotRequestException("invalidHello");
        var challenge = hello["authentication"]?["challenge"]?.GetValue<string>() ?? throw new BotRequestException("invalidHello");
        try
        {
            await current.RequestAsync("Authenticate", new JsonObject { ["authentication"] = AuthenticationResponse(password, salt, challenge) }, Timeout, cancellationToken);
        }
        catch (BotRequestException error) when (error.Kind == "rejected") { throw new BotRequestException("authenticationFailed"); }
    }

    public async Task RefreshDiscoveryAsync(CancellationToken cancellationToken)
    {
        var current = Volatile.Read(ref session) ?? throw new BotRequestException("disconnected");
        var events = await OptionalRequestAsync(current, "GetEvents", cancellationToken);
        var actions = await OptionalRequestAsync(current, "GetActions", cancellationToken);
        var triggers = await OptionalRequestAsync(current, "GetCodeTriggers", cancellationToken);
        var eventMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (events?["events"] is JsonObject categories)
            foreach (var category in categories)
                if (category.Value is JsonArray values) eventMap[category.Key] = values.Select(value => value?.GetValue<string>() ?? "").Where(value => value.Length > 0).ToArray();
        var actionList = actions?["actions"] is JsonArray actionArray ? actionArray.OfType<JsonObject>().Select(ReadAction).Where(item => item is not null).Cast<BotAction>().ToArray() : [];
        var triggerList = triggers?["triggers"] is JsonArray triggerArray ? triggerArray.OfType<JsonObject>().Select(item => new BotCodeTrigger(
            Clean(item["name"]?.GetValue<string>() ?? ""), item["eventName"]?.GetValue<string>() ?? "", Clean(item["category"]?.GetValue<string>() ?? ""))).ToArray() : [];
        Volatile.Write(ref discovery, new(eventMap, actionList, triggerList, events?["events"] is JsonObject,
            actions?["actions"] is JsonArray, triggers?["triggers"] is JsonArray));
    }

    private BotAction? ReadAction(JsonObject item) => Guid.TryParse(item["id"]?.GetValue<string>(), out var id)
        ? new(id, Clean(item["name"]?.GetValue<string>() ?? ""), item["enabled"]?.GetValue<bool>() ?? false, Clean(item["group"]?.GetValue<string>() ?? "")) : null;
    private string Clean(string value) => CredentialRedactor.Text(value, sensitive.Snapshot());
    private TimeSpan Timeout => TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds);

    private async Task<JsonObject?> OptionalRequestAsync(BotProtocolSession current, string request, CancellationToken cancellationToken)
    {
        try { return await current.RequestAsync(request, null, Timeout, cancellationToken); }
        catch (BotRequestException error) when (error.Kind == "rejected") { return null; }
    }

    private async Task SubscribeAsync(BotProtocolSession current, CancellationToken cancellationToken)
    {
        var selected = new JsonObject();
        foreach (var category in Discovery.Events.Where(category => new[] { "Twitch", "YouTube", "Kick", "Kofi", "General", "Custom", "Raw" }.Contains(category.Key, StringComparer.OrdinalIgnoreCase)))
            selected[category.Key] = new JsonArray(category.Value.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        if (selected.Count > 0) await current.RequestAsync("Subscribe", new JsonObject { ["events"] = selected }, Timeout, cancellationToken);
    }

    private async Task ConsumeAsync(BotProtocolSession current, Func<JsonObject, CancellationToken, Task> onEvent, CancellationToken cancellationToken)
    {
        await foreach (var item in current.Events.ReadAllAsync(cancellationToken))
        {
            try { await onEvent(item, cancellationToken); }
            catch (Exception error) when (error is not OperationCanceledException) { Record("event", "processingFailed", false); }
        }
    }

    public async Task<BotExecution> ExecuteActionAsync(Guid actionId, JsonObject arguments, bool executeLive, CancellationToken cancellationToken)
    {
        if (!executeLive) return Record("DoAction", "simulated", false);
        if (!configuration.AllowedActionIds.Contains(actionId)) return Record("DoAction", "actionNotSelected", false);
        var action = Discovery.Actions.SingleOrDefault(item => item.Id == actionId);
        if (action is null || !action.Enabled) return Record("DoAction", "missingAction", false);
        return await ExecuteAsync("DoAction", new JsonObject { ["action"] = new JsonObject { ["id"] = actionId.ToString() }, ["args"] = arguments.DeepClone() }, cancellationToken);
    }

    public async Task<BotExecution> ExecuteCodeTriggerAsync(string eventName, JsonObject arguments, bool executeLive, CancellationToken cancellationToken)
    {
        if (!executeLive) return Record("ExecuteCodeTrigger", "simulated", false);
        if (!configuration.ForwardLiveEvents) return Record("ExecuteCodeTrigger", "forwardingDisabled", false);
        if (!Discovery.CodeTriggersSupported) return Record("ExecuteCodeTrigger", "unsupportedCapability", false);
        if (!Discovery.CodeTriggers.Any(item => item.EventName == eventName)) return Record("ExecuteCodeTrigger", "missingTrigger", false);
        return await ExecuteAsync("ExecuteCodeTrigger", new JsonObject { ["triggerName"] = eventName, ["args"] = arguments.DeepClone() }, cancellationToken);
    }

    private async Task<BotExecution> ExecuteAsync(string operation, JsonObject payload, CancellationToken cancellationToken)
    {
        var current = Volatile.Read(ref session);
        if (current is null || State.State != "connected") return Record(operation, "disconnected", false);
        try { await current.RequestAsync(operation, payload, Timeout, cancellationToken); return Record(operation, "acknowledged", false); }
        catch (BotRequestException error) { return Record(operation, error.MayHaveExecuted ? "uncertain" : error.Kind, error.MayHaveExecuted); }
    }

    private BotExecution Record(string operation, string outcome, bool mayHaveExecuted)
    {
        var record = new BotExecution(Guid.CreateVersion7(), operation, outcome, mayHaveExecuted, DateTimeOffset.UtcNow);
        lock (executionsSync) { executions.Enqueue(record); while (executions.Count > 200) executions.Dequeue(); }
        return record;
    }
}
