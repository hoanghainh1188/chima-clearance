namespace B2.Desktop.Services;

/// <summary>
/// Vietnam business calendar day (Asia/Ho_Chi_Minh), matching server-side
/// <c>VietnamTime.BusinessDateFromUtc</c>. Desktop cannot reference Application
/// assemblies; keep conversion rules identical when changing either copy.
/// </summary>
public static class VietnamBusinessDate
{
    public static DateOnly TodayFromUtc(DateTimeOffset utcNow)
    {
        var tz = ResolveVietnamTimeZone();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, tz);
        return DateOnly.FromDateTime(local);
    }

    public static DateOnly Today() => TodayFromUtc(DateTimeOffset.UtcNow);

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
