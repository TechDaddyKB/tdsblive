using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class GifRedactionTests
{
    private const string PublicUrl = "https://media4.giphy.com/media/synthetic/giphy.gif?cid=synthetic-public-attribution&ep=v1_gifs_trending&rid=giphy.gif&ct=g";
    [Theory]
    [InlineData("type", "url")]
    [InlineData("kind", "imageUrl")]
    public void ExplicitGifMetadataPreservesTheFullPublicCdnUrl(string type, string field)
    {
        var data = new JsonObject { [type] = "gif", [field] = PublicUrl };
        Assert.Equal(PublicUrl, CredentialRedactor.Json(data)![field]!.GetValue<string>());
        Assert.Equal(CredentialRedactor.Replacement, CredentialRedactor.Text(PublicUrl));
    }

    [Theory]
    [InlineData("https://media4.giphy.com/media/synthetic/giphy.gif?api_key=synthetic-private")]
    [InlineData("https://media4.giphy.com/media/synthetic/giphy.gif?%74oken=synthetic-private")]
    [InlineData("https://user:synthetic-password@media4.giphy.com/media/synthetic/giphy.gif?cid=public")]
    [InlineData("https://media4.giphy.com/api/login?cid=public")]
    [InlineData("https://untrusted.giphy.com/media/synthetic/giphy.gif?cid=public")]
    [InlineData("https://media4.giphy.com.attacker.invalid/media/synthetic/giphy.gif?cid=public")]
    [InlineData("https://example.invalid/media/synthetic/giphy.gif?cid=public")]
    public void PublicGifExceptionNeverPermitsCredentialsUnknownQueriesOrOtherEndpoints(string url)
    {
        var data = new JsonObject { ["kind"] = "gif", ["imageUrl"] = url };
        Assert.Equal(CredentialRedactor.Replacement, CredentialRedactor.Json(data)!["imageUrl"]!.GetValue<string>());
    }

    [Fact]
    public void KnownSecretsAndUnstructuredUrlsRemainRedacted()
    {
        var typed = new JsonObject { ["kind"] = "gif", ["imageUrl"] = PublicUrl };
        Assert.Equal(CredentialRedactor.Replacement, CredentialRedactor.Json(typed, ["synthetic-public-attribution"])!["imageUrl"]!.GetValue<string>());
        var ordinary = new JsonObject { ["imageUrl"] = PublicUrl };
        Assert.Equal(CredentialRedactor.Replacement, CredentialRedactor.Json(ordinary)!["imageUrl"]!.GetValue<string>());
    }
}
