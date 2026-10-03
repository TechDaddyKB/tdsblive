using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class DiagnosticSanitizerTests
{
    [Fact]
    public void RemovesAllValuesAndUnknownNamesWithoutChangingScalarTypes()
    {
        var source = JsonNode.Parse("""{"user":{"displayName":"Private person","personal-key":"private-value"},"amount":125,"events":[true,null],"unknownField1":"other"}""");
        var result = DiagnosticSanitizer.Shape(source)!;
        var json = result.ToJsonString();
        Assert.DoesNotContain("Private", json); Assert.DoesNotContain("personal-key", json);
        Assert.DoesNotContain("private-value", json); Assert.DoesNotContain("125", json);
        Assert.Equal("sample", result["user"]!["displayName"]!.GetValue<string>());
        Assert.Equal(0, result["amount"]!.GetValue<int>());
        Assert.False(result["events"]![0]!.GetValue<bool>());
        Assert.Null(result["events"]![1]);
        Assert.Equal(125, source!["amount"]!.GetValue<int>());
    }

    [Fact]
    public void BoundsArraysAndDepthAndDoesNotCollideWithExistingUnknownNames()
    {
        var source = new JsonObject { ["private"] = 1, ["unknownField1"] = 2,
            ["events"] = new JsonArray(Enumerable.Range(0, 200).Select(i => (JsonNode?)JsonValue.Create(i)).ToArray()) };
        var result = DiagnosticSanitizer.Shape(source)!.AsObject();
        Assert.Equal(3, result.Count); Assert.Equal(100, result["events"]!.AsArray().Count);
        Assert.Equal("[depth limit]", DiagnosticSanitizer.Shape(source, 33)!.GetValue<string>());
    }
}
