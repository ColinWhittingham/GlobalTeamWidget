using GlobalTeamWidget.Models;

namespace GlobalTeamWidget.Services;

public interface IHolidayService
{
    Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(string countryCode, int year);
    Task<bool> IsTodayHolidayAsync(string countryCode, DateOnly today);
    Task<PublicHoliday?> GetTodayHolidayAsync(string countryCode, DateOnly today);
    Task PrefetchNextYearIfDecemberAsync(string countryCode, DateOnly today);
    bool IsCountrySupported(string countryCode);
}
