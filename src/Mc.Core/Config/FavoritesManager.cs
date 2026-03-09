using System.Text.Json;

namespace Mc.Core.Config;

/// <summary>
/// Manages the list of favorite directories stored as a JSON array.
/// File: <see cref="ConfigPaths.FavoritesFile"/>
/// </summary>
public static class FavoritesManager
{
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static List<string> Load()
    {
        var file = ConfigPaths.FavoritesFile;
        if (!File.Exists(file))
            return [];
        try
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<string> favorites)
    {
        ConfigPaths.EnsureDirectoriesExist();
        File.WriteAllText(ConfigPaths.FavoritesFile,
            JsonSerializer.Serialize(favorites.ToList(), _opts));
    }

    /// <summary>
    /// Adds <paramref name="path"/> to favorites if not already present.
    /// Returns true if added, false if already existed.
    /// </summary>
    public static bool Add(string path)
    {
        var list = Load();
        if (list.Contains(path, StringComparer.OrdinalIgnoreCase))
            return false;
        list.Add(path);
        Save(list);
        return true;
    }
}
