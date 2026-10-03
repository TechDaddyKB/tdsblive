using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Host;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class RumbleShapeInspectorTests
{
    [Fact]
    public void ReportsKnownUnknownNullOnlyTypeDriftAndChangesWithoutValues()
    {
        var inspector = new RumbleShapeInspector();
        var first = inspector.Inspect(new JsonObject { ["channel_id"] = null });
        Assert.Empty(first["unknownFields"]!.AsArray());
        var changed = inspector.Inspect(new JsonObject { ["channel_id"] = "private", ["future"] = new JsonArray(true, 2, "private") });
        Assert.Contains("$[\"channel_id\"]:string", changed["unknownFields"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Contains("$[\"channel_id\"]:null", changed["removedFields"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.DoesNotContain("private", changed.ToJsonString());
        var same = inspector.Inspect(new JsonObject { ["channel_id"] = "different", ["future"] = new JsonArray(true, 2, "different") });
        Assert.Empty(same["addedFields"]!.AsArray()); Assert.Empty(same["removedFields"]!.AsArray());
    }

    [Fact]
    public void SnapshotInspectorRespectsRawRetentionAndSanitizedExportStripsDynamicKeys()
    {
        var store = new EventInspectorStore(new ApplicationConfiguration());
        store.Add(new(null, "rumble.snapshot"), new JsonObject { ["private-name"] = "private-message" });
        var entry = Assert.Single(store.Read());
        Assert.NotNull(entry.Payload?["shapeInspection"]);
        Assert.DoesNotContain("private-name", DiagnosticSanitizer.Shape(entry.Payload)!.ToJsonString());
        var hidden = new EventInspectorStore(new ApplicationConfiguration { RetainRawEvents = false });
        hidden.Add(new(null, "rumble.snapshot"), new JsonObject { ["private-name"] = "private-message" });
        Assert.Null(Assert.Single(hidden.Read()).Payload);
    }
}
