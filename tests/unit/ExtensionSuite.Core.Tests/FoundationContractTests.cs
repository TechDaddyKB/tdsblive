using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class FoundationContractTests
{
    [Fact]
    public void DefaultsAreLocalHttpAndValidated()
    {
        var config = new ApplicationConfiguration();
        config.Validate();
        Assert.Equal("127.0.0.1", config.Server.Host);
        Assert.Equal(17474, config.Server.Port);
        Assert.False(config.Server.EnableLan);
        Assert.Equal(14, config.LogRetentionDays);
    }

    [Theory]
    [InlineData("0.0.0.0", 17474, false)]
    [InlineData("localhost", 17474, false)]
    [InlineData("127.0.0.1", 0, false)]
    [InlineData("127.0.0.1", 65536, false)]
    public void UnsafeBindingsAreRejected(string host, int port, bool lan) =>
        Assert.Throws<ArgumentException>(() => new ServerConfiguration { Host = host, Port = port, EnableLan = lan }.Validate());

    [Fact]
    public void ExplicitLanAndIpv6LoopbackAreSupported()
    {
        new ServerConfiguration { Host = "0.0.0.0", EnableLan = true }.Validate();
        new ServerConfiguration { Host = "::1" }.Validate();
    }

    [Fact]
    public void RedactionIsRecursiveAndDoesNotMutateInput()
    {
        var input = JsonNode.Parse("""{"password":"synthetic-value","nested":[{"stream_key":"synthetic-value","unknown":42}],"link":"https://example.invalid/api?key=synthetic-value"}""");
        var clean = CredentialRedactor.Json(input)!;
        Assert.DoesNotContain("synthetic-value", clean.ToJsonString());
        Assert.Contains("synthetic-value", input!.ToJsonString());
        Assert.Equal(42, clean["nested"]![0]!["unknown"]!.GetValue<int>());
    }

    [Fact]
    public void KnownValuesAndRumbleCredentialPathsAreRemoved()
    {
        Assert.Equal("hello [REDACTED]", CredentialRedactor.Text("hello synthetic", ["synthetic"]));
        Assert.Equal(CredentialRedactor.Replacement, CredentialRedactor.Text("https://rumble.com/api/synthetic"));
        Assert.Equal("https://example.invalid/avatar.png", CredentialRedactor.Text("https://example.invalid/avatar.png"));
        Assert.Null(CredentialRedactor.Json(null));
    }

    [Fact]
    public void CanonicalEventsRequireUtcAndUuidV7()
    {
        var item = new CanonicalEvent { Source = "internal", Platform = "system", Type = "test", NativeType = "test",
            DedupeKey = "test", OccurredAt = DateTimeOffset.UtcNow };
        item.Validate();
        Assert.Throws<ArgumentException>(() => (item with { Id = Guid.NewGuid() }).Validate());
        Assert.Throws<ArgumentException>(() => (item with { OccurredAt = item.OccurredAt.ToOffset(TimeSpan.FromHours(1)) }).Validate());
        Assert.Throws<ArgumentException>(() => (item with { Source = "" }).Validate());
        Assert.Throws<ArgumentException>(() => (item with { Provenance = (EventProvenance)99 }).Validate());
    }
}
