using System.Text.Json;
using GlobalTeamWidget.Models;

namespace GlobalTeamWidget.Services;

public class CurrencyService : ICurrencyService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly CacheService _cache;

    public CurrencyService(IHttpClientFactory httpFactory, CacheService cache)
    {
        _httpFactory = httpFactory;
        _cache = cache;
    }

    public async Task<CurrencyRate?> GetRateAsync(Guid tileId, string quoteCurrency, string baseCurrency)
    {
        var fileName = CacheService.CurrencyFileName(tileId);

        if (string.Equals(quoteCurrency, baseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return new CurrencyRate
            {
                TileId = tileId, QuoteCurrency = quoteCurrency, BaseCurrency = baseCurrency,
                Rate = 1m, IsAvailable = true, FetchedAt = DateTimeOffset.UtcNow,
            };
        }

        try
        {
            var client = _httpFactory.CreateClient("currency");
            // open.er-api.com: GET /v6/latest/{base} — free, no key, 160+ currencies
            var response = await client.GetAsync($"v6/latest/{baseCurrency.ToUpperInvariant()}");

            if (!response.IsSuccessStatusCode)
            {
                var unavailable = new CurrencyRate
                {
                    TileId = tileId, QuoteCurrency = quoteCurrency, BaseCurrency = baseCurrency,
                    IsAvailable = false, FetchedAt = DateTimeOffset.UtcNow,
                };
                await _cache.WriteCacheAsync(fileName, unavailable);
                return unavailable;
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

            // open.er-api.com response: { "result": "success", "base_code": "GBP", "rates": { "VND": 33500 } }
            if (!doc.RootElement.TryGetProperty("rates", out var rates) ||
                !rates.TryGetProperty(quoteCurrency.ToUpperInvariant(), out var rateEl))
            {
                var unavailable = new CurrencyRate
                {
                    TileId = tileId, QuoteCurrency = quoteCurrency, BaseCurrency = baseCurrency,
                    IsAvailable = false, FetchedAt = DateTimeOffset.UtcNow,
                };
                await _cache.WriteCacheAsync(fileName, unavailable);
                return unavailable;
            }

            var rate = (decimal)rateEl.GetDouble();
            var result = new CurrencyRate
            {
                TileId = tileId, QuoteCurrency = quoteCurrency, BaseCurrency = baseCurrency,
                Rate = Math.Round(rate, 4), // FormatRate handles display rounding
                IsAvailable = true, FetchedAt = DateTimeOffset.UtcNow,
            };
            await _cache.WriteCacheAsync(fileName, result);
            return result;
        }
        catch
        {
            return await _cache.ReadCacheAsync<CurrencyRate>(fileName);
        }
    }
}
