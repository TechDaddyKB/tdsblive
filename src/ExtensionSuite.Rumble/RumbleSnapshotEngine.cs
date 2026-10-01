using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;

namespace ExtensionSuite.Rumble;

public sealed class RumbleSnapshotEngine(SensitiveValues sensitive, int offlineConfirmationPolls = 2)
{
    public RumbleBatch Reconcile(RumbleState previous, RumblePoll poll, string credentialContext,
        bool startupBaseline, EventProvenance provenance = EventProvenance.Live)
    {
        if (poll.ObservedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Poll time must be UTC.");
        var state = JsonSerializer.Deserialize<RumbleState>(JsonSerializer.Serialize(previous))!;
        state.PollSequence++;
        var events = new List<CanonicalEvent>(); var diagnostics = new List<RumbleDiagnostic>();
        RumbleSnapshot? snapshot = null;
        var outcome = poll.Outcome;
        if (outcome == "ok")
        {
            try { snapshot = RumbleSnapshotParser.Parse(poll.Payload ?? throw new RumbleShapeException(), credentialContext, sensitive); }
            catch (RumbleShapeException) { outcome = "unexpected_json"; }
        }
        if (snapshot is null)
        {
            state.ConsecutiveFailures++;
            Health(state, outcome, poll, events, provenance);
            diagnostics.Add(new("pollFailure", new() { ["outcome"] = outcome, ["httpStatus"] = poll.HttpStatus }));
            return new(state, events.ToArray(), diagnostics.ToArray(), false, null);
        }
        state.ConsecutiveFailures = 0;
        Health(state, "healthy", poll, events, provenance);
        diagnostics.AddRange(snapshot.Diagnostics);
        var newScope = !state.Scopes.TryGetValue(snapshot.Scope, out var scope);
        if (newScope) state.Scopes[snapshot.Scope] = scope = new();
        var baseline = startupBaseline || newScope || state.ActiveScope != snapshot.Scope;
        state.ActiveScope = snapshot.Scope;
        if (baseline) scope!.InitializedCollections.Clear();
        ReconcileRecords(scope!, snapshot.Followers, "follows", snapshot.FollowersWindowValid, baseline, poll, events, diagnostics, provenance);
        // Documented subscription shapes are diagnostic candidates, never authoritative money/automation.
        ReconcileRecords(scope!, snapshot.Subscriptions, "subscriptions", snapshot.SubscriptionsWindowValid, baseline, poll, events, diagnostics, provenance);
        foreach (var stream in snapshot.Streams)
            ReconcileStream(scope!, stream, snapshot.WindowSize, baseline, poll, events, diagnostics, provenance);
        foreach (var absent in scope!.Streams.Where(entry => snapshot.Streams.All(stream => stream.Id != entry.Key)))
            ObserveOffline(absent.Key, absent.Value, baseline, poll, events, provenance);
        scope.LastSuccess = poll.ObservedAt;
        PruneChat(scope, poll.ObservedAt);
        return new(state, events.OrderBy(item => item.OccurredAt).ThenBy(item => item.Type, StringComparer.Ordinal).ToArray(),
            diagnostics.ToArray(), baseline, snapshot.Raw);
    }

    private void ReconcileStream(RumbleScopeState scope, RumbleStream stream, int windowSize, bool baseline,
        RumblePoll poll, List<CanonicalEvent> events, List<RumbleDiagnostic> diagnostics, EventProvenance provenance)
    {
        var firstEvent = events.Count;
        var first = !scope.Streams.TryGetValue(stream.Id, out var current);
        if (first) scope.Streams[stream.Id] = current = new() { Title = stream.Title };
        current!.Title = stream.Title;
        if (stream.Live)
        {
            if (!current.Live && !baseline) events.Add(Status("stream.online", stream.Id, poll, provenance));
            current.Live = true; current.OfflineObservations = 0;
        }
        else ObserveOffline(stream.Id, current, baseline, poll, events, provenance);
        if (!baseline && !first)
        {
            Stat("stream.viewers", current.Viewers, stream.Viewers, stream.Id, poll, events, provenance);
            Stat("stream.likes", current.Likes, stream.Likes, stream.Id, poll, events, provenance);
            var keys = stream.Messages.Select(item => item.CoreFingerprint).ToArray();
            if (current.PreviousWindowValid && stream.ChatWindowValid && current.PreviousMessages.Length >= windowSize && keys.Length >= windowSize &&
                !current.PreviousMessages.Intersect(keys, StringComparer.Ordinal).Any())
                diagnostics.Add(new("rumble.chat.possible_gap", new() { ["streamId"] = stream.Id, ["previousCount"] = current.PreviousMessages.Length,
                    ["currentCount"] = keys.Length, ["overlapCount"] = 0,
                    ["pollIntervalMs"] = scope.LastSuccess is { } last ? (poll.ObservedAt - last).TotalMilliseconds : 0 }));
        }
        current.Viewers = stream.Viewers; current.Likes = stream.Likes;
        ReconcileRecords(scope, stream.Messages, "chat:" + stream.Id, stream.ChatWindowValid, baseline || first, poll, events, diagnostics, provenance);
        ReconcileRecords(scope, stream.Rants, "rants:" + stream.Id, stream.RantWindowValid, baseline || first, poll, events, diagnostics, provenance);
        if (stream.ChatWindowValid)
        {
            current.PreviousMessages = stream.Messages.Select(item => item.CoreFingerprint).ToArray();
            current.PreviousWindowValid = true;
        }
        else current.PreviousWindowValid = false;
        for (var index = firstEvent; index < events.Count; index++)
            if (events[index].Stream?.Id == stream.Id) events[index] = events[index] with { Stream = new(stream.Id, stream.Title) };
    }

    private void ObserveOffline(string id, RumbleStreamState stream, bool baseline, RumblePoll poll, List<CanonicalEvent> events, EventProvenance provenance)
    {
        if (baseline) { stream.Live = false; stream.OfflineObservations = 0; return; }
        if (!stream.Live) return;
        if (++stream.OfflineObservations < offlineConfirmationPolls) return;
        stream.Live = false; stream.OfflineObservations = 0;
        events.Add(Status("stream.offline", id, poll, provenance) with { Stream = new(id, stream.Title) });
    }

    private static void ReconcileRecords(RumbleScopeState scope, RumbleRecord[] records, string collection, bool validWindow, bool baseline, RumblePoll poll,
        List<CanonicalEvent> events, List<RumbleDiagnostic> diagnostics, EventProvenance provenance)
    {
        baseline |= !scope.InitializedCollections.Contains(collection);
        if (validWindow) scope.InitializedCollections.Add(collection);
        foreach (var group in records.GroupBy(item => item.CoreFingerprint).OrderBy(group => group.First().CreatedAt))
        {
            var entries = group.OrderBy(record => record.Fingerprint, StringComparer.Ordinal).ToArray();
            var item = entries[0]; var count = entries.Length;
            if (!scope.History.TryGetValue(group.Key, out var occurrence))
                scope.History[group.Key] = occurrence = new() { Kind = item.Kind, Fingerprint = item.Fingerprint };
            else if (item.Kind == "chat.message" && occurrence.Fingerprint != item.Fingerprint)
                diagnostics.Add(new("chatBadgeMutationAmbiguous", new() { ["policy"] = "core-identity-high-water", ["emittedForBadgeChange"] = false }));
            for (var index = occurrence.MaximumCount; index < count && !baseline; index++)
            {
                var selected = entries[index];
                var id = RumbleSnapshotParser.Hash(occurrence.Fingerprint, index.ToString(CultureInfo.InvariantCulture));
                var candidate = item.Kind == "support.subscription";
                events.Add(new CanonicalEvent { Source = "rumble", Platform = "rumble", Type = candidate ? "rumble.subscription.candidate" : item.Kind,
                    NativeType = "Rumble." + item.Kind, OccurredAt = item.CreatedAt, ReceivedAt = poll.ObservedAt, DedupeKey = id,
                    User = new(null, item.User, item.User, item.Avatar, selected.Badges), Message = item.Text is null ? null : new(item.Text),
                    Stream = item.StreamId is null ? null : new(item.StreamId, null), Provenance = provenance,
                    Monetary = item.Kind == "support.rant" ? new(item.Cents, "USD", "exact") : null,
                    Raw = selected.Raw.DeepClone().AsObject() });
                if (candidate) diagnostics.Add(new("subscriptionLiveUnverified", new() { ["authoritativeAutomationEnabled"] = false }));
            }
            occurrence.MaximumCount = Math.Max(occurrence.MaximumCount, count);
            occurrence.LastSeen = poll.ObservedAt; occurrence.Sequence = poll.ObservedAt.UtcTicks;
        }
    }

    private static void PruneChat(RumbleScopeState scope, DateTimeOffset now)
    {
        var messages = scope.History.Where(item => item.Value.Kind == "chat.message").OrderByDescending(item => item.Value.Sequence).ToArray();
        foreach (var item in messages.Skip(10000).Where(item => now - item.Value.LastSeen > TimeSpan.FromHours(24))) scope.History.Remove(item.Key);
    }

    private static void Health(RumbleState state, string health, RumblePoll poll, List<CanonicalEvent> events, EventProvenance provenance)
    {
        if (state.Health == health) return;
        state.Health = health;
        events.Add(Status("integration.health", null, poll, provenance, new() { ["health"] = health, ["httpStatus"] = poll.HttpStatus }));
    }
    private static void Stat(string type, long? previous, long? value, string stream, RumblePoll poll, List<CanonicalEvent> events, EventProvenance provenance)
    {
        if (value is not null && previous is not null && value != previous) events.Add(Status(type, stream, poll, provenance, new() { ["value"] = value, ["previousValue"] = previous }));
    }
    private static CanonicalEvent Status(string type, string? stream, RumblePoll poll, EventProvenance provenance, JsonObject? details = null) =>
        new() { Source = "rumble", Platform = "rumble", Type = type, NativeType = "Rumble." + type, OccurredAt = poll.ObservedAt,
            ReceivedAt = poll.ObservedAt, DedupeKey = Guid.CreateVersion7().ToString(), Provenance = provenance,
            Stream = stream is null ? null : new(stream, null), Raw = details,
            Metrics = type == "integration.health" ? new(Health: RumbleSnapshotParser.Text(details?["health"])) :
                type is "stream.viewers" or "stream.likes" ? new(RumbleSnapshotParser.Nonnegative(details?["value"])) : null };
}
