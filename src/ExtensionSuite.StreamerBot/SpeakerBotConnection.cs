using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;

namespace ExtensionSuite.StreamerBot;

public sealed class SpeakerBotConnection(IntegrationConfiguration configuration)
{
    private BotProtocolSession? session;
    private BotConnectionState state = new(configuration.Enabled ? "disconnected" : "disabled");
    private readonly object executionsSync = new();
    private readonly Queue<BotExecution> executions = new();
    public BotConnectionState State => Volatile.Read(ref state);
    public BotExecution[] Executions { get { lock (executionsSync) return executions.ToArray(); } }
    private static readonly string[] QueueOperations = ["Pause", "Resume", "Clear", "Stop", "Enable", "Disable", "Events", "Mode"];

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!configuration.Enabled) return;
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var current = new BotProtocolSession();
            try
            {
                Volatile.Write(ref state, new("connecting"));
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds));
                await current.ConnectAsync(new UriBuilder("ws", configuration.Host, configuration.Port, configuration.Endpoint).Uri, false, deadline.Token);
                Volatile.Write(ref session, current);
                // GetInfo is present in the tested 0.1.7 runtime, but is not part of the documented queue contract.
                // A rejected probe must not prevent connection to older runtimes.
                string? version = null;
                try
                {
                    var info = await current.RequestAsync("GetInfo", null, TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds), cancellationToken);
                    version = info["result"]?["version"]?.GetValue<string>();
                }
                catch (BotRequestException error) when (error.Kind == "rejected") { /* Optional runtime metadata. */ }
                Volatile.Write(ref state, new("connected", version));
                attempt = 0;
                await current.Completion.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception error) when (error is BotRequestException or WebSocketException or JsonException or InvalidOperationException or OperationCanceledException)
            {
                Volatile.Write(ref state, new("reconnecting", FailureKind: error is BotRequestException requestError ? requestError.Kind : "connectionFailure"));
            }
            finally { Volatile.Write(ref session, null); await current.DisposeAsync(); }
            var seconds = Math.Min(configuration.MaximumReconnectDelaySeconds, configuration.ReconnectDelaySeconds * Math.Pow(2, Math.Min(attempt++, 8)));
            try { await Task.Delay(TimeSpan.FromSeconds(seconds + Random.Shared.NextDouble() * seconds * 0.1), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
        }
        Volatile.Write(ref state, new("disconnected"));
    }

    public Task<BotExecution> SpeakAsync(string voice, string message, bool badWordFilter, bool executeLive, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voice) || voice.Length > 128 || string.IsNullOrWhiteSpace(message) || message.Length > 4096)
            throw new ArgumentException("Speech requires a bounded voice alias and message.");
        return ExecuteAsync("Speak", new JsonObject { ["voice"] = voice, ["message"] = message, ["badWordFilter"] = badWordFilter }, executeLive, cancellationToken);
    }

    public Task<BotExecution> QueueAsync(string operation, string? value, bool executeLive, CancellationToken cancellationToken)
    {
        if (!QueueOperations.Contains(operation)) throw new ArgumentException("Unsupported Speaker.bot queue operation.");
        var payload = new JsonObject();
        if (operation == "Events")
        {
            if (value is not ("on" or "off")) throw new ArgumentException("Events requires on or off.");
            payload["state"] = value;
        }
        if (operation == "Mode")
        {
            if (value is not ("all" or "command")) throw new ArgumentException("Mode requires all or command.");
            payload["mode"] = value;
        }
        return ExecuteAsync(operation, payload, executeLive, cancellationToken);
    }

    private async Task<BotExecution> ExecuteAsync(string operation, JsonObject payload, bool executeLive, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        var outcome = "simulated";
        var uncertain = false;
        if (executeLive)
        {
            var current = Volatile.Read(ref session);
            if (current is null || State.State != "connected") outcome = "disconnected";
            else
            {
                try { await current.RequestAsync(operation, payload, TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds), cancellationToken); outcome = "acknowledged"; }
                catch (BotRequestException error) { uncertain = error.MayHaveExecuted; outcome = uncertain ? "uncertain" : error.Kind; }
            }
        }
        var record = new BotExecution(id, operation, outcome, uncertain, DateTimeOffset.UtcNow);
        lock (executionsSync) { executions.Enqueue(record); while (executions.Count > 200) executions.Dequeue(); }
        return record;
    }
}
