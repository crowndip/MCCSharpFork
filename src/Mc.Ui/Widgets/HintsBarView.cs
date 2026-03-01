using Terminal.Gui;

namespace Mc.Ui.Widgets;

/// <summary>
/// Hints (tips) bar shown between the panels and the command line.
/// Displays rotating helpful tips, equivalent to the "Hint:" line in original MC.
/// Equivalent to the hints subsystem in src/filemanager/midnight.c.
/// </summary>
public sealed class HintsBarView : View
{
    private static readonly string[] Tips =
    [
        "Hint: Press F1 for help.",
        "Hint: Use Tab to switch between panels.",
        "Hint: Press '+' to select files matching a pattern.",
        "Hint: Use Ctrl+O to toggle the subshell.",
        "Hint: Press F5 to copy and F6 to move files.",
        "Hint: Use Ctrl+\\ to open the hotlist.",
        "Hint: Press '*' to invert file selection.",
        "Hint: Use Ctrl+Space to calculate directory sizes.",
        "Hint: Press Alt+. to show/hide dotfiles.",
        "Hint: Use F3 to view and F4 to edit a file.",
        "Hint: Press Ctrl+R to rescan the current panel.",
        "Hint: Use F7 to create a new directory.",
        "Hint: Press Insert or Space to mark/unmark a file.",
        "Hint: Use '\\' to deselect all marked files.",
        "Hint: Press Ctrl+F in the viewer to open the next file.",
        "Hint: Use '/' in the viewer to search forward.",
        "Hint: Press Tab in the command line to complete filenames.",
        "Hint: Use Ctrl+A / Ctrl+E to jump to start/end of command line.",
        "Hint: Press Ctrl+K to delete to end of line in the command line.",
        "Hint: Use Ctrl+H or Alt+H to see command history.",
    ];

    private int _tipIndex;
    private readonly Label _label;

    public HintsBarView()
    {
        Height = 1;
        Width  = Dim.Fill();
        ColorScheme = McTheme.StatusBar;
        CanFocus = false;

        _tipIndex = new Random().Next(Tips.Length);
        _label = new Label
        {
            X = 0, Y = 0,
            Width  = Dim.Fill(),
            Height = 1,
            Text   = Tips[_tipIndex],
            ColorScheme = McTheme.StatusBar,
        };
        Add(_label);
    }

    /// <summary>Advance to the next tip (called on panel navigation).</summary>
    public void NextTip()
    {
        _tipIndex = (_tipIndex + 1) % Tips.Length;
        _label.Text = Tips[_tipIndex];
        SetNeedsDraw();
    }
}
