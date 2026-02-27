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
}
