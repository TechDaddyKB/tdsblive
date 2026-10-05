using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using ExtensionSuite.Desktop;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class LinuxSetupWindowTests
{
    private static async Task RunAsync(Func<Task> test)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await session.Dispatch(async () => { await test(); return true; }, deadline.Token);
    }

    private static T Field<T>(LinuxSetupWindow window, string id) where T : Control => window.GetLogicalDescendants()
        .OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == "Linux-" + id);

    [Fact]
    public Task MissingExistingSetupDoesNotCreateOrStartAnEmptyReplacement() => RunAsync(async () =>
    {
        using var example = new OwnedExample();
        var missing = Path.Combine(example.Root, "missing prefix");
        var starts = 0;
        var cancels = 0;
        var window = new LinuxSetupWindow(example.Root, example.Settings.ApplicationPath,
            (_, _) => { starts++; return Task.CompletedTask; }, () => cancels++,
            example.Settings with { PrefixDirectory = missing, DataDirectory = Path.Combine(missing, "drive_c", "TDSBLiveData") });
        window.Show();
        await window.RefreshProfilesAsync();
        Assert.False(Field<CheckBox>(window, "NewSetup").IsChecked == true);
        await window.StartSelectedAsync();
        Assert.Equal(0, starts);
        Assert.Contains("missing", window.Feedback.Text);
        Assert.False(Directory.Exists(missing));
        Assert.True(window.StartButton.IsEnabled);
        window.Close();
        Assert.Equal(1, cancels);
    });

    [Fact]
    public Task MultipleProfilesRequireAChoiceAndKeepTheSelectedProfile() => RunAsync(async () =>
    {
        using var example = new OwnedExample();
        var second = Path.Combine(example.Settings.PrefixDirectory, "drive_c", "TDSBLiveData");
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(second, "tdsblive.db"), "owned marker; never opened");
        LinuxLauncherSettings? started = null;
        var window = new LinuxSetupWindow(example.Root, example.Settings.ApplicationPath,
            (selection, isNew) => { Assert.False(isNew); started = selection; return Task.CompletedTask; }, () => { },
            example.Settings with { DataDirectory = "" });
        window.Show();
        await window.RefreshProfilesAsync();
        var profiles = Field<ComboBox>(window, "Profiles");
        Assert.Equal(2, profiles.ItemCount);
        Assert.Equal(-1, profiles.SelectedIndex);
        await window.StartSelectedAsync();
        Assert.Null(started);
        profiles.SelectedIndex = 1;
        var selected = Field<TextBox>(window, "DataDirectory").Text;
        await window.RefreshProfilesAsync();
        Assert.Equal(selected, Field<TextBox>(window, "DataDirectory").Text);
        Assert.True(profiles.SelectedIndex >= 0);
        await window.StartSelectedAsync();
        Assert.Equal(selected, started!.DataDirectory);
        Assert.False(window.IsVisible);
    });

    [Fact]
    public Task SwitchingRunnersRetainsBothCustomProgramsAndTheExistingSetup() => RunAsync(() =>
    {
        using var example = new OwnedExample();
        var window = new LinuxSetupWindow(example.Root, example.Settings.ApplicationPath,
            (_, _) => Task.CompletedTask, () => { }, example.Settings);
        window.Show();
        var runner = Field<ComboBox>(window, "Runner");
        var program = Field<TextBox>(window, "RunnerPath");
        runner.SelectedIndex = 1;
        program.Text = Path.Combine(example.Root, "custom Proton launcher", "umu-run");
        var umu = program.Text;
        runner.SelectedIndex = 0;
        Assert.Equal(example.Settings.RunnerPath, program.Text);
        runner.SelectedIndex = 1;
        Assert.Equal(umu, program.Text);
        Assert.Equal(example.Settings.PrefixDirectory, Field<TextBox>(window, "Prefix").Text);
        Assert.Equal(example.Settings.DataDirectory, Field<TextBox>(window, "DataDirectory").Text);
        window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task NewSetupRequiresExplicitChoiceAndDoesNotEraseTheExistingChoice() => RunAsync(async () =>
    {
        using var example = new OwnedExample();
        LinuxLauncherSettings? started = null;
        var window = new LinuxSetupWindow(example.Root, example.Settings.ApplicationPath,
            (selection, isNew) => { Assert.True(isNew); started = selection; return Task.CompletedTask; }, () => { }, example.Settings);
        window.Show();
        var newSetup = Field<CheckBox>(window, "NewSetup");
        newSetup.IsChecked = true;
        newSetup.IsChecked = false;
        Assert.Equal(example.Settings.DataDirectory, Field<TextBox>(window, "DataDirectory").Text);
        Field<TextBox>(window, "Prefix").Text = "";
        newSetup.IsChecked = true;
        Assert.Equal(Path.Combine(example.Root, "wine-prefix"), Field<TextBox>(window, "Prefix").Text);
        await window.StartSelectedAsync();
        Assert.Equal(Path.Combine(example.Root, "wine-prefix", "drive_c", "TDSBLiveData"), started!.DataDirectory);
        Assert.False(Directory.Exists(started.PrefixDirectory)); // The form does not initialize Wine itself.
        Assert.False(window.IsVisible);
    });

    [Fact]
    public Task PendingStartCannotBeDuplicatedOrClosedAndFailureKeepsSelections() => RunAsync(async () =>
    {
        using var example = new OwnedExample();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        var cancels = 0;
        var window = new LinuxSetupWindow(example.Root, example.Settings.ApplicationPath,
            (_, _) => { starts++; return pending.Task; }, () => cancels++, example.Settings);
        window.Show();
        var starting = window.StartSelectedAsync();
        Assert.False(window.StartButton.IsEnabled);
        Assert.False(Field<TextBox>(window, "Prefix").IsEffectivelyEnabled);
        Assert.False(Field<Button>(window, "Cancel").IsEnabled);
        window.Close();
        Assert.True(window.IsVisible);
        Assert.Equal(0, cancels);
        await window.StartSelectedAsync();
        Assert.Equal(1, starts);
        pending.SetException(new IOException("owned runner output must not be shown"));
        await starting;
        Assert.True(window.StartButton.IsEnabled);
        Assert.Contains("could not start", window.Feedback.Text);
        Assert.DoesNotContain("owned runner output", window.Feedback.Text);
        Assert.Equal(example.Settings.PrefixDirectory, Field<TextBox>(window, "Prefix").Text);
        Assert.Equal(example.Settings.DataDirectory, Field<TextBox>(window, "DataDirectory").Text);
        window.Close();
        Assert.Equal(1, cancels);
    });

    [Theory]
    [InlineData(320, 360, false)]
    [InlineData(320, 360, true)]
    [InlineData(640, 700, false)]
    [InlineData(640, 700, true)]
    public Task ShortAndNarrowFormContainsScrollableFieldsAndReachableNamedButtons(int width, int height, bool dark) => RunAsync(() =>
    {
        using var example = new OwnedExample();
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = new LinuxSetupWindow(example.Root, example.Settings.ApplicationPath,
            (_, _) => Task.CompletedTask, () => { }, example.Settings) { Width = width, Height = height };
        window.Show();
        window.UpdateLayout();
        Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, window.ActualThemeVariant);
        var scroll = Assert.Single(window.GetLogicalDescendants().OfType<ScrollViewer>(),
            viewer => viewer.Content is StackPanel panel && panel.Children.OfType<CheckBox>().Any());
        Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        foreach (var text in scroll.GetLogicalDescendants().OfType<TextBlock>())
        {
            var position = text.TranslatePoint(default, scroll);
            Assert.NotNull(position);
            Assert.True(position.Value.X >= 0);
            Assert.True(position.Value.X + text.Bounds.Width <= scroll.Viewport.Width + 1);
        }
        foreach (var id in new[] { "Start", "Cancel" })
        {
            var button = Field<Button>(window, id);
            Assert.True(button.Bounds.Height >= 44);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            var origin = button.TranslatePoint(default, window);
            Assert.NotNull(origin);
            Assert.True(origin.Value.X >= 0 && origin.Value.Y >= 0);
            Assert.True(origin.Value.X + button.Bounds.Width <= window.Bounds.Width);
            Assert.True(origin.Value.Y + button.Bounds.Height <= window.Bounds.Height);
        }
        Assert.True(Field<Button>(window, "Cancel").IsCancel);
        window.Close();
        return Task.CompletedTask;
    });

    private sealed class OwnedExample : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tdsblive-linux-form-" + Guid.NewGuid().ToString("N"));
        public LinuxLauncherSettings Settings { get; }
        public OwnedExample()
        {
            Directory.CreateDirectory(Root);
            var runner = Path.Combine(Root, "wine");
            var application = Path.Combine(Root, "TDSBLive.exe");
            File.WriteAllText(runner, "owned runner marker; never executed");
            File.WriteAllText(application, "owned executable marker; never executed");
            var prefix = Path.Combine(Root, "existing prefix");
            var data = Path.Combine(prefix, "drive_c", "users", "owned", "AppData", "Local", "TDSBLive");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, "configuration.json"), "{}");
            Settings = new(LinuxRunnerKind.Wine, runner, prefix, application, data);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
