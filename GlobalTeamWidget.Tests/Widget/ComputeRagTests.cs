using GlobalTeamWidget.Models;
using GlobalTeamWidget.Services;
using GlobalTeamWidget.Widget;
using Moq;
using Xunit;

namespace GlobalTeamWidget.Tests.Widget;

/// <summary>
/// Tests the five-branch RAG status logic in AdaptiveCardBuilder.
/// Covers all transitions: Green / Amber (×2) / Red (×3).
/// </summary>
public class ComputeRagTests
{
    // Tile with Mon–Fri, 09:00–17:00
    private static LocationTile StandardTile(string countryCode = "GB") => new()
    {
        CountryCode  = countryCode,
        IanaTimezone = "Europe/London",
        WorkHoursStart = new TimeOnly(9, 0),
        WorkHoursEnd   = new TimeOnly(17, 0),
        WorkingDays  = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                        DayOfWeek.Thursday, DayOfWeek.Friday]
    };

    // Tuesday 2026-07-07 at the given clock time
    private static DateTimeOffset Tuesday(int hour, int minute = 0) =>
        new(2026, 7, 7, hour, minute, 0, TimeSpan.Zero);

    // Saturday
    private static DateTimeOffset Saturday(int hour = 10) =>
        new(2026, 7, 4, hour, 0, 0, TimeSpan.Zero);

    private static AdaptiveCardBuilder MakeBuilder(
        PublicHoliday? todayHoliday = null,
        string countryCode = "GB")
    {
        var holidays = new Mock<IHolidayService>();
        holidays.Setup(h => h.GetTodayHolidayAsync(countryCode, It.IsAny<DateOnly>()))
                .ReturnsAsync(todayHoliday);

        var timezone = new Mock<ITimezoneService>();
        return new AdaptiveCardBuilder(holidays.Object, timezone.Object);
    }

    // ── Green ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Green_WhenWithinWorkingHours()
    {
        var (status, _) = await MakeBuilder().ComputeRagAsync(StandardTile(), Tuesday(12));
        Assert.Equal(RagStatus.Green, status);
    }

    [Fact]
    public async Task Green_AtExactStartTime()
    {
        var (status, _) = await MakeBuilder().ComputeRagAsync(StandardTile(), Tuesday(9, 0));
        Assert.Equal(RagStatus.Green, status);
    }

    [Fact]
    public async Task Green_AtExactEndTime()
    {
        var (status, _) = await MakeBuilder().ComputeRagAsync(StandardTile(), Tuesday(17, 0));
        Assert.Equal(RagStatus.Green, status);
    }

    // ── Amber: starting soon ───────────────────────────────────────────────

    [Fact]
    public async Task Amber_WhenWithinOneHourBeforeStart()
    {
        // 30 minutes before 09:00
        var (status, reason) = await MakeBuilder().ComputeRagAsync(StandardTile(), Tuesday(8, 30));
        Assert.Equal(RagStatus.Amber, status);
        Assert.Equal("Starting soon", reason);
    }

    [Fact]
    public async Task Amber_AtExactOneHourBeforeStart()
    {
        // Exactly at amberBefore (08:00)
        var (status, _) = await MakeBuilder().ComputeRagAsync(StandardTile(), Tuesday(8, 0));
        Assert.Equal(RagStatus.Amber, status);
    }

    // ── Amber: just ended ─────────────────────────────────────────────────

    [Fact]
    public async Task Amber_WhenWithinOneHourAfterEnd()
    {
        // 30 minutes after 17:00
        var (status, reason) = await MakeBuilder().ComputeRagAsync(StandardTile(), Tuesday(17, 30));
        Assert.Equal(RagStatus.Amber, status);
        Assert.Equal("Just ended", reason);
    }

    [Fact]
    public async Task Amber_AtExactOneHourAfterEnd()
    {
        // Exactly at amberAfter (18:00)
        var (status, _) = await MakeBuilder().ComputeRagAsync(StandardTile(), Tuesday(18, 0));
        Assert.Equal(RagStatus.Amber, status);
    }

    // ── Red: out of hours ─────────────────────────────────────────────────

    [Fact]
    public async Task Red_WhenMoreThanOneHourBeforeStart()
    {
        // 07:59 — one minute before amberBefore window
        var (status, _) = await MakeBuilder().ComputeRagAsync(StandardTile(), Tuesday(7, 59));
        Assert.Equal(RagStatus.Red, status);
    }

    [Fact]
    public async Task Red_WhenMoreThanOneHourAfterEnd()
    {
        // 18:01 — one minute after amberAfter window
        var (status, _) = await MakeBuilder().ComputeRagAsync(StandardTile(), Tuesday(18, 1));
        Assert.Equal(RagStatus.Red, status);
    }

    // ── Red: non-working day ──────────────────────────────────────────────

    [Fact]
    public async Task Red_OnNonWorkingDay()
    {
        // Saturday — not in Mon–Fri working days
        var (status, reason) = await MakeBuilder().ComputeRagAsync(StandardTile(), Saturday(10));
        Assert.Equal(RagStatus.Red, status);
        Assert.Equal("Off day", reason);
    }

    // ── Red: public holiday ───────────────────────────────────────────────

    [Fact]
    public async Task Red_OnPublicHoliday_EvenDuringWorkingHours()
    {
        var holiday = new PublicHoliday
        {
            CountryCode = "GB",
            Date = new DateOnly(2026, 7, 7),
            Name = "Test Bank Holiday"
        };

        // 10:00 on a Tuesday — would be Green except for the holiday
        var (status, reason) = await MakeBuilder(holiday).ComputeRagAsync(StandardTile(), Tuesday(10));
        Assert.Equal(RagStatus.Red, status);
        Assert.Contains("Test Bank Holiday", reason);
    }

    [Fact]
    public async Task Red_HolidayCheck_TakesPrecedence_OverWorkingDayCheck()
    {
        // Holiday on a Saturday: still Red (holiday reason, not "Off day")
        var holiday = new PublicHoliday
        {
            CountryCode = "GB",
            Date = new DateOnly(2026, 7, 4),
            Name = "Weekend Holiday"
        };
        var (status, reason) = await MakeBuilder(holiday).ComputeRagAsync(StandardTile(), Saturday(10));
        Assert.Equal(RagStatus.Red, status);
        Assert.Contains("Weekend Holiday", reason);
    }
}
