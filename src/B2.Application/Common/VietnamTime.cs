namespace B2.Application.Common;

public static class VietnamTime
{
    /// <summary>
    /// Asia/Ho_Chi_Minh, resolved once at startup. Falls back to the Windows id
    /// "SE Asia Standard Time" on hosts that ship without the IANA tz database.
    /// Single source of truth for every server-side tz read — Quartz cron
    /// triggers, report hour-bucketing, and business-date math all read this.
    /// (B2.Desktop keeps its own copy: Constitution V forbids
    /// B2.Desktop → B2.Application, so it cannot reference this helper.)
    /// </summary>
    public static TimeZoneInfo TimeZone { get; } = ResolveVietnamTimeZone();

    public static DateOnly BusinessDateFromUtc(DateTimeOffset utcNow)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, TimeZone);
        return DateOnly.FromDateTime(local);
    }

    /// <summary>
    /// Half-open UTC interval <c>[StartUtc, EndUtc)</c> covering the Vietnam business day
    /// <paramref name="date"/> — for filtering stored instants (EntryTime / ExitTime) by the
    /// day they physically happened on, rather than by their declaration's BusinessDate.
    /// </summary>
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) BusinessDayUtcBounds(DateOnly date)
    {
        var start = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), TimeZone);
        var end = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeZone);
        return (new DateTimeOffset(start, TimeSpan.Zero), new DateTimeOffset(end, TimeSpan.Zero));
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
