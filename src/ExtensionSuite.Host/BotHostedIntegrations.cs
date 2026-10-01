using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.StreamerBot;

namespace ExtensionSuite.Host;

public sealed class StreamerBotHostedIntegration(StreamerBotConnection connection, StreamerBotEventNormalizer normalizer,
    EventInspectorStore inspector, EventStore events, EditorEventHub hub, ApplicationConfiguration configuration,
    SensitiveValues sensitive) : IIsolatedIntegration
{
    public string Name => "streamerbot";
    public Task RunAsync(CancellationToken cancellationToken) => connection.RunAsync(ReceiveAsync, cancellationToken);
    private async Task ReceiveAsync(JsonObject payload, CancellationToken cancellationToken)
    {
        var result = normalizer.Normalize(payload, DateTimeOffset.UtcNow);
        inspector.Add(result, CredentialRedactor.Json(payload, sensitive.Snapshot()) as JsonObject ?? new JsonObject());
        if (result.Event is not { } item) return;
        if (item.Provenance == EventProvenance.Live)
            await events.AcceptAsync(item, item.NativeId ?? item.Id.ToString(), retainRaw: configuration.RetainRawEvents, cancellationToken: cancellationToken);
        else hub.Publish(item with { Raw = configuration.RetainRawEvents ? item.Raw : null });
    }
}

public sealed class SpeakerBotHostedIntegration(SpeakerBotConnection connection) : IIsolatedIntegration
{
    public string Name => "speakerbot";
    public Task RunAsync(CancellationToken cancellationToken) => connection.RunAsync(cancellationToken);
}
