using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class AutomationTemporaryPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExtendAndRestartDoNotToggleAnAlreadyEnabledEffect()
    {
        var active = AutomationTemporaryPolicy.Apply(null, Start, 60, "extend").State;
        var extend = AutomationTemporaryPolicy.Apply(active, Start.AddSeconds(20), 60, "extend");
        Assert.Equal("reschedule", extend.Operation);
        Assert.Equal(Start.AddSeconds(120), extend.State!.ExpiresAt);
        var restart = AutomationTemporaryPolicy.Apply(active, Start.AddSeconds(20), 60, "restart");
        Assert.Equal(Start.AddSeconds(80), restart.State!.ExpiresAt);
        Assert.Equal("ignored", AutomationTemporaryPolicy.Apply(active, Start.AddSeconds(20), 60, "ignore").Operation);
    }

    [Fact]
    public void QueueIsBoundedAndNextEnableFollowsConfirmedRevert()
    {
        var active = new TemporaryActionState(Start.AddSeconds(60));
        var queued = AutomationTemporaryPolicy.Apply(active, Start, 60, "queue", 1);
        Assert.Equal(1, queued.State!.QueuedCount);
        Assert.Equal("queue-full", AutomationTemporaryPolicy.Apply(queued.State, Start, 60, "queue", 1).Operation);
        Assert.Equal("revert-first", AutomationTemporaryPolicy.Apply(queued.State, Start.AddSeconds(60), 60, "extend").Operation);
        var next = AutomationTemporaryPolicy.AfterConfirmedRevert(queued.State, Start.AddSeconds(60), 60);
        Assert.Equal("enable", next.Operation);
        Assert.Equal(Start.AddSeconds(120), next.State!.ExpiresAt);
        Assert.Equal(0, next.State.QueuedCount);
        Assert.Equal("idle", AutomationTemporaryPolicy.AfterConfirmedRevert(next.State, Start.AddSeconds(120), 60).Operation);
    }
}
