namespace GlobalTeamWidget.Models;

public class PublicHoliday
{
    public string CountryCode { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = true;
}
