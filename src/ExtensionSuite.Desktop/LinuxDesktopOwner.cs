using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace ExtensionSuite.Desktop;

// This lease belongs to the native controls, including after a backend crash.
// Its empty activation marker grants no backend authority and stores no token.
internal sealed class LinuxDesktopOwner : IDisposable
{
    private readonly FileStream lease;
    private readonly string requestPath;
    private readonly CancellationTokenSource stopping = new();
    private Task? watching;
    private bool disposed;

    private LinuxDesktopOwner(FileStream lease, string requestPath)
        => (this.lease, this.requestPath) = (lease, requestPath);

    public static LinuxDesktopOwner? AcquireOrRequestOpen(string profile, string runtimeDirectory)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        var root = Path.Combine(Path.GetFullPath(runtimeDirectory), "tdsblive-desktop");
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var identity = LinuxPhysicalPath.Resolve(profile);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var leasePath = Path.Combine(root, key + ".lock");
        var requestPath = Path.Combine(root, key + ".open");
        FileStream lease;
        try
        {
            var options = PrivateFileOptions();
            options.Share = FileShare.None;
            lease = new FileStream(leasePath, options);
        }
        catch (IOException error) when (error.HResult == 11)
        {
            RequestOpen(requestPath);
            return null;
        }
        if (Flock(lease.SafeFileHandle.DangerousGetHandle().ToInt32(), 2 | 4) == 0)
            return new(lease, requestPath);
        var nativeError = Marshal.GetLastPInvokeError();
        lease.Dispose();
        if (nativeError != 11) throw new IOException("Desktop controls ownership could not be checked.");
        RequestOpen(requestPath);
        return null;
    }

    [UnsupportedOSPlatform("windows")]
    private static void RequestOpen(string path)
    {
        using var request = new FileStream(path, PrivateFileOptions());
    }

    [UnsupportedOSPlatform("windows")]
    private static FileStreamOptions PrivateFileOptions() => new()
    {
        Mode = FileMode.OpenOrCreate, Access = FileAccess.ReadWrite,
        Share = FileShare.ReadWrite | FileShare.Delete,
        UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
    };

    public void Watch(Action activate)
    {
        if (watching is not null) throw new InvalidOperationException("Desktop activation is already being watched.");
        var cancellation = stopping.Token;
        watching = Task.Run(async () =>
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    try
                    {
                        if (File.Exists(requestPath))
                        {
                            File.Delete(requestPath);
                            if (!cancellation.IsCancellationRequested) activate();
                        }
                    }
                    catch (IOException) { /* An activation can race with another request. */ }
                    catch (UnauthorizedAccessException) { /* Keep existing controls available. */ }
                    await Task.Delay(500, cancellation);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }, cancellation);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping.Cancel();
        lease.Dispose();
        if (watching is null) stopping.Dispose();
        else _ = watching.ContinueWith(_ => stopping.Dispose(), TaskScheduler.Default);
    }

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(int descriptor, int operation);
}
