using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ExtensionSuite.DesktopControl;

// This protocol is deliberately separate from the public HTTP API. Bootstrap
// credentials exist only in memory and a redirected parent/child process pipe.
public sealed record DesktopBootstrap(int Port, string SessionToken)
{
    public override string ToString() => $"DesktopBootstrap {{ Port = {Port}, SessionToken = [redacted] }}";
}
public sealed record DesktopRequest(string SessionToken, string Command)
{
    public override string ToString() => "DesktopRequest { SessionToken = [redacted] }";
}
public sealed record DesktopReply(string State, string? EditorUrl = null, bool Accepted = false, int OpenRequests = 0);

public static class DesktopProtocol
{
    public const string ReadyPrefix = "TDSBLIVE-DESKTOP ";
    // Private redirected stdout only. Never write this frame through logging.
    public const string BootstrapPrefix = "TDSBLIVE-DESKTOP-BOOTSTRAP ";
    public const int MaximumFrameBytes = 4096;
    public static string NewSessionToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static bool ValidSessionToken(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    public static bool Authenticate(string expected, string? supplied) => ValidSessionToken(supplied) &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied!));

    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new byte[MaximumFrameBytes];
        var single = new byte[1];
        var count = 0;
        while (count < bytes.Length)
        {
            if (await stream.ReadAsync(single, cancellationToken) == 0) throw new EndOfStreamException();
            if (single[0] == '\n')
                return JsonSerializer.Deserialize<T>(bytes.AsSpan(0, count)) ?? throw new JsonException();
            bytes[count++] = single[0];
        }
        throw new InvalidDataException("Desktop control message is too large.");
    }

    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length >= MaximumFrameBytes) throw new InvalidDataException("Desktop control message is too large.");
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<DesktopReply> SendAsync(DesktopBootstrap bootstrap, string command, CancellationToken cancellationToken)
    {
        if (bootstrap.Port is < 1 or > 65535 || !ValidSessionToken(bootstrap.SessionToken))
            throw new ArgumentException("Invalid desktop bootstrap.");
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, bootstrap.Port, cancellationToken);
        await using var stream = client.GetStream();
        await WriteAsync(stream, new DesktopRequest(bootstrap.SessionToken, command), cancellationToken);
        return await ReadAsync<DesktopReply>(stream, cancellationToken);
    }
}
