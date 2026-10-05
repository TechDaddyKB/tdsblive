using ExtensionSuite.Desktop;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class LinuxLauncherSettingsTests
{
    [Theory]
    [InlineData(LinuxRunnerKind.Wine)]
    [InlineData(LinuxRunnerKind.Umu)]
    public void ExistingSetupAndRunnerArePreservedWithoutShellArguments(LinuxRunnerKind runner)
    {
        using var example = new OwnedExample(runner);
        var before = File.ReadAllBytes(Path.Combine(example.Settings.DataDirectory, "configuration.json"));
        var normalized = example.Settings.Normalize();
        var launch = normalized.CreateStartInfo();
        Assert.Equal(example.Settings.PrefixDirectory, launch.Environment["WINEPREFIX"]);
        Assert.Equal(example.Settings.RunnerPath, launch.FileName);
        Assert.False(launch.UseShellExecute);
        Assert.True(launch.RedirectStandardInput && launch.RedirectStandardOutput && launch.RedirectStandardError);
        Assert.Empty(launch.Arguments);
        Assert.Equal(example.Settings.ApplicationPath, launch.ArgumentList[0]);
        Assert.Equal("--TDSBLive:DesktopMode=external", launch.ArgumentList[1]);
        Assert.Equal("--TDSBLive:OpenEditor=false", launch.ArgumentList[2]);
        Assert.Equal("--TDSBLive:DataDirectory=C:\\users\\owned\\AppData\\Local\\TDSBLive", launch.ArgumentList[3]);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(example.Settings.DataDirectory, "configuration.json")));
        if (runner == LinuxRunnerKind.Umu)
        {
            Assert.Equal(example.Settings.ProtonDirectory, launch.Environment["PROTONPATH"]);
            Assert.Equal("0", launch.Environment["GAMEID"]);
            Assert.Equal("waitforexitandrun", launch.Environment["PROTON_VERB"]);
            Assert.False(launch.Environment.ContainsKey("STORE"));
        }
    }

    [Fact]
    public void MissingOrEmptyExistingProfileCannotSilentlyBecomeANewSetup()
    {
        using var example = new OwnedExample(LinuxRunnerKind.Wine);
        var empty = Path.Combine(example.Settings.PrefixDirectory, "drive_c", "empty setup");
        var settings = example.Settings with { DataDirectory = empty };
        Assert.Throws<ArgumentException>(() => settings.Normalize());
        Assert.False(Directory.Exists(empty));
        Directory.CreateDirectory(empty);
        Assert.Throws<ArgumentException>(() => settings.CreateStartInfo());
        Assert.Empty(Directory.EnumerateFileSystemEntries(empty));
        Directory.Delete(Path.Combine(example.Settings.PrefixDirectory, "drive_c"), true);
        Assert.Throws<ArgumentException>(() => example.Settings.Normalize());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void OrdinaryWineSetupDoesNotRequireProton(string? proton)
    {
        using var example = new OwnedExample(LinuxRunnerKind.Wine);
        var settings = (example.Settings with { ProtonDirectory = proton }).Normalize();
        Assert.Null(settings.ProtonDirectory);
        Assert.Equal(example.Settings.RunnerPath, settings.CreateStartInfo().FileName);
    }

    [Fact]
    public void NewSetupIsExplicitAndCannotOverwriteANonemptyFolderOrWholeDrive()
    {
        using var example = new OwnedExample(LinuxRunnerKind.Wine);
        Assert.Throws<ArgumentException>(() => example.Settings.Normalize(createNewProfile: true));
        var drive = Path.Combine(example.Settings.PrefixDirectory, "drive_c");
        Assert.Throws<ArgumentException>(() => (example.Settings with { DataDirectory = drive }).Normalize(createNewProfile: true));
        var newData = Path.Combine(drive, "TDSBLiveData");
        var candidate = example.Settings with { DataDirectory = newData };
        var launch = candidate.CreateStartInfo(createNewProfile: true);
        Assert.EndsWith("C:\\TDSBLiveData", launch.ArgumentList[3]);
        Assert.False(Directory.Exists(newData));
    }

    [Fact]
    public void DiscoveryRetainsMultipleExistingProfilesAndIgnoresUnrelatedFolders()
    {
        using var example = new OwnedExample(LinuxRunnerKind.Wine);
        var second = Path.Combine(example.Settings.PrefixDirectory, "drive_c", "TDSBLiveData");
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(second, "tdsblive.db"), "owned marker");
        Directory.CreateDirectory(Path.Combine(example.Settings.PrefixDirectory, "drive_c", "users", "other", "AppData", "Local", "TDSBLive"));
        var found = LinuxLauncherSettings.FindProfiles(example.Settings.PrefixDirectory);
        Assert.Equal(2, found.Count);
        Assert.Contains(example.Settings.DataDirectory, found);
        Assert.Contains(second, found);
        Assert.Throws<ArgumentException>(() => LinuxLauncherSettings.FindProfiles("relative-prefix"));
        Assert.Empty(LinuxLauncherSettings.FindProfiles(Path.Combine(example.Root, "absent")));
    }

    [Fact]
    public void UnavailableRunnerProtonAndApplicationChoicesFailClearly()
    {
        using var example = new OwnedExample(LinuxRunnerKind.Umu);
        Assert.Throws<ArgumentException>(() => (example.Settings with { Runner = (LinuxRunnerKind)99 }).Normalize());
        Assert.Throws<ArgumentException>(() => (example.Settings with { RunnerPath = "umu-run" }).Normalize());
        Assert.Throws<ArgumentException>(() => (example.Settings with { RunnerPath = example.Settings.ApplicationPath }).Normalize());
        Assert.Throws<ArgumentException>(() => (example.Settings with { ApplicationPath = example.Settings.RunnerPath }).Normalize());
        Assert.Throws<ArgumentException>(() => (example.Settings with { ProtonDirectory = null }).Normalize());
        Assert.Throws<ArgumentException>(() => (example.Settings with { ProtonDirectory = example.Root }).Normalize());
        Assert.Throws<ArgumentException>(() => (example.Settings with { PrefixDirectory = "" }).Normalize());
        Assert.Throws<ArgumentException>(() => (example.Settings with { DataDirectory = example.Settings.DataDirectory + "\n" }).Normalize());
        Assert.Throws<ArgumentException>(() => (example.Settings with { DataDirectory = Path.Combine(example.Root, "not mapped") }).Normalize(createNewProfile: true));
        File.Delete(Path.Combine(example.Settings.ProtonDirectory!, "toolmanifest.vdf"));
        Assert.Throws<ArgumentException>(() => example.Settings.Normalize());
    }

    [Theory]
    [InlineData(null, "mscoree=b;mshtml=")]
    [InlineData("", "mscoree=b;mshtml=")]
    [InlineData("d3dcompiler_47=n,b;*mscoree=n;mshtml=n", "d3dcompiler_47=n,b;mscoree=b;mshtml=")]
    [InlineData("mscoree,msxml3=n,b; mfplat=n;odd-entry", "msxml3=n,b;mfplat=n;odd-entry;mscoree=b;mshtml=")]
    public void BackendOverrideRetainsOtherDllChoices(string? inherited, string expected) =>
        Assert.Equal(expected, LinuxLauncherSettings.BackendOverrides(inherited));

    [Fact]
    public void WindowsIncompatibleProfileNamesAreRejected()
    {
        using var example = new OwnedExample(LinuxRunnerKind.Wine);
        var candidate = example.Settings with { DataDirectory = Path.Combine(example.Settings.PrefixDirectory, "drive_c", "bad?setup") };
        Assert.Throws<ArgumentException>(() => candidate.Normalize(createNewProfile: true));
    }

    [LinuxOnlyFact]
    public void ExistingSetupOutsideDriveCUsesItsActualWineDriveMapping()
    {
        using var example = new OwnedExample(LinuxRunnerKind.Wine);
        var data = Path.Combine(example.Root, "separate saved setup");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "configuration.json"), "{\"displayName\":\"Existing external setup\"}");
        var mappings = Path.Combine(example.Settings.PrefixDirectory, "dosdevices");
        Directory.CreateDirectory(mappings);
        Directory.CreateSymbolicLink(Path.Combine(mappings, "z:"), "/");
        var settings = example.Settings with { DataDirectory = data };
        var normalized = settings.Normalize();
        Assert.Equal("Z:\\" + data.TrimStart('/').Replace('/', '\\'), normalized.WindowsDataDirectory());
        var launch = normalized.CreateStartInfo();
        Assert.Equal("--TDSBLive:DataDirectory=" + normalized.WindowsDataDirectory(), launch.ArgumentList[3]);
        Assert.Equal("{\"displayName\":\"Existing external setup\"}", File.ReadAllText(Path.Combine(data, "configuration.json")));
    }

    private sealed class LinuxOnlyFactAttribute : FactAttribute
    {
        public LinuxOnlyFactAttribute()
        {
            if (!OperatingSystem.IsLinux()) Skip = "Actual Unix Wine drive symlinks require Linux.";
        }
    }

    private sealed class OwnedExample : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tdsblive-linux-settings-" + Guid.NewGuid().ToString("N"));
        public LinuxLauncherSettings Settings { get; }
        public OwnedExample(LinuxRunnerKind runner)
        {
            var program = Path.Combine(Root, "application with spaces; dollars$ & ()", "TDSBLive.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(program)!);
            File.WriteAllText(program, "owned executable marker; never executed");
            var loader = Path.Combine(Root, runner == LinuxRunnerKind.Wine ? "wine" : "umu-run");
            File.WriteAllText(loader, "owned runner marker; never executed");
            var prefix = Path.Combine(Root, "existing Windows settings with spaces");
            var data = Path.Combine(prefix, "drive_c", "users", "owned", "AppData", "Local", "TDSBLive");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, "configuration.json"), "{\"displayName\":\"Preserved owned setup\"}");
            var proton = Path.Combine(Root, "installed Proton");
            Directory.CreateDirectory(proton);
            File.WriteAllText(Path.Combine(proton, "proton"), "owned marker; never executed");
            File.WriteAllText(Path.Combine(proton, "toolmanifest.vdf"), "owned marker; never interpreted");
            Settings = new(runner, loader, prefix, program, data, proton);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
