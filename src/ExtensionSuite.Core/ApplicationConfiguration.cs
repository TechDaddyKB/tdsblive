using System.Net;

namespace ExtensionSuite.Core;

public sealed record ApplicationConfiguration
{
    public ServerConfiguration Server { get; init; } = new();
    public IntegrationConfiguration StreamerBot { get; init; } = new("127.0.0.1", 8080);
    public IntegrationConfiguration SpeakerBot { get; init; } = new("127.0.0.1", 7680);
    public RumbleConfiguration Rumble { get; init; } = new();
    public int LogRetentionDays { get; init; } = 14;
    public bool RetainRawEvents { get; init; } = true;
    public string DisplayName { get; init; } = "TDSBLive";

    public void Validate()
    {
        Server.Validate();
        StreamerBot.Validate();
        SpeakerBot.Validate();
        _ = new RumblePollInterval(Rumble.PollIntervalSeconds, Rumble.AdvancedSlowerPolling);
        if (LogRetentionDays is < 1 or > 365 || string.IsNullOrWhiteSpace(DisplayName))
            throw new ArgumentException("Invalid retention or display name.");
    }
}

public sealed record ServerConfiguration
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 17474;
    public bool EnableLan { get; init; }
    public void Validate()
    {
        if (!IPAddress.TryParse(Host, out var address) || Port is < 1 or > 65535)
            throw new ArgumentException("Server requires a numeric IP address and valid port.");
        if (!IPAddress.IsLoopback(address) && !EnableLan)
            throw new ArgumentException("Non-loopback binding requires explicit LAN opt-in.");
    }
}

public sealed record IntegrationConfiguration(string Host, int Port)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host) || Uri.CheckHostName(Host) == UriHostNameType.Unknown || Port is < 1 or > 65535)
            throw new ArgumentException("Integration requires a valid host and port.");
    }
}

public sealed record RumbleConfiguration
{
    public bool Enabled { get; init; }
    public int PollIntervalSeconds { get; init; } = 7;
    public bool AdvancedSlowerPolling { get; init; }
}
