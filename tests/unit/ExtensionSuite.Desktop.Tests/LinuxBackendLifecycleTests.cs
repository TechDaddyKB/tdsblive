using System.Diagnostics;
using System.Text.Json;
using ExtensionSuite.Desktop;
using ExtensionSuite.DesktopControl;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

// Real owned child processes exercise the pipe/transport and handle lifecycle.
// The runner is a test double; these tests do not qualify Wine or UMU.
public sealed class LinuxBackendLifecycleTests
{
    [OwnedRunnerFact]
    public async Task ExistingProfileAcknowledgementRequiresSuccessfulChildExitAndGrantsNoSession()
    {
        foreach (var kind in new[] { LinuxRunnerKind.Wine, LinuxRunnerKind.Umu })
        {
            using (var duplicate = new OwnedRunner("already-running", kind: kind))
            {
                await Assert.ThrowsAsync<LinuxBackendAlreadyRunningException>(() =>
                    LinuxBackendProcess.StartAsync(duplicate.Settings, false, CancellationToken.None));
                Assert.False(File.Exists(Path.Combine(duplicate.Root, "started")));
            }
            using var failed = new OwnedRunner("already-running-fails", kind: kind);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                LinuxBackendProcess.StartAsync(failed.Settings, false, CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(failed.Root, "started")));
        }
    }

    [OwnedRunnerFact]
    public async Task LaunchUsesPrivateBootstrapAndRetainsSelectedProfileAcrossRestart()
    {
        using var runner = new OwnedRunner();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        string firstCapability;
        using (var first = await LinuxBackendProcess.StartAsync(runner.Settings, false, deadline.Token))
        {
            firstCapability = first.Bootstrap.SessionToken;
            Assert.Equal("running", (await first.WaitUntilRunningAsync(deadline.Token)).State);
            runner.AssertMetadata(firstCapability);
            Assert.True((await DesktopProtocol.SendAsync(first.Bootstrap, "restart", deadline.Token)).Accepted);
            Assert.Equal(0, await first.WaitForExitAsync(deadline.Token));
        }
        using var second = await LinuxBackendProcess.StartAsync(runner.Settings, false, deadline.Token);
        Assert.NotEqual(firstCapability, second.Bootstrap.SessionToken);
        Assert.Equal("running", (await second.WaitUntilRunningAsync(deadline.Token)).State);
        Assert.Equal("owned settings", File.ReadAllText(runner.ConfigurationPath));
        Assert.True((await DesktopProtocol.SendAsync(second.Bootstrap, "quit", deadline.Token)).Accepted);
        Assert.Equal(0, await second.WaitForExitAsync(deadline.Token));
    }

    [OwnedRunnerFact]
    public async Task ClosingCompanionHandlesDoesNotKillTheBackend()
    {
        using var runner = new OwnedRunner();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var process = await LinuxBackendProcess.StartAsync(runner.Settings, false, deadline.Token);
        await process.WaitUntilRunningAsync(deadline.Token);
        var session = process.Bootstrap;
        process.Dispose();
        process.Dispose();
        Assert.Equal("running", (await DesktopProtocol.SendAsync(session, "status", deadline.Token)).State);
        Assert.True((await DesktopProtocol.SendAsync(session, "quit", deadline.Token)).Accepted);
        await runner.WaitForExitAsync(deadline.Token);
    }

    [OwnedRunnerFact]
    public async Task UmuOutputBootstrapAcceptsEmptyInputAndRetainsTheProfileWithAFreshRestartCapability()
    {
        using var runner = new OwnedRunner(kind: LinuxRunnerKind.Umu);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        string firstCapability;
        using (var first = await LinuxBackendProcess.StartAsync(runner.Settings, false, deadline.Token))
        {
            firstCapability = first.Bootstrap.SessionToken;
            Assert.Equal("running", (await first.WaitUntilRunningAsync(deadline.Token)).State);
            runner.AssertMetadata(firstCapability);
            Assert.True((await DesktopProtocol.SendAsync(first.Bootstrap, "restart", deadline.Token)).Accepted);
            Assert.Equal(0, await first.WaitForExitAsync(deadline.Token));
        }
        using var second = await LinuxBackendProcess.StartAsync(runner.Settings, false, deadline.Token);
        Assert.False(DesktopProtocol.Authenticate(firstCapability, second.Bootstrap.SessionToken));
        Assert.Equal("running", (await second.WaitUntilRunningAsync(deadline.Token)).State);
        runner.AssertMetadata(second.Bootstrap.SessionToken);
        Assert.Equal("owned settings", File.ReadAllText(runner.ConfigurationPath));
        Assert.True((await DesktopProtocol.SendAsync(second.Bootstrap, "quit", deadline.Token)).Accepted);
        Assert.Equal(0, await second.WaitForExitAsync(deadline.Token));
    }

    [OwnedRunnerFact]
    public async Task ExplicitNewPrefixIsInitializedBeforeBackendStartup()
    {
        using var runner = new OwnedRunner(createNew: true);
        Assert.False(Directory.Exists(Path.GetDirectoryName(runner.Settings.PrefixDirectory)));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var process = await LinuxBackendProcess.StartAsync(runner.Settings, true, deadline.Token);
        Assert.Equal("mscoree,mshtml=", File.ReadAllText(Path.Combine(runner.Root, "initialized")));
        Assert.True(Directory.Exists(Path.Combine(runner.Settings.PrefixDirectory, "drive_c")));
        Assert.Equal("running", (await process.WaitUntilRunningAsync(deadline.Token)).State);
        runner.AssertMetadata(process.Bootstrap.SessionToken);
        await DesktopProtocol.SendAsync(process.Bootstrap, "quit", deadline.Token);
        Assert.Equal(0, await process.WaitForExitAsync(deadline.Token));
    }

    [OwnedRunnerFact]
    public async Task FailedInitializationNeverStartsTheBackend()
    {
        using var runner = new OwnedRunner("init-fails", createNew: true);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LinuxBackendProcess.StartAsync(runner.Settings, true, CancellationToken.None));
        Assert.Contains("prepare", error.Message);
        Assert.False(File.Exists(Path.Combine(runner.Root, "started")));
        Assert.False(File.Exists(runner.ConfigurationPath));
    }

    [OwnedRunnerFact]
    public async Task IncompleteApplicationFolderIsRejectedBeforeAnyProcessLaunch()
    {
        using var runner = new OwnedRunner();
        File.Delete(Path.Combine(runner.Root, "ExtensionSuite.DesktopControl.dll"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            LinuxBackendProcess.StartAsync(runner.Settings, false, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(runner.Root, "pid")));
    }

    [OwnedRunnerFact]
    public async Task MalformedMarkerAndStoppedStatusCannotAdmitARunningSession()
    {
        using (var broken = new OwnedRunner("bad-marker"))
            await Assert.ThrowsAsync<IOException>(() => LinuxBackendProcess.StartAsync(broken.Settings, false, CancellationToken.None));
        using var stopped = new OwnedRunner("not-running");
        using var process = await LinuxBackendProcess.StartAsync(stopped.Settings, false, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => process.WaitUntilRunningAsync(CancellationToken.None));
        await DesktopProtocol.SendAsync(process.Bootstrap, "quit", CancellationToken.None);
        Assert.Equal(0, await process.WaitForExitAsync(CancellationToken.None));
    }

    [OwnedRunnerFact]
    public async Task CancelledStartupDoesNotHangOrTerminateAnUnownedProcess()
    {
        using var runner = new OwnedRunner("no-marker");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancelled = new CancellationTokenSource();
        var starting = LinuxBackendProcess.StartAsync(runner.Settings, false, cancelled.Token);
        while (!File.Exists(Path.Combine(runner.Root, "started"))) await Task.Delay(20, deadline.Token);
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
        using var ownedProcess = runner.Process();
        Assert.False(ownedProcess.HasExited); // Only this test fixture's cleanup may kill it.
    }

    internal sealed class OwnedRunner : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tdsblive-runner-" + Guid.NewGuid().ToString("N"));
        public LinuxLauncherSettings Settings { get; }
        public string ConfigurationPath => Path.Combine(Settings.DataDirectory, "configuration.json");

        public OwnedRunner(string mode = "normal", bool createNew = false, LinuxRunnerKind kind = LinuxRunnerKind.Wine)
        {
            Directory.CreateDirectory(Root);
            var runner = Path.Combine(Root, kind == LinuxRunnerKind.Umu ? "umu-run" : "wine");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "desktop-runner.py"), runner);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(runner, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.WriteAllText(Path.Combine(Root, "mode"), mode);
            var application = Path.Combine(Root, "TDSBLive.exe");
            File.WriteAllText(application, "Owned transport fixture; never executed.");
            File.WriteAllText(Path.Combine(Root, "ExtensionSuite.DesktopControl.dll"), "Owned completeness marker.");
            var prefix = createNew ? Path.Combine(Root, "new launcher settings", "Windows settings with spaces") :
                Path.Combine(Root, "Windows settings with spaces");
            string? proton = null;
            if (kind == LinuxRunnerKind.Umu)
            {
                proton = Path.Combine(Root, "selected Proton");
                Directory.CreateDirectory(proton);
                File.WriteAllText(Path.Combine(proton, "proton"), "Owned runner selection marker; never executed.");
                File.WriteAllText(Path.Combine(proton, "toolmanifest.vdf"), "Owned runner selection marker.");
            }
            Settings = new(kind, runner, prefix, application, Path.Combine(prefix, "drive_c", "TDSBLiveData"), proton);
            if (!createNew)
            {
                Directory.CreateDirectory(Settings.DataDirectory);
                File.WriteAllText(ConfigurationPath, "owned settings");
            }
        }

        public Process Process() => System.Diagnostics.Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(Root, "pid"))));
        public async Task WaitForExitAsync(CancellationToken token)
        {
            using var process = Process();
            await process.WaitForExitAsync(token);
            Assert.True(process.HasExited);
        }
        public void AssertMetadata(string capability)
        {
            var text = File.ReadAllText(Path.Combine(Root, "launch.json"));
            Assert.DoesNotContain(capability, text);
            using var metadata = JsonDocument.Parse(text);
            Assert.Equal(Settings.PrefixDirectory, metadata.RootElement.GetProperty("prefix").GetString());
            Assert.Contains("mscoree=b", metadata.RootElement.GetProperty("overrides").GetString());
            var expectedArguments = new List<string> { Settings.ApplicationPath, "--TDSBLive:DesktopMode=external", "--TDSBLive:OpenEditor=false",
                "--TDSBLive:DataDirectory=C:\\TDSBLiveData" };
            if (Settings.Runner == LinuxRunnerKind.Umu) expectedArguments.Add("--TDSBLive:DesktopBootstrap=output");
            Assert.Equal(expectedArguments,
                metadata.RootElement.GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        }
        public void Dispose()
        {
            if (File.Exists(Path.Combine(Root, "pid")))
            {
                try
                {
                    using var process = Process();
                    // Only this owned Python transport fixture. Never a Wine server or real backend.
                    if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
                }
                catch (ArgumentException) { }
            }
            Directory.Delete(Root, true);
        }
    }

    internal sealed class OwnedRunnerFactAttribute : FactAttribute
    {
        public OwnedRunnerFactAttribute()
        {
            if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/python3"))
                Skip = "Requires Linux and the owned Python runner test double.";
        }
    }
}
