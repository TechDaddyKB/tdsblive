namespace ExtensionSuite.Core;

public sealed record TemporaryActionState(DateTimeOffset ExpiresAt, int QueuedCount = 0);
public sealed record TemporaryActionDecision(string Operation, TemporaryActionState? State);

public static class AutomationTemporaryPolicy
{
    /// <summary>Pure policy evaluation. Persist the decision before external dispatch;
    /// only activate an effect after confirmed enable dispatch. Expiry must be reverted
    /// before another effect starts, even if the timer worker was delayed.</summary>
    public static TemporaryActionDecision Apply(TemporaryActionState? active, DateTimeOffset now,
        int durationSeconds, string policy, int maximumQueued = 20)
    {
        if (now.Offset != TimeSpan.Zero || durationSeconds is < 1 or > 86400 || maximumQueued is < 1 or > 100 ||
            policy is not ("extend" or "restart" or "ignore" or "queue") ||
            active is { QueuedCount: < 0 } || active?.ExpiresAt.Offset != TimeSpan.Zero && active is not null)
            throw new ArgumentException("Invalid temporary action policy input.");
        if (active is null) return new("enable", new(now.AddSeconds(durationSeconds)));
        if (active.ExpiresAt <= now) return new("revert-first", active);
        return policy switch
        {
            "extend" => new("reschedule", active with { ExpiresAt = active.ExpiresAt.AddSeconds(durationSeconds) }),
            "restart" => new("reschedule", active with { ExpiresAt = now.AddSeconds(durationSeconds) }),
            "ignore" => new("ignored", active),
            "queue" when active.QueuedCount >= maximumQueued => new("queue-full", active),
            "queue" => new("queued", active with { QueuedCount = active.QueuedCount + 1 }),
            _ => throw new ArgumentException("Unknown temporary policy.")
        };
    }

    public static TemporaryActionDecision AfterConfirmedRevert(TemporaryActionState active, DateTimeOffset now,
        int durationSeconds)
    {
        if (active.QueuedCount <= 0) return new("idle", null);
        var next = Apply(null, now, durationSeconds, "queue");
        return next with { State = next.State! with { QueuedCount = active.QueuedCount - 1 } };
    }
}
