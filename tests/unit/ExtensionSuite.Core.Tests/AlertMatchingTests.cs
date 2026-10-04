using ExtensionSuite.Core;
using Xunit;
namespace ExtensionSuite.Core.Tests;

public sealed class AlertMatchingTests
{
    [Fact]
    public void ConditionsSerializeExactIntegersAndStillReadLegacyNumbers()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var alert = new AlertCondition(Value: long.MaxValue);
        var automation = new AutomationCondition("kofi", "support.donation", Value: long.MaxValue);
        var alertJson = System.Text.Json.JsonSerializer.Serialize(alert, options);
        var automationJson = System.Text.Json.JsonSerializer.Serialize(automation, options);
        Assert.Contains("\"value\":\"9223372036854775807\"", alertJson);
        Assert.Contains("\"value\":\"9223372036854775807\"", automationJson);
        Assert.Equal(alert, System.Text.Json.JsonSerializer.Deserialize<AlertCondition>(alertJson, options));
        Assert.Equal(automation, System.Text.Json.JsonSerializer.Deserialize<AutomationCondition>(automationJson, options));
        Assert.Equal(500, System.Text.Json.JsonSerializer.Deserialize<AlertCondition>("{\"value\":500}", options)!.Value);
        Assert.Equal(500, System.Text.Json.JsonSerializer.Deserialize<AutomationCondition>("{\"platform\":\"kofi\",\"eventType\":\"support.donation\",\"value\":500}", options)!.Value);
    }
    private sealed record Vector(string Operator, string Value, string? Upper, string? Amount, string? Currency, int? Digits, bool Gated, string? GiftRole, bool Expected);
    [Fact]
    public void SharedDraftAndBackendMatchingSpecification()
    {
        var vectors = System.Text.Json.JsonSerializer.Deserialize<Vector[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "alert-matching.json")), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        foreach (var v in vectors) {
            var condition = new AlertCondition("native-money", v.Operator, long.Parse(v.Value), v.Upper is null ? null : long.Parse(v.Upper), "USD", 2);
            var item = Event() with { Support = v.Amount is null ? null : Event().Support! with { NativeMoney = new(long.Parse(v.Amount), v.Currency!, v.Digits!.Value), GatedReason = v.Gated ? "unverified" : null, GiftRole = v.GiftRole ?? "none" } };
            Assert.Equal(v.Expected, AlertMatching.Matches(Design(condition), item));
        }
    }
    private static CanonicalEvent Event(long amount = 500) => new() { Source = "owned", Platform = "kofi", Type = "support.donation", NativeType = "Kofi.Donation", DedupeKey = "owned", OccurredAt = DateTimeOffset.UtcNow, Support = new("donation", 1, new(amount, "USD", 2)) };
    private static OverlayWidget Design(AlertCondition? condition = null) => new() { Kind = "alert", Alert = new() { EventTypes = ["support.donation"], Platforms = ["kofi"], Condition = condition } };
    [Theory]
    [InlineData(499, false)][InlineData(500, true)][InlineData(999, true)][InlineData(1000, false)]
    public void NativeTierIsHalfOpen(long amount, bool expected) => Assert.Equal(expected, AlertMatching.Matches(Design(new("native-money", "range", 500, 1000, "USD", 2)), Event(amount)));
    [Fact]
    public void MissingGatedRecipientWrongCurrencyAndScaleDoNotMatch()
    {
        var w = Design(new("native-money", "minimum", 500, Currency: "USD", MinorUnitDigits: 2)); var e = Event();
        foreach (var support in new SupportDetails?[] { null, e.Support! with { GatedReason = "unverified" }, e.Support! with { GiftRole = "recipient" }, e.Support! with { NativeMoney = new(500, "EUR", 2) }, e.Support! with { NativeMoney = new(500, "USD", 0) } }) Assert.False(AlertMatching.Matches(w, e with { Support = support }));
    }
    [Fact]
    public void OrderedWinnerIsSelectedBeforeAnyQueueAdmissionAndLegacyDesignsRemainIndependent()
    {
        var low = Design(new("native-money", "minimum", 500, Currency: "USD", MinorUnitDigits: 2)); var high = Design(new("native-money", "minimum", 1000, Currency: "USD", MinorUnitDigits: 2)); var independent = Design();
        var set = new AlertSet(Guid.NewGuid().ToString(), "Donation tiers", [high.Id, low.Id]);
        var scene = new OverlayDefinition { Widgets = [low, high, independent], AlertSets = [set] }; scene.Validate();
        Assert.Equal([high.Id, independent.Id], AlertMatching.Select(scene, Event(1000)));
        Assert.Equal([low.Id, independent.Id], AlertMatching.Select(scene, Event(999)));
        Assert.Equal([low.Id, high.Id, independent.Id], AlertMatching.Select(scene with { AlertSets = [set with { Selection = "all" }] }, Event(1000)));
        Assert.Equal([low.Id, high.Id, independent.Id], AlertMatching.Select(scene with { AlertSets = [] }, Event(1000)));
    }
    [Fact]
    public void IncomingIdentityAndHiddenDesignsAreRespected()
    {
        var w = Design() with { Alert = new() { EventTypes = ["integration.custom"], Platforms = ["general"], NativeType = "General.Custom", CustomTriggerKey = "owned.sparkle" } };
        var e = Event() with { Type = "integration.custom", Platform = "general", NativeType = "General.Custom", AlertTriggerKey = "owned.sparkle" };
        Assert.True(AlertMatching.Matches(w, e)); Assert.False(AlertMatching.Matches(w, e with { AlertTriggerKey = null })); Assert.False(AlertMatching.Matches(w, e with { NativeType = "General.Other" })); Assert.False(AlertMatching.Matches(w with { Hidden = true }, e));
    }
    [Fact]
    public void InvalidSetReferencesDuplicateMembershipAndInvalidConditionsAreRejected()
    {
        var w = Design(); var set = new AlertSet(Guid.NewGuid().ToString(), "Tier", [w.Id]); var scene = new OverlayDefinition { Widgets = [w], AlertSets = [set] };
        scene.Validate(); Assert.True(AlertMatching.RequiresV2(scene)); Assert.False(AlertMatching.RequiresV2(scene with { AlertSets = [] }));
        foreach (var sets in new[] { new[] { set with { WidgetIds = ["missing"] } }, new[] { set, set with { Id = Guid.NewGuid().ToString() } }, new[] { set with { WidgetIds = [w.Id, w.Id] } }, new[] { set with { Selection = "random" } }, new[] { set with { WidgetIds = [] } } }) Assert.Throws<ArgumentException>(() => (scene with { AlertSets = sets }).Validate());
        Assert.Throws<ArgumentException>(() => Design(new(Operator: "multiple", Value: 0)).Validate());
    }
}
