using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.StreamerBot;
using ExtensionSuite.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class ChatMediaTests
{
    [Fact]
    public async Task MediaSurvivesNormalizationPersistenceAndPublicHistoryWithoutRawPayload()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var envelope = JsonNode.Parse("""{"event":{"source":"Twitch","type":"ChatMessage"},"data":{"messageId":"synthetic-media","text":"Hello Wave","parts":[{"type":"text","text":"Hello "},{"type":"emote","text":"Wave","source":"7TV","imageUrl":"https://example.invalid/wave.gif"}]}}""")!.AsObject();
        var item = new StreamerBotEventNormalizer(new()).Normalize(envelope, DateTimeOffset.UtcNow).Event!;
        Assert.True(await app.Services.GetRequiredService<EventStore>().AcceptAsync(item, "media-contract"));
        var history = (await client.GetFromJsonAsync<CanonicalEvent[]>("/api/overlays/combined-chat/chat", EventStore.JsonOptions))!;
        var stored = Assert.Single(history); Assert.Null(stored.Raw); Assert.Equal(item.Message!.Parts, stored.Message!.Parts);
        Assert.Equal("7TV", stored.Message.Parts![1].Source);
    }

    [Theory]
    [InlineData("twitch")]
    [InlineData("7TV")]
    [InlineData("BTTV")]
    [InlineData("FFZ")]
    public void StructuredProviderPartsRetainTextArtworkAndZeroWidth(string source)
    {
        var data = JsonNode.Parse("""{"parts":[{"type":"text","text":"Hello "},{"type":"emote","text":"Wave","imageUrl":"https://example.invalid/animated.gif","zeroWidth":true}]}""")!.AsObject();
        data["parts"]![1]!["source"] = source;
        var result = ChatMediaNormalizer.Normalize("Hello Wave", data, "twitch")!;
        Assert.Equal("Hello Wave", result.Text); Assert.Equal("Hello ", result.Parts![0].Text);
        Assert.Equal(new EventMessagePart("emote", "Wave", "https://example.invalid/animated.gif", source, true), result.Parts[1]);
    }

    [Fact]
    public void InclusiveUtf16SpansPreserveEmojiWhitespaceAndRejectOverlapOrMismatchedNames()
    {
        var data = JsonNode.Parse("""{"emotes":[{"StartIndex":3,"EndIndex":7,"Name":"Kappa","ImageUrl":"https://example.invalid/kappa.png","Type":"twitch"},{"StartIndex":3,"EndIndex":7,"Name":"Kappa","ImageUrl":"https://example.invalid/duplicate.png"},{"StartIndex":0,"EndIndex":0,"Name":"wrong","ImageUrl":"https://example.invalid/a.png"},{"StartIndex":99,"EndIndex":100,"ImageUrl":"https://example.invalid/a.png"}]}""")!.AsObject();
        var result = ChatMediaNormalizer.Normalize("😀 Kappa !", data, "twitch")!;
        Assert.Equal("😀 Kappa !", string.Concat(result.Parts!.Select(p => p.Text)));
        Assert.Equal("https://example.invalid/kappa.png", Assert.Single(result.Parts!, p => p.Kind == "emote").ImageUrl);
    }

    [Fact]
    public void GifOnlyMessageAndAttachmentHaveBoundedExplicitMediaWithoutFetchingLinks()
    {
        var data = JsonNode.Parse("""{"parts":[{"type":"gif","gifId":"synthetic","url":"https://example.invalid/reaction.gif"}]}""")!.AsObject();
        var result = ChatMediaNormalizer.Normalize(null, data, "twitch")!;
        Assert.Equal("", result.Text); Assert.Equal("gif", Assert.Single(result.Parts!).Kind);
        var attachment = ChatMediaNormalizer.Normalize("Reaction", data, "twitch")!;
        Assert.Equal("text", attachment.Parts![0].Kind); Assert.Equal("gif", attachment.Parts[1].Kind);
        Assert.Null(ChatMediaNormalizer.Normalize("https://example.invalid/reaction.gif", new(), "twitch")!.Parts);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml,unsafe")]
    [InlineData("https://user:password@example.invalid/a.gif")]
    [InlineData("file:///tmp/a.gif")]
    public void UnsafeImageUrlsFallBackToOriginalText(string url)
    {
        var data = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["type"] = "emote", ["text"] = "Wave", ["imageUrl"] = url }) };
        var result = ChatMediaNormalizer.Normalize("Wave", data, "twitch")!;
        Assert.Equal("Wave", string.Concat(result.Parts!.Select(p => p.Text))); Assert.All(result.Parts!, p => Assert.Null(p.ImageUrl));
    }

    [Fact]
    public void OversizedOrInconsistentPartsCannotReplaceTheOriginalMessage()
    {
        var data = new JsonObject { ["parts"] = new JsonArray(Enumerable.Range(0, 257).Select(_ => (JsonNode)new JsonObject { ["type"] = "text", ["text"] = "x" }).ToArray()) };
        Assert.Null(ChatMediaNormalizer.Normalize("Original", data, "twitch")!.Parts);
        data["parts"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "Changed" });
        Assert.Equal(new EventMessage("Original"), ChatMediaNormalizer.Normalize("Original", data, "twitch"));
    }
}
