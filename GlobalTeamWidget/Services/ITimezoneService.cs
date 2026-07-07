namespace GlobalTeamWidget.Services;

public interface ITimezoneService
{
    DateTimeOffset GetLocalTime(string ianaTimezone);
    TimeZoneInfo GetTimeZoneInfo(string ianaTimezone);
}
