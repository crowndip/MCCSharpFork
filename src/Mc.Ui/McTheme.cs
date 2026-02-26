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

        // Apply to global colors
        Colors.ColorSchemes["Base"]   = Panel;
        Colors.ColorSchemes["Dialog"] = Dialog;
        Colors.ColorSchemes["Menu"]   = Menu;
        Colors.ColorSchemes["Error"]  = Error;
    }
}
