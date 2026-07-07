namespace GlobalTeamWidget.Models;

public class WeatherSnapshot
{
    public Guid TileId { get; set; }
    public decimal TemperatureCelsius { get; set; }
    public decimal TemperatureFahrenheit => Math.Round(TemperatureCelsius * 9m / 5m + 32m, 1);
    public int ConditionCode { get; set; }
    public string ConditionLabel { get; set; } = string.Empty;
    public DateTimeOffset FetchedAt { get; set; }
    public bool IsStale => DateTimeOffset.UtcNow - FetchedAt > TimeSpan.FromMinutes(15);
}
