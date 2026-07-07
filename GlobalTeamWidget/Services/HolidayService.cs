using System.Text.Json;
using System.Text.Json.Serialization;
using GlobalTeamWidget.Models;

namespace GlobalTeamWidget.Services;

public class HolidayService : IHolidayService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly CacheService _cache;

    public HolidayService(IHttpClientFactory httpFactory, CacheService cache)
    {
        _httpFactory = httpFactory;
        _cache = cache;
    }

    public async Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(string countryCode, int year)
    {
        var fileName = CacheService.HolidayFileName(countryCode, year);
        var cached = await _cache.ReadCacheAsync<HolidayCacheEntry>(fileName);
        if (cached is not null)
            return cached.Holidays;

        try
        {
            var client = _httpFactory.CreateClient("holidays");
            var response = await client.GetAsync($"api/v3/PublicHolidays/{year}/{countryCode.ToUpperInvariant()}");

            if (!response.IsSuccessStatusCode)
            {
                await CacheEmpty(fileName, countryCode, year);
                return [];
            }

            var items = await JsonSerializer.DeserializeAsync<NagerHoliday[]>(
                await response.Content.ReadAsStreamAsync()) ?? [];

            var holidays = items
                .Where(h => h.Types.Contains("Public"))
                .Select(h => new PublicHoliday
                {
                    CountryCode = countryCode.ToUpperInvariant(),
                    Date = DateOnly.Parse(h.Date),
                    Name = h.Name,
                })
                .ToList();

            await _cache.WriteCacheAsync(fileName, new HolidayCacheEntry
            {
                CountryCode = countryCode, Year = year,
                FetchedAt = DateTimeOffset.UtcNow, Holidays = holidays,
            });
            return holidays;
        }
        catch
        {
            await CacheEmpty(fileName, countryCode, year);
            return [];
        }
    }

    public async Task<bool> IsTodayHolidayAsync(string countryCode, DateOnly today)
        => await GetTodayHolidayAsync(countryCode, today) is not null;

    public async Task<PublicHoliday?> GetTodayHolidayAsync(string countryCode, DateOnly today)
    {
        var holidays = await GetHolidaysAsync(countryCode, today.Year);
        return holidays.FirstOrDefault(h => h.Date == today);
    }

    public async Task PrefetchNextYearIfDecemberAsync(string countryCode, DateOnly today)
    {
        if (today.Month != 12) return;
        var nextYear = today.Year + 1;
        var fileName = CacheService.HolidayFileName(countryCode, nextYear);
        var cached = await _cache.ReadCacheAsync<HolidayCacheEntry>(fileName);
        if (cached is null)
            await GetHolidaysAsync(countryCode, nextYear);
    }

    // Tracks unsupported countries discovered at runtime (empty list returned from API)
    private readonly HashSet<string> _unsupportedCountries = new(StringComparer.OrdinalIgnoreCase);

    public bool IsCountrySupported(string countryCode)
        => !_unsupportedCountries.Contains(countryCode);

    private async Task CacheEmpty(string fileName, string countryCode, int year)
    {
        _unsupportedCountries.Add(countryCode);
        await _cache.WriteCacheAsync(fileName, new HolidayCacheEntry
        {
            CountryCode = countryCode, Year = year,
            FetchedAt = DateTimeOffset.UtcNow, Holidays = [],
        });
    }

    private record HolidayCacheEntry
    {
        public string CountryCode { get; init; } = string.Empty;
        public int Year { get; init; }
        public DateTimeOffset FetchedAt { get; init; }
        public List<PublicHoliday> Holidays { get; init; } = [];
    }

    private record NagerHoliday
    {
        [JsonPropertyName("date")] public string Date { get; init; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("types")] public string[] Types { get; init; } = [];
    }
}
