using System.Text.Json;
using Windows.Storage;

namespace GlobalTeamWidget.Services;

public class CacheService
{
    private static StorageFolder CacheFolder => ApplicationData.Current.LocalFolder;

    public async Task<T?> ReadCacheAsync<T>(string fileName) where T : class
    {
        try
        {
            var folder = await CacheFolder.CreateFolderAsync("cache", CreationCollisionOption.OpenIfExists);
            var file = await folder.TryGetItemAsync(fileName) as StorageFile;
            if (file is null) return null;

            var json = await FileIO.ReadTextAsync(file);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task WriteCacheAsync<T>(string fileName, T data)
    {
        try
        {
            var folder = await CacheFolder.CreateFolderAsync("cache", CreationCollisionOption.OpenIfExists);
            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, JsonSerializer.Serialize(data));
        }
        catch
        {
            // Cache writes are best-effort; never throw to caller
        }
    }

    public static string WeatherFileName(Guid tileId) => $"weather_{tileId:N}.json";
    public static string CurrencyFileName(Guid tileId) => $"currency_{tileId:N}.json";
    public static string HolidayFileName(string countryCode, int year) => $"holidays_{countryCode}_{year}.json";
}
