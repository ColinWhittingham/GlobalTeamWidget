using GlobalTeamWidget.Models;

namespace GlobalTeamWidget.Services;

public interface IWeatherService
{
    Task<WeatherSnapshot?> GetWeatherAsync(Guid tileId, double latitude, double longitude);
}
