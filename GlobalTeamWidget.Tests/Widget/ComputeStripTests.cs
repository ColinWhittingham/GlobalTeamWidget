using GlobalTeamWidget.Models;
using GlobalTeamWidget.Services;
using GlobalTeamWidget.Widget;
using Moq;
using Xunit;

namespace GlobalTeamWidget.Tests.Widget;

/// <summary>
/// Tests the 7-day non-working-day strip, focusing on the December→January
/// year-boundary crossing that requires fetching two years of holiday data.
/// </summary>
public class ComputeStripTests
{
    private static LocationTile MonFriTile(string countryCode = "GB") => new()
    {
        CountryCode  = countryCode,
        IanaTimezone = "Europe/London",
        WorkingDays  = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                        DayOfWeek.Thursday, DayOfWeek.Friday]
    };

    private static AdaptiveCardBuilder MakeBuilder(
        IReadOnlyList<PublicHoliday>? yr2026 = null,
        IReadOnlyList<PublicHoliday>? yr2027 = null,
        string cc = "GB")
    {
        var holidays = new Mock<IHolidayService>();
        holidays.Setup(h => h.GetHolidaysAsync(cc, 2026))
                .ReturnsAsync(yr2026 ?? []);
        holidays.Setup(h => h.GetHolidaysAsync(cc, 2027))
                .ReturnsAsync(yr2027 ?? []);
        holidays.Setup(h => h.PrefetchNextYearIfDecemberAsync(cc, It.IsAny<DateOnly>()))
                .Returns(Task.CompletedTask);

        return new AdaptiveCardBuilder(holidays.Object, new Mock<ITimezoneService>().Object);
    }

    // ── Year boundary: December strip spans into January ──────────────────

    [Fact]
    public async Task Strip_StartingDec28_FetchesBothYears()
    {
        // Dec 28 2026 is a Monday; strip covers Mon 28 Dec → Sun 3 Jan 2027
        // Jan 1 2027 is a Friday and a public holiday in many countries
        var newYear = new PublicHoliday { Date = new DateOnly(2027, 1, 1), Name = "New Year's Day" };

        var builder = MakeBuilder(yr2026: [], yr2027: [newYear]);
        var tile    = MonFriTile();
        var strip   = await builder.ComputeStrip(tile, new DateOnly(2026, 12, 28));

        Assert.Equal(7, strip.Length);

        // Day 4 of the strip is Jan 1 (Mon 28 + 4 = Fri 1 Jan 2027)
        var jan1 = strip[4];
        Assert.Equal(new DateOnly(2027, 1, 1), jan1.Date);
        Assert.True(jan1.IsNonWorking);
        Assert.Equal("PublicHoliday", jan1.Reason);
        Assert.Equal("New Year's Day", jan1.HolidayName);
    }

    [Fact]
    public async Task Strip_StartingDec28_NextYearFetch_IsCalled()
    {
        var holidays = new Mock<IHolidayService>();
        holidays.Setup(h => h.GetHolidaysAsync("GB", It.IsAny<int>())).ReturnsAsync([]);
        holidays.Setup(h => h.PrefetchNextYearIfDecemberAsync("GB", It.IsAny<DateOnly>())).Returns(Task.CompletedTask);

        var builder = new AdaptiveCardBuilder(holidays.Object, new Mock<ITimezoneService>().Object);
        await builder.ComputeStrip(MonFriTile(), new DateOnly(2026, 12, 28));

        // Both years must be queried when start date is in December
        holidays.Verify(h => h.GetHolidaysAsync("GB", 2026), Times.Once);
        holidays.Verify(h => h.GetHolidaysAsync("GB", 2027), Times.Once);
    }

    [Fact]
    public async Task Strip_StartingNovember_DoesNotFetchNextYear()
    {
        var holidays = new Mock<IHolidayService>();
        holidays.Setup(h => h.GetHolidaysAsync("GB", It.IsAny<int>())).ReturnsAsync([]);
        holidays.Setup(h => h.PrefetchNextYearIfDecemberAsync("GB", It.IsAny<DateOnly>())).Returns(Task.CompletedTask);

        var builder = new AdaptiveCardBuilder(holidays.Object, new Mock<ITimezoneService>().Object);
        await builder.ComputeStrip(MonFriTile(), new DateOnly(2026, 11, 28));

        holidays.Verify(h => h.GetHolidaysAsync("GB", 2026), Times.Once);
        holidays.Verify(h => h.GetHolidaysAsync("GB", 2027), Times.Never);
    }

    // ── Correct classification of each day type ───────────────────────────

    [Fact]
    public async Task Strip_WeekendsMarkedNonWorking()
    {
        // Mon 28 Dec 2026 → strip includes Sat 2 Jan and Sun 3 Jan
        var builder = MakeBuilder();
        var strip   = await builder.ComputeStrip(MonFriTile(), new DateOnly(2026, 12, 28));

        var sat = strip.Single(d => d.Date == new DateOnly(2027, 1, 2));
        var sun = strip.Single(d => d.Date == new DateOnly(2027, 1, 3));

        Assert.True(sat.IsNonWorking);
        Assert.Equal("Weekend", sat.Reason);
        Assert.True(sun.IsNonWorking);
        Assert.Equal("Weekend", sun.Reason);
    }

    [Fact]
    public async Task Strip_WorkingDayMarkedWorking()
    {
        // Mon 28 Dec is a working day with no holiday
        var builder = MakeBuilder();
        var strip   = await builder.ComputeStrip(MonFriTile(), new DateOnly(2026, 12, 28));

        var mon = strip[0]; // first day = Dec 28 (Monday)
        Assert.Equal(new DateOnly(2026, 12, 28), mon.Date);
        Assert.False(mon.IsNonWorking);
        Assert.Equal("WorkingDay", mon.Reason);
    }

    [Fact]
    public async Task Strip_PublicHolidayOnWeekday_OverridesWorkingDay()
    {
        // Dec 28 (Monday) is a bank holiday — should show as PublicHoliday, not WorkingDay
        var boxing = new PublicHoliday { Date = new DateOnly(2026, 12, 28), Name = "Boxing Day (substitute)" };

        var builder = MakeBuilder(yr2026: [boxing]);
        var strip   = await builder.ComputeStrip(MonFriTile(), new DateOnly(2026, 12, 28));

        var day = strip[0];
        Assert.True(day.IsNonWorking);
        Assert.Equal("PublicHoliday", day.Reason);
        Assert.Equal("Boxing Day (substitute)", day.HolidayName);
    }

    [Fact]
    public async Task Strip_Always7Days()
    {
        var builder = MakeBuilder();
        var strip   = await builder.ComputeStrip(MonFriTile(), new DateOnly(2026, 7, 1));
        Assert.Equal(7, strip.Length);
    }

    [Fact]
    public async Task Strip_DatesAreConsecutive()
    {
        var builder = MakeBuilder();
        var start   = new DateOnly(2026, 12, 29);
        var strip   = await builder.ComputeStrip(MonFriTile(), start);

        for (int i = 0; i < 7; i++)
            Assert.Equal(start.AddDays(i), strip[i].Date);
    }
}
