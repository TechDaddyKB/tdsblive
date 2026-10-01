using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class WidgetContractTests
{
    [Fact]
    public void InvalidQueueFiltersTimingCapacityAndMediaPoliciesAreRejected()
    {
        var a = new AlertSettings();
        AlertSettings[] invalid = [a with { EventTypes = null! }, a with { EventTypes = [] }, a with { EventTypes = Enumerable.Repeat("type", 65).ToArray() },
            a with { EventTypes = [""] }, a with { EventTypes = [new('a', 129)] }, a with { Platforms = null! },
            a with { Platforms = Enumerable.Repeat("platform", 17).ToArray() }, a with { Platforms = [" "] }, a with { Platforms = [new('p', 65)] },
            a with { Template = null! }, a with { Template = new('t', 4097) }, a with { Group = " " }, a with { Group = new('g', 65) },
            a with { Priority = -101 }, a with { Priority = 101 }, a with { DurationMs = 99 }, a with { DurationMs = 300001 },
            a with { CooldownMs = -1 }, a with { CooldownMs = 3600001 }, a with { Concurrency = 0 }, a with { Concurrency = 9 },
            a with { MaximumQueueLength = 0 }, a with { MaximumQueueLength = 201 }, a with { InterruptPolicy = "always" },
            a with { OverflowPolicy = "unbounded" }, a with { Animation = "script" }, a with { MediaAssetId = "../private" }, a with { SoundAssetId = "https://remote.invalid/sound" }];
        foreach (var value in invalid) Assert.Throws<ArgumentException>(value.Validate);
        (a with { Priority = -100, DurationMs = 100, CooldownMs = 0, Concurrency = 1, MaximumQueueLength = 1, Animation = "none", Template = "" }).Validate();
        (a with { Priority = 100, DurationMs = 300000, CooldownMs = 3600000, Concurrency = 8, MaximumQueueLength = 200,
            InterruptPolicy = "higher-priority", OverflowPolicy = "drop-newest", Animation = "slide", MediaAssetId = new('a', 64), SoundAssetId = new('b', 64), EventTypes = ["*"], Platforms = ["custom"] }).Validate();
    }
    [Fact]
    public void CanvasGeometryStylesAndAssetIdentifiersRejectUnsafeOrNonFiniteValues()
    {
        var w = new OverlayWidget();
        OverlayWidget[] invalid = [w with { Id = "../other" }, w with { Name = " " }, w with { Name = new('n', 129) }, w with { Kind = "iframe" },
            w with { X = double.NaN }, w with { X = -7681 }, w with { X = 7681 }, w with { Y = double.PositiveInfinity }, w with { Y = -7681 },
            w with { Width = 0 }, w with { Width = 7681 }, w with { Height = 0 }, w with { Height = 7681 },
            w with { Rotation = -361 }, w with { Rotation = 361 }, w with { Rotation = double.NaN }, w with { Text = null! }, w with { Text = new('t', 4097) },
            w with { Color = null! }, w with { Color = "red" }, w with { Color = "#12345g" }, w with { Color = "1234567" },
            w with { FontSize = 7 }, w with { FontSize = 201 }, w with { Volume = double.NaN }, w with { Volume = -.1 }, w with { Volume = 1.1 },
            w with { AssetId = "../outside" }, w with { Chat = null! }, w with { Alert = null! }];
        foreach (var value in invalid) Assert.Throws<ArgumentException>(value.Validate);
        (w with { X = -7680, Y = 7680, Width = 1, Height = 7680, Rotation = -360, Color = "#aBcDeF", FontSize = 200, Volume = 0 }).Validate();
        foreach (var kind in new[] { "text", "image", "video", "audio", "chat", "alert" }) (w with { Kind = kind, Volume = 1, Rotation = 360 }).Validate();
    }
    [Fact]
    public void OverlayBoundsRejectNullDuplicateOrExcessiveLayersAndAcceptConfiguredRevisionLimits()
    {
        var d = new OverlayDefinition { CanvasEnabled = true }; var w = new OverlayWidget();
        Assert.Throws<ArgumentException>(() => (d with { Widgets = null! }).Validate());
        Assert.Throws<ArgumentException>(() => (d with { Widgets = [null!] }).Validate());
        Assert.Throws<ArgumentException>(() => (d with { Widgets = Enumerable.Range(0, 101).Select(_ => new OverlayWidget()).ToArray() }).Validate());
        Assert.Throws<ArgumentException>(() => (d with { Widgets = [w, w] }).Validate());
        Assert.Throws<ArgumentException>(() => (d with { RevisionLimit = 0 }).Validate());
        Assert.Throws<ArgumentException>(() => (d with { RevisionLimit = 201 }).Validate());
        (d with { Widgets = Enumerable.Range(0, 100).Select(_ => new OverlayWidget()).ToArray(), RevisionLimit = 200 }).Validate();
        (d with { RevisionLimit = 1 }).Validate();
    }
}
