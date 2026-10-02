namespace ExtensionSuite.Host;

public sealed record RecoveryPreview(Guid Id, DateTimeOffset ExpiresAt);
public sealed record ApplicationOperation(string Kind, PreparedRecovery? Restore = null);

public sealed class ApplicationLifecycle(IHostApplicationLifetime lifetime, TimeProvider clock) : IAsyncDisposable, IDisposable
{
    private readonly object sync = new();
    private (RecoveryPreview Preview, PreparedRecovery Recovery)? staged;
    private ApplicationOperation? operation;
    public Guid Generation { get; } = Guid.CreateVersion7();
    public ApplicationOperation? Operation { get { lock (sync) return operation; } }

    public async Task<RecoveryPreview?> StageAsync(PreparedRecovery recovery)
    {
        PreparedRecovery? previous;
        RecoveryPreview preview;
        lock (sync)
        {
            if (operation is not null) return null;
            previous = staged?.Recovery;
            preview = new(Guid.CreateVersion7(), clock.GetUtcNow().AddMinutes(15));
            staged = (preview, recovery);
        }
        if (previous is not null) await previous.DisposeAsync();
        return preview;
    }

    public bool RequestRestore(Guid id)
    {
        lock (sync)
        {
            if (operation is not null || staged is not { } candidate || candidate.Preview.Id != id || candidate.Preview.ExpiresAt <= clock.GetUtcNow()) return false;
            operation = new("restore", candidate.Recovery);
            staged = null;
            return true;
        }
    }

    public bool Request(string kind)
    {
        if (kind is not ("restart" or "quit")) throw new ArgumentException("Invalid application operation.");
        lock (sync)
        {
            if (operation is not null) return false;
            operation = new(kind);
            return true;
        }
    }

    public void StopAfterResponse(HttpContext context) => context.Response.OnCompleted(() =>
    {
        lifetime.StopApplication();
        return Task.CompletedTask;
    });

    public async ValueTask DisposeAsync()
    {
        PreparedRecovery? previous;
        lock (sync) { previous = staged?.Recovery; staged = null; }
        if (previous is not null) await previous.DisposeAsync();
        // A queued recovery belongs to Program's post-shutdown operation, not DI disposal.
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();
}
