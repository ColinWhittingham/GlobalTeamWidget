namespace GlobalTeamWidget.Services;

public record GeocodingResult(string DisplayName, double Latitude, double Longitude, string IanaTimezone, string CountryCode);

public interface IGeocodingService
{
    Task<GeocodingResult?> SearchCityAsync(string cityName);
}
