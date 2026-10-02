using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class AutomationSpeechTests
{
    private static CanonicalEvent Donation(string message) => new()
    {
        Source = "streamerbot", Platform = "kofi", Type = "donation", NativeType = "Donation",
        DedupeKey = "owned-speech", OccurredAt = DateTimeOffset.UtcNow,
        User = new(DisplayName: "Owned viewer"), Message = new(message),
        Automation = new(Anonymous: false, MessagePublic: true),
        Support = new("donation", 1, new(1234, "USD", 2))
    };

    [Fact]
    public void PrivateOrUnknownVisibilityNeverSpeaksKofiMessageEvenWhenAnonymousMessagesAreAllowed()
    {
        var settings = new AutomationSpeechSettings { Template = "{message}", IgnoreAnonymousMessage = false };
        foreach (bool? visibility in new bool?[] { null, false })
        {
            var item = Donation("private message") with { Automation = new(false, visibility) };
            Assert.Equal(new AutomationSpeechResult("empty", ""), AutomationSpeech.Prepare(settings, item, false));
        }
        Assert.Equal("private message", AutomationSpeech.Prepare(settings, Donation("private message"), false).Text);
    }

    [Fact]
    public void SanitizesExpandedFieldsWithoutRecursivelyExpandingViewerTokens()
    {
        var result = AutomationSpeech.Prepare(new() { BlockedWords = ["badword"] },
            Donation("AAAAAAA!!!!! https://example.test/private www.example.test BADWORD {from}"), false);
        Assert.Equal("ready", result.State);
        Assert.Contains("Owned viewer donated 12.34 USD", result.Text);
        Assert.Contains("AAA!!!", result.Text);
        Assert.Contains("[filtered] {from}", result.Text);
        Assert.DoesNotContain("example.test", result.Text);
    }

    [Fact]
    public void AnonymousMessageCanBeSuppressedIndependentlyOfAmount()
    {
        var result = AutomationSpeech.Prepare(new(), Donation("private viewer message"), true);
        Assert.Equal("Anonymous donated 12.34 USD.", result.Text);
    }

    [Fact]
    public void CharacterLimitDoesNotSplitEmojiOrCombiningCharacters()
    {
        var result = AutomationSpeech.Prepare(new() { Template = "{message}", MaximumCharacters = 2 }, Donation("😀e\u0301tail"), false);
        Assert.Equal("😀e\u0301", result.Text);
    }

    [Fact]
    public void LanguageRestrictionsFailClosedAndModerationNeverSaysReady()
    {
        var settings = new AutomationSpeechSettings { AllowedLanguages = ["en"], ManualModeration = true };
        Assert.Equal("language-review", AutomationSpeech.Prepare(settings, Donation("hello"), false).State);
        Assert.Equal("language-review", AutomationSpeech.Prepare(settings, Donation("hello"), false, "fr").State);
        Assert.Equal("moderation-pending", AutomationSpeech.Prepare(settings, Donation("hello"), false, "EN").State);
    }

    [Fact]
    public void DisabledFieldsAndEmptyTextAreHonest()
    {
        var settings = new AutomationSpeechSettings { Template = "{from}{amount}{currency}{message}", SpeakUsername = false, SpeakAmount = false, SpeakMessage = false };
        Assert.Equal(new AutomationSpeechResult("empty", ""), AutomationSpeech.Prepare(settings, Donation("hello"), false));
    }
}
