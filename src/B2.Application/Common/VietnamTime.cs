namespace B2.Application.Common;

public static class VietnamTime
{
    public static DateOnly BusinessDateFromUtc(DateTimeOffset utcNow)
    {
        var tz = ResolveVietnamTimeZone();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, tz);
        return DateOnly.FromDateTime(local);
    }

    /// <summary>
    /// Returns the UTC half-open interval [start, end) that covers exactly one Vietnam business calendar day
    /// (F-12: vehicles entered on that calendar day in Vietnam, by vehicle entry timestamp).
    /// </summary>
    public static (DateTimeOffset StartInclusiveUtc, DateTimeOffset EndExclusiveUtc) UtcRangeForBusinessDate(DateOnly businessDate)
    {
        var tz = ResolveVietnamTimeZone();
        var startLocal = new DateTime(businessDate.Year, businessDate.Month, businessDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal.AddDays(1), tz);
        return (new DateTimeOffset(startUtc, TimeSpan.Zero), new DateTimeOffset(endUtc, TimeSpan.Zero));
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
