using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExtensionSuite.Core;

public sealed record AutomationSpeechSettings
{
    public string Template { get; init; } = "{from} donated {amount} {currency}. {message}";
    public string Voice { get; init; } = "";
    public int MaximumCharacters { get; init; } = 300;
    public bool SpeakUsername { get; init; } = true;
    public bool SpeakAmount { get; init; } = true;
    public bool SpeakMessage { get; init; } = true;
    public bool StripUrls { get; init; } = true;
    public int MaximumRepeatedCharacters { get; init; } = 3;
    public int MaximumPunctuationRun { get; init; } = 3;
    public bool IgnoreAnonymousMessage { get; init; } = true;
    public bool SpeakerBadWordFilter { get; init; } = true;
    public string[] BlockedWords { get; init; } = [];
    public string[] AllowedLanguages { get; init; } = [];
    public bool ManualModeration { get; init; }

    public void Validate()
    {
        if (Template is null || Template.Length > 2000 || Voice is null || Voice.Length > 128 ||
            MaximumCharacters is < 1 or > 2000 || MaximumRepeatedCharacters is < 1 or > 20 ||
            MaximumPunctuationRun is < 1 or > 20 || BlockedWords is null || BlockedWords.Length > 200 ||
            BlockedWords.Any(word => string.IsNullOrWhiteSpace(word) || word.Length > 64) ||
            AllowedLanguages is null || AllowedLanguages.Length > 32 ||
            AllowedLanguages.Any(language => string.IsNullOrWhiteSpace(language) || language.Length > 35))
            throw new ArgumentException("Invalid speech settings.");
    }
}

public sealed record AutomationSpeechResult(string State, string Text);

public static partial class AutomationSpeech
{
    // Language and anonymity must come from verified event metadata or moderator input.
    // Never infer a permitted language from the absence of metadata.
    public static AutomationSpeechResult Prepare(AutomationSpeechSettings settings, CanonicalEvent item,
        bool anonymous, string? language = null)
    {
        settings.Validate();
        var languageReview = settings.AllowedLanguages.Length > 0 &&
            !settings.AllowedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase);
        var native = item.Support?.NativeMoney;
        var amount = native is null ? "" : (native.AmountMinor / Scale(native.MinorUnitDigits)).ToString("F" + native.MinorUnitDigits, CultureInfo.InvariantCulture);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["from"] = settings.SpeakUsername ? (anonymous ? "Anonymous" : item.User?.DisplayName ?? "Supporter") : "",
            ["amount"] = settings.SpeakAmount ? amount : "",
            ["currency"] = settings.SpeakAmount ? native?.Currency ?? "" : "",
            ["message"] = settings.SpeakMessage && !(anonymous && settings.IgnoreAnonymousMessage) &&
                (!item.Platform.Equals("kofi", StringComparison.OrdinalIgnoreCase) || item.Automation?.MessagePublic == true)
                ? item.Message?.Text ?? "" : ""
        };
        // A single pass prevents viewer text containing template tokens from expanding again.
        var text = Tokens().Replace(settings.Template, match => fields.GetValueOrDefault(match.Groups[1].Value, ""));
        if (settings.StripUrls) text = Urls().Replace(text, " ");
        foreach (var word in settings.BlockedWords)
            text = text.Replace(word, "[filtered]", StringComparison.OrdinalIgnoreCase);
        var result = new StringBuilder();
        var elements = StringInfo.GetTextElementEnumerator(text);
        string? previous = null;
        var repeat = 0;
        var punctuation = 0;
        var count = 0;
        while (elements.MoveNext() && count < settings.MaximumCharacters)
        {
            var element = elements.GetTextElement();
            if (element.Any(char.IsControl)) continue;
            repeat = element == previous ? repeat + 1 : 1;
            previous = element;
            punctuation = element.All(char.IsPunctuation) ? punctuation + 1 : 0;
            if (repeat > settings.MaximumRepeatedCharacters || punctuation > settings.MaximumPunctuationRun) continue;
            result.Append(element); count++;
        }
        text = Whitespace().Replace(result.ToString(), " ").Trim();
        return new(text.Length == 0 ? "empty" : languageReview ? "language-review" : settings.ManualModeration ? "moderation-pending" : "ready", text);
    }

    private static decimal Scale(int digits) => digits switch { 0 => 1, 1 => 10, 2 => 100, 3 => 1000, 4 => 10000, _ => throw new ArgumentException("Invalid native money scale.") };
    [GeneratedRegex(@"\{(from|amount|currency|message)\}", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Tokens();
    [GeneratedRegex(@"(?:https?://|www\.)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex Urls();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Whitespace();
}
