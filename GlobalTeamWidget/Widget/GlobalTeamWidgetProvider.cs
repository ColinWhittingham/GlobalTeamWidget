using System.Collections.Concurrent;
using System.Text.Json;
using GlobalTeamWidget.Models;
using GlobalTeamWidget.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.Widgets.Providers;

namespace GlobalTeamWidget.Widget;

public class GlobalTeamWidgetProvider : IWidgetProvider
{
    private readonly IConfigurationService _config;
    private readonly IWeatherService _weather;
    private readonly ICurrencyService _currency;
    private readonly IHolidayService _holidays;
    private readonly AdaptiveCardBuilder _cardBuilder;
    private readonly ILogger<GlobalTeamWidgetProvider> _logger;

    private readonly ConcurrentDictionary<string, WidgetContext> _activeWidgets = new();
    private Timer? _refreshTimer;

    // Current pagination page (shared across all widget instances — there's only ever one)
    private int _currentPage = 0;

    private static System.Windows.Threading.Dispatcher? _wpfDispatcher;
    public static void SetWpfDispatcher(System.Windows.Threading.Dispatcher d) => _wpfDispatcher = d;

    public GlobalTeamWidgetProvider(
        IConfigurationService config,
        IWeatherService weather,
        ICurrencyService currency,
        IHolidayService holidays,
        AdaptiveCardBuilder cardBuilder,
        ILogger<GlobalTeamWidgetProvider> logger)
    {
        _config = config;
        _weather = weather;
        _currency = currency;
        _holidays = holidays;
        _cardBuilder = cardBuilder;
        _logger = logger;
    }

    // ── IWidgetProvider lifecycle ─────────────────────────────────────────────

    public void CreateWidget(WidgetContext ctx)
    {
        _activeWidgets.AddOrUpdate(ctx.Id, ctx, (_, __) => ctx);
        _logger.LogInformation("Widget created: {Id}", ctx.Id);
        StartRefreshTimer();
        _ = RefreshAsync(ctx.Id);
    }

    public void Activate(WidgetContext ctx)
    {
        _activeWidgets.AddOrUpdate(ctx.Id, ctx, (_, __) => ctx);
        _ = RefreshAsync(ctx.Id);
    }

    public void Deactivate(string widgetId)
    {
        _logger.LogInformation("Widget deactivated: {Id}", widgetId);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        _activeWidgets.TryRemove(widgetId, out _);
        if (_activeWidgets.IsEmpty) StopRefreshTimer();
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs args)
    {
        _activeWidgets.AddOrUpdate(args.WidgetContext.Id, args.WidgetContext, (_, __) => args.WidgetContext);
        _ = RefreshAsync(args.WidgetContext.Id);
    }

    public void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        var verb = args.Verb;
        _logger.LogInformation("Action {Verb}", verb);

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrEmpty(args.Data) ? "{}" : args.Data);

            switch (verb)
            {
                case "next-page":
                    _currentPage++;
                    NotifyRefresh();
                    break;

                case "prev-page":
                    _currentPage = Math.Max(0, _currentPage - 1);
                    NotifyRefresh();
                    break;

                case "refresh":
                case "noop":
                    NotifyRefresh();
                    break;

                case "open-edit":
                    var tileIdStr = doc.RootElement.TryGetProperty("tileId", out var el) ? el.GetString() : null;
                    var tileId = tileIdStr is not null && Guid.TryParse(tileIdStr, out var g) ? g : (Guid?)null;
                    OpenSettingsWindow(tileId);
                    break;

                case "remove-tile":
                    if (doc.RootElement.TryGetProperty("tileId", out var rtEl)
                        && Guid.TryParse(rtEl.GetString(), out var removeId))
                    {
                        _config.RemoveTile(removeId);
                        _currentPage = 0;
                        NotifyRefresh();
                    }
                    break;

                default:
                    _logger.LogWarning("Unknown verb: {Verb}", verb);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling action {Verb}", verb);
        }
    }

    public void NotifyRefresh()
    {
        foreach (var id in _activeWidgets.Keys.ToList())
            _ = RefreshAsync(id);
    }

    // ── Core refresh ──────────────────────────────────────────────────────────

    private async Task RefreshAsync(string widgetId)
    {
        try
        {
            var tiles = GetAllTiles();
            var globalSettings = _config.LoadGlobalSettings();

            var weatherTasks  = tiles.Select(t => _weather.GetWeatherAsync(t.Id, t.Latitude, t.Longitude));
            var currencyTasks = tiles.Select(t => _currency.GetRateAsync(t.Id, t.CurrencyCode, globalSettings.BaseCurrencyCode));
            await Task.WhenAll(weatherTasks.Concat(currencyTasks.Cast<Task>()));

            var weatherMap  = tiles.Zip(await Task.WhenAll(weatherTasks)).ToDictionary(x => x.First.Id, x => x.Second);
            var currencyMap = tiles.Zip(await Task.WhenAll(currencyTasks)).ToDictionary(x => x.First.Id, x => x.Second);

            // Clamp page in case tiles were removed
            const int tilesPerPage = 4;
            int totalPages = Math.Max(1, (int)Math.Ceiling(tiles.Count / (double)tilesPerPage));
            _currentPage = Math.Clamp(_currentPage, 0, totalPages - 1);

            var cardJson = await _cardBuilder.BuildDashboardAsync(
                tiles, weatherMap, currencyMap, _currentPage, _config.GetRemainingSlots());

            PushCard(widgetId, cardJson, string.Join(",", tiles.Select(t => t.Id)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh widget {Id}", widgetId);
        }
    }

    private static void PushCard(string widgetId, string cardJson, string customState = "")
    {
        var opts = new WidgetUpdateRequestOptions(widgetId)
        {
            Template = cardJson,
            Data = "{}",
            CustomState = customState,
        };
        WidgetManager.GetDefault().UpdateWidget(opts);
    }

    private List<LocationTile> GetAllTiles() =>
        _config.GetTileIds()
               .Select(id => _config.LoadTile(id))
               .Where(t => t is not null)
               .Select(t => t!)
               .ToList();

    private void OpenSettingsWindow(Guid? tileId)
    {
        var dispatcher = _wpfDispatcher;
        if (dispatcher is null) return;

        dispatcher.BeginInvoke(() =>
        {
            var tile = tileId.HasValue ? _config.LoadTile(tileId.Value) : null;
            var window = new UI.SettingsWindow(tile);
            window.TileConfigured += (_, saved) => { _config.SaveTile(saved); _currentPage = 0; NotifyRefresh(); };
            window.TileRemoved += (_, id) => { _config.RemoveTile(id); _currentPage = 0; NotifyRefresh(); };
            window.Show();
        });
    }

    private void StartRefreshTimer()
    {
        if (_refreshTimer is not null) return;
        var settings = _config.LoadGlobalSettings();
        var interval = TimeSpan.FromMinutes(settings.DataRefreshIntervalMinutes);
        _refreshTimer = new Timer(_ => NotifyRefresh(), null, interval, interval);
    }

    private void StopRefreshTimer()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }
}
