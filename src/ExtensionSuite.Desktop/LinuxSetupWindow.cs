using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Controls.Platform;
using Avalonia.Platform.Storage;

namespace ExtensionSuite.Desktop;

public sealed class LinuxSetupWindow : Window
{
    private readonly Func<LinuxLauncherSettings, bool, Task> start;
    private readonly Action cancel;
    private readonly Func<Task>? installShortcut;
    private readonly string newPrefix;
    private readonly ComboBox runner = new() { ItemsSource = new[] { "Wine", "Proton (UMU)" }, MinHeight = 44 };
    private readonly TextBox runnerPath = new() { MinHeight = 44 };
    private readonly TextBox prefix = new() { MinHeight = 44 };
    private readonly TextBox application = new() { MinHeight = 44 };
    private readonly TextBox proton = new() { MinHeight = 44 };
    private readonly TextBox data = new() { MinHeight = 44 };
    private readonly ComboBox profiles = new() { MinHeight = 44 };
    private readonly CheckBox newSetup = new() { Content = new TextBlock
        { Text = "Start a new empty TDSBLive setup", TextWrapping = TextWrapping.Wrap }, MinHeight = 44 };
    private readonly TextBlock destination = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox shortcut = new() { Content = new TextBlock
        { Text = "Add TDSBLive to my applications menu", TextWrapping = TextWrapping.Wrap }, MinHeight = 44 };
    private readonly Button launch = new() { Content = "Start TDSBLive", MinHeight = 44, MinWidth = 132 };
    private readonly Button close = new() { Content = "Cancel", MinHeight = 44, MinWidth = 100 };
    private readonly StackPanel fields = new() { Spacing = 18 };
    private readonly Dictionary<int, string?> runnerChoices = new();
    private int previousRunner;
    private Control protonField = null!;
    private Control existingFields = null!;
    private bool busy, started, canceled;
    internal Button StartButton => launch;
    internal TextBlock Feedback => feedback;

