using Mc.Core.Config;

namespace Mc.FileManager;

/// <summary>
/// Manages the directory hotlist (bookmarks) with hierarchical group support.
/// Equivalent to src/filemanager/hotlist.c in the original C codebase.
/// </summary>
public sealed class HotlistManager
{
    // ── Model ────────────────────────────────────────────────────────────────

    public abstract class HotlistItem
    {
        public string Label { get; set; } = string.Empty;
    }

    public sealed class HotlistEntry : HotlistItem
    {
        public string Path { get; set; } = string.Empty;
        public HotlistEntry(string label, string path) { Label = label; Path = path; }
    }

    public sealed class HotlistGroup : HotlistItem
    {
        public List<HotlistItem> Children { get; } = [];
        public HotlistGroup(string label) { Label = label; }
    }

    // ── State ────────────────────────────────────────────────────────────────

    public HotlistGroup Root { get; } = new HotlistGroup(string.Empty);

    /// <summary>Flat list of all entries (for legacy callers).</summary>
    public IReadOnlyList<HotlistEntry> Entries => GetAllEntries(Root);

    public HotlistManager()
    {
        LoadFromFile(ConfigPaths.HotlistFile);
    }

    // ── Flat API (backwards-compat) ──────────────────────────────────────────

    public void Add(string label, string path, string? groupLabel = null)
    {
        var group = groupLabel == null ? Root : FindOrCreateGroup(Root, groupLabel);
        group.Children.RemoveAll(i => i is HotlistEntry e && e.Path == path);
        group.Children.Add(new HotlistEntry(label, path));
        Save();
    }

    public void Remove(string path)
    {
        RemoveEntry(Root, path);
        Save();
    }

    public bool Contains(string path) => Entries.Any(e => e.Path == path);

    public IReadOnlyList<HotlistEntry> GetGroup(string? groupLabel)
    {
        if (groupLabel == null) return Root.Children.OfType<HotlistEntry>().ToList();
        var g = FindGroup(Root, groupLabel);
        return g?.Children.OfType<HotlistEntry>().ToList() ?? [];
    }

    // ── Group API ────────────────────────────────────────────────────────────

    public HotlistGroup AddGroup(HotlistGroup parent, string label)
    {
        var g = new HotlistGroup(label);
        parent.Children.Add(g);
        Save();
        return g;
    }

    public void RemoveItem(HotlistGroup parent, HotlistItem item)
    {
        parent.Children.Remove(item);
        Save();
    }

    public void MoveItem(HotlistGroup source, HotlistGroup dest, HotlistItem item)
    {
        source.Children.Remove(item);
        dest.Children.Add(item);
        Save();
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path)) return;
        Root.Children.Clear();
        var lines = File.ReadAllLines(path);
        int pos = 0;
        ReadGroup(Root, lines, ref pos);
    }

    private static void ReadGroup(HotlistGroup group, string[] lines, ref int pos)
    {
        while (pos < lines.Length)
        {
            var trimmed = lines[pos].Trim();
            pos++;

            if (trimmed.StartsWith("GROUP "))
            {
                var label = trimmed[6..].Trim(' ', '"');
                var sub = new HotlistGroup(label);
                group.Children.Add(sub);
                ReadGroup(sub, lines, ref pos);
            }
            else if (trimmed == "ENDGROUP")
            {
                return;
            }
            else if (trimmed.StartsWith("ENTRY "))
            {
                var parts = ParseQuotedParts(trimmed[6..]);
                if (parts.Count >= 2)
                    group.Children.Add(new HotlistEntry(parts[0], parts[1]));
            }
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPaths.HotlistFile)!);
        using var writer = new StreamWriter(ConfigPaths.HotlistFile);
        WriteGroup(writer, Root, 0);
    }

    private static void WriteGroup(StreamWriter w, HotlistGroup group, int depth)
    {
        var indent = new string(' ', depth * 2);
        foreach (var item in group.Children)
        {
            if (item is HotlistEntry e)
            {
                w.WriteLine($"{indent}ENTRY \"{e.Label}\" \"{e.Path}\"");
            }
            else if (item is HotlistGroup g)
            {
                w.WriteLine($"{indent}GROUP \"{g.Label}\"");
                WriteGroup(w, g, depth + 1);
                w.WriteLine($"{indent}ENDGROUP");
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<HotlistEntry> GetAllEntries(HotlistGroup group)
    {
        var result = new List<HotlistEntry>();
        foreach (var item in group.Children)
        {
            if (item is HotlistEntry e) result.Add(e);
            else if (item is HotlistGroup g) result.AddRange(GetAllEntries(g));
        }
        return result;
    }

    private static bool RemoveEntry(HotlistGroup group, string path)
    {
        for (int i = 0; i < group.Children.Count; i++)
        {
            if (group.Children[i] is HotlistEntry e && e.Path == path)
            { group.Children.RemoveAt(i); return true; }
            if (group.Children[i] is HotlistGroup g && RemoveEntry(g, path))
                return true;
        }
        return false;
    }

    private static HotlistGroup? FindGroup(HotlistGroup group, string label)
    {
        foreach (var item in group.Children)
        {
            if (item is HotlistGroup g)
            {
                if (g.Label == label) return g;
                var found = FindGroup(g, label);
                if (found != null) return found;
            }
        }
        return null;
    }

    private HotlistGroup FindOrCreateGroup(HotlistGroup parent, string label)
    {
        var existing = FindGroup(parent, label);
        if (existing != null) return existing;
        var g = new HotlistGroup(label);
        parent.Children.Add(g);
        return g;
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

file static class ConfigPaths
{
    public static string HotlistFile =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "mc", "hotlist");
}
