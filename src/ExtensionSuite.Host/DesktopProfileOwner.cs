using System.Security.Cryptography;
using System.Text;

namespace ExtensionSuite.Host;

/// <summary>Exclusive profile ownership without storing desktop credentials on disk.</summary>
public sealed class DesktopProfileOwner : IAsyncDisposable, IDisposable
{
    private FileStream? lease;
    private readonly string requestPath;
    private readonly CancellationTokenSource stopping = new();
    private Task? watching;
    private bool disposed;
    public string LeasePath { get; }

    private DesktopProfileOwner(FileStream lease, string requestPath)
        => (this.lease, this.requestPath, LeasePath) = (lease, requestPath, lease.Name);

    public static DesktopProfileOwner? AcquireOrRequestOpen(string directory)
    {
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        Directory.CreateDirectory(directory);
        var parent = Path.GetDirectoryName(directory) ?? throw new ArgumentException("Desktop profiles need a parent directory.");
        var identity = OperatingSystem.IsWindows() ? directory.ToUpperInvariant() : directory;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        // Recovery atomically renames the entire data folder. Keep ownership
        // outside it so Windows can move that folder while the lease stays held.
        var leasePath = Path.Combine(parent, ".tdsblive-desktop-" + key + ".lock");
        var requestPath = Path.Combine(directory, ".desktop-open.request");
        try
        {
            var lease = new FileStream(leasePath, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            return new(lease, requestPath);
        }
        catch (IOException)
        {
            // This marker only requests a browser window. It grants no control
            // authority and contains no token, configuration or private data.
            // Set permissions at creation: the owner may consume and delete
            // this empty marker immediately, including before this handle closes.
            var options = new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.Write,
                Share = FileShare.ReadWrite | FileShare.Delete
            };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using var request = new FileStream(requestPath, options);
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
        }, stopping.Token);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping.Cancel();
        ReleaseOwnership();
    }

    public void ReleaseOwnership()
    {
        lease?.Dispose();
        lease = null;
    }

    public async Task StopWatchingAsync()
    {
        await stopping.CancelAsync();
        if (watching is not null)
        {
            try { await watching; }
            catch (OperationCanceledException)
            {
                // A watcher canceled before its first scheduled iteration is stopped too.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await StopWatchingAsync();
        stopping.Dispose();
    }
}
