using ExtensionSuite.Desktop;
using ExtensionSuite.DesktopControl;
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
}
