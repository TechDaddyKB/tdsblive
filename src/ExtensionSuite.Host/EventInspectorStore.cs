using System.Text;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.StreamerBot;

namespace ExtensionSuite.Host;

public sealed record InspectorEntry(Guid Id, DateTimeOffset ReceivedAt, string Classification, string? Limitation,
    CanonicalEvent? Event, JsonObject? Payload);

public sealed class EventInspectorStore(ApplicationConfiguration configuration)
{
    private readonly object sync = new();
    private readonly Queue<(InspectorEntry Entry, int Bytes)> entries = new();
    private int bytes;
    private readonly RumbleShapeInspector rumbleShapes = new();
    public long Discarded { get; private set; }

    public void Add(EventNormalization result, JsonObject sanitizedPayload)
    {
        var payload = configuration.RetainRawEvents ? sanitizedPayload.DeepClone() as JsonObject : null;
        if (payload is not null && result.Classification == "rumble.snapshot")
        {
            lock (sync) payload = new JsonObject { ["snapshot"] = payload, ["shapeInspection"] = rumbleShapes.Inspect(payload) };
        }
        var size = payload is null ? 0 : Encoding.UTF8.GetByteCount(payload.ToJsonString());
        var limitation = result.Limitation;
        if (size > 65536) { payload = null; size = 0; limitation = "inspectorPayloadTooLarge"; }
        var item = new InspectorEntry(Guid.CreateVersion7(), DateTimeOffset.UtcNow, result.Classification, limitation,
            result.Event is null ? null : result.Event with { Raw = null }, payload);
        // Normalized content also contributes to the bound; oversized entries retain routing metadata only.
        size += Encoding.UTF8.GetByteCount(System.Text.Json.JsonSerializer.Serialize(item));
        if (size > 131072) { item = item with { Event = null, Payload = null, Limitation = "inspectorEntryTooLarge" }; size = 1024; }
        lock (sync)
        {
            entries.Enqueue((item, size)); bytes += size;
            while (entries.Count > 500 || bytes > 2097152) { bytes -= entries.Dequeue().Bytes; Discarded++; }
        }
    }

    public InspectorEntry[] Read(string? filter = null, int limit = 100)
    {
        if (limit is < 1 or > 200 || filter?.Length > 128) throw new ArgumentException("Inspector query exceeds its bounds.");
        lock (sync) return entries.Reverse().Select(item => item.Entry)
            .Where(item => string.IsNullOrEmpty(filter) || string.Join(' ', item.Event?.NativeType, item.Classification, item.Event?.Message?.Text, item.Event?.User?.DisplayName).Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Take(limit).ToArray();
    }

    public InspectorEntry? Find(Guid id) { lock (sync) return entries.Select(item => item.Entry).FirstOrDefault(item => item.Id == id); }
}
