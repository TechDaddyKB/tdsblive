using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class AutomationConditionTests
{
    private static CanonicalEvent Event(long quantity, NativeMoney? money = null) => new()
    {
        Source = "streamerbot", Platform = "twitch", Type = "cheer", NativeType = "Cheer",
        DedupeKey = "owned-condition", OccurredAt = DateTimeOffset.UtcNow,
        Support = new("bits", quantity, money)
    };

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(499, true)]
    [InlineData(500, false)]
    public void RangeIsHalfOpen(long amount, bool expected) =>
        Assert.Equal(expected, new AutomationCondition("twitch", "cheer", Operator: "range", Value: 100, UpperExclusive: 500).Matches(Event(amount)));

    [Theory]
    [InlineData("exact", 100, true)]
    [InlineData("exact", 101, false)]
    [InlineData("minimum", long.MaxValue, true)]
    [InlineData("multiple", 0, false)]
    [InlineData("multiple", 200, true)]
    [InlineData("multiple", 201, false)]
    public void QuantityBoundaries(string operation, long amount, bool expected) =>
        Assert.Equal(expected, new AutomationCondition("twitch", "cheer", Operator: operation, Value: 100).Matches(Event(amount)));

    [Fact]
    public void NativeMinimumRequiresMatchingCurrencyAndScale()
    {
        var rule = new AutomationCondition("twitch", "cheer", "native-money", Value: 1000, Currency: "USD", MinorUnitDigits: 2);
        Assert.True(rule.Matches(Event(1, new(1000, "USD", 2))));
        Assert.False(rule.Matches(Event(1, new(999, "USD", 2))));
        Assert.False(rule.Matches(Event(1, new(1000, "EUR", 2))));
        Assert.False(rule.Matches(Event(1)));
    }

    [Fact]
    public void GatedRecipientAndUnrelatedEventsNeverMatch()
    {
        var rule = new AutomationCondition("twitch", "cheer");
        var item = Event(100);
        Assert.False(rule.Matches(item with { Platform = "rumble" }));
        Assert.False(rule.Matches(item with { Type = "chat" }));
        Assert.False(rule.Matches(item with { Support = item.Support! with { GatedReason = "unverified" } }));
        Assert.False(rule.Matches(item with { Support = item.Support! with { GiftRole = "recipient" } }));
    }

    [Fact]
    public void InvalidConditionsFailBeforeExecution()
    {
        Assert.Throws<ArgumentException>(() => new AutomationCondition("twitch", "cheer", Operator: "multiple", Value: 0).Validate());
        Assert.Throws<ArgumentException>(() => new AutomationCondition("twitch", "cheer", Operator: "range", Value: 10, UpperExclusive: 10).Validate());
        Assert.Throws<ArgumentException>(() => new AutomationCondition("kofi", "donation", "native-money").Validate());
    }
}
