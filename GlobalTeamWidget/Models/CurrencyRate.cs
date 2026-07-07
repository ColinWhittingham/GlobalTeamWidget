namespace GlobalTeamWidget.Models;

public class CurrencyRate
{
    public Guid TileId { get; set; }
    public string QuoteCurrency { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTimeOffset FetchedAt { get; set; }
    public bool IsStale => DateTimeOffset.UtcNow - FetchedAt > TimeSpan.FromMinutes(15);
}
