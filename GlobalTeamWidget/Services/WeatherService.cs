using System.Text.Json;
using GlobalTeamWidget.Models;

namespace GlobalTeamWidget.Services;

public class WeatherService : IWeatherService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly CacheService _cache;

    private static readonly Dictionary<int, string> WmoLabels = new()
    {
        [0] = "Clear sky", [1] = "Mainly clear", [2] = "Partly cloudy", [3] = "Overcast",
        [45] = "Fog", [48] = "Icy fog",
        [51] = "Light drizzle", [53] = "Drizzle", [55] = "Heavy drizzle",
        [61] = "Light rain", [63] = "Rain", [65] = "Heavy rain",
        [71] = "Light snow", [73] = "Snow", [75] = "Heavy snow", [77] = "Snow grains",
        [80] = "Rain showers", [81] = "Showers", [82] = "Heavy showers",
        [85] = "Snow showers", [86] = "Heavy snow showers",
        [95] = "Thunderstorm", [96] = "Thunderstorm w/ hail", [99] = "Thunderstorm w/ heavy hail",
    };

    public WeatherService(IHttpClientFactory httpFactory, CacheService cache)
    {
        _httpFactory = httpFactory;
        _cache = cache;
    }

    public async Task<WeatherSnapshot?> GetWeatherAsync(Guid tileId, double latitude, double longitude)
    {
        var fileName = CacheService.WeatherFileName(tileId);
        try
        {
            var client = _httpFactory.CreateClient("weather");
            var url = $"v1/forecast?latitude={latitude:F4}&longitude={longitude:F4}&current=temperature_2m,weathercode&temperature_unit=celsius&timezone=auto";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var current = doc.RootElement.GetProperty("current");
            var tempC = Math.Round((decimal)current.GetProperty("temperature_2m").GetDouble(), 1);
            var code = current.GetProperty("weathercode").GetInt32();

            var snapshot = new WeatherSnapshot
            {
                TileId = tileId,
                TemperatureCelsius = tempC,
                ConditionCode = code,
                ConditionLabel = WmoLabels.GetValueOrDefault(code, "Unknown"),
                FetchedAt = DateTimeOffset.UtcNow,
            };

            await _cache.WriteCacheAsync(fileName, snapshot);
            return snapshot;
        }
        catch
        {
            return await _cache.ReadCacheAsync<WeatherSnapshot>(fileName);
        }
    }
}
