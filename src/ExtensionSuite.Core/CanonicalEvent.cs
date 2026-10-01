using System.Text.Json.Nodes;

namespace ExtensionSuite.Core;

public enum EventProvenance { Live, Simulation, Replay }

public sealed record EventUser(string? PlatformUserId = null, string? Login = null,
    string? DisplayName = null, string? AvatarUrl = null, string[]? Badges = null);
public sealed record EventMessage(string? Text);
public sealed record EventMoney(long? MinorUnits, string? Currency, string ValuationKind);
public sealed record EventStream(string? Id, string? Title);
public sealed record EventMetrics(long? Value = null, string? Health = null);

public sealed record CanonicalEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public required string Source { get; init; }
    public required string Platform { get; init; }
    public required string Type { get; init; }
    public required string NativeType { get; init; }
    public string? NativeId { get; init; }
    public EventUser? User { get; init; }
    public EventMessage? Message { get; init; }
    public EventMoney? Monetary { get; init; }
    public EventStream? Stream { get; init; }
    public EventMetrics? Metrics { get; init; }
    public required string DedupeKey { get; init; }
    public JsonObject? Raw { get; init; }
    public EventProvenance Provenance { get; init; }
    public Guid? CorrelationId { get; init; }
    public string[] BridgePath { get; init; } = [];

    public void Validate()
    {
        if (Id.Version != 7 || OccurredAt.Offset != TimeSpan.Zero || ReceivedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Events require UUIDv7 identifiers and UTC timestamps.");
        if (new[] { Source, Platform, Type, NativeType, DedupeKey }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Event routing and dedupe fields are required.");
        if (!Enum.IsDefined(Provenance) || BridgePath.Length > 16)
            throw new ArgumentException("Invalid event provenance or bridge path.");
    }
}
