namespace VKFoodArea.Web.Services;

public static class WebDisplayTime
{
    private static readonly Lazy<TimeZoneInfo> DisplayTimeZone = new(ResolveDisplayTimeZone);

    public static DateTime TodayStartUtc => ToUtcStartOfDay(ToDisplayTime(DateTime.UtcNow));
    public static DateTime TomorrowStartUtc => TodayStartUtc.AddDays(1);

    public static DateTime ToDisplayTime(DateTime utcTime)
    {
        var utc = utcTime.Kind == DateTimeKind.Utc
            ? utcTime
            : DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, DisplayTimeZone.Value);
    }

    public static DateTime ToUtcStartOfDay(DateTime displayDate)
    {
        var localStart = DateTime.SpecifyKind(displayDate.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localStart, DisplayTimeZone.Value);
    }

    public static DateTime ToUtcStartOfNextDay(DateTime displayDate)
        => ToUtcStartOfDay(displayDate.Date.AddDays(1));

    public static string Format(DateTime utcTime, string format = "dd/MM/yyyy HH:mm:ss")
        => ToDisplayTime(utcTime).ToString(format);

    private static TimeZoneInfo ResolveDisplayTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Bangkok" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}
