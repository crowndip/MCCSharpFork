using Mc.Core.Config;

namespace Mc.FileManager;

/// <summary>
/// Manages the directory hotlist (bookmarks).
/// Equivalent to src/filemanager/hotlist.c in the original C codebase.
/// </summary>
public sealed class HotlistManager
{
    private readonly List<HotlistEntry> _entries = [];

    public record HotlistEntry(string Label, string Path, string? Group = null);

    public IReadOnlyList<HotlistEntry> Entries => _entries.AsReadOnly();

    public HotlistManager()
    {
        LoadFromFile(ConfigPaths.HotlistFile);
    }

    public void Add(string label, string path, string? group = null)
    {
        _entries.RemoveAll(e => e.Path == path);
        _entries.Add(new HotlistEntry(label, path, group));
        Save();
    }

    public void Remove(string path)
    {
        _entries.RemoveAll(e => e.Path == path);
        Save();
    }

    public bool Contains(string path) => _entries.Any(e => e.Path == path);

    public IReadOnlyList<HotlistEntry> GetGroup(string? group)
        => _entries.Where(e => e.Group == group).ToList();

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path)) return;
        _entries.Clear();

        string? currentGroup = null;
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("GROUP "))
            {
                currentGroup = trimmed[6..].Trim(' ', '"');
                continue;
            }
            if (trimmed == "ENDGROUP") { currentGroup = null; continue; }
            if (trimmed.StartsWith("ENTRY "))
            {
                // ENTRY "label" "path"
                var parts = ParseQuotedParts(trimmed[6..]);
                if (parts.Count >= 2)
                    _entries.Add(new HotlistEntry(parts[0], parts[1], currentGroup));
            }
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPaths.HotlistFile)!);
        using var writer = new StreamWriter(ConfigPaths.HotlistFile);
        string? currentGroup = null;
        foreach (var entry in _entries)
        {
            if (entry.Group != currentGroup)
            {
                if (currentGroup != null) writer.WriteLine("ENDGROUP");
                if (entry.Group != null) writer.WriteLine($"GROUP \"{entry.Group}\"");
                currentGroup = entry.Group;
            }
            writer.WriteLine($"ENTRY \"{entry.Label}\" \"{entry.Path}\"");
        }
        if (currentGroup != null) writer.WriteLine("ENDGROUP");
    }

    private static List<string> ParseQuotedParts(string s)
    {
        var result = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            while (i < s.Length && s[i] == ' ') i++;
            if (i >= s.Length) break;
            if (s[i] == '"')
            {
                i++;
                int start = i;
                while (i < s.Length && s[i] != '"') i++;
                result.Add(s[start..i]);
                i++;
            }
            else
            {
                int start = i;
                while (i < s.Length && s[i] != ' ') i++;
                result.Add(s[start..i]);
            }
        }
        return result;
    }
}

// Forward reference to ConfigPaths (defined in Mc.Core)
file static class ConfigPaths
{
    public static string HotlistFile =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "mc", "hotlist");
}
