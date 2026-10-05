using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ExtensionSuite.Desktop;
using Xunit;
using static ExtensionSuite.Desktop.Tests.LinuxBackendLifecycleTests;

namespace ExtensionSuite.Desktop.Tests;

public sealed class LinuxStartupTests
{
    private static async Task RunAsync(Func<Task> test)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await ui.Dispatch(async () => { await test(); return true; }, deadline.Token);
    }

    private static Button Button(DesktopApp app, string label) => app.Controls!.GetLogicalDescendants()
        .OfType<Button>().Single(button => Equals(button.Content, label));
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    private static string Text(DesktopApp app) => string.Join("\n", app.Controls!.GetLogicalDescendants()
        .OfType<TextBlock>().Select(text => text.Text));
    private static async Task UntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!condition()) await Task.Delay(20, deadline.Token);
    }

    [OwnedRunnerFact]
    public Task FirstRunAndExplicitSetupDoNotStartOrOverwriteAnExistingProfile() => RunAsync(async () =>
    {
        using var runner = new OwnedRunner();
        using var config = new OwnedConfiguration(runner.Root);
        using var app = new DesktopApp(_ => Assert.Fail("No browser before startup"), _ => false, _ => Task.FromResult(false));
        await app.StartLinuxAsync(false);
        Assert.NotNull(app.LinuxSetup);
        Assert.False(File.Exists(config.Store.FilePath));
        Assert.False(File.Exists(Path.Combine(runner.Root, "started")));
        app.LinuxSetup.Close();
        using var another = new DesktopApp(_ => Assert.Fail("No browser before startup"), _ => false, _ => Task.FromResult(false));
        config.Store.Save(runner.Settings);
        var original = File.ReadAllBytes(config.Store.FilePath);
        await another.StartLinuxAsync(true);
        Assert.NotNull(another.LinuxSetup);
        Assert.Equal(original, File.ReadAllBytes(config.Store.FilePath));
        Assert.False(File.Exists(Path.Combine(runner.Root, "started")));
        another.LinuxSetup.Close();
    });

    [OwnedRunnerFact]
    public Task UnknownOrMalformedSavedChoicesShowRecoveryWithoutRewritingTheFile() => RunAsync(async () =>
    {
        using var runner = new OwnedRunner();
        using var config = new OwnedConfiguration(runner.Root);
        foreach (var invalid in new[] { "{not json", "{\"version\":99,\"settings\":null}", new string('x', 16 * 1024 + 1) })
        {
            using var app = new DesktopApp(_ => Assert.Fail("No browser during recovery"), _ => false, _ => Task.FromResult(false));
            Directory.CreateDirectory(config.Store.DirectoryPath);
            File.WriteAllText(config.Store.FilePath, invalid);
            await app.StartLinuxAsync(false);
            Assert.Null(app.LinuxSetup);
            Assert.Contains("could not be loaded", Text(app));
            Assert.False(Button(app, "Quit").IsEnabled);
            Assert.Equal(invalid, File.ReadAllText(config.Store.FilePath));
            Assert.False(File.Exists(Path.Combine(runner.Root, "pid")));
            Click(Button(app, "Close desktop controls"));
            app.Controls!.Close();
        }
    });

    [OwnedRunnerFact]
    public Task FailedSavedStartupRetainsSelectionsAndOffersSetupAgain() => RunAsync(async () =>
    {
        using var runner = new OwnedRunner("bad-marker");
        using var config = new OwnedConfiguration(runner.Root);
        config.Store.Save(runner.Settings);
        var original = File.ReadAllBytes(config.Store.FilePath);
        using var app = new DesktopApp(_ => Assert.Fail("No browser after failed startup"), _ => false, _ => Task.FromResult(false));
        await app.StartLinuxAsync(false);
        Assert.NotNull(app.LinuxSetup);
        Assert.Contains("existing setup selected", app.LinuxSetup.Feedback.Text);
        Assert.Equal(original, File.ReadAllBytes(config.Store.FilePath));
        Assert.Equal("owned settings", File.ReadAllText(runner.ConfigurationPath));
        app.LinuxSetup.Close();
    });

    [OwnedRunnerFact]
    public Task SavedStartupOpensNativeBrowserAndGracefulQuitFinishesTheOwnedLifecycle() => RunAsync(async () =>
    {
        using var runner = new OwnedRunner();
        using var config = new OwnedConfiguration(runner.Root);
        config.Store.Save(runner.Settings);
        var opened = new List<Uri>();
        using var app = new DesktopApp(opened.Add, _ => false, _ => Task.FromResult(true));
        await app.StartLinuxAsync(false);
        await UntilAsync(() => opened.Count == 1 && app.Controls?.IsVisible == true && Button(app, "Quit").IsEnabled);
        Assert.Equal("http://127.0.0.1:23456/editor", opened[0].AbsoluteUri);
        Assert.Equal(runner.Settings, config.Store.Load());
        Click(Button(app, "Quit"));
        Assert.NotNull(app.LinuxLifecycle);
        await app.LinuxLifecycle.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(Button(app, "Quit").IsEnabled);
        Assert.Equal("owned settings", File.ReadAllText(runner.ConfigurationPath));
        app.Controls!.Close();
    });

    [OwnedRunnerFact]
    public Task ConfirmedRestartWaitsForExitAndReopensTheSameSetupBeforeQuit() => RunAsync(async () =>
    {
        using var runner = new OwnedRunner();
        using var config = new OwnedConfiguration(runner.Root);
        config.Store.Save(runner.Settings);
        var opened = new List<Uri>();
        using var app = new DesktopApp(opened.Add, _ => false, _ => Task.FromResult(true));
        await app.StartLinuxAsync(false);
        await UntilAsync(() => opened.Count == 1 && app.Controls?.IsVisible == true && Button(app, "Restart").IsEnabled);
        var firstPid = File.ReadAllText(Path.Combine(runner.Root, "pid"));
        Click(Button(app, "Restart"));
        await UntilAsync(() => opened.Count == 2 && Button(app, "Quit").IsEnabled);
        Assert.NotEqual(firstPid, File.ReadAllText(Path.Combine(runner.Root, "pid")));
        Assert.Equal(opened[0], opened[1]);
        Assert.Equal(runner.Settings, config.Store.Load());
        Assert.Equal("owned settings", File.ReadAllText(runner.ConfigurationPath));
        Click(Button(app, "Quit"));
        await app.LinuxLifecycle!.WaitAsync(TimeSpan.FromSeconds(10));
        app.Controls!.Close();
    });

    [OwnedRunnerFact]
    public Task UnsuccessfulExitShowsRecoveryAndDoesNotRelaunchTheBackend() => RunAsync(async () =>
    {
        using var runner = new OwnedRunner("bad-exit");
        using var config = new OwnedConfiguration(runner.Root);
        config.Store.Save(runner.Settings);
        using var app = new DesktopApp(_ => { }, _ => false, _ => Task.FromResult(true));
        await app.StartLinuxAsync(false);
        await UntilAsync(() => app.Controls?.IsVisible == true && Button(app, "Quit").IsEnabled);
        var pid = File.ReadAllText(Path.Combine(runner.Root, "pid"));
        Click(Button(app, "Quit"));
        await app.LinuxLifecycle!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("could not complete", Text(app));
        Assert.False(Button(app, "Restart").IsEnabled);
        Assert.Equal(pid, File.ReadAllText(Path.Combine(runner.Root, "pid")));
        Click(Button(app, "Close desktop controls"));
        app.Controls!.Close();
    });

    private sealed class OwnedConfiguration : IDisposable
    {
        private readonly string? previous = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        public LinuxLauncherSettingsStore Store { get; }
        public OwnedConfiguration(string root)
        {
            var directory = Path.Combine(root, "native choices");
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", directory);
            Store = new(Path.Combine(directory, "tdsblive"));
        }
        public void Dispose() => Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", previous);
    }
}
