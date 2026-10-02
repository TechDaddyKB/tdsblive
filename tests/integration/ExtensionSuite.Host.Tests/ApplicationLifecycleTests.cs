using System.ComponentModel;
using System.Diagnostics;
using ExtensionSuite.Host;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class ApplicationLifecycleTests
{
    [Fact]
    public async Task ExpiredConfirmationCannotQueueRestoreAndReplacementCleansOldStage()
    {
        using var host = new FoundationHostFactory();
        using var client = host.CreateClient();
        var archive = new RecoveryArchive(host.Services.GetRequiredService<ApplicationPaths>());
        using var bytes = new MemoryStream();
        await archive.CreateAsync(bytes);
        bytes.Position = 0;
        var first = await archive.PrepareAsync(bytes);
        var clock = new Clock();
        using var lifecycle = new ApplicationLifecycle(new Lifetime(), clock);
        var preview = await lifecycle.StageAsync(first);
        Assert.NotNull(preview);
        clock.Now = preview.ExpiresAt;
        Assert.False(lifecycle.RequestRestore(preview.Id));
        Assert.Null(lifecycle.Operation);
        bytes.Position = 0;
        var second = await archive.PrepareAsync(bytes);
        var replacement = await lifecycle.StageAsync(second);
        Assert.NotNull(replacement);
        Assert.False(Directory.Exists(first.DirectoryPath));
        Assert.False(lifecycle.RequestRestore(preview.Id));
        Assert.True(lifecycle.RequestRestore(replacement.Id));
        Assert.False(lifecycle.Request("quit"));
        // Queued restoration belongs to the post-shutdown owner, not lifecycle disposal.
        lifecycle.Dispose();
        Assert.True(Directory.Exists(second.DirectoryPath));
        await second.DisposeAsync();
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void ConcurrentStopRequestsAcceptExactlyOneOperation()
    {
        using var lifecycle = new ApplicationLifecycle(new Lifetime(), TimeProvider.System);
        var accepted = 0;
        Parallel.For(0, 32, index =>
        {
            if (lifecycle.Request(index % 2 == 0 ? "restart" : "quit")) Interlocked.Increment(ref accepted);
        });
        Assert.Equal(1, accepted);
        Assert.NotNull(lifecycle.Operation);
        Assert.False(lifecycle.RequestRestore(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => lifecycle.Request("unknown"));
    }

    [Theory]
    [InlineData("dotnet", true)]
    [InlineData("dotnet.exe", true)]
    [InlineData("TDSBLive.exe", false)]
    public void RelaunchUsesSeparateArgumentsWithoutShell(string executable, bool managed)
    {
        ProcessStartInfo? captured = null;
        var result = ApplicationRelauncher.TryStart(executable, "app with spaces.dll", "data with spaces", false, info =>
        {
            captured = info;
            return new Process();
        });
        Assert.True(result);
        Assert.NotNull(captured);
        Assert.False(captured.UseShellExecute);
        Assert.Equal(managed ? new[] { "app with spaces.dll", "--TDSBLive:DataDirectory", "data with spaces", "--TDSBLive:OpenEditor=false" }
            : new[] { "--TDSBLive:DataDirectory", "data with spaces", "--TDSBLive:OpenEditor=false" }, captured.ArgumentList);
    }

    [Fact]
    public void RelaunchReportsMissingProcessAndLaunchFailures()
    {
        Assert.False(ApplicationRelauncher.TryStart("missing", "app.dll", "data", true, _ => null));
        Assert.False(ApplicationRelauncher.TryStart("missing", "app.dll", "data", true, _ => throw new Win32Exception()));
        Assert.False(ApplicationRelauncher.TryStart("missing", "app.dll", "data", true, _ => throw new InvalidOperationException()));
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}
