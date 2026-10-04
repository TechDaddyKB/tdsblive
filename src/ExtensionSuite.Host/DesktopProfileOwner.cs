namespace ExtensionSuite.Host;

/// <summary>Exclusive profile ownership without storing desktop credentials on disk.</summary>
public sealed class DesktopProfileOwner : IAsyncDisposable, IDisposable
{
    private readonly FileStream lease;
    private readonly string requestPath;
    private readonly CancellationTokenSource stopping = new();
    private Task? watching;
    private bool disposed;

    private DesktopProfileOwner(FileStream lease, string requestPath)
        => (this.lease, this.requestPath) = (lease, requestPath);

    public static DesktopProfileOwner? AcquireOrRequestOpen(string directory)
    {
        Directory.CreateDirectory(directory);
        var requestPath = Path.Combine(directory, ".desktop-open.request");
        try
        {
            var lease = new FileStream(Path.Combine(directory, ".desktop-owner.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            return new(lease, requestPath);
        }
        catch (IOException)
        {
            // This marker only requests a browser window. It grants no control
            // authority and contains no token, configuration or private data.
            File.WriteAllText(requestPath, Guid.NewGuid().ToString());
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(requestPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return null;
        }
    }

    public void Watch(Action openEditor)
    {
        watching = Task.Run(async () =>
        {
            while (!stopping.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(requestPath))
                    {
                        File.Delete(requestPath);
                        openEditor();
                    }
                    await Task.Delay(500, stopping.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (IOException) { await Task.Delay(500, stopping.Token); }
            }
        });
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping.Cancel();
        lease.Dispose();
    }

    public async Task StopWatchingAsync()
    {
        stopping.Cancel();
        if (watching is not null)
        {
            try { await watching; }
            catch (OperationCanceledException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await StopWatchingAsync();
        stopping.Dispose();
    }
}
