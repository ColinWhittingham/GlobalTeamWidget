using System.Text.Json;
using GlobalTeamWidget.Models;
using GlobalTeamWidget.Services;

namespace GlobalTeamWidget.Widget;

public enum RagStatus { Green, Amber, Red }
public record NonWorkingDay(DateOnly Date, bool IsNonWorking, string Reason, string HolidayName = "");

public class AdaptiveCardBuilder
{
    private readonly IHolidayService _holidays;
    private readonly ITimezoneService _timezone;

    public AdaptiveCardBuilder(IHolidayService holidays, ITimezoneService timezone)
    {
        _holidays = holidays;
        _timezone = timezone;
    }

    // ── RAG computation ───────────────────────────────────────────────────────
    public async Task<(RagStatus Status, string Reason)> ComputeRagAsync(LocationTile tile, DateTimeOffset localNow)
    {
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var holiday = await _holidays.GetTodayHolidayAsync(tile.CountryCode, today);
        if (holiday is not null)
            return (RagStatus.Red, $"PH: {holiday.Name}");

        var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
        if (!tile.WorkingDays.Contains(localNow.DayOfWeek))
            return (RagStatus.Red, "Off day");

        if (localTime >= tile.WorkHoursStart && localTime <= tile.WorkHoursEnd)
            return (RagStatus.Green, "In hours");

        var amberBefore = tile.WorkHoursStart.Add(TimeSpan.FromHours(-1));
        var amberAfter  = tile.WorkHoursEnd.Add(TimeSpan.FromHours(1));
        if (localTime >= amberBefore && localTime < tile.WorkHoursStart)
            return (RagStatus.Amber, "Starting soon");
        if (localTime > tile.WorkHoursEnd && localTime <= amberAfter)
            return (RagStatus.Amber, "Just ended");

        return (RagStatus.Red, "Out of hours");
    }

    // ── 7-day strip ───────────────────────────────────────────────────────────
    public async Task<NonWorkingDay[]> ComputeStrip(LocationTile tile, DateOnly startDate)
    {
        var holidays = await _holidays.GetHolidaysAsync(tile.CountryCode, startDate.Year);
        var nextYear = startDate.Month == 12
            ? await _holidays.GetHolidaysAsync(tile.CountryCode, startDate.Year + 1)
            : (IReadOnlyList<PublicHoliday>)Array.Empty<PublicHoliday>();
        var allHolidays = holidays.Concat(nextYear).ToHashSet();

        await _holidays.PrefetchNextYearIfDecemberAsync(tile.CountryCode, startDate);

        return Enumerable.Range(0, 7).Select(i =>
        {
            var date = startDate.AddDays(i);
            var ph = allHolidays.FirstOrDefault(h => h.Date == date);
            if (ph is not null) return new NonWorkingDay(date, true, "PublicHoliday", ph.Name);
            if (!tile.WorkingDays.Contains(date.DayOfWeek)) return new NonWorkingDay(date, true, "Weekend");
            return new NonWorkingDay(date, false, "WorkingDay");
        }).ToArray();
    }

