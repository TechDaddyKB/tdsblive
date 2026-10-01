using System.Text.Json.Nodes;
using ExtensionSuite.StreamerBot;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class SupportPayloadTests
{
    [Fact]
    public void GiftChannelRequiresExplicitIdentityAndConnectedDiscovery()
    {
        var payload = new JsonObject { ["total"] = 2, ["id"] = "batch" };
        var unknown = SupportPayloadNormalizer.Normalize("twitch", "GiftBomb", payload, false)!;
        Assert.Null(unknown.GiftScopeKey);
        Assert.Equal("gift_channel_unavailable", unknown.GatedReason);
        var discovery = JsonNode.Parse("{\"connected\":[\"twitch\",\"kick\"],\"platforms\":{\"twitch\":{\"broadcastUserId\":123},\"kick\":{\"broadcasterUserId\":\"456\"},\"youtube\":{\"broadcastUserId\":\"old\"}}}")!.AsObject();
        Assert.Equal("123", StreamerBotConnection.BroadcasterIdentifier(discovery, "twitch"));
        Assert.Equal("456", StreamerBotConnection.BroadcasterIdentifier(discovery, "kick"));
        Assert.Null(StreamerBotConnection.BroadcasterIdentifier(discovery, "youtube"));
        payload["broadcaster"] = new JsonObject { ["id"] = 123 };
        var known = SupportPayloadNormalizer.Normalize("twitch", "GiftBomb", payload, false)!;
        Assert.Equal("channel:123", known.GiftScopeKey);
        Assert.Null(known.GatedReason);
    }
    [Theory]
    [InlineData("USD", 1250000, 125, 2)]
    [InlineData("JPY", 125000000, 125, 0)]
    [InlineData("KWD", 1250000, 1250, 3)]
    public void PaidMessagesRetainExplicitCurrencyScale(string currency, long micros, long minor, int digits)
    {
        var data = new JsonObject { ["microAmount"] = micros, ["currencyCode"] = currency };
        var support = SupportPayloadNormalizer.Normalize("youtube", "SuperChat", data, false)!;
        Assert.Null(support.GatedReason);
        Assert.Equal(minor, support.NativeMoney!.AmountMinor);
        Assert.Equal(digits, support.NativeMoney.MinorUnitDigits);
    }

    [Theory]
    [InlineData("{\"microAmount\":1250000,\"decimalAmount\":2,\"currencyCode\":\"USD\"}", "conflicting_native_amounts")]
    [InlineData("{\"microAmount\":1250000,\"currencyCode\":\"JPY\"}", "native_precision_unrepresentable")]
    [InlineData("{\"microAmount\":1250000,\"currencyCode\":\"XYZ\"}", "currency_scale_unverified")]
    [InlineData("{\"amount\":\"$12.50\",\"currencyCode\":\"USD\"}", "native_amount_unavailable")]
    public void UncertainOrConflictingAmountsCannotBecomeExactMoney(string json, string reason)
    {
        var support = SupportPayloadNormalizer.Normalize("youtube", "SuperSticker", JsonNode.Parse(json)!.AsObject(), false)!;
        Assert.Null(support.NativeMoney);
        Assert.Equal(reason, support.GatedReason);
    }

    [Fact]
    public void KofiRequiresDocumentedForwardingAndPreservesReportedFacts()
    {
        var data = JsonNode.Parse("{\"amount\":\"12.50\",\"currency\":\"USD\"}")!.AsObject();
        var direct = SupportPayloadNormalizer.Normalize("kofi", "Donation", data, false)!;
        Assert.Null(direct.NativeMoney);
        Assert.Equal("12.50", direct.ReportedAmountMajor);
        Assert.Equal("kofi_websocket_schema_unverified", direct.GatedReason);
        var forwarded = SupportPayloadNormalizer.Normalize("kofi", "Donation", data, true)!;
        Assert.Equal(1250, forwarded.NativeMoney!.AmountMinor);
    }

    [Fact]
    public void GiftOriginAndRecipientsArePreservedForCorrelation()
    {
        var data = JsonNode.Parse("{\"fromCommunitySubGift\":true,\"communityGiftId\":\"batch-1\",\"recipient\":{\"id\":\"recipient-1\"},\"subTier\":\"1000\"}")!.AsObject();
        var gift = SupportPayloadNormalizer.Normalize("twitch", "GiftSub", data, false)!;
        Assert.Equal("individual", gift.GiftRole);
        Assert.Equal("batch-1", gift.GiftCorrelationKey);
        Assert.Equal(new[] { "id:recipient-1" }, gift.GiftRecipientKeys);
        data.Remove("fromCommunitySubGift");
        Assert.Equal("gift_origin_unavailable", SupportPayloadNormalizer.Normalize("twitch", "GiftSub", data, false)!.GatedReason);
        data["fromCommunitySubGift"] = false;
        Assert.Equal("gift_origin_conflict", SupportPayloadNormalizer.Normalize("twitch", "GiftSub", data, false)!.GatedReason);
    }

    [Fact]
    public void MissingBitsAreUnknownRatherThanAnInventedQuantity()
    {
        var missing = SupportPayloadNormalizer.Normalize("twitch", "Cheer", new JsonObject(), false)!;
        Assert.Equal(0, missing.Quantity);
        Assert.Equal("quantity_unavailable", missing.GatedReason);
        var actual = SupportPayloadNormalizer.Normalize("twitch", "Cheer", new JsonObject { ["bits"] = 25 }, false)!;
        Assert.Equal(25, actual.Quantity);
    }

    [Fact]
    public void GiftPeriodsRequireExplicitTimezoneAndPrimeRulesRemainDistinct()
    {
        var data = JsonNode.Parse("{\"subscribedAt\":\"2026-01-05T12:00:00\",\"expiresAt\":\"2026-02-05T12:00:00Z\",\"recipient\":{\"id\":\"one\"}}")!.AsObject();
        var unknown = SupportPayloadNormalizer.Normalize("kick", "GiftSubscription", data, false)!;
        Assert.Null(unknown.GiftPeriodStart);
        data["subscribedAt"] = "2026-01-05T14:00:00+02:00";
        var dated = SupportPayloadNormalizer.Normalize("kick", "GiftSubscription", data, false)!;
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero), dated.GiftPeriodStart);
        var prime = SupportPayloadNormalizer.Normalize("twitch", "Sub", new JsonObject { ["isPrime"] = true, ["subTier"] = "1000" }, false)!;
        Assert.Equal("prime", prime.Tier);
        var gifted = SupportPayloadNormalizer.Normalize("twitch", "ReSub", new JsonObject { ["isGift"] = true }, false)!;
        Assert.Equal("recipient", gifted.GiftRole);
    }
}
