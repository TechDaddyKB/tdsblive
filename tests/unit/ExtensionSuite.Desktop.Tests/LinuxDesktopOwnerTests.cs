using ExtensionSuite.Desktop;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class LinuxDesktopOwnerTests
{
    [Fact]
    public async Task DuplicateRequestsActivationWhileOwnerLivesWithoutBackendOrProfileFiles()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var example = new OwnedExample();
        var profile = Path.Combine(example.Root, "missing-prefix", "drive_c", "TDSBLiveData");
        using var owner = LinuxDesktopOwner.AcquireOrRequestOpen(profile, example.Root);
        Assert.NotNull(owner);
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Watch(() => activated.TrySetResult());
        Assert.Null(LinuxDesktopOwner.AcquireOrRequestOpen(profile, example.Root));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(Directory.Exists(Path.Combine(example.Root, "missing-prefix")));
        var files = Directory.GetFiles(Path.Combine(example.Root, "tdsblive-desktop"));
        Assert.All(files, file => Assert.Equal(0, new FileInfo(file).Length));
        foreach (var file in files)
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        owner.Dispose();
        using var replacement = LinuxDesktopOwner.AcquireOrRequestOpen(profile, example.Root);
        Assert.NotNull(replacement);
    }

    [Fact]
    public void AliasedProfilesShareOwnershipWhileOtherProfilesRemainIndependent()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var example = new OwnedExample();
        var prefix = Path.Combine(example.Root, "prefix");
        Directory.CreateDirectory(prefix);
        var alias = Path.Combine(example.Root, "alias prefix");
        Directory.CreateSymbolicLink(alias, prefix);
        using var owner = LinuxDesktopOwner.AcquireOrRequestOpen(Path.Combine(prefix, "drive_c", "setup"), example.Root);
        Assert.NotNull(owner);
        Assert.Null(LinuxDesktopOwner.AcquireOrRequestOpen(Path.Combine(alias, "drive_c", "setup"), example.Root));
        using var other = LinuxDesktopOwner.AcquireOrRequestOpen(Path.Combine(prefix, "drive_c", "other"), example.Root);
        Assert.NotNull(other);
        Assert.False(Directory.Exists(Path.Combine(prefix, "drive_c")));
    }

    [Fact]
    public async Task ClosingControlsStopsActivationAndReleasesOwnership()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var example = new OwnedExample();
        var profile = Path.Combine(example.Root, "profile");
        var calls = 0;
        var owner = LinuxDesktopOwner.AcquireOrRequestOpen(profile, example.Root)!;
        owner.Watch(() => Interlocked.Increment(ref calls));
        owner.Dispose();
        owner.Dispose();
        using var replacement = LinuxDesktopOwner.AcquireOrRequestOpen(profile, example.Root);
        Assert.NotNull(replacement);
        Assert.Null(LinuxDesktopOwner.AcquireOrRequestOpen(profile, example.Root));
        await Task.Delay(700);
        Assert.Equal(0, calls);
    }

    private sealed class OwnedExample : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tdsblive-native-owner-" + Guid.NewGuid().ToString("N"));
        public OwnedExample() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
