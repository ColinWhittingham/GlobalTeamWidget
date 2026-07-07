using GlobalTeamWidget.Models;

namespace GlobalTeamWidget.Services;

public interface IConfigurationService
{
    GlobalSettings LoadGlobalSettings();
    void SaveGlobalSettings(GlobalSettings settings);

    IReadOnlyList<Guid> GetTileIds();
    LocationTile? LoadTile(Guid id);
    void SaveTile(LocationTile tile);
    void RemoveTile(Guid id);
    int GetRemainingSlots();
}
