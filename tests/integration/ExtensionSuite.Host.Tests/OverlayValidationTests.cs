using System.Text;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using ExtensionSuite.Overlays;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class OverlayValidationTests
{
    public static TheoryData<string, byte[]> Signatures
    {
        get
        {
            var png = new byte[33]; new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0); Encoding.ASCII.GetBytes("IHDR").CopyTo(png, 12);
            return new() {
                { "image/png", png }, { "image/jpeg", [255, 216, 255, 0, 0, 0, 255, 217] }, { "image/gif", Encoding.ASCII.GetBytes("GIF89a0000") },
                { "image/webp", Encoding.ASCII.GetBytes("RIFF0000WEBP0000") }, { "audio/wav", Encoding.ASCII.GetBytes("RIFF0000WAVE0000") },
                { "audio/mpeg", Encoding.ASCII.GetBytes("ID300000") }, { "audio/ogg", Encoding.ASCII.GetBytes("OggS0000") },
                { "video/webm", [0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0] }, { "video/mp4", Encoding.ASCII.GetBytes("0000ftypisom0000") },
                { "font/woff", Encoding.ASCII.GetBytes("wOFF0000") }, { "font/woff2", Encoding.ASCII.GetBytes("wOF20000") },
                { "font/ttf", [0, 1, 0, 0, 0, 0, 0, 0] }, { "font/otf", Encoding.ASCII.GetBytes("OTTO0000") } };
        }
    }
    [Theory]
    [MemberData(nameof(Signatures))]
    public void MimeSignaturesAreCheckedBeforeAdmission(string mime, byte[] bytes)
    {
        Assert.False(AssetValidation.Validate(bytes, mime).Sanitized);
        Assert.Throws<ArgumentException>(() => AssetValidation.Validate(new byte[bytes.Length], mime));
        Assert.Throws<ArgumentException>(() => AssetValidation.Validate(bytes, "text/html"));
    }
    [Fact]
    public void SvgAndSettingsRejectEntityScriptsComplexityAndUnboundedInputs()
    {
        Assert.Throws<ArgumentException>(() => AssetValidation.Validate(new byte[7], "image/png"));
        Assert.Throws<ArgumentException>(() => AssetValidation.Validate(new byte[AssetValidation.MaximumBytes + 1], "image/png"));
        Assert.Throws<ArgumentException>(() => AssetValidation.Validate(Encoding.UTF8.GetBytes("<svg>broken"), "image/svg+xml"));
        Assert.Throws<ArgumentException>(() => AssetValidation.Validate(Encoding.UTF8.GetBytes("<html>unsupported</html>"), "image/svg+xml"));
        var nested = "<svg xmlns=\"http://www.w3.org/2000/svg\">" + string.Concat(Enumerable.Repeat("<g>", 40)) + string.Concat(Enumerable.Repeat("</g>", 40)) + "</svg>";
        Assert.Throws<ArgumentException>(() => AssetValidation.Validate(Encoding.UTF8.GetBytes(nested), "image/svg+xml"));
        foreach (var settings in new[] { new ChatSettings { Platforms = ["unknown"] }, new() { Platforms = ["twitch", "twitch"] }, new() { Font = "Arial;display:none" }, new() { BackgroundOpacity = double.NaN },
            new() { MaximumMessages = 0 }, new() { IgnoredUsers = [""] }, new() { PlatformColors = [] }, new() { AnimationIn = "script" } }) Assert.Throws<ArgumentException>(settings.Validate);
        Assert.Throws<ArgumentException>(() => new OverlayDefinition { Id = "../escape" }.Validate());
        Assert.Throws<ArgumentException>(() => new OverlayDefinition { Version = int.MaxValue }.Validate());
        var settingsForBots = new ChatSettings { HideBotMessages = true, IgnoredUsers = ["ignored"], IgnoredPrefixes = ["!"] };
        CanonicalEvent message = new() { Source = "test", Platform = "rumble", Type = "chat.message", NativeType = "Synthetic", DedupeKey = "synthetic", OccurredAt = DateTimeOffset.UtcNow, Message = new("hello") };
        Assert.True(settingsForBots.Accepts(message)); Assert.False(settingsForBots.Accepts(message with { User = new(Login: "NightBot") }));
        Assert.False(settingsForBots.Accepts(message with { User = new(IsBot: true) })); Assert.False(settingsForBots.Accepts(message with { User = new(Login: "Ignored") }));
        Assert.False(settingsForBots.Accepts(message with { Message = new("!command") })); Assert.False(settingsForBots.Accepts(message with { Type = "support.rant" }));
        Assert.Equal("audio/wav", AssetValidation.NormalizeMime("audio/x-wav; charset=utf-8")); Assert.Equal("audio/mpeg", AssetValidation.NormalizeMime("audio/mp3"));
        Assert.Equal("image/jpeg", AssetValidation.NormalizeMime("image/jpg")); Assert.Equal("font/woff", AssetValidation.NormalizeMime("application/font-woff"));
        Assert.Equal("font/ttf", AssetValidation.NormalizeMime("application/x-font-ttf")); Assert.Equal("font/otf", AssetValidation.NormalizeMime("application/vnd.ms-opentype"));
    }
    [Fact]
    public async Task AssetsAndTokensSurviveReopenWithoutPlaintextTokensAndFontsRequireDeclaredPermission()
    {
        using var app = new FoundationHostFactory(); using var client = app.CreateClient();
        var assets = app.Services.GetRequiredService<AssetStore>();
        var bytes = Encoding.ASCII.GetBytes("wOF20000");
        await Assert.ThrowsAsync<ArgumentException>(() => assets.UploadAsync(new MemoryStream(bytes), "font.woff2", "font/woff2", null, default));
        var font = await assets.UploadAsync(new MemoryStream(bytes), "font.woff2", "font/woff2", "OFL-1.1", default); Assert.NotNull(font);
        Assert.Equal(font, await assets.UploadAsync(new MemoryStream(bytes), "duplicate.woff2", "font/woff2", "OFL-1.1", default));
        Assert.Equal(font, await new AssetStore(app.Services.GetRequiredService<ApplicationPaths>(), app.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<FoundationDbContext>>(), TimeProvider.System).GetAsync(font.Id, default));
        await Assert.ThrowsAsync<ArgumentException>(() => assets.UploadAsync(new MemoryStream(new byte[AssetValidation.MaximumBytes + 1]), "large.gif", "image/gif", null, default));
        Assert.Throws<ArgumentException>(() => assets.PathFor("../../private"));
        var store = app.Services.GetRequiredService<OverlayStore>();
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateTokenAsync("combined-chat", 0, default));
        var token = await store.CreateTokenAsync("combined-chat", 1, default);
        Assert.True(await store.AuthorizeAsync(token.Token, "combined-chat", default)); Assert.False(await store.AuthorizeAsync("invalid", "combined-chat", default));
        Assert.False(await store.AuthorizeAsync(token.Token, "wrong-overlay", default));
        var expired = new OverlayStore(app.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<FoundationDbContext>>(), new(), new FutureClock());
        Assert.False(await expired.AuthorizeAsync(token.Token, "combined-chat", default));
        await using var db = app.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<FoundationDbContext>>().CreateDbContext();
        Assert.DoesNotContain(token.Token, (await db.OverlayTokens.FindAsync(token.Info.Id))!.Hash);
    }
    private sealed class FutureClock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddDays(2); }
}
