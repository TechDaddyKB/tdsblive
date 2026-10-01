using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class BotConfigurationTests
{
    public static IEnumerable<object[]> InvalidConfigurations()
    {
        var valid = new IntegrationConfiguration("127.0.0.1", 8080);
        foreach (var invalid in new[] {
            valid with { Host = "" }, valid with { Host = "invalid host" }, valid with { Port = 0 }, valid with { Port = 65536 },
            valid with { Endpoint = "" }, valid with { Endpoint = "relative" }, valid with { Endpoint = "//host" },
            valid with { Endpoint = "/?credential=synthetic" }, valid with { Endpoint = "/#fragment" }, valid with { Endpoint = "/\\path" },
            valid with { Endpoint = "/\n" }, valid with { Endpoint = "/" + new string('x', 256) },
            valid with { RequestTimeoutSeconds = 0 }, valid with { RequestTimeoutSeconds = 61 },
            valid with { ReconnectDelaySeconds = 0 }, valid with { ReconnectDelaySeconds = 61 },
            valid with { MaximumReconnectDelaySeconds = 1 }, valid with { MaximumReconnectDelaySeconds = 301 },
            valid with { AllowedActionIds = null! }, valid with { AllowedActionIds = new Guid[129] }, valid with { AllowedActionIds = [Guid.Empty] }
        }) yield return [invalid];
    }

    [Theory]
    [MemberData(nameof(InvalidConfigurations))]
    public void RejectsUnsafeOrUnboundedConnectionSettings(IntegrationConfiguration configuration) =>
        Assert.Throws<ArgumentException>(configuration.Validate);

    [Fact]
    public void AcceptsExplicitRemoteEndpointAndPreservesSafeDefaults()
    {
        var configuration = new IntegrationConfiguration("localhost", 8080) { Endpoint = "/bot", Enabled = true,
            AllowedActionIds = [Guid.NewGuid()], RequestTimeoutSeconds = 1, ReconnectDelaySeconds = 1, MaximumReconnectDelaySeconds = 300 };
        configuration.Validate();
        Assert.False(new ApplicationConfiguration().StreamerBot.Enabled);
        Assert.False(new ApplicationConfiguration().StreamerBot.ForwardLiveEvents);
        Assert.Equal(7680, new ApplicationConfiguration().SpeakerBot.Port);
    }
}
