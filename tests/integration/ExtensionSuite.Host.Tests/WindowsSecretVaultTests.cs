using System.Runtime.Versioning;
using System.Security.Cryptography;
using ExtensionSuite.Data;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Real Windows DPAPI is required; Windows CI runs this test.";
    }
}

[SupportedOSPlatform("windows")]
public sealed class WindowsSecretVaultTests
{
    [WindowsFact]
    public async Task ActualDpapiRoundtripReplacementDeletionAndCorruption()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-secrets-test", Guid.NewGuid().ToString());
        var vault = new WindowsSecretVault(directory);
        try
        {
            Assert.Null(await vault.GetAsync("missing"));
            await vault.SetAsync("streamerbot-password", "synthetic-test-value");
            var encrypted = await File.ReadAllBytesAsync(Path.Combine(directory, "streamerbot-password.dpapi"));
            Assert.DoesNotContain("synthetic-test-value", System.Text.Encoding.UTF8.GetString(encrypted));
            Assert.Equal("synthetic-test-value", await new WindowsSecretVault(directory).GetAsync("streamerbot-password"));
            await vault.SetAsync("streamerbot-password", "replacement");
            Assert.Equal("replacement", await vault.GetAsync("streamerbot-password"));
            await vault.DeleteAsync("streamerbot-password");
            Assert.Null(await vault.GetAsync("streamerbot-password"));
            await Assert.ThrowsAsync<ArgumentException>(() => vault.SetAsync("../escape", "value"));
            await File.WriteAllBytesAsync(Path.Combine(directory, "broken.dpapi"), [1, 2, 3]);
            await Assert.ThrowsAsync<CryptographicException>(() => vault.GetAsync("broken"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
