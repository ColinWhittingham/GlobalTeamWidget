using TimeZoneConverter;

namespace GlobalTeamWidget.Services;

public class TimezoneService : ITimezoneService
{
    public DateTimeOffset GetLocalTime(string ianaTimezone)
    {
        var tz = GetTimeZoneInfo(ianaTimezone);
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
    }

    public TimeZoneInfo GetTimeZoneInfo(string ianaTimezone)
    {
        try
        {
            return TZConvert.GetTimeZoneInfo(ianaTimezone);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
