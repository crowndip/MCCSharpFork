using Terminal.Gui;

namespace Mc.Ui;

/// <summary>
/// Maps the mc skin color definitions to Terminal.Gui ColorSchemes.
/// </summary>
public static class McTheme
{
    public static ColorScheme Panel { get; private set; } = new();
    public static ColorScheme PanelSelected { get; private set; } = new();
    public static ColorScheme Dialog { get; private set; } = new();
    public static ColorScheme Menu { get; private set; } = new();
    public static ColorScheme StatusBar { get; private set; } = new();
    public static ColorScheme ButtonBar { get; private set; } = new();
    public static ColorScheme Error { get; private set; } = new();

    // Low-level drawing attributes for the file panel (matches MC default.ini skin)
    public static Terminal.Gui.Attribute PanelFrame { get; private set; }
    public static Terminal.Gui.Attribute PanelHeader { get; private set; }
    public static Terminal.Gui.Attribute PanelFile { get; private set; }
    public static Terminal.Gui.Attribute PanelDirectory { get; private set; }
    public static Terminal.Gui.Attribute PanelExecutable { get; private set; }
    public static Terminal.Gui.Attribute PanelSymlink { get; private set; }
    public static Terminal.Gui.Attribute PanelMarked { get; private set; }
    public static Terminal.Gui.Attribute PanelCursor { get; private set; }
    public static Terminal.Gui.Attribute PanelMarkedCursor { get; private set; }
    public static Terminal.Gui.Attribute PanelStatus { get; private set; }

