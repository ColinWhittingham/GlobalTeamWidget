using System.Text.Json;
using GlobalTeamWidget.Models;
using GlobalTeamWidget.Services;
using Xunit;

namespace GlobalTeamWidget.Tests.Services;

/// <summary>
/// Tests the backup/restore serialisation that preserves locations between
/// MSIX reinstalls. This is the layer that broke during development (locations
/// lost on upgrade) because WriteBackup was silently failing to the wrong path.
///
/// These tests exercise the JSON schema and the TileEntry↔LocationTile
/// round-trip without requiring a live MSIX LocalSettings context.
/// </summary>
public class ConfigurationBackupTests
{
    private static ConfigurationService.TileEntry FullEntry() => new()
    {
        Id           = "11111111-2222-3333-4444-555555555555",
        DisplayName  = "London HQ",
        IanaTimezone = "Europe/London",
        CurrencyCode = "GBP",
        CountryCode  = "GB",
        WorkStart    = "08:30",
        WorkEnd      = "18:00",
        WorkingDays  = "1,2,3,4,5",   // Mon–Fri
        DisplayOrder = 0,
        Latitude     = 51.5074,
        Longitude    = -0.1278,
    };

    // ── TileEntry ↔ LocationTile round-trip ───────────────────────────────

    [Fact]
    public void ToTile_PreservesAllFields()
    {
        var entry = FullEntry();
        var tile  = entry.ToTile();

        Assert.Equal(Guid.Parse(entry.Id),         tile.Id);
        Assert.Equal(entry.DisplayName,             tile.DisplayName);
        Assert.Equal(entry.IanaTimezone,            tile.IanaTimezone);
        Assert.Equal(entry.CurrencyCode,            tile.CurrencyCode);
        Assert.Equal(entry.CountryCode,             tile.CountryCode);
        Assert.Equal(new TimeOnly(8, 30),           tile.WorkHoursStart);
        Assert.Equal(new TimeOnly(18, 0),           tile.WorkHoursEnd);
        Assert.Equal(entry.DisplayOrder,            tile.DisplayOrder);
        Assert.Equal(entry.Latitude,                tile.Latitude);
        Assert.Equal(entry.Longitude,               tile.Longitude);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                      DayOfWeek.Thursday, DayOfWeek.Friday],
                     tile.WorkingDays);
    }

    [Fact]
    public void From_CapturesAllFields()
    {
        var tile = new LocationTile
        {
            Id           = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DisplayName  = "Stockholm",
            IanaTimezone = "Europe/Stockholm",
            CurrencyCode = "SEK",
            CountryCode  = "SE",
            WorkHoursStart = new TimeOnly(9, 0),
            WorkHoursEnd   = new TimeOnly(17, 0),
            WorkingDays  = [DayOfWeek.Monday, DayOfWeek.Friday],
            DisplayOrder = 2,
            Latitude     = 59.3293,
            Longitude    = 18.0686,
        };

        var entry = ConfigurationService.TileEntry.From(tile);

        Assert.Equal(tile.Id.ToString(),            entry.Id);
        Assert.Equal(tile.DisplayName,              entry.DisplayName);
        Assert.Equal(tile.IanaTimezone,             entry.IanaTimezone);
        Assert.Equal(tile.CurrencyCode,             entry.CurrencyCode);
        Assert.Equal(tile.CountryCode,              entry.CountryCode);
        Assert.Equal("09:00",                       entry.WorkStart);
        Assert.Equal("17:00",                       entry.WorkEnd);
        Assert.Equal("1,5",                         entry.WorkingDays);  // Mon=1, Fri=5
        Assert.Equal(tile.DisplayOrder,             entry.DisplayOrder);
        Assert.Equal(tile.Latitude,                 entry.Latitude);
        Assert.Equal(tile.Longitude,                entry.Longitude);
    }

    [Fact]
    public void RoundTrip_From_ThenToTile_IsIdempotent()
    {
        var original = new LocationTile
        {
            Id           = Guid.NewGuid(),
            DisplayName  = "Hanoi",
            IanaTimezone = "Asia/Bangkok",
            CurrencyCode = "VND",
            CountryCode  = "VN",
            WorkHoursStart = new TimeOnly(8, 0),
            WorkHoursEnd   = new TimeOnly(17, 30),
            WorkingDays  = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
            DisplayOrder = 3,
            Latitude     = 21.0285,
            Longitude    = 105.8542,
        };

        var restored = ConfigurationService.TileEntry.From(original).ToTile();

        Assert.Equal(original.Id,            restored.Id);
        Assert.Equal(original.DisplayName,   restored.DisplayName);
        Assert.Equal(original.IanaTimezone,  restored.IanaTimezone);
        Assert.Equal(original.CurrencyCode,  restored.CurrencyCode);
        Assert.Equal(original.CountryCode,   restored.CountryCode);
        Assert.Equal(original.WorkHoursStart,restored.WorkHoursStart);
        Assert.Equal(original.WorkHoursEnd,  restored.WorkHoursEnd);
        Assert.Equal(original.WorkingDays,   restored.WorkingDays);
        Assert.Equal(original.DisplayOrder,  restored.DisplayOrder);
        Assert.Equal(original.Latitude,      restored.Latitude);
        Assert.Equal(original.Longitude,     restored.Longitude);
    }

    // ── JSON serialisation ────────────────────────────────────────────────

    [Fact]
    public void ConfigBackup_SerializesAndDeserializes()
    {
        var backup = new ConfigurationService.ConfigBackup
        {
            Version         = 1,
            BaseCurrency    = "GBP",
            RefreshInterval = 15,
            Tiles           = [FullEntry()]
        };

        var json     = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
        var restored = JsonSerializer.Deserialize<ConfigurationService.ConfigBackup>(json);

        Assert.NotNull(restored);
        Assert.Equal("GBP", restored.BaseCurrency);
        Assert.Equal(15,    restored.RefreshInterval);
        Assert.Single(restored.Tiles);
        Assert.Equal("London HQ", restored.Tiles[0].DisplayName);
    }

    [Fact]
    public void ToTile_WithMissingOptionalFields_UsesDefaults()
    {
        // Minimal entry — only required fields
        var entry = new ConfigurationService.TileEntry
        {
            Id          = Guid.NewGuid().ToString(),
            DisplayName = "Minimal",
        };

        var tile = entry.ToTile();

        Assert.Equal("Minimal",              tile.DisplayName);
        Assert.Equal("UTC",                  tile.IanaTimezone);
        Assert.Equal(new TimeOnly(9, 0),     tile.WorkHoursStart);
        Assert.Equal(new TimeOnly(17, 0),    tile.WorkHoursEnd);
    }

    [Fact]
    public void ToTile_WithInvalidGuid_GeneratesNewGuid()
    {
        var entry = new ConfigurationService.TileEntry { Id = "not-a-guid", DisplayName = "Test" };
        var tile  = entry.ToTile();
        Assert.NotEqual(Guid.Empty, tile.Id);
    }

    // ── Corrupt/missing backup ────────────────────────────────────────────

    [Fact]
    public void Deserialize_CorruptJson_ThrowsAndCanBeCaught()
    {
        // Verify the caller (RestoreFromBackup) pattern of silently catching is safe
        var ex = Record.Exception(() =>
            JsonSerializer.Deserialize<ConfigurationService.ConfigBackup>("{ this is not json }"));
        Assert.NotNull(ex); // deserialization DOES throw on corrupt JSON
        // RestoreFromBackup wraps this in try/catch — the above confirms the exception type is catchable
    }
}
