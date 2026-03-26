using System.Text.Json;

namespace Mc.Core.Config;

/// <summary>
/// Manages the list of recently visited directories stored as a JSON array.
/// Newest entries are first; the list is capped at <see cref="MaxEntries"/> items.
/// File: <see cref="ConfigPaths.RecentFile"/>
/// </summary>
public static class RecentDirectoriesManager
{
    public const int MaxEntries = 20;

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static List<string> Load()
    {
        var file = ConfigPaths.RecentFile;
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

    public static void Save(IEnumerable<string> recent)
    {
        ConfigPaths.EnsureDirectoriesExist();
        File.WriteAllText(ConfigPaths.RecentFile,
            JsonSerializer.Serialize(recent.ToList(), _opts));
    }

    /// <summary>
    /// Adds <paramref name="path"/> to the front of the recent list.
    /// Removes any existing duplicate, then trims to <see cref="MaxEntries"/>.
    /// Returns true if the list was modified (i.e. the path was new or moved).
    /// </summary>
    public static bool Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var list = Load();

        // Remove existing occurrence (case-insensitive) so we can move it to front
        int existing = list.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (existing == 0)
            return false; // already the most recent — nothing to do

        if (existing > 0)
            list.RemoveAt(existing);

        list.Insert(0, path);

        if (list.Count > MaxEntries)
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);

        Save(list);
        return true;
    }
}
