using GlobalTeamWidget.Services;
using GlobalTeamWidget.Widget;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace GlobalTeamWidget;

internal static class Program
{
    private static IHost? _host;

    [STAThread]
    static void Main(string[] args)
    {
        // WinRT ComWrappers must be initialised before any WinRT/COM usage.
        // In a plain WPF app (no WinUI 3 ASTA), MarshalInterface<T>.FromManaged
        // works correctly from the STA COM thread.
        WinRT.ComWrappersSupport.InitializeComWrappers();

        _host = BuildHost();
        _host.Start();

        bool isComServer = args.Any(a =>
            a.Contains("--com-server", StringComparison.OrdinalIgnoreCase));

        if (isComServer)
        {
            RunComServer();
        }
        else
        {
            // Direct launch — open the Tile Manager UI via WPF
            UI.SettingsWindow.SetServices(
                _host.Services.GetRequiredService<IGeocodingService>(),
                _host.Services.GetRequiredService<IConfigurationService>());

            var app = new Application();
            var config   = _host.Services.GetRequiredService<IConfigurationService>();
            var provider = _host.Services.GetRequiredService<GlobalTeamWidgetProvider>();
            app.Run(new UI.TileManagerWindow(config, provider));
        }
    }

    private static void RunComServer()
    {
        // STA thread (Main) — COM routes IClassFactory calls here via the
        // STA message pump. WinRT.MarshalInterface<T>.FromManaged works correctly
        // on a plain STA (no ASTA restriction from WinUI 3).
        var provider = _host!.Services.GetRequiredService<GlobalTeamWidgetProvider>();

        // Capture the STA dispatcher for opening UI windows from COM callbacks.
        GlobalTeamWidgetProvider.SetWpfDispatcher(
            System.Windows.Threading.Dispatcher.CurrentDispatcher);

        // Register settings services for any windows opened from widget actions.
        UI.SettingsWindow.SetServices(
            _host.Services.GetRequiredService<IGeocodingService>(),
            _host.Services.GetRequiredService<IConfigurationService>());

        using var server = new ComServer(provider);

        // Run a WPF-style STA message pump so COM can dispatch callbacks.
        // This replaces the WinUI 3 Application.Start() loop.
        System.Windows.Threading.Dispatcher.Run();
    }

    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<CacheService>();
                services.AddSingleton<IConfigurationService, ConfigurationService>();
                services.AddSingleton<ITimezoneService, TimezoneService>();

                services.AddHttpClient("weather", c =>
                {
                    c.BaseAddress = new Uri("https://api.open-meteo.com/");
                    c.Timeout = TimeSpan.FromSeconds(10);
                    c.DefaultRequestHeaders.Add("User-Agent", "GlobalTeamWidget/1.0");
                });
                services.AddHttpClient("geocoding", c =>
                {
                    c.BaseAddress = new Uri("https://geocoding-api.open-meteo.com/");
                    c.Timeout = TimeSpan.FromSeconds(10);
                    c.DefaultRequestHeaders.Add("User-Agent", "GlobalTeamWidget/1.0");
                });
                services.AddHttpClient("currency", c =>
                {
                    // open.er-api.com: free, no API key, 160+ currencies (covers VND etc.)
                    c.BaseAddress = new Uri("https://open.er-api.com/");
                    c.Timeout = TimeSpan.FromSeconds(10);
                    c.DefaultRequestHeaders.Add("User-Agent", "GlobalTeamWidget/1.0");
                });
                services.AddHttpClient("holidays", c =>
                {
                    c.BaseAddress = new Uri("https://date.nager.at/");
                    c.Timeout = TimeSpan.FromSeconds(10);
                    c.DefaultRequestHeaders.Add("User-Agent", "GlobalTeamWidget/1.0");
                });

                services.AddSingleton<IGeocodingService, GeocodingService>();
                services.AddSingleton<IWeatherService, WeatherService>();
                services.AddSingleton<ICurrencyService, CurrencyService>();
                services.AddSingleton<IHolidayService, HolidayService>();
                services.AddSingleton<AdaptiveCardBuilder>();
                services.AddSingleton<GlobalTeamWidgetProvider>();
            })
            .ConfigureLogging(logging =>
            {
                logging.AddEventLog(cfg => cfg.SourceName = "GlobalTeamWidget");
            })
            .Build();
}
