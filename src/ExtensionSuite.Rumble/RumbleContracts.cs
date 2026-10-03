using System.Text.Json.Nodes;
using ExtensionSuite.Core;

namespace ExtensionSuite.Rumble;

public sealed record RumblePoll(DateTimeOffset ObservedAt, string Outcome, int? HttpStatus = null,
    JsonObject? Payload = null, TimeSpan? RetryAfter = null);
public sealed record RumbleDiagnostic(string Code, JsonObject Details);
public sealed record RumbleSnapshot(string Scope, int WindowSize, RumbleStream[] Streams,
    RumbleRecord[] Followers, RumbleRecord[] Subscriptions, JsonObject Raw, RumbleDiagnostic[] Diagnostics,
    bool FollowersWindowValid, bool SubscriptionsWindowValid);
public sealed record RumbleStream(string Id, string? Title, bool Live, long? Viewers, long? Likes,
    RumbleRecord[] Messages, RumbleRecord[] Rants, bool ChatWindowValid, bool RantWindowValid);
public sealed record RumbleRecord(string Kind, string Fingerprint, string CoreFingerprint,
    DateTimeOffset CreatedAt, string User, string? Text, string? Avatar, string[] Badges,
    long? Cents, string? StreamId, JsonObject Raw);

public sealed class RumbleState
{
    public Dictionary<string, RumbleScopeState> Scopes { get; set; } = [];
    public string? ActiveScope { get; set; }
    public string Health { get; set; } = "notStarted";
    public int ConsecutiveFailures { get; set; }
    public long PollSequence { get; set; }
}
public sealed class RumbleScopeState
{
    public long DuplicateRecords { get; set; }
    public long PossibleGaps { get; set; }
    public Dictionary<string, RumbleStreamState> Streams { get; set; } = [];
    public Dictionary<string, RumbleOccurrence> History { get; set; } = [];
    public HashSet<string> InitializedCollections { get; set; } = [];
    public DateTimeOffset? LastSuccess { get; set; }
}
public sealed class RumbleStreamState
{
    public bool Live { get; set; }
    public int OfflineObservations { get; set; }
    public long? Viewers { get; set; }
    public long? Likes { get; set; }
    public string? Title { get; set; }
    public string[] PreviousMessages { get; set; } = [];
    public bool PreviousWindowValid { get; set; }
}
public sealed class RumbleOccurrence
{
    public required string Kind { get; set; }
    public required string Fingerprint { get; set; }
    public int MaximumCount { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public long Sequence { get; set; }
}
public sealed record RumbleBatch(RumbleState State, CanonicalEvent[] Events, RumbleDiagnostic[] Diagnostics,
    bool Baseline, JsonObject? RawSnapshot);

public interface IRumbleStore
{
    Task<RumbleState> LoadAsync(string credentialContext, EventProvenance provenance, CancellationToken cancellationToken);
    Task<CanonicalEvent[]> CommitAsync(string credentialContext, EventProvenance provenance, RumbleBatch batch,
        bool retainRaw, bool forwardTriggers, CancellationToken cancellationToken);
}
