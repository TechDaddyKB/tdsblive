using System.Diagnostics;
using System.Text.Json;
using ExtensionSuite.Desktop;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class LinuxApplicationShortcutTests
{
    [LinuxShortcutFact]
    public void ShortcutIsPerUserAndContainsOnlyNativeLaunchAndExplicitSetup()
    {
        using var fixture = new OwnedShortcut();
        var shortcut = LinuxApplicationShortcut.Install(fixture.Executable, fixture.Icon, fixture.DataHome);
        Assert.Equal(Path.Combine(fixture.DataHome, "applications", LinuxApplicationShortcut.FileName), shortcut);
        var text = File.ReadAllText(shortcut);
        Assert.Contains("Name=TDSBLive\n", text);
        Assert.Contains("Terminal=false", text);
        Assert.Contains("Name=Change Linux setup", text);
        Assert.Contains(" --setup\n", text);
        Assert.DoesNotContain("Autostart", text);
        Assert.DoesNotContain("WINEPREFIX", text);
        Assert.DoesNotContain("SessionToken", text);
        if (OperatingSystem.IsLinux())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(shortcut));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(shortcut)!, "*.tmp"));
    }

    [LinuxShortcutFact]
    public void ReinstallationAtomicallyReplacesTheShortcutWithoutFollowingItsSymlink()
    {
        using var fixture = new OwnedShortcut();
        var shortcut = LinuxApplicationShortcut.Install(fixture.Executable, fixture.Icon, fixture.DataHome);
        var first = File.ReadAllText(shortcut);
        File.Delete(shortcut);
        var unrelated = Path.Combine(fixture.Root, "unrelated");
        File.WriteAllText(unrelated, "keep this owned marker");
        File.CreateSymbolicLink(shortcut, unrelated);
        Assert.Equal(shortcut, LinuxApplicationShortcut.Install(fixture.Executable, fixture.Icon, fixture.DataHome));
        Assert.Null(new FileInfo(shortcut).LinkTarget);
        Assert.Equal(first, File.ReadAllText(shortcut));
        Assert.Equal("keep this owned marker", File.ReadAllText(unrelated));
    }

    [LinuxShortcutFact]
    public void IncompleteInvalidOrUnexecutableFoldersCannotCreateAShortcut()
    {
        using var fixture = new OwnedShortcut();
        foreach (var invalid in new[] { "relative", fixture.Executable + "\n", Path.Combine(fixture.Root, "missing"), fixture.Executable + "=" })
            Assert.Throws<ArgumentException>(() => LinuxApplicationShortcut.Install(invalid, fixture.Icon, fixture.DataHome));
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(fixture.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Assert.Throws<ArgumentException>(() => LinuxApplicationShortcut.Install(fixture.Executable, fixture.Icon, fixture.DataHome));
        Assert.False(Directory.Exists(fixture.DataHome));
    }

    [LinuxShortcutFact]
    public void InstallationFailureLeavesExistingFilesAlone()
    {
        using var fixture = new OwnedShortcut();
        Directory.CreateDirectory(fixture.DataHome);
        var blocker = Path.Combine(fixture.DataHome, "applications");
        File.WriteAllText(blocker, "keep this owned marker");
        Assert.Throws<IOException>(() => LinuxApplicationShortcut.Install(fixture.Executable, fixture.Icon, fixture.DataHome));
        Assert.Equal("keep this owned marker", File.ReadAllText(blocker));
        Assert.Single(Directory.EnumerateFileSystemEntries(fixture.DataHome));
    }

    [GioShortcutFact]
    public async Task ActualDesktopLauncherPreservesReservedCharactersAsOneExecutable()
    {
        using var fixture = new OwnedShortcut();
        var shortcut = LinuxApplicationShortcut.Install(fixture.Executable, fixture.Icon, fixture.DataHome);
        var info = new ProcessStartInfo("/usr/bin/gio") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        info.ArgumentList.Add("launch");
        info.ArgumentList.Add(shortcut);
        info.Environment["XDG_DATA_HOME"] = fixture.DataHome;
        using var process = Process.Start(info)!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var errors = process.StandardError.ReadToEndAsync(deadline.Token);
        await process.WaitForExitAsync(deadline.Token);
        await Task.WhenAll(output, errors);
        Assert.True(process.ExitCode == 0, "Owned GIO launcher failed: " + await errors);
        var opened = Path.Combine(Path.GetDirectoryName(fixture.Executable)!, "opened.json");
        while (!File.Exists(opened)) await Task.Delay(20, deadline.Token);
        using var arguments = JsonDocument.Parse(File.ReadAllText(opened));
        Assert.Equal("./TDSBLive.Desktop", Assert.Single(arguments.RootElement.GetProperty("arguments").EnumerateArray()).GetString());
        Assert.Equal(Path.GetDirectoryName(fixture.Executable), arguments.RootElement.GetProperty("directory").GetString());
    }

    private sealed class OwnedShortcut : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tdsblive-shortcut-" + Guid.NewGuid().ToString("N"));
        public string Executable { get; }
        public string Icon { get; }
        public string DataHome => Path.Combine(Root, "owned user data");
        public OwnedShortcut()
        {
            var folder = Path.Combine(Root, "app café with spaces $ percent% quote\" slash\\ apostrophe' & () equals=");
            Directory.CreateDirectory(folder);
            Executable = Path.Combine(folder, "TDSBLive.Desktop");
            Icon = Path.Combine(folder, "tdsblive.svg");
            File.WriteAllText(Icon, "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            File.WriteAllText(Executable, """
                #!/usr/bin/env python3
                import json
                import sys
                from pathlib import Path
                Path(__file__).with_name('opened.json').write_text(json.dumps({'arguments': sys.argv, 'directory': str(Path.cwd())}))
                """);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }

    private class LinuxShortcutFactAttribute : FactAttribute
    {
        public LinuxShortcutFactAttribute()
        {
            if (!OperatingSystem.IsLinux()) Skip = "Linux per-user applications menu only.";
        }
    }
    private sealed class GioShortcutFactAttribute : LinuxShortcutFactAttribute
    {
        public GioShortcutFactAttribute()
        {
            if (!File.Exists("/usr/bin/gio") || !File.Exists("/usr/bin/python3"))
                Skip = "Requires the actual GIO desktop launcher and owned Python executable.";
        }
    }
}
