namespace GlobalTeamWidget.Models;

public class LocationTile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string IanaTimezone { get; set; } = "UTC";
    public string CurrencyCode { get; set; } = "USD";
    public string CountryCode { get; set; } = "US";
    public TimeOnly WorkHoursStart { get; set; } = new(9, 0);
    public TimeOnly WorkHoursEnd { get; set; } = new(17, 0);
    public DayOfWeek[] WorkingDays { get; set; } = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    public int DisplayOrder { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
