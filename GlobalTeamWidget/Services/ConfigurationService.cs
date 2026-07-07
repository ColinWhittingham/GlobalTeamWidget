using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GlobalTeamWidget.Models;
using Windows.Storage;

namespace GlobalTeamWidget.Services;

public class ConfigurationService : IConfigurationService
{
    private const int MaxTiles = 8;
    private const string KeyPrefix = "gtw_";
    private const string KeyBaseCurrency    = KeyPrefix + "baseCurrency";
    private const string KeyRefreshInterval = KeyPrefix + "refreshIntervalMinutes";
    private const string KeyTileCount       = KeyPrefix + "tileCount";
    private const string KeyTileOrder       = KeyPrefix + "tileOrder";

    // Documents folder: guaranteed outside the MSIX container, user-visible,
    // and OneDrive-syncable. Survives Remove-AppxPackage + reinstall.
    internal static readonly string BackupPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "GlobalTeamWidget", "config.json");

    private ApplicationDataContainer Settings => ApplicationData.Current.LocalSettings;

    public ConfigurationService()
    {
        // If LocalSettings are empty (e.g. after a reinstall), restore from backup.
        if (GetTileIds().Count == 0 && File.Exists(BackupPath))
            RestoreFromBackup();
    }

    // ── Global settings ───────────────────────────────────────────────────────

    public GlobalSettings LoadGlobalSettings()
    {
        var settings = new GlobalSettings();
        if (Settings.Values[KeyBaseCurrency] is string c && !string.IsNullOrWhiteSpace(c))
            settings.BaseCurrencyCode = c;
        else
            settings.BaseCurrencyCode = DetectSystemCurrency();
        if (Settings.Values[KeyRefreshInterval] is int iv)
            settings.DataRefreshIntervalMinutes = Math.Clamp(iv, 5, 60);
        return settings;
    }

    public void SaveGlobalSettings(GlobalSettings settings)
    {
        Settings.Values[KeyBaseCurrency]    = settings.BaseCurrencyCode;
        Settings.Values[KeyRefreshInterval] = Math.Clamp(settings.DataRefreshIntervalMinutes, 5, 60);
        WriteBackup();
    }

    // ── Tile CRUD ─────────────────────────────────────────────────────────────

    public IReadOnlyList<Guid> GetTileIds()
    {
        if (Settings.Values[KeyTileOrder] is not string s || string.IsNullOrWhiteSpace(s))
            return [];
        return s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Guid.TryParse(x.Trim(), out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();
    }

    public LocationTile? LoadTile(Guid id)
    {
        var p = P(id);
        if (Settings.Values[p + "displayName"] is not string name) return null;

        var tile = new LocationTile { Id = id, DisplayName = name };
        if (Settings.Values[p + "ianaTimezone"] is string tz) tile.IanaTimezone = tz;
        if (Settings.Values[p + "currencyCode"] is string cc) tile.CurrencyCode = cc;
        if (Settings.Values[p + "countryCode"]  is string co) tile.CountryCode  = co;
        if (Settings.Values[p + "workStart"] is string ws && TimeOnly.TryParse(ws, out var st)) tile.WorkHoursStart = st;
        if (Settings.Values[p + "workEnd"]   is string we && TimeOnly.TryParse(we, out var en)) tile.WorkHoursEnd   = en;
        if (Settings.Values[p + "displayOrder"] is int ord) tile.DisplayOrder = ord;
        if (Settings.Values[p + "latitude"]  is double lat) tile.Latitude  = lat;
        if (Settings.Values[p + "longitude"] is double lon) tile.Longitude = lon;
        if (Settings.Values[p + "workingDays"] is string ds)
            tile.WorkingDays = ds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(x => int.TryParse(x, out var d) ? (DayOfWeek)d : (DayOfWeek?)null)
                                 .Where(d => d.HasValue).Select(d => d!.Value).ToArray();
        return tile;
    }

    public void SaveTile(LocationTile tile)
    {
        var ids = GetTileIds().ToList();
        if (!ids.Contains(tile.Id))
        {
            if (ids.Count >= MaxTiles)
                throw new InvalidOperationException($"Maximum of {MaxTiles} tiles reached.");
            tile.DisplayOrder = ids.Count;
            ids.Add(tile.Id);
            Settings.Values[KeyTileOrder] = string.Join(',', ids);
            Settings.Values[KeyTileCount] = ids.Count;
        }
        var p = P(tile.Id);
        Settings.Values[p + "displayName"]  = tile.DisplayName;
        Settings.Values[p + "ianaTimezone"] = tile.IanaTimezone;
        Settings.Values[p + "currencyCode"] = tile.CurrencyCode;
        Settings.Values[p + "countryCode"]  = tile.CountryCode;
        Settings.Values[p + "workStart"]    = tile.WorkHoursStart.ToString("HH:mm");
        Settings.Values[p + "workEnd"]      = tile.WorkHoursEnd.ToString("HH:mm");
        Settings.Values[p + "workingDays"]  = string.Join(',', tile.WorkingDays.Select(d => (int)d));
        Settings.Values[p + "displayOrder"] = tile.DisplayOrder;
        Settings.Values[p + "latitude"]     = tile.Latitude;
        Settings.Values[p + "longitude"]    = tile.Longitude;
        WriteBackup();
    }

    public void RemoveTile(Guid id)
    {
        var ids = GetTileIds().ToList();
        ids.Remove(id);
        foreach (var key in Settings.Values.Keys.Where(k => k.StartsWith(P(id))).ToList())
            Settings.Values.Remove(key);
        for (int i = 0; i < ids.Count; i++)
            Settings.Values[P(ids[i]) + "displayOrder"] = i;
        Settings.Values[KeyTileOrder] = string.Join(',', ids);
        Settings.Values[KeyTileCount] = ids.Count;
        WriteBackup();
    }

    public int GetRemainingSlots() => MaxTiles - GetTileIds().Count;

    // ── Backup / restore ──────────────────────────────────────────────────────

    private void WriteBackup()
    {
        try
        {
            var dir = Path.GetDirectoryName(BackupPath)!;
            Directory.CreateDirectory(dir);
            var gs = LoadGlobalSettings();
            var backup = new ConfigBackup
            {
                BaseCurrency    = gs.BaseCurrencyCode,
                RefreshInterval = gs.DataRefreshIntervalMinutes,
                Tiles = GetTileIds()
                    .Select(id => LoadTile(id))
                    .Where(t => t is not null)
                    .Select(t => TileEntry.From(t!))
                    .ToList()
            };
            File.WriteAllText(BackupPath,
                JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Best-effort — backup path is user's Documents folder so failures are rare
        }
    }

    private void RestoreFromBackup()
    {
        try
        {
            var json   = File.ReadAllText(BackupPath);
            var backup = JsonSerializer.Deserialize<ConfigBackup>(json);
            if (backup is null) return;

            if (!string.IsNullOrWhiteSpace(backup.BaseCurrency))
                Settings.Values[KeyBaseCurrency] = backup.BaseCurrency;
            if (backup.RefreshInterval > 0)
                Settings.Values[KeyRefreshInterval] = backup.RefreshInterval;

            foreach (var entry in backup.Tiles.OrderBy(t => t.DisplayOrder))
                SaveTile(entry.ToTile());
        }
        catch { /* silently skip bad backup */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string P(Guid id) => $"{KeyPrefix}tile_{id:N}_";

    private static string DetectSystemCurrency()
    {
        try { return new RegionInfo(CultureInfo.CurrentCulture.Name).ISOCurrencySymbol; }
        catch { return "USD"; }
    }

    // ── Backup schema ─────────────────────────────────────────────────────────

    internal class ConfigBackup
    {
        public int Version { get; set; } = 1;
        public string? BaseCurrency    { get; set; }
        public int RefreshInterval     { get; set; }
        public List<TileEntry> Tiles   { get; set; } = [];
    }

    internal class TileEntry
    {
        public string Id           { get; set; } = string.Empty;
        public string DisplayName  { get; set; } = string.Empty;
        public string IanaTimezone { get; set; } = "UTC";
        public string CurrencyCode { get; set; } = "USD";
        public string CountryCode  { get; set; } = "US";
        public string WorkStart    { get; set; } = "09:00";
        public string WorkEnd      { get; set; } = "17:00";
        public string WorkingDays  { get; set; } = "1,2,3,4,5";
        public int    DisplayOrder { get; set; }
        public double Latitude     { get; set; }
        public double Longitude    { get; set; }

        public static TileEntry From(LocationTile t) => new()
        {
            Id           = t.Id.ToString(),
            DisplayName  = t.DisplayName,
            IanaTimezone = t.IanaTimezone,
            CurrencyCode = t.CurrencyCode,
            CountryCode  = t.CountryCode,
            WorkStart    = t.WorkHoursStart.ToString("HH:mm"),
            WorkEnd      = t.WorkHoursEnd.ToString("HH:mm"),
            WorkingDays  = string.Join(',', t.WorkingDays.Select(d => (int)d)),
            DisplayOrder = t.DisplayOrder,
            Latitude     = t.Latitude,
            Longitude    = t.Longitude,
        };

        public LocationTile ToTile() => new()
        {
            Id           = Guid.TryParse(Id, out var g) ? g : Guid.NewGuid(),
            DisplayName  = DisplayName,
            IanaTimezone = IanaTimezone,
            CurrencyCode = CurrencyCode,
            CountryCode  = CountryCode,
            WorkHoursStart = TimeOnly.TryParse(WorkStart, out var s) ? s : new TimeOnly(9, 0),
            WorkHoursEnd   = TimeOnly.TryParse(WorkEnd,   out var e) ? e : new TimeOnly(17, 0),
            WorkingDays  = WorkingDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(x => int.TryParse(x, out var d) ? (DayOfWeek)d : (DayOfWeek?)null)
                               .Where(d => d.HasValue).Select(d => d!.Value).ToArray(),
            DisplayOrder = DisplayOrder,
            Latitude     = Latitude,
            Longitude    = Longitude,
        };
    }
}
