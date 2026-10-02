namespace ExtensionSuite.Core;

public sealed record LedgerPeriodRange(DateTimeOffset? StartInclusive, DateTimeOffset? EndExclusive)
{
    public void Validate()
    {
        if (StartInclusive?.Offset != null && StartInclusive.Value.Offset != TimeSpan.Zero ||
            EndExclusive?.Offset != null && EndExclusive.Value.Offset != TimeSpan.Zero ||
            StartInclusive is { } start && EndExclusive is { } end && start >= end)
            throw new ArgumentException("Ledger period requires ordered UTC boundaries.");
    }
}

public static class LedgerPeriods
{
    public static LedgerPeriodRange Resolve(string period, string timeZoneId, DateTimeOffset now,
        DateOnly? customStart = null, DateOnly? customEndExclusive = null, DateTimeOffset? currentStreamStartedAt = null)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        if (period == "all-time") return new(null, null);
        if (period == "current-stream")
        {
            if (currentStreamStartedAt is not { } start || start > now)
                throw new ArgumentException("Current-stream aggregation requires a persisted stream start no later than now.");
            return new(start.ToUniversalTime(), now.ToUniversalTime().AddTicks(1));
        }
        DateOnly startDate, endDate;
        switch (period)
        {
            case "today": startDate = today; endDate = today.AddDays(1); break;
            case "week":
                startDate = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); endDate = startDate.AddDays(7); break;
            case "month": startDate = new(today.Year, today.Month, 1); endDate = startDate.AddMonths(1); break;
            case "year": startDate = new(today.Year, 1, 1); endDate = startDate.AddYears(1); break;
            case "custom":
                if (customStart is not { } from || customEndExclusive is not { } to || from >= to)
                    throw new ArgumentException("Custom periods require ordered local dates with an exclusive end date.");
                startDate = from; endDate = to; break;
            default: throw new ArgumentException("Unknown ledger period.");
        }
        var result = new LedgerPeriodRange(LocalBoundary(startDate, zone), LocalBoundary(endDate, zone));
        result.Validate(); return result;
    }

    private static DateTimeOffset LocalBoundary(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // A midnight transition can skip an hour or an entire civil date.
        for (var minutes = 0; zone.IsInvalidTime(local); minutes++)
        {
            if (minutes >= 2880) throw new ArgumentException("No valid boundary within two days of the requested date.");
            local = local.AddMinutes(1);
        }
        // Earlier UTC occurrence includes both occurrences of an ambiguous midnight.
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
