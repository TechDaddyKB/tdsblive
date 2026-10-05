using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
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

    private LinuxBackendProcess(Process process, TaskCompletionSource<DesktopBootstrap> ready, string? inputCapability)
    {
        this.process = process;
        output = inputCapability is null
            ? ReadBootstrapOutputAsync(process.StandardOutput, ready, readers.Token)
            : ReadInputBootstrapAsync(process.StandardOutput, ready, inputCapability, readers.Token);
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

        var ready = new TaskCompletionSource<DesktopBootstrap>(TaskCreationOptions.RunContinuationsAsynchronously);
        Observe(ready.Task);
        var capability = selected.Runner == LinuxRunnerKind.Umu ? null : DesktopProtocol.NewSessionToken();
        var process = Process.Start(selected.CreateStartInfo(createNewProfile)) ?? throw new InvalidOperationException("The Windows app could not start.");
        var owned = new LinuxBackendProcess(process, ready, capability);
        try
        {
            if (capability is not null)
                await DesktopProtocol.WriteAsync(process.StandardInput.BaseStream, new DesktopBootstrap(0, capability), deadline.Token);
            process.StandardInput.Close();
            owned.Bootstrap = await ready.Task.WaitAsync(deadline.Token);
            return owned;
        }
        catch (LinuxBackendAlreadyRunningException)
        {
            try
            {
                await process.WaitForExitAsync(deadline.Token);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("The existing-profile request did not complete successfully.");
            }
            finally { owned.Dispose(); }
            throw;
        }
        catch { owned.Dispose(); throw; }
    }

    private static async Task InitializeWineAsync(LinuxLauncherSettings selected, CancellationToken cancellationToken)
    {
        // The suggested prefix can have a missing parent on a first launch.
        // Prepare its directory; Wine must create its own drive mappings.
        // Never precreate drive_c/dosdevices or stop an existing prefix server.
        Directory.CreateDirectory(selected.PrefixDirectory);
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

    private static async Task ReadInputBootstrapAsync(TextReader reader, TaskCompletionSource<DesktopBootstrap> ready,
        string capability, CancellationToken cancellationToken)
    {
        var port = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Observe(port.Task);
        var reading = ReadOutputAsync(reader, port, cancellationToken);
        try { ready.TrySetResult(new(await port.Task, capability)); }
        catch (LinuxBackendAlreadyRunningException) { ready.TrySetException(new LinuxBackendAlreadyRunningException()); }
        catch (IOException) { ready.TrySetException(new IOException("The Windows app's desktop pipe is unavailable.")); }
        await reading;
    }

    internal static Task ReadOutputAsync(TextReader reader, TaskCompletionSource<int> ready, CancellationToken cancellationToken) =>
        ReadLinesAsync(reader, 128, line =>
        {
            var span = line.Span;
            if (span.SequenceEqual(DesktopProtocol.AlreadyRunningMarker))
                ready.TrySetException(new LinuxBackendAlreadyRunningException());
            if (span.StartsWith(DesktopProtocol.ReadyPrefix, StringComparison.Ordinal) &&
                int.TryParse(span[DesktopProtocol.ReadyPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var port) && port is >= 1 and <= 65535)
                ready.TrySetResult(port);
        }, () => ready.TrySetException(new IOException("The Windows app's desktop pipe is unavailable.")), cancellationToken);

    internal static Task ReadBootstrapOutputAsync(TextReader reader, TaskCompletionSource<DesktopBootstrap> ready,
        CancellationToken cancellationToken) =>
        ReadLinesAsync(reader, DesktopProtocol.MaximumFrameBytes + DesktopProtocol.BootstrapPrefix.Length, line =>
        {
            var span = line.Span;
            if (span.SequenceEqual(DesktopProtocol.AlreadyRunningMarker))
                ready.TrySetException(new LinuxBackendAlreadyRunningException());
            if (!span.StartsWith(DesktopProtocol.BootstrapPrefix, StringComparison.Ordinal)) return;
            var payload = span[DesktopProtocol.BootstrapPrefix.Length..];
            if (Encoding.UTF8.GetByteCount(payload) >= DesktopProtocol.MaximumFrameBytes) return;
            try
            {
                var bootstrap = JsonSerializer.Deserialize<DesktopBootstrap>(payload.ToString());
                if (bootstrap is { Port: >= 1 and <= 65535 } && DesktopProtocol.ValidSessionToken(bootstrap.SessionToken))
                    ready.TrySetResult(bootstrap);
            }
            catch (JsonException) { /* Ignore malformed private frames without logging their contents. */ }
        }, () => ready.TrySetException(new IOException("The Windows app's private desktop bootstrap is unavailable. Choose the current complete Windows application folder.")),
            cancellationToken);

    private static async Task ReadLinesAsync(TextReader reader, int maximumLineCharacters,
        Action<ReadOnlyMemory<char>> accept, Action unavailable, CancellationToken cancellationToken)
    {
        // Backend and runner output can contain private data. Discard it without
        // logging. Only a bounded legacy port or private bootstrap frame is parsed.
        var buffer = new char[1024];
        var line = new char[maximumLineCharacters];
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
                        if (!overflow) accept(line.AsMemory(0, length));
                        count = 0; overflow = false;
                    }
                    else if (count < line.Length) line[count++] = character;
                    else overflow = true;
                }
            }
            unavailable();
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException)
        {
            unavailable();
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

internal sealed class LinuxBackendAlreadyRunningException : IOException
{
    public LinuxBackendAlreadyRunningException() : base("The existing TDSBLive profile was asked to open its editor.") { }
}
