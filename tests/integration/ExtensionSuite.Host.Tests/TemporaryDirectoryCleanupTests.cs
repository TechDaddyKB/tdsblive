using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class WindowsCleanupFactAttribute : FactAttribute
{
    public WindowsCleanupFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Windows file-handle deletion semantics are required.";
    }
}

public sealed class TemporaryDirectoryCleanupTests
{
    [WindowsCleanupFact]
    public async Task TransientWindowsFileLockIsReleasedBeforeTemporaryCleanupCompletes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-cleanup-check", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var handle = new FileStream(Path.Combine(directory, "owned.db"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var release = Task.Run(async () => { await Task.Delay(100); handle.Dispose(); });
            FoundationHostFactory.DeleteTemporaryDirectory(directory);
            await release;
            Assert.False(Directory.Exists(directory));
        }
        finally { handle.Dispose(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [WindowsCleanupFact]
    public void PersistentWindowsFileLockFailsInsteadOfHidingAResourceLeak()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-cleanup-check", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var handle = new FileStream(Path.Combine(directory, "owned.db"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        try
        {
            Assert.Throws<IOException>(() => FoundationHostFactory.DeleteTemporaryDirectory(directory));
            Assert.True(Directory.Exists(directory));
        }
        finally { handle.Dispose(); Directory.Delete(directory, true); }
    }
}
