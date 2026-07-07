using GlobalTeamWidget.Models;
using GlobalTeamWidget.Services;
using GlobalTeamWidget.Widget;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace GlobalTeamWidget.UI;

public partial class TileManagerWindow : Window
{
    private readonly IConfigurationService _config;
    private readonly GlobalTeamWidgetProvider _provider;
    private readonly ObservableCollection<TileRow> _rows = [];

    public TileManagerWindow(IConfigurationService config, GlobalTeamWidgetProvider provider)
    {
        InitializeComponent();
        _config = config;
        _provider = provider;
        TileList.ItemsSource = _rows;
        Refresh();
    }

    private void Refresh()
    {
        _rows.Clear();
        foreach (var id in _config.GetTileIds())
        {
            var tile = _config.LoadTile(id);
            if (tile is not null)
                _rows.Add(new TileRow(tile.Id, tile.DisplayName, $"{tile.IanaTimezone} · {tile.CurrencyCode}"));
        }

        int remaining = _config.GetRemainingSlots();
        SlotCount.Text = remaining > 0
            ? $"{remaining} slot{(remaining == 1 ? "" : "s")} remaining"
            : "Maximum locations reached";
        AddButton.IsEnabled = remaining > 0;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e) => Close();

    private void OnAddTile(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void OnEditTile(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
            OpenEditor(_config.LoadTile(id));
    }

    private void OnRemoveTile(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            _config.RemoveTile(id);
            _provider.NotifyRefresh();
            Refresh();
        }
    }

    private void OpenEditor(LocationTile? tile)
    {
        var editor = new SettingsWindow(tile);
        editor.TileConfigured += (_, saved) =>
        {
            _config.SaveTile(saved);
            _provider.NotifyRefresh();
            Refresh();
        };
        editor.TileRemoved += (_, id) =>
        {
            _config.RemoveTile(id);
            _provider.NotifyRefresh();
            Refresh();
        };
        editor.Owner = this;
        editor.ShowDialog();
    }

    public record TileRow(Guid TileId, string DisplayName, string SubTitle);
}
