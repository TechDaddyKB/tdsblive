using ExtensionSuite.Core;
using Xunit;

namespace ExtensionSuite.Core.Tests;

public sealed class LedgerPeriodTests
{
    [Theory]
    [InlineData(2026, 3, 8, 23)]
    [InlineData(2026, 11, 1, 25)]
    public void LocalTodayUsesDstDayLength(int year, int month, int day, int hours)
    {
        var now = new DateTimeOffset(year, month, day, 18, 0, 0, TimeSpan.Zero);
        var range = LedgerPeriods.Resolve("today", "America/Chicago", now);
        Assert.Equal(TimeSpan.FromHours(hours), range.EndExclusive - range.StartInclusive);
        Assert.Equal(day, TimeZoneInfo.ConvertTime(range.StartInclusive!.Value, TimeZoneInfo.FindSystemTimeZoneById("America/Chicago")).Day);
    }

    [Fact]
    public void MondayWeekMonthAndYearUseConfiguredLocalDate()
    {
        var utc = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.Zero); // Still December in Chicago.
        var week = LedgerPeriods.Resolve("week", "America/Chicago", utc);
        Assert.Equal(new DateTimeOffset(2025, 12, 29, 6, 0, 0, TimeSpan.Zero), week.StartInclusive);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 6, 0, 0, TimeSpan.Zero), week.EndExclusive);
        Assert.Equal(new DateTimeOffset(2025, 12, 1, 6, 0, 0, TimeSpan.Zero), LedgerPeriods.Resolve("month", "America/Chicago", utc).StartInclusive);
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 6, 0, 0, TimeSpan.Zero), LedgerPeriods.Resolve("year", "America/Chicago", utc).StartInclusive);
        var monday = LedgerPeriods.Resolve("week", "UTC", new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), monday.StartInclusive);
    }

    [Fact]
    public void CustomDatesAreHalfOpenAndStreamStartIsExplicit()
    {
        var now = new DateTimeOffset(2026, 3, 10, 18, 0, 0, TimeSpan.Zero);
        var range = LedgerPeriods.Resolve("custom", "America/Chicago", now, new(2026, 3, 7), new(2026, 3, 10));
        Assert.Equal(TimeSpan.FromHours(71), range.EndExclusive - range.StartInclusive);
        Assert.Equal(new DateTimeOffset(2026, 3, 7, 6, 0, 0, TimeSpan.Zero), range.StartInclusive);
        var stream = LedgerPeriods.Resolve("current-stream", "America/Chicago", now, currentStreamStartedAt: now.AddHours(-2));
        Assert.Equal(now.AddHours(-2), stream.StartInclusive); Assert.Equal(now.AddTicks(1), stream.EndExclusive);
        Assert.Equal(new LedgerPeriodRange(null, null), LedgerPeriods.Resolve("all-time", "UTC", now));
        Assert.Throws<ArgumentException>(() => LedgerPeriods.Resolve("current-stream", "UTC", now));
        Assert.Throws<ArgumentException>(() => LedgerPeriods.Resolve("current-stream", "UTC", now, currentStreamStartedAt: now.AddDays(1)));
        Assert.Throws<ArgumentException>(() => LedgerPeriods.Resolve("custom", "UTC", now, new(2026, 1, 2), new(2026, 1, 1)));
        Assert.Throws<ArgumentException>(() => LedgerPeriods.Resolve("unknown", "UTC", now));
        Assert.Throws<TimeZoneNotFoundException>(() => LedgerPeriods.Resolve("today", "invalid-zone", now));
        Assert.Throws<ArgumentException>(() => new LedgerPeriodRange(now, now).Validate());
        Assert.Throws<ArgumentException>(() => new LedgerPeriodRange(now.ToOffset(TimeSpan.FromHours(1)), null).Validate());
    }
}
