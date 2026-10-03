using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;
public sealed class AdvancedWidgetContractTests
{
    [Fact]
    public void InvalidProgressAndEventFiltersFailBeforePersistence()
    {
        var p = new ProgressSettings();
        ProgressSettings[] invalid = [p with { Source = "script" }, p with { Value = double.NaN }, p with { Value = -1 }, p with { Value = 1e13 },
            p with { Target = 0 }, p with { Target = double.PositiveInfinity }, p with { Target = 1e13 }, p with { Label = null! }, p with { Label = new('a', 257) },
            p with { Orientation = "diagonal" }, p with { FillColor = "url(private)" }, p with { TrackColor = "#12345g" }];
        foreach (var value in invalid) Assert.Throws<ArgumentException>(value.Validate);
        (p with { Source = "ledger-usd", Target = .01, Value = 1e12, Orientation = "vertical" }).Validate();
        var e = new EventListSettings();
        EventListSettings[] bad = [e with { EventTypes = null! }, e with { EventTypes = [] }, e with { EventTypes = [""] }, e with { EventTypes = [new('t', 129)] },
            e with { EventTypes = Enumerable.Repeat("t", 65).ToArray() }, e with { Platforms = null! }, e with { Platforms = [" "] }, e with { Platforms = [new('p', 65)] },
            e with { Platforms = Enumerable.Repeat("p", 17).ToArray() }, e with { IgnoredUsers = null! }, e with { IgnoredUsers = [""] }, e with { IgnoredUsers = [new('u', 129)] },
            e with { IgnoredUsers = Enumerable.Repeat("u", 129).ToArray() }, e with { Count = 0 }, e with { Count = 101 }, e with { DurationMs = 99 }, e with { DurationMs = 86400001 },
            e with { Template = null! }, e with { Template = new('t', 4097) }, e with { Font = "url(private)" }];
        foreach (var value in bad) Assert.Throws<ArgumentException>(value.Validate);
        var item = new CanonicalEvent { Source = "owned-test", NativeType = "Follow", DedupeKey = "owned", Type = "community.follow", Platform = "rumble", User = new(DisplayName: "Viewer") };
        Assert.True(e.Accepts(item)); Assert.False((e with { IgnoredUsers = ["viewer"] }).Accepts(item)); Assert.False((e with { Platforms = ["twitch"] }).Accepts(item));
        Assert.True((e with { EventTypes = ["*"] }).Accepts(item with { Type = "integration.custom" }));
        Assert.False(e.Accepts(item with { Type = "integration.custom" }));
        foreach (var kind in new[] { "event-list", "goal-bar", "progress-bar" }) (new OverlayWidget { Kind = kind, GroupId = Guid.NewGuid().ToString() }).Validate();
        Assert.Throws<ArgumentException>(() => new OverlayWidget { GroupId = "unsafe" }.Validate());
        Assert.Throws<ArgumentException>(() => new OverlayWidget { Progress = null! }.Validate());
        Assert.Throws<ArgumentException>(() => new OverlayWidget { EventList = null! }.Validate());
    }
}
