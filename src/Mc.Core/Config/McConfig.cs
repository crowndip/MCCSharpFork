using System.Globalization;

namespace Mc.Core.Config;

/// <summary>
/// INI-based configuration manager.
/// Replaces GKeyFile from the original C codebase.
/// Config is stored in ~/.config/mc/ini
/// </summary>
public sealed class McConfig
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections = new(StringComparer.OrdinalIgnoreCase);
    private string _filePath = string.Empty;

    public static McConfig Load(string path)
    {
        var cfg = new McConfig { _filePath = path };
        if (File.Exists(path))
            cfg.Parse(File.ReadAllLines(path));
        return cfg;
    }

    public static McConfig LoadDefault()
        => Load(ConfigPaths.MainConfigFile);

    private void Parse(string[] lines)
    {
        string currentSection = "Default";
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            if (!_sections.TryGetValue(currentSection, out var dict))
                _sections[currentSection] = dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dict[key] = value;
        }
    }

    public void Save() => Save(_filePath);

    public void Save(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        using var writer = new StreamWriter(path);
        foreach (var (section, dict) in _sections)
        {
            writer.WriteLine($"[{section}]");
            foreach (var (k, v) in dict)
                writer.WriteLine($"{k}={v}");
            writer.WriteLine();
        }
    }

    // --- Getters ---
    public string GetString(string section, string key, string defaultValue = "")
        => _sections.TryGetValue(section, out var d) && d.TryGetValue(key, out var v) ? v : defaultValue;

    public bool GetBool(string section, string key, bool defaultValue = false)
    {
        var s = GetString(section, key);
        if (string.IsNullOrEmpty(s)) return defaultValue;
        return s is "1" or "true" or "yes" or "on";
    }

    public int GetInt(string section, string key, int defaultValue = 0)
        => int.TryParse(GetString(section, key), out var i) ? i : defaultValue;

    public double GetDouble(string section, string key, double defaultValue = 0)
        => double.TryParse(GetString(section, key), CultureInfo.InvariantCulture, out var d) ? d : defaultValue;

    public IReadOnlyList<string> GetStringList(string section, string key, string separator = ";")
    {
        var val = GetString(section, key);
        return string.IsNullOrEmpty(val)
            ? []
            : val.Split(separator, StringSplitOptions.RemoveEmptyEntries);
    }

    // --- Setters ---
    public void Set(string section, string key, string value)
    {
        if (!_sections.TryGetValue(section, out var d))
            _sections[section] = d = [];
        d[key] = value;
    }

    public void Set(string section, string key, bool value) => Set(section, key, value ? "1" : "0");
    public void Set(string section, string key, int value) => Set(section, key, value.ToString());
    public void Set(string section, string key, double value) => Set(section, key, value.ToString(CultureInfo.InvariantCulture));
    public void Set(string section, string key, IEnumerable<string> values, string separator = ";")
        => Set(section, key, string.Join(separator, values));

    public bool HasKey(string section, string key)
        => _sections.TryGetValue(section, out var d) && d.ContainsKey(key);

    public IReadOnlyList<string> GetSections() => [.. _sections.Keys];
    public IReadOnlyList<string> GetKeys(string section)
        => _sections.TryGetValue(section, out var d) ? [.. d.Keys] : [];
}
