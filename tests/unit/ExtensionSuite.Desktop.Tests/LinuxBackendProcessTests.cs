using ExtensionSuite.Desktop;
using ExtensionSuite.DesktopControl;
using System.Text.Json;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class LinuxBackendProcessTests
{
    [Theory]
    [InlineData("TDSBLIVE-DESKTOP 1\n", 1)]
    [InlineData("runner output\r\nTDSBLIVE-DESKTOP 65535\r\nother output\n", 65535)]
    [InlineData("TDSBLIVE-DESKTOP 12345\nTDSBLIVE-DESKTOP 54321\n", 12345)]
    public async Task OnlyTheFirstValidBoundedPortMarkerIsAccepted(string text, int expected)
    {
        var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reader = new StringReader(text);
        await LinuxBackendProcess.ReadOutputAsync(reader, ready, CancellationToken.None);
        Assert.Equal(expected, await ready.Task);
    }

    [Theory]
    [InlineData("TDSBLIVE-DESKTOP 0\n")]
    [InlineData("TDSBLIVE-DESKTOP 65536\n")]
    [InlineData("TDSBLIVE-DESKTOP -1\n")]
    [InlineData("TDSBLIVE-DESKTOP +1\n")]
    [InlineData("TDSBLIVE-DESKTOP 5 extra\n")]
    [InlineData("prefix TDSBLIVE-DESKTOP 12345\n")]
    [InlineData("TDSBLIVE-DESKTOP 12345")]
    public async Task MalformedIncompleteOrForeignMarkersDoNotEstablishControl(string text)
    {
        var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reader = new StringReader(text);
        await LinuxBackendProcess.ReadOutputAsync(reader, ready, CancellationToken.None);
        var error = await Assert.ThrowsAsync<IOException>(() => ready.Task);
        Assert.DoesNotContain(text, error.Message);
    }

    [Fact]
    public async Task LongPrivateOutputIsDiscardedAndDoesNotHideTheNextValidMarker()
    {
        var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reader = new StringReader(new string('x', 2 * 1024 * 1024) +
            "TDSBLIVE-DESKTOP 11111\n" + DesktopProtocol.ReadyPrefix + "23456\n");
        await LinuxBackendProcess.ReadOutputAsync(reader, ready, CancellationToken.None);
        Assert.Equal(23456, await ready.Task);
    }

    [Fact]
    public async Task ReaderFailuresDoNotExposeRunnerOutput()
    {
        var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reader = new BrokenReader();
        await LinuxBackendProcess.ReadOutputAsync(reader, ready, CancellationToken.None);
        var error = await Assert.ThrowsAsync<IOException>(() => ready.Task);
        Assert.DoesNotContain("private owned output", error.Message);
        Assert.Null(error.InnerException);
    }

    private sealed class BrokenReader : StringReader
    {
        public BrokenReader() : base("") { }
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("private owned output"));
    }

    [Fact]
    public async Task PrivateOutputAcceptsOnlyTheFirstValidBootstrapAndIgnoresOrdinaryPortMarkers()
    {
        var first = new DesktopBootstrap(23456, DesktopProtocol.NewSessionToken());
        var second = new DesktopBootstrap(34567, DesktopProtocol.NewSessionToken());
        using var reader = new StringReader(DesktopProtocol.ReadyPrefix + "12345\n" +
            DesktopProtocol.BootstrapPrefix + JsonSerializer.Serialize(first) + "\r\n" +
            DesktopProtocol.BootstrapPrefix + JsonSerializer.Serialize(second) + "\n");
        var ready = new TaskCompletionSource<DesktopBootstrap>(TaskCreationOptions.RunContinuationsAsynchronously);
        await LinuxBackendProcess.ReadBootstrapOutputAsync(reader, ready, CancellationToken.None);
        var actual = await ready.Task;
        Assert.Equal(first.Port, actual.Port);
        Assert.True(DesktopProtocol.Authenticate(first.SessionToken, actual.SessionToken));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(65536, false)]
    [InlineData(23456, true)]
    public async Task InvalidPrivateBootstrapNeverExposesItsContents(int port, bool invalidToken)
    {
        var token = invalidToken ? "owned-invalid-marker" : DesktopProtocol.NewSessionToken();
        var frame = DesktopProtocol.BootstrapPrefix + JsonSerializer.Serialize(new DesktopBootstrap(port, token));
        using var reader = new StringReader(frame + "\n");
        var ready = new TaskCompletionSource<DesktopBootstrap>(TaskCreationOptions.RunContinuationsAsynchronously);
        await LinuxBackendProcess.ReadBootstrapOutputAsync(reader, ready, CancellationToken.None);
        var error = await Assert.ThrowsAsync<IOException>(() => ready.Task);
        Assert.DoesNotContain(token, error.Message);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task OversizedAndMalformedPrivateFramesDoNotHideTheNextCompleteBootstrap()
    {
        var expected = new DesktopBootstrap(23456, DesktopProtocol.NewSessionToken());
        using var reader = new StringReader(DesktopProtocol.BootstrapPrefix + "{bad json}\n" +
            DesktopProtocol.BootstrapPrefix + new string('x', 2 * 1024 * 1024) + "\n" +
            DesktopProtocol.BootstrapPrefix + JsonSerializer.Serialize(expected) + "\n");
        var ready = new TaskCompletionSource<DesktopBootstrap>(TaskCreationOptions.RunContinuationsAsynchronously);
        await LinuxBackendProcess.ReadBootstrapOutputAsync(reader, ready, CancellationToken.None);
        Assert.True(DesktopProtocol.Authenticate(expected.SessionToken, (await ready.Task).SessionToken));
    }
}
