using ExtensionSuite.Host;
using ExtensionSuite.StreamerBot;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationBotAdapterTests
{
    [Theory]
    [InlineData("acknowledged", false, "dispatched")]
    [InlineData("acknowledged", true, "uncertain")]
    [InlineData("uncertain", false, "uncertain")]
    [InlineData("disconnected", false, "failed")]
    [InlineData("missingAction", false, "rejected")]
    [InlineData("actionNotSelected", false, "rejected")]
    public void AcknowledgmentIsNotPlaybackAndAmbiguityTakesPrecedence(string state, bool uncertain, string expected)
    {
        var outcome = AutomationBotAdapter.Outcome(new(Guid.NewGuid(), "owned", state, uncertain, DateTimeOffset.UtcNow));
        Assert.Equal(expected, outcome.State);
        Assert.NotNull(outcome.Detail);
        Assert.NotEqual("completed", outcome.State);
    }
}