    public static void ApplyDefault()
    {
        // Classic MC blue skin
        Panel = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.White, Color.Blue),
            Focus     = new Terminal.Gui.Attribute(Color.Black,       Color.Cyan),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow,Color.Blue),
            HotFocus  = new Terminal.Gui.Attribute(Color.BrightYellow,Color.Cyan),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,        Color.Blue),
        };

        PanelSelected = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.Black,        Color.Cyan),
            Focus     = new Terminal.Gui.Attribute(Color.Black,        Color.Cyan),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Cyan),
            HotFocus  = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Cyan),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,         Color.Cyan),
        };

        Dialog = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.Black,       Color.White),
            Focus     = new Terminal.Gui.Attribute(Color.White, Color.Blue),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightRed,   Color.White),
            HotFocus  = new Terminal.Gui.Attribute(Color.BrightRed,   Color.Blue),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,        Color.White),
        };

        Menu = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.Black,       Color.Cyan),
            Focus     = new Terminal.Gui.Attribute(Color.Black,       Color.White),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightRed,   Color.Cyan),
            HotFocus  = new Terminal.Gui.Attribute(Color.BrightRed,   Color.White),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,        Color.Cyan),
        };

        StatusBar = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.Black, Color.Cyan),
            Focus     = new Terminal.Gui.Attribute(Color.Black, Color.Cyan),
            HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.Cyan),
            HotFocus  = new Terminal.Gui.Attribute(Color.Black, Color.Cyan),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,  Color.Cyan),
        };

        ButtonBar = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.Black,       Color.Cyan),
            Focus     = new Terminal.Gui.Attribute(Color.Black,       Color.Cyan),
            HotNormal = new Terminal.Gui.Attribute(Color.White, Color.Black),
            HotFocus  = new Terminal.Gui.Attribute(Color.White, Color.Black),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,        Color.Black),
        };

        Error = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.White, Color.Red),
            Focus     = new Terminal.Gui.Attribute(Color.BrightYellow,Color.Red),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow,Color.Red),
            HotFocus  = new Terminal.Gui.Attribute(Color.BrightYellow,Color.Red),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,        Color.Red),
        };

        // Panel drawing attributes (from MC default.ini skin)
        // _default_=lightgray;blue  selected=black;cyan  marked=yellow;blue
        // header=yellow;blue  frame=lightgray;blue
        PanelFrame        = new Terminal.Gui.Attribute(Color.Gray,         Color.Blue);
        PanelHeader       = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Blue);
        PanelFile         = new Terminal.Gui.Attribute(Color.Gray,         Color.Blue);
        PanelDirectory    = new Terminal.Gui.Attribute(Color.White,        Color.Blue);
        PanelExecutable   = new Terminal.Gui.Attribute(Color.BrightGreen,  Color.Blue);
        PanelSymlink      = new Terminal.Gui.Attribute(Color.Cyan,         Color.Blue);
        PanelMarked       = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Blue);
        PanelCursor       = new Terminal.Gui.Attribute(Color.Black,        Color.Cyan);
        PanelMarkedCursor = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Cyan);
        PanelStatus       = new Terminal.Gui.Attribute(Color.Black,        Color.Cyan);

        // Apply to global colors
        Colors.ColorSchemes["Base"]   = Panel;
        Colors.ColorSchemes["Dialog"] = Dialog;
        Colors.ColorSchemes["Menu"]   = Menu;
        Colors.ColorSchemes["Error"]  = Error;
    }

    // ── Skin file support ─────────────────────────────────────────────────────
    // Parses MC-format INI skin files (from /usr/share/mc/skins/ or
    // ~/.local/share/mc/skins/) and applies colors to the static theme.
    // Equivalent to mc_skin_init() / mc_skin_color_get() in lib/skin/.

    public static IReadOnlyList<string> FindSkinFiles()
    {
        var dirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                         ".local", "share", "mc", "skins"),
            "/usr/share/mc/skins",
            "/usr/local/share/mc/skins",
        };
        var result = new List<string>();
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            result.AddRange(Directory.GetFiles(dir, "*.ini").OrderBy(f => f));
        }
        return result;
    }

    public static bool ApplySkin(string skinFilePath)
    {
        if (!File.Exists(skinFilePath)) return false;

        // Parse simple INI: [section] and key=value lines
        var sections = new Dictionary<string, Dictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        foreach (var raw in File.ReadAllLines(skinFilePath))
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                if (!sections.ContainsKey(currentSection))
                    sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            if (currentSection == null || line.StartsWith('#') || !line.Contains('=')) continue;
            var eq  = line.IndexOf('=');
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            sections[currentSection][key] = val;
        }

        Color? Get(string section, string key)
        {
            if (!sections.TryGetValue(section, out var s)) return null;
            if (!s.TryGetValue(key, out var v)) return null;
            return ParseMcColor(v);
        }

        (Color fg, Color bg) Pair(string section, string key, Color defFg, Color defBg)
        {
            if (!sections.TryGetValue(section, out var s)) return (defFg, defBg);
            if (!s.TryGetValue(key, out var v)) return (defFg, defBg);
            var parts = v.Split(';');
            var fg = parts.Length > 0 ? ParseMcColor(parts[0].Trim()) ?? defFg : defFg;
            var bg = parts.Length > 1 ? ParseMcColor(parts[1].Trim()) ?? defBg : defBg;
            return (fg, bg);
        }

        var (pFg, pBg) = Pair("core",      "_default_", Color.Gray,         Color.Blue);
        var (sFg, sBg) = Pair("core",      "selected",  Color.Black,        Color.Cyan);
        var (mFg, mBg) = Pair("menu",      "_default_", Color.Black,        Color.Cyan);
        var (msFg,msBg)= Pair("menu",      "selected",  Color.Black,        Color.White);
        var (dFg, dBg) = Pair("dialog",    "_default_", Color.Black,        Color.White);
        var (dsFg,dsBg)= Pair("dialog",    "selected",  Color.White,        Color.Blue);
        var (stFg,stBg)= Pair("statusbar", "_default_", Color.Black,        Color.Cyan);
        var (bbFg,bbBg)= Pair("buttonbar", "button",    Color.Black,        Color.Cyan);
        var (erFg,erBg)= Pair("error",     "_default_", Color.White,        Color.Red);
        var (mkFg, _)  = Pair("panel",     "marked",    Color.BrightYellow, pBg);
        var (exFg, _)  = Pair("panel",     "executable",Color.BrightGreen,  pBg);
        var (syFg, _)  = Pair("panel",     "link",      Color.Cyan,         pBg);
        var (hdFg, _)  = Pair("panel",     "header",    Color.BrightYellow, pBg);
        var (drFg, _)  = Pair("panel",     "directory", Color.White,        pBg);

        Panel = MakeScheme(pFg, pBg, sFg, sBg);
        Dialog = MakeScheme(dFg, dBg, dsFg, dsBg);
        Menu = MakeScheme(mFg, mBg, msFg, msBg);
        StatusBar = MakeScheme(stFg, stBg, stFg, stBg);
        ButtonBar = MakeScheme(bbFg, bbBg, bbFg, bbBg);
        Error = MakeScheme(erFg, erBg, Color.BrightYellow, erBg);

        PanelFrame        = new Terminal.Gui.Attribute(pFg,  pBg);
        PanelHeader       = new Terminal.Gui.Attribute(hdFg, pBg);
        PanelFile         = new Terminal.Gui.Attribute(pFg,  pBg);
        PanelDirectory    = new Terminal.Gui.Attribute(drFg, pBg);
        PanelExecutable   = new Terminal.Gui.Attribute(exFg, pBg);
        PanelSymlink      = new Terminal.Gui.Attribute(syFg, pBg);
        PanelMarked       = new Terminal.Gui.Attribute(mkFg, pBg);
        PanelCursor       = new Terminal.Gui.Attribute(sFg,  sBg);
        PanelMarkedCursor = new Terminal.Gui.Attribute(mkFg, sBg);
        PanelStatus       = new Terminal.Gui.Attribute(stFg, stBg);

        Colors.ColorSchemes["Base"]   = Panel;
        Colors.ColorSchemes["Dialog"] = Dialog;
        Colors.ColorSchemes["Menu"]   = Menu;
        Colors.ColorSchemes["Error"]  = Error;
        return true;
    }

    private static ColorScheme MakeScheme(Color fg, Color bg, Color focusFg, Color focusBg) =>
        new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(fg,          bg),
            Focus     = new Terminal.Gui.Attribute(focusFg,     focusBg),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightRed, bg),
            HotFocus  = new Terminal.Gui.Attribute(Color.BrightRed, focusBg),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,  bg),
        };

    private static Color? ParseMcColor(string name) => name.ToLowerInvariant() switch
    {
        "black"                    => Color.Black,
        "red"                      => Color.Red,
        "green"                    => Color.Green,
        "brown" or "yellow"        => Color.Yellow,
        "navy" or "blue"           => Color.Blue,
        "magenta" or "purple"      => Color.Magenta,
        "cyan" or "teal"           => Color.Cyan,
        "lightgray" or "gray" or "silver" => Color.Gray,
        "darkgray"                 => Color.DarkGray,
        "brightred"  or "lightred"        => Color.BrightRed,
        "brightgreen" or "lightgreen"     => Color.BrightGreen,
        "brightyellow" or "lightyellow"   => Color.BrightYellow,
        "brightblue"  or "lightblue"      => Color.BrightBlue,
        "brightmagenta" or "lightmagenta" => Color.BrightMagenta,
        "brightcyan" or "lightcyan"       => Color.BrightCyan,
        "white" or "brightwhite"          => Color.White,
        _ => null,
    };
}
