using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlobalTeamWidget.Services;

public class GeocodingService : IGeocodingService
{
    private readonly IHttpClientFactory _httpFactory;

    public GeocodingService(IHttpClientFactory httpFactory) => _httpFactory = httpFactory;

    public async Task<GeocodingResult?> SearchCityAsync(string cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName)) return null;

        try
        {
            var client = _httpFactory.CreateClient("geocoding");
            var url = $"v1/search?name={Uri.EscapeDataString(cityName)}&count=1&language=en&format=json";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return null;

            var first = results[0];
            return new GeocodingResult(
                DisplayName: first.GetProperty("name").GetString() ?? cityName,
                Latitude: first.GetProperty("latitude").GetDouble(),
                Longitude: first.GetProperty("longitude").GetDouble(),
                IanaTimezone: first.GetProperty("timezone").GetString() ?? "UTC",
                CountryCode: first.GetProperty("country_code").GetString()?.ToUpperInvariant() ?? "US"
            );
        }
        catch
        {
            return null;
        }
    }
}
