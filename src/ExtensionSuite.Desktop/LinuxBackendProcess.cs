using System.Diagnostics;
using System.Globalization;
using ExtensionSuite.DesktopControl;

namespace ExtensionSuite.Desktop;

// Owns handles and pipe readers, not authority to kill Wine or other applications.
// Disposing the companion must leave a running backend available through its editor.
public sealed class LinuxBackendProcess : IDisposable
{
    private readonly Process process;
    private readonly CancellationTokenSource readers = new();
    private readonly Task output;
    private readonly Task errors;
    private bool disposed;
    public DesktopBootstrap Bootstrap { get; private set; } = null!;

    private LinuxBackendProcess(Process process, TaskCompletionSource<int> ready)
    {
        this.process = process;
        output = ReadOutputAsync(process.StandardOutput, ready, readers.Token);
        errors = DrainAsync(process.StandardError, readers.Token);
    }

    public static async Task<LinuxBackendProcess> StartAsync(LinuxLauncherSettings settings,
        bool createNewProfile, CancellationToken cancellationToken)
    {
        var selected = settings.Normalize(createNewProfile);
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(selected.ApplicationPath)!, "ExtensionSuite.DesktopControl.dll")))
            throw new ArgumentException("Choose the current complete Windows application folder. Older versions do not support native desktop controls.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        if (createNewProfile && selected.Runner == LinuxRunnerKind.Wine &&
            !Directory.Exists(Path.Combine(selected.PrefixDirectory, "drive_c")))
            await InitializeWineAsync(selected, deadline.Token);

        var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Observe(ready.Task);
        var process = Process.Start(selected.CreateStartInfo(createNewProfile)) ?? throw new InvalidOperationException("The Windows app could not start.");
        var owned = new LinuxBackendProcess(process, ready);
        try
        {
            var capability = DesktopProtocol.NewSessionToken();
            await DesktopProtocol.WriteAsync(process.StandardInput.BaseStream, new DesktopBootstrap(0, capability), deadline.Token);
            process.StandardInput.Close();
            var port = await ready.Task.WaitAsync(deadline.Token);
            owned.Bootstrap = new(port, capability);
            return owned;
        }
        catch { owned.Dispose(); throw; }
    }

    private static async Task InitializeWineAsync(LinuxLauncherSettings selected, CancellationToken cancellationToken)
    {
        // Wine must create its own drive mappings before .NET starts. Never
        // precreate drive_c/dosdevices or shut down an existing prefix server.
        var info = selected.CreateStartInfo(createNewProfile: true);
        info.ArgumentList.Clear();
        info.ArgumentList.Add("wineboot.exe");
        info.ArgumentList.Add("--init");
        info.Environment["WINEDLLOVERRIDES"] = "mscoree,mshtml=";
        using var initializing = Process.Start(info) ?? throw new InvalidOperationException("Wine could not prepare the new Windows settings folder.");
        initializing.StandardInput.Close();
        var output = DrainAsync(initializing.StandardOutput, cancellationToken);
        var errors = DrainAsync(initializing.StandardError, cancellationToken);
        try
        {
            await initializing.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(output, errors);
            if (initializing.ExitCode != 0 || !Directory.Exists(Path.Combine(selected.PrefixDirectory, "drive_c")))
                throw new InvalidOperationException("Wine could not prepare the new Windows settings folder.");
        }
        finally
        {
            Observe(output); Observe(errors);
        }
    }

    public async Task<DesktopReply> WaitUntilRunningAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        while (true)
        {
            using var request = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            request.CancelAfter(TimeSpan.FromSeconds(3));
            var reply = await DesktopProtocol.SendAsync(Bootstrap, "status", request.Token);
            if (reply.State == "running") return reply;
            if (reply.State != "starting") throw new InvalidOperationException("The Windows app did not reach its running state.");
            await Task.Delay(200, deadline.Token);
        }
    }

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    internal static async Task ReadOutputAsync(TextReader reader, TaskCompletionSource<int> ready, CancellationToken cancellationToken)
    {
        // Backend and runner output can contain private data. Discard it without
        // logging, retaining only a bounded line for the nonsensitive port marker.
        var buffer = new char[1024];
        var line = new char[128];
        var count = 0;
        var overflow = false;
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
            {
                for (var index = 0; index < read; index++)
                {
                    var character = buffer[index];
                    if (character == '\n')
                    {
                        var length = count > 0 && line[count - 1] == '\r' ? count - 1 : count;
                        if (!overflow && line.AsSpan(0, length).StartsWith(DesktopProtocol.ReadyPrefix, StringComparison.Ordinal) &&
                            int.TryParse(line.AsSpan(DesktopProtocol.ReadyPrefix.Length, length - DesktopProtocol.ReadyPrefix.Length),
                                NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= 65535)
                            ready.TrySetResult(port);
                        count = 0; overflow = false;
                    }
                    else if (count < line.Length) line[count++] = character;
                    else overflow = true;
                }
            }
            ready.TrySetException(new IOException("The Windows app ended before desktop controls connected. If TDSBLive is already running, use its editor controls."));
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException)
        {
            ready.TrySetException(new IOException("The Windows app's desktop pipe is unavailable."));
        }
    }

    private static async Task DrainAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        try { while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken) != 0) { } }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException) { }
    }

    private static void Observe(Task task) => _ = task.ContinueWith(failed => { _ = failed.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        readers.Cancel();
        process.Dispose(); // No Kill(), wineserver -k, or process-tree termination.
        Observe(output); Observe(errors);
        readers.Dispose();
    }
}
