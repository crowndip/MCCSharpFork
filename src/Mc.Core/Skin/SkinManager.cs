using Mc.Core.Config;

namespace Mc.Core.Skin;

/// <summary>
/// Loads and manages skins (color themes).
/// Equivalent to lib/skin/ in the original C codebase.
/// </summary>
public sealed class SkinManager
{
    private SkinDefinition _active = new();
    private readonly Dictionary<string, SkinDefinition> _skins = [];

    public SkinDefinition Active => _active;

    public SkinManager()
    {
        RegisterBuiltIn();
    }

    private void RegisterBuiltIn()
    {
        _skins["default"] = new SkinDefinition { Name = "default", Description = "Default blue skin" };

        _skins["dark"] = new SkinDefinition
        {
            Name = "dark",
            Description = "Dark skin",
            PanelNormal = new("white", "black"),
            PanelSelected = new("black", "white"),
            PanelHeader = new("brightwhite", "black"),
            DialogNormal = new("white", "black"),
        };

        _skins["monocrome"] = new SkinDefinition
        {
            Name = "monocrome",
            Description = "High contrast monochrome skin",
            PanelNormal = new("white", "black"),
            PanelSelected = new("black", "white"),
            PanelMarked = new("brightwhite", "black"),
            PanelDirectory = new("brightwhite", "black"),
        };
    }

    public void LoadFromFile(string skinFile)
    {
        if (!File.Exists(skinFile)) return;
        var cfg = McConfig.Load(skinFile);
        var skin = new SkinDefinition
        {
            Name = cfg.GetString("skin", "name", Path.GetFileNameWithoutExtension(skinFile)),
            Description = cfg.GetString("skin", "description", string.Empty),
            PanelNormal = ParseColorPair(cfg, "panel", "normal", _active.PanelNormal),
            PanelSelected = ParseColorPair(cfg, "panel", "selected", _active.PanelSelected),
            PanelMarked = ParseColorPair(cfg, "panel", "marked", _active.PanelMarked),
            DialogNormal = ParseColorPair(cfg, "dialog", "normal", _active.DialogNormal),
        };
        _skins[skin.Name] = skin;
    }

    public void LoadDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*.ini"))
            LoadFromFile(f);
    }

    public void Activate(string name)
    {
        if (_skins.TryGetValue(name, out var skin))
            _active = skin;
    }

    public IReadOnlyList<string> GetAvailableSkins() => [.. _skins.Keys];

    private static ColorPair ParseColorPair(McConfig cfg, string section, string key, ColorPair fallback)
    {
        var val = cfg.GetString(section, key);
        if (string.IsNullOrEmpty(val)) return fallback;
        var parts = val.Split(';', 2);
        return parts.Length == 2
            ? new ColorPair(parts[0].Trim(), parts[1].Trim())
            : fallback;
    }
}