    public LinuxSetupWindow(string settingsDirectory, string bundledApplication,
        Func<LinuxLauncherSettings, bool, Task> start, Action cancel, LinuxLauncherSettings? initial = null,
        Func<Task>? installShortcut = null)
    {
        (this.start, this.cancel) = (start, cancel);
        this.installShortcut = installShortcut;
        newPrefix = Path.Combine(settingsDirectory, "wine-prefix");
        Title = "Set up TDSBLive on Linux";
        Width = 640; Height = 700; MinWidth = 320; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        X11Properties.SetNetWmWindowType(this, X11NetWmWindowType.Dialog);
        runner.SelectedIndex = initial?.Runner == LinuxRunnerKind.Umu ? 1 : 0;
        runnerPath.Text = initial?.RunnerPath ?? InstalledRunner("wine");
        previousRunner = runner.SelectedIndex;
        runnerChoices[previousRunner] = runnerPath.Text;
        prefix.Text = initial?.PrefixDirectory ?? DefaultExistingPrefix();
        application.Text = initial?.ApplicationPath ?? bundledApplication;
        proton.Text = initial?.ProtonDirectory;
        data.Text = initial?.DataDirectory;
        AccessibleName(runner, "Windows app runner", "Linux-Runner");
        AccessibleName(runnerPath, "Installed Wine or UMU program", "Linux-RunnerPath");
        AccessibleName(prefix, "Windows settings folder (Wine prefix)", "Linux-Prefix");
        AccessibleName(application, "TDSBLive Windows application", "Linux-Application");
        AccessibleName(proton, "Installed Proton folder", "Linux-Proton");
        AccessibleName(data, "Existing TDSBLive setup folder", "Linux-DataDirectory");
        AccessibleName(profiles, "Saved TDSBLive setups", "Linux-Profiles");
        AccessibleName(newSetup, "Start a new empty TDSBLive setup", "Linux-NewSetup");
        AccessibleName(launch, "Start TDSBLive", "Linux-Start");
        AccessibleName(close, "Cancel Linux setup", "Linux-Cancel");
        AccessibleName(feedback, "Setup status", "Linux-Status");
        AccessibleName(shortcut, "Add TDSBLive to my applications menu", "Linux-Shortcut");
        close.IsCancel = true;

        var header = new StackPanel { Spacing = 10 };
        header.Children.Add(new TextBlock { Text = "Keep your setup, choose how to run it", FontSize = 22, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        header.Children.Add(Help("Wine or Proton runs the Windows app. This launcher gives you a native Linux tray and opens your normal browser."));
        fields.Children.Add(header);
        fields.Children.Add(Label("1. Choose the Windows app runner", runner));
        fields.Children.Add(Field("Runner program", runnerPath, false, "Choose the installed wine, wine64 or umu-run program."));
        protonField = Field("Proton folder", proton, true, "For UMU, choose the installed Proton folder containing proton and toolmanifest.vdf.");
        fields.Children.Add(protonField);
        fields.Children.Add(Field("2. Windows settings folder (Wine prefix)", prefix, true,
            "Already using TDSBLive? Choose the same folder you used before. It holds Windows settings and the keys for your saved connections."));
        fields.Children.Add(newSetup);
        fields.Children.Add(Help("Leave this unchecked to keep an existing setup. For your first TDSBLive setup, check it to start with empty settings."));
        var existing = new StackPanel { Spacing = 12 };
        existing.Children.Add(Label("Saved setups found in this folder", profiles));
        existing.Children.Add(Field("Existing TDSBLive setup folder", data, true,
            "Choose your previous setup. If several are listed, choose the one you used before. Browse can find a setup stored elsewhere."));
        existingFields = existing;
        fields.Children.Add(existing);
        fields.Children.Add(destination);
        fields.Children.Add(Field("3. TDSBLive Windows application", application, false,
            "The Linux download includes the current Windows app. Keep all of its files together; use TDSBLive.exe from that folder."));
        if (installShortcut is not null)
        {
            fields.Children.Add(Label("4. Add a shortcut (optional)", shortcut));
            fields.Children.Add(Help("Find TDSBLive in your applications menu next time. Keep the Linux app folder in place so the shortcut keeps working. This does not start TDSBLive when you sign in."));
        }
        var scroll = new ScrollViewer { Content = fields, Margin = new Thickness(24, 20, 24, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.PropertyChanged += (_, change) =>
        {
            // Long paths and checkbox captions must not expand the vertical form
            // past its viewport when a native window shrinks.
            if (change.Property == ScrollViewer.ViewportProperty && scroll.Viewport.Width > 0)
                fields.Width = scroll.Viewport.Width;
        };
        var footer = new StackPanel { Margin = new Thickness(24, 12, 24, 20), Spacing = 12 };
        footer.Children.Add(feedback);
        var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12 };
        actions.Children.Add(close); actions.Children.Add(launch); footer.Children.Add(actions);
        var page = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        page.Children.Add(scroll); Grid.SetRow(footer, 1); page.Children.Add(footer);
        Content = page;

        runner.SelectionChanged += (_, _) =>
        {
            runnerChoices[previousRunner] = runnerPath.Text;
            protonField.IsVisible = runner.SelectedIndex == 1;
            runnerPath.Text = runnerChoices.TryGetValue(runner.SelectedIndex, out var selected) ? selected :
                InstalledRunner(runner.SelectedIndex == 1 ? "umu-run" : "wine");
            previousRunner = runner.SelectedIndex;
        };
        newSetup.IsCheckedChanged += (_, _) => UpdateChoice();
        prefix.TextChanged += (_, _) => UpdateDestination();
        prefix.LostFocus += async (_, _) => await RefreshProfilesAsync();
        profiles.SelectionChanged += (_, _) => { if (profiles.SelectedItem is ProfileChoice choice) data.Text = choice.Path; };
        launch.Click += async (_, _) => await StartSelectedAsync();
        close.Click += (_, _) => Close();
        Closing += (_, e) =>
        {
            if (busy) { e.Cancel = true; return; }
            if (!started && !canceled) { canceled = true; cancel(); }
        };
        protonField.IsVisible = runner.SelectedIndex == 1;
        UpdateChoice();
        Opened += async (_, _) => await RefreshProfilesAsync();
    }

    private void UpdateChoice()
    {
        var isNew = newSetup.IsChecked == true;
        existingFields.IsVisible = !isNew;
        destination.IsVisible = isNew;
        if (isNew && string.IsNullOrWhiteSpace(prefix.Text)) prefix.Text = newPrefix;
        UpdateDestination();
    }

    private void UpdateDestination() => destination.Text = "New TDSBLive settings will be saved in: " +
        (string.IsNullOrWhiteSpace(prefix.Text) ? "Choose a Windows settings folder first." : Path.Combine(prefix.Text, "drive_c", "TDSBLiveData"));

    internal async Task RefreshProfilesAsync()
    {
        var selectedPrefix = prefix.Text;
        if (string.IsNullOrWhiteSpace(selectedPrefix)) return;
        try
        {
            var found = await Task.Run(() => LinuxLauncherSettings.FindProfiles(selectedPrefix));
            if (prefix.Text != selectedPrefix || busy) return;
            var choices = found.Select(path => new ProfileChoice(path)).ToArray();
            profiles.ItemsSource = choices;
            profiles.SelectedItem = choices.FirstOrDefault(choice => choice.Path == data.Text);
            if (string.IsNullOrWhiteSpace(data.Text) && choices.Length == 1) profiles.SelectedIndex = 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            feedback.Text = "Choose a local Windows settings folder. Browse can help you find your previous folder.";
        }
    }

    internal async Task StartSelectedAsync()
    {
        if (busy || started) return;
        LinuxLauncherSettings selection;
        var isNew = newSetup.IsChecked == true;
        try
        {
            selection = new LinuxLauncherSettings(runner.SelectedIndex == 1 ? LinuxRunnerKind.Umu : LinuxRunnerKind.Wine,
                runnerPath.Text ?? "", prefix.Text ?? "", application.Text ?? "",
                isNew ? Path.Combine(prefix.Text ?? "", "drive_c", "TDSBLiveData") : data.Text ?? "", proton.Text).Normalize(isNew);
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            feedback.Text = error is ArgumentException ? error.Message : "The selected folder could not be read. Check that you can open it, then try again.";
            return;
        }
        busy = true; fields.IsEnabled = false; launch.IsEnabled = false; close.IsEnabled = false;
        feedback.Text = runner.SelectedIndex == 1 ? "Preparing Proton and TDSBLive… Keep this window open. The first start can take a few minutes." : "Starting TDSBLive… Keep this window open.";
        try
        {
            if (shortcut.IsChecked == true && installShortcut is not null)
            {
                try { await installShortcut(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    feedback.Text = "The applications-menu shortcut could not be added. Check that the Linux app folder is complete and your applications folder is writable, or leave the shortcut box unchecked to start without it.";
                    return;
                }
            }
            await start(selection, isNew);
            started = true;
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or
            Win32Exception or OperationCanceledException or UnauthorizedAccessException)
        {
            feedback.Text = "TDSBLive could not start. Your saved setup has not been moved. Check the application folder, runner and Windows settings folder, then try again.";
        }
        finally { busy = false; fields.IsEnabled = true; close.IsEnabled = true; launch.IsEnabled = !started; }
        if (started) Close();
    }

    private Control Field(string label, TextBox entry, bool folder, string help)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(entry);
        var browse = new Button { Content = "Browse…", MinHeight = 44, MinWidth = 88, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetName(browse, "Browse for " + label);
        Grid.SetColumn(browse, 1); row.Children.Add(browse);
        browse.Click += async (_, _) =>
        {
            try
            {
                IStorageItem? selected;
                if (folder) selected = (await StorageProvider.OpenFolderPickerAsync(new() { Title = label, AllowMultiple = false })).FirstOrDefault();
                else selected = (await StorageProvider.OpenFilePickerAsync(new() { Title = label, AllowMultiple = false })).FirstOrDefault();
                if (selected is null) return;
                using (selected)
                {
                    var path = selected.TryGetLocalPath();
                    if (path is null) { feedback.Text = "Choose a file or folder stored on this computer."; return; }
                    entry.Text = path;
                }
                if (entry == prefix) await RefreshProfilesAsync();
            }
            catch (Exception error) when (error is IOException or NotSupportedException or InvalidOperationException or UnauthorizedAccessException)
            {
                feedback.Text = "The picker could not open. You can paste the file or folder path into this field.";
            }
        };
        var group = new StackPanel { Spacing = 8 };
        group.Children.Add(Label(label, row)); group.Children.Add(Help(help)); return group;
    }

    private static Control Label(string label, Control field)
    {
        var group = new StackPanel { Spacing = 8 };
        group.Children.Add(new TextBlock { Text = label, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        group.Children.Add(field); return group;
    }
    private static TextBlock Help(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private static void AccessibleName(Control field, string label, string id)
    {
        AutomationProperties.SetName(field, label); AutomationProperties.SetAutomationId(field, id);
    }
    private static string InstalledRunner(string name) => (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator).Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists) ?? "";
    private static string DefaultExistingPrefix()
    {
        var candidate = Environment.GetEnvironmentVariable("WINEPREFIX") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wine");
        return Path.IsPathFullyQualified(candidate) && Directory.Exists(candidate) ? candidate : "";
    }
    private sealed record ProfileChoice(string Path)
    {
        public override string ToString() => Path;
    }
}
