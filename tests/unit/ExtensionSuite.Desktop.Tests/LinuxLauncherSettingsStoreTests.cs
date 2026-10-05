using System.Text.Json;
using ExtensionSuite.Desktop;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class LinuxLauncherSettingsStoreTests
{
    [Fact]
    public void MissingSettingsRemainMissingUntilTheUserCompletesSetup()
    {
        using var example = new OwnedExample();
        Assert.Null(example.Store.Load());
        Assert.False(Directory.Exists(example.Store.DirectoryPath));
    }

    [Fact]
    public void PrivateRoundTripAndReplacementRetainTheChosenSetup()
    {
        using var example = new OwnedExample();
        var before = File.ReadAllBytes(Path.Combine(example.Settings.DataDirectory, "configuration.json"));
        example.Store.Save(example.Settings);
        Assert.Equal(example.Settings, example.Store.Load());
        var json = File.ReadAllText(example.Store.FilePath);
        Assert.DoesNotContain("SessionToken", json, StringComparison.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(example.Store.FilePath));
        example.Store.Save(example.Settings with { ProtonDirectory = null });
        Assert.Null(example.Store.Load()!.ProtonDirectory);
        Assert.Single(Directory.EnumerateFiles(example.Store.DirectoryPath));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(example.Settings.DataDirectory, "configuration.json")));
    }

    [Fact]
    public void ARemovedProfileCannotBeRecreatedByLoadingOrSavingSelections()
    {
        using var example = new OwnedExample();
        example.Store.Save(example.Settings);
        var before = File.ReadAllBytes(example.Store.FilePath);
        Directory.Delete(example.Settings.DataDirectory, true);
        Assert.Throws<ArgumentException>(() => example.Store.Load());
        Assert.Throws<ArgumentException>(() => example.Store.Save(example.Settings));
        Assert.False(Directory.Exists(example.Settings.DataDirectory));
        Assert.Equal(before, File.ReadAllBytes(example.Store.FilePath));
    }

    [Theory]
    [InlineData("{\"version\":2,\"settings\":null}")]
    [InlineData("null")]
    [InlineData("{}")]
    public void UnknownVersionsOrMissingSettingsAreNotReplaced(string text)
    {
        using var example = new OwnedExample();
        Directory.CreateDirectory(example.Store.DirectoryPath);
        File.WriteAllText(example.Store.FilePath, text);
        Assert.Throws<InvalidDataException>(() => example.Store.Load());
        Assert.Equal(text, File.ReadAllText(example.Store.FilePath));
    }

    [Fact]
    public void OversizedAndMalformedFilesFailWithoutChangingTheirContents()
    {
        using var example = new OwnedExample();
        Directory.CreateDirectory(example.Store.DirectoryPath);
        var oversized = new string(' ', 16 * 1024 + 1);
        File.WriteAllText(example.Store.FilePath, oversized);
        Assert.Throws<InvalidDataException>(() => example.Store.Load());
        Assert.Equal(oversized, File.ReadAllText(example.Store.FilePath));
        File.WriteAllText(example.Store.FilePath, "{");
        Assert.Throws<JsonException>(() => example.Store.Load());
        Assert.Equal("{", File.ReadAllText(example.Store.FilePath));
    }

    [LinuxOnlyFact]
    public void SettingsLinksCannotReadOrOverwriteAnUnrelatedFile()
    {
        using var example = new OwnedExample();
        Directory.CreateDirectory(example.Store.DirectoryPath);
        var unrelated = Path.Combine(example.Root, "unrelated owned example.json");
        File.WriteAllText(unrelated, "untouched owned content");
        File.CreateSymbolicLink(example.Store.FilePath, unrelated);
        Assert.Throws<InvalidDataException>(() => example.Store.Load());
        example.Store.Save(example.Settings);
        Assert.Equal("untouched owned content", File.ReadAllText(unrelated));
        Assert.Null(new FileInfo(example.Store.FilePath).LinkTarget);
        Assert.Equal(example.Settings, example.Store.Load());
    }

    private sealed class LinuxOnlyFactAttribute : FactAttribute
    {
        public LinuxOnlyFactAttribute()
        {
            if (!OperatingSystem.IsLinux()) Skip = "Actual Unix symlink behavior requires Linux.";
        }
    }

    private sealed class OwnedExample : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tdsblive-linux-store-" + Guid.NewGuid().ToString("N"));
        public LinuxLauncherSettings Settings { get; }
        public LinuxLauncherSettingsStore Store { get; }
        public OwnedExample()
        {
            var prefix = Path.Combine(Root, "preserved Windows settings");
            var data = Path.Combine(prefix, "drive_c", "TDSBLiveData");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, "configuration.json"), "{\"displayName\":\"Preserved example\"}");
            var program = Path.Combine(Root, "TDSBLive.exe");
            var runner = Path.Combine(Root, "wine");
            File.WriteAllText(program, "owned marker; never executed");
            File.WriteAllText(runner, "owned marker; never executed");
            Settings = new(LinuxRunnerKind.Wine, runner, prefix, program, data);
            Store = new(Path.Combine(Root, "native launcher settings"));
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
