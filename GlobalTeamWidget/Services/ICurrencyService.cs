using GlobalTeamWidget.Models;

namespace GlobalTeamWidget.Services;

public interface ICurrencyService
{
    Task<CurrencyRate?> GetRateAsync(Guid tileId, string quoteCurrency, string baseCurrency);
}