    // ── Paginated horizontal-band dashboard ───────────────────────────────────
    public async Task<string> BuildDashboardAsync(
        IReadOnlyList<LocationTile> allTiles,
        Dictionary<Guid, WeatherSnapshot?> weather,
        Dictionary<Guid, CurrencyRate?> rates,
        int page,
        int remainingSlots)
    {
        if (allTiles.Count == 0)
            return BuildEmptyStateCard();

        const int tilesPerPage = 4;
        int totalPages = (int)Math.Ceiling(allTiles.Count / (double)tilesPerPage);
        page = Math.Clamp(page, 0, totalPages - 1);

        var pageTiles = allTiles
            .OrderBy(t => t.DisplayOrder)
            .Skip(page * tilesPerPage)
            .Take(tilesPerPage)
            .ToList();

        var bands = new List<object>();
        foreach (var tile in pageTiles)
            bands.Add(await BuildLocationBandAsync(tile, weather, rates));

        // No add-band in the tile list — the toolbar "+ Add" handles this cleanly

        // Bottom toolbar — inline hyperlink-style clickable text, not large action buttons
        var toolbarCols = new List<object>();

        if (totalPages > 1)
        {
            if (page > 0)
                toolbarCols.Add(MiniAction("← Prev", "prev-page"));
            toolbarCols.Add(new
            {
                type = "Column", width = "auto",
                items = new object[] { new { type = "TextBlock", text = $"{page + 1}/{totalPages}", size = "Small", isSubtle = true } }
            });
            if (page < totalPages - 1)
                toolbarCols.Add(MiniAction("Next →", "next-page"));
        }

        // Spacer to push remaining items right
        toolbarCols.Add(new { type = "Column", width = "stretch", items = Array.Empty<object>() });

        if (remainingSlots > 0)
            toolbarCols.Add(MiniAction("＋ Add", "open-edit", "Accent"));
        toolbarCols.Add(MiniAction("⟳", "refresh"));

        var toolbar = new
        {
            type = "ColumnSet",
            spacing = "Small",
            columns = toolbarCols.ToArray()
        };

        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new
                {
                    type = "Container",
                    height = "stretch",
                    spacing = "None",
                    items = bands.ToArray()
                },
                (object)toolbar
            },
            actions = Array.Empty<object>()  // no large action buttons
        };
        return JsonSerializer.Serialize(card);
    }

    // ── Single horizontal location band ───────────────────────────────────────
    private async Task<object> BuildLocationBandAsync(
        LocationTile tile,
        Dictionary<Guid, WeatherSnapshot?> weather,
        Dictionary<Guid, CurrencyRate?> rates)
    {
        var localNow = _timezone.GetLocalTime(tile.IanaTimezone);
        var today    = DateOnly.FromDateTime(localNow.DateTime);
        var (rag, ragReason) = await ComputeRagAsync(tile, localNow);
        var strip    = await ComputeStrip(tile, today);

        var w = weather.GetValueOrDefault(tile.Id);
        var r = rates.GetValueOrDefault(tile.Id);

        var ragColor = rag switch { RagStatus.Green => "Good", RagStatus.Amber => "Warning", _ => "Attention" };
        var ragDot   = rag switch { RagStatus.Green => "🟢", RagStatus.Amber => "🟡", _ => "🔴" };

        // Weather: condition + temps on one line
        var weatherText = w is null ? "–"
            : $"{w.ConditionLabel}  {w.TemperatureCelsius:F0}°C/{w.TemperatureFahrenheit:F0}°F";

        // Currency: compact format
        var currencyText = r is null ? "–"
            : !r.IsAvailable ? $"{tile.CurrencyCode}: N/A"
            : $"1 {r.BaseCurrency} = {FormatRate(r.Rate)} {r.QuoteCurrency}";

        // Staleness: amber warning row when cached data is older than the refresh interval
        bool weatherStale  = w?.IsStale == true;
        bool currencyStale = r?.IsStale == true;
        string? staleText  = (weatherStale || currencyStale)
            ? $"⚠ offline – data as of {(weatherStale ? w!.FetchedAt : r!.FetchedAt).LocalDateTime:HH:mm}"
            : null;

        // 7-day strip: day letter + coloured marker, side-by-side columns
        var stripDays = strip.Select(d =>
        {
            var marker = d.Reason == "PublicHoliday" ? "P" : d.IsNonWorking ? "✕" : "✓";
            var color  = d.Reason == "PublicHoliday" ? "Warning"
                       : d.IsNonWorking ? "Default" : "Good";
            return (object)new
            {
                type = "Column",
                width = "stretch",
                items = new object[]
                {
                    new { type = "TextBlock", text = d.Date.ToString("ddd")[..1],
                          horizontalAlignment = "Center", size = "Small", isSubtle = true },
                    new { type = "TextBlock", text = marker, color,
                          horizontalAlignment = "Center", size = "Small", spacing = "None" }
                }
            };
        }).ToArray<object>();

        return new
        {
            type = "Container",
            style = "emphasis",
            spacing = "Small",
            selectAction = new
            {
                type = "Action.Execute", verb = "open-edit",
                data = new { tileId = tile.Id.ToString() }
            },
            items = BuildBandItems(ragDot, tile.DisplayName, localNow, ragColor, stripDays,
                                  weatherText, currencyText, staleText)
        };
    }

    private static object[] BuildBandItems(
        string ragDot, string name, DateTimeOffset now, string ragColor,
        object[] stripDays, string weather, string currency, string? staleText)
    {
        var items = new List<object>
        {
            // Row 1: name + RAG dot
            new
            {
                type = "ColumnSet", spacing = "None",
                columns = new object[]
                {
                    new { type = "Column", width = "auto",
                          items = new object[] { new { type = "TextBlock", text = ragDot } } },
                    new { type = "Column", width = "stretch",
                          items = new object[] { new { type = "TextBlock", text = name,
                              weight = "Bolder", wrap = false } } },
                }
            },
            // Row 2: time+date (30%) | 7-day strip (70%)
            new
            {
                type = "ColumnSet", spacing = "None",
                columns = new object[]
                {
                    new
                    {
                        type = "Column", width = "3", verticalContentAlignment = "Center",
                        items = new object[]
                        {
                            new { type = "TextBlock", text = now.ToString("HH:mm"),
                                  size = "ExtraLarge", weight = "Bolder", color = ragColor },
                            new { type = "TextBlock", text = now.ToString("ddd d MMM"),
                                  size = "Small", isSubtle = true, spacing = "None" }
                        }
                    },
                    new
                    {
                        type = "Column", width = "7", verticalContentAlignment = "Center",
                        items = new object[] { new { type = "ColumnSet", spacing = "None", columns = stripDays } }
                    }
                }
            },
            // Row 3: weather (60%) | currency (40%)
            new
            {
                type = "ColumnSet", spacing = "None",
                columns = new object[]
                {
                    new { type = "Column", width = "3",
                          items = new object[] { new { type = "TextBlock", text = weather, size = "Small", wrap = false } } },
                    new { type = "Column", width = "2",
                          items = new object[] { new { type = "TextBlock", text = currency, size = "Small", isSubtle = true, wrap = false } } }
                }
            }
        };

        // Row 4 (conditional): amber staleness warning when offline / data is old
        if (staleText is not null)
            items.Add(new { type = "TextBlock", text = staleText, size = "Small", color = "Warning", spacing = "None" });

        return items.ToArray();
    }

    // Show 2 decimal places when fractional, none when whole (e.g. 34691 VND, 12.87 SEK)
    internal static string FormatRate(decimal rate)
    {
        var rounded = Math.Round(rate, 2);
        return rounded == Math.Floor(rounded)
            ? rounded.ToString("N0")   // whole number with thousands separator: 34,691
            : rounded.ToString("F2");  // 2 decimal places: 12.87
    }

    // Small hyperlink-style clickable column for the bottom toolbar
    private static object MiniAction(string label, string verb, string color = "Default") => new
    {
        type = "Column",
        width = "auto",
        spacing = "Medium",
        selectAction = new { type = "Action.Execute", verb },
        items = new object[]
        {
            new { type = "TextBlock", text = label, size = "Default", color, isSubtle = color == "Default" }
        }
    };

    public string BuildEmptyStateCard()
    {
        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new { type = "TextBlock", text = "Global Team Widget", weight = "Bolder", size = "Medium" },
                new { type = "TextBlock", text = "Add your first location to get started.", wrap = true },
                new
                {
                    type = "ColumnSet", spacing = "Small",
                    columns = new object[] { MiniAction("＋ Add location", "open-edit", "Accent") }
                }
            },
            actions = Array.Empty<object>()
        };
        return JsonSerializer.Serialize(card);
    }
}
