using System.Text.Json;
using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class AutomationRuleTests
{
    [Fact]
    public void RoundTripPreservesOrderedActionsAndTemporaryReversion()
    {
        var rule = new AutomationRule
        {
            Condition = new("twitch", "cheer", Operator: "exact", Value: 100),
            Actions = [new() { Kind = "sound", OverlayId = "owned-overlay", SoundAssetIds = ["owned-a", "owned-b"], Volume = .8m },
                new() { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), RevertActionId = Guid.NewGuid(), DurationSeconds = 60, StackPolicy = "queue" }]
        };
        rule.Validate();
        var restored = JsonSerializer.Deserialize<AutomationRule>(JsonSerializer.Serialize(rule))!;
        restored.Validate();
        Assert.False(restored.Enabled);
        Assert.Equal(rule.Actions.Select(action => action.Id), restored.Actions.Select(action => action.Id));
        Assert.Equal(rule.Actions[1].RevertActionId, restored.Actions[1].RevertActionId);
        Assert.Equal("queue", restored.Actions[1].StackPolicy);
        Assert.Equal(.8m, restored.Actions[0].Volume);
    }

    [Fact]
    public void IncompleteOrMixedActionConfigurationCannotBeSaved()
    {
        Assert.Throws<ArgumentException>(() => new AutomationAction { Kind = "speech", Speech = new() }.Validate());
        Assert.Throws<ArgumentException>(() => new AutomationAction { Kind = "sound", SoundAssetIds = ["audio"] }.Validate());
        Assert.Throws<ArgumentException>(() => new AutomationAction { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), SoundAssetIds = ["audio"] }.Validate());
        Assert.Throws<ArgumentException>(() => new AutomationAction { Kind = "streamerbot", StreamerBotActionId = Guid.NewGuid(), RevertActionId = Guid.NewGuid() }.Validate());
    }

    [Fact]
    public void DuplicateActionIdsCannotCollideInExecutionTracking()
    {
        var action = new AutomationAction { Speech = new() { Voice = "owned-voice" } };
        Assert.Throws<ArgumentException>(() => new AutomationRule { Condition = new("kofi", "donation"), Actions = [action, action] }.Validate());
    }
}
