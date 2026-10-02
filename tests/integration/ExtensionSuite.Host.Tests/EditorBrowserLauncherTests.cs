using System.ComponentModel;
using System.Diagnostics;
using ExtensionSuite.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class EditorBrowserLauncherTests
{
    [Theory]
    [InlineData("127.0.0.1", "http://127.0.0.1:18474/editor")]
    [InlineData("0.0.0.0", "http://127.0.0.1:18474/editor")]
    [InlineData("::", "http://[::1]:18474/editor")]
    [InlineData("::1", "http://[::1]:18474/editor")]
    [InlineData("127.0.0.2", "http://127.0.0.2:18474/editor")]
    public void OpensConfiguredHttpAddressWithoutLaunchingTheTestersBrowser(string host, string expected)
    {
        ProcessStartInfo? request = null;
        var launcher = new EditorBrowserLauncher(NullLogger<EditorBrowserLauncher>.Instance, info => { request = info; return null; });
        Assert.True(launcher.Open(new() { Host = host, Port = 18474, EnableLan = true }));
        Assert.NotNull(request);
        Assert.Equal(expected, request.FileName);
        Assert.True(request.UseShellExecute);
        Assert.Empty(request.Arguments);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingBrowserIsReportedWithoutPreventingHostOperation(bool nativeFailure)
    {
        var launcher = new EditorBrowserLauncher(NullLogger<EditorBrowserLauncher>.Instance,
            _ =>
            {
                if (nativeFailure) throw new Win32Exception("Owned failure");
                throw new InvalidOperationException("Owned failure");
            });
        Assert.False(launcher.Open(new()));
    }

    [Fact]
    public void InvalidConfigurationCannotLaunchAnArbitraryAddress()
    {
        var calls = 0;
        var launcher = new EditorBrowserLauncher(NullLogger<EditorBrowserLauncher>.Instance, _ => { calls++; return null; });
        Assert.Throws<ArgumentException>(() => launcher.Open(new() { Host = "https://example.test/path" }));
        Assert.Equal(0, calls);
    }
}
