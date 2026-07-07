using GlobalTeamWidget.Models;
using GlobalTeamWidget.Services;
using System.Windows;
using TimeZoneConverter;

namespace GlobalTeamWidget.UI;

public partial class SettingsWindow : Window
{
    public event EventHandler<LocationTile>? TileConfigured;
    public event EventHandler<Guid>? TileRemoved;

    private readonly LocationTile? _existingTile;
    private double _latitude;
    private double _longitude;

    private static IGeocodingService? _geocodingService;
    private static IConfigurationService? _configService;

    public static void SetServices(IGeocodingService geo, IConfigurationService cfg)
    {
        _geocodingService = geo;
        _configService = cfg;
    }

    public SettingsWindow(LocationTile? existingTile)
    {
        InitializeComponent();
        _existingTile = existingTile;

        if (existingTile is not null)
        {
            Title = "Edit Location";
            DisplayNameBox.Text = existingTile.DisplayName;
            TimezoneBox.Text = existingTile.IanaTimezone;
            CurrencyBox.Text = existingTile.CurrencyCode;
            CountryBox.Text = existingTile.CountryCode;
            WorkStartBox.Text = existingTile.WorkHoursStart.ToString("HH:mm");
            WorkEndBox.Text = existingTile.WorkHoursEnd.ToString("HH:mm");
            _latitude = existingTile.Latitude;
            _longitude = existingTile.Longitude;
            PopulateWorkingDays(existingTile.WorkingDays);
            RemoveButton.Visibility = Visibility.Visible;
        }
        else
        {
            Title = "Add Location";
            DayMon.IsChecked = DayTue.IsChecked = DayWed.IsChecked =
            DayThu.IsChecked = DayFri.IsChecked = true;
        }

        if (_configService is not null)
            BaseCurrencyBox.Text = _configService.LoadGlobalSettings().BaseCurrencyCode;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e) => Close();

    private async void OnSearchCity(object sender, RoutedEventArgs e)
    {
        if (_geocodingService is null) return;
        var city = CitySearchBox.Text.Trim();
        if (string.IsNullOrEmpty(city)) return;

        SearchButton.IsEnabled = false;
        GeocodingStatus.Visibility = Visibility.Collapsed;

        var result = await _geocodingService.SearchCityAsync(city);

        if (result is null)
        {
            GeocodingStatus.Text = "Location not found. Try a different city name.";
            GeocodingStatus.Visibility = Visibility.Visible;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
                DisplayNameBox.Text = result.DisplayName.Length > 30 ? result.DisplayName[..30] : result.DisplayName;
            TimezoneBox.Text = result.IanaTimezone;
            CountryBox.Text = result.CountryCode;
            _latitude = result.Latitude;
            _longitude = result.Longitude;
        }

        SearchButton.IsEnabled = true;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ValidationMessage.Visibility = Visibility.Collapsed;
        var errors = Validate();
        if (errors.Count > 0)
        {
            ValidationMessage.Text = string.Join("\n", errors);
            ValidationMessage.Visibility = Visibility.Visible;
            return;
        }

        var tile = _existingTile ?? new LocationTile();
        tile.DisplayName  = DisplayNameBox.Text.Trim();
        tile.IanaTimezone = TimezoneBox.Text.Trim();
        tile.CurrencyCode = CurrencyBox.Text.Trim().ToUpperInvariant();
        tile.CountryCode  = CountryBox.Text.Trim().ToUpperInvariant();
        tile.WorkHoursStart = TimeOnly.Parse(WorkStartBox.Text);
        tile.WorkHoursEnd   = TimeOnly.Parse(WorkEndBox.Text);
        tile.WorkingDays = CollectWorkingDays();
        tile.Latitude  = _latitude;
        tile.Longitude = _longitude;

        if (_configService is not null && !string.IsNullOrWhiteSpace(BaseCurrencyBox.Text))
        {
            var gs = _configService.LoadGlobalSettings();
            gs.BaseCurrencyCode = BaseCurrencyBox.Text.Trim().ToUpperInvariant();
            _configService.SaveGlobalSettings(gs);
        }

        TileConfigured?.Invoke(this, tile);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (_existingTile is not null)
        {
            TileRemoved?.Invoke(this, _existingTile.Id);
            Close();
        }
    }

    private List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text)) errors.Add("Display name is required.");
        if (string.IsNullOrWhiteSpace(TimezoneBox.Text)) errors.Add("Timezone is required.");
        if (!IsValidIana(TimezoneBox.Text)) errors.Add("Not a valid IANA timezone.");
        if (string.IsNullOrWhiteSpace(CurrencyBox.Text) || CurrencyBox.Text.Length != 3)
            errors.Add("Currency must be a 3-letter ISO 4217 code.");
        if (string.IsNullOrWhiteSpace(CountryBox.Text) || CountryBox.Text.Length != 2)
            errors.Add("Country must be a 2-letter ISO 3166-1 code.");
        if (!TimeOnly.TryParse(WorkStartBox.Text, out var start)) errors.Add("Start time must be HH:mm.");
        if (!TimeOnly.TryParse(WorkEndBox.Text, out var end)) errors.Add("End time must be HH:mm.");
        if (errors.Count == 0 && end <= start) errors.Add("End time must be after start time.");
        if (CollectWorkingDays().Length == 0) errors.Add("At least one working day must be selected.");
        return errors;
    }

    private static bool IsValidIana(string tz)
    {
        try { TZConvert.GetTimeZoneInfo(tz); return true; }
        catch { return false; }
    }

    private DayOfWeek[] CollectWorkingDays()
    {
        var days = new List<DayOfWeek>();
        if (DayMon.IsChecked == true) days.Add(DayOfWeek.Monday);
        if (DayTue.IsChecked == true) days.Add(DayOfWeek.Tuesday);
        if (DayWed.IsChecked == true) days.Add(DayOfWeek.Wednesday);
        if (DayThu.IsChecked == true) days.Add(DayOfWeek.Thursday);
        if (DayFri.IsChecked == true) days.Add(DayOfWeek.Friday);
        if (DaySat.IsChecked == true) days.Add(DayOfWeek.Saturday);
        if (DaySun.IsChecked == true) days.Add(DayOfWeek.Sunday);
        return [.. days];
    }

    private void PopulateWorkingDays(DayOfWeek[] days)
    {
        DayMon.IsChecked = days.Contains(DayOfWeek.Monday);
        DayTue.IsChecked = days.Contains(DayOfWeek.Tuesday);
        DayWed.IsChecked = days.Contains(DayOfWeek.Wednesday);
        DayThu.IsChecked = days.Contains(DayOfWeek.Thursday);
        DayFri.IsChecked = days.Contains(DayOfWeek.Friday);
        DaySat.IsChecked = days.Contains(DayOfWeek.Saturday);
        DaySun.IsChecked = days.Contains(DayOfWeek.Sunday);
    }
}
