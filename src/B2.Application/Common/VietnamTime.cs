namespace B2.Application.Common;

public static class VietnamTime
{
    public static DateOnly BusinessDateFromUtc(DateTimeOffset utcNow)
    {
        var tz = ResolveVietnamTimeZone();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, tz);
        return DateOnly.FromDateTime(local);
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
