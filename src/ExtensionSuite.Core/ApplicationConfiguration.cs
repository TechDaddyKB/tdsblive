using System.Net;

namespace ExtensionSuite.Core;

public sealed record ApplicationConfiguration
{
    public ServerConfiguration Server { get; init; } = new();
    public IntegrationConfiguration StreamerBot { get; init; } = new("127.0.0.1", 8080);
    public IntegrationConfiguration SpeakerBot { get; init; } = new("127.0.0.1", 7680);
    public RumbleConfiguration Rumble { get; init; } = new();
    public int LogRetentionDays { get; init; } = 14;
    public string MinimumLogLevel { get; init; } = "Information";
    public bool RetainRawEvents { get; init; } = true;
    public string DisplayName { get; init; } = "TDSBLive";

    public void Validate()
    {
        if (Server is null || StreamerBot is null || SpeakerBot is null || Rumble is null)
            throw new ArgumentException("Configuration sections cannot be null.");
        Server.Validate();
        StreamerBot.Validate();
        SpeakerBot.Validate();
        _ = new RumblePollInterval(Rumble.PollIntervalSeconds, Rumble.AdvancedSlowerPolling);
        Rumble.Validate();
        if (LogRetentionDays is < 1 or > 365 || string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Length > 128)
            throw new ArgumentException("Invalid retention or display name.");
        if (!new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None" }.Contains(MinimumLogLevel))
            throw new ArgumentException("Invalid minimum log level.");
    }
}

public sealed record ServerConfiguration
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 17474;
    public bool EnableLan { get; init; }
    public string[] AllowedHosts { get; init; } = [];
    public void Validate()
    {
        if (!IPAddress.TryParse(Host, out var address) || Port is < 1 or > 65535)
            throw new ArgumentException("Server requires a numeric IP address and valid port.");
        if (!IPAddress.IsLoopback(address) && !EnableLan)
            throw new ArgumentException("Non-loopback binding requires explicit LAN opt-in.");
        if (AllowedHosts is null || AllowedHosts.Any(host => Uri.CheckHostName(host) == UriHostNameType.Unknown || host == "*"))
            throw new ArgumentException("Allowed hosts must be explicit hostnames or IP addresses.");
    }
}

public sealed record IntegrationConfiguration(string Host, int Port)
{
    public bool Enabled { get; init; }
    public string Endpoint { get; init; } = "/";
    public int RequestTimeoutSeconds { get; init; } = 10;
    public int ReconnectDelaySeconds { get; init; } = 2;
    public int MaximumReconnectDelaySeconds { get; init; } = 30;
    public Guid[] AllowedActionIds { get; init; } = [];
    public bool ForwardLiveEvents { get; init; }
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host) || Uri.CheckHostName(Host) == UriHostNameType.Unknown || Port is < 1 or > 65535)
            throw new ArgumentException("Integration requires a valid host and port.");
        if (string.IsNullOrWhiteSpace(Endpoint) || !Endpoint.StartsWith('/') || Endpoint.StartsWith("//", StringComparison.Ordinal) ||
            Endpoint.Length > 256 || Endpoint.IndexOfAny(['?', '#', '\\']) >= 0 || Endpoint.Any(char.IsControl))
            throw new ArgumentException("Integration endpoint requires a bounded absolute path without credentials or query parameters.");
        if (RequestTimeoutSeconds is < 1 or > 60 || ReconnectDelaySeconds is < 1 or > 60 ||
            MaximumReconnectDelaySeconds < ReconnectDelaySeconds || MaximumReconnectDelaySeconds > 300)
            throw new ArgumentException("Integration timeouts and reconnect bounds are invalid.");
        if (AllowedActionIds is null || AllowedActionIds.Length > 128 || AllowedActionIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Selected action identifiers require a bounded list of non-empty GUIDs.");
    }
}

public sealed record RumbleConfiguration
{
    public bool Enabled { get; init; }
    public int PollIntervalSeconds { get; init; } = 7;
    public bool AdvancedSlowerPolling { get; init; }
    public bool ForwardTriggers { get; init; }
    public int RequestTimeoutSeconds { get; init; } = 15;
    public int OfflineConfirmationPolls { get; init; } = 2;
    public void Validate()
    {
        if (RequestTimeoutSeconds is < 1 or > 60 || OfflineConfirmationPolls is < 2 or > 10 || PollIntervalSeconds > 86400)
            throw new ArgumentException("Rumble timeout, debounce or advanced polling bounds are invalid.");
    }
}
