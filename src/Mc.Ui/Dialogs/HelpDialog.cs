using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>Built-in help dialog. Equivalent to src/help.c.</summary>
public static class HelpDialog
{
    private const string HelpText = """
        Midnight Commander for .NET — Keyboard Reference
        ==================================================

        PANEL NAVIGATION
          Arrow keys, PgUp/PgDn   Move cursor
          Tab / Shift+Tab         Switch between panels
          Ctrl+R                  Refresh panels
          Ctrl+U                  Swap panel directories
          Backspace               Go to parent directory
          ~                       Go to home directory
          Ctrl+\                  Toggle hidden files

        FILE OPERATIONS
          F3 or Enter             View selected file
          F4                      Edit selected file
          F5                      Copy marked files to other panel
          F6                      Move/rename marked files
          F7                      Create directory
          F8 or Delete            Delete marked files
          Ins or Space            Mark/unmark file
          +                       Mark files by pattern
          *                       Invert marking
          \                       Unmark all

        SEARCH & NAVIGATION
          F7 (in viewer/editor)   Find / Search
          Ctrl+S                  Sort dialog
          Alt+H                   Directory history

        GENERAL
          F1                      This help
          F9 or Esc               Main menu
          F10 or Ctrl+C           Quit
          Ctrl+O                  Shell (suspend mc)
          Ctrl+L                  Refresh screen
    """;

    public static void Show()
    {
        var d = new Dialog
        {
            Title = "Help",
            Width = Application.Driver.Cols - 4,
            Height = Application.Driver.Rows - 4,
            ColorScheme = McTheme.Dialog,
        };

        var textView = new TextView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(3),
            Text = HelpText,
            ReadOnly = true,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(textView);

        var ok = new Button { X = Pos.Center(), Y = Pos.Bottom(textView), Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok);
        Application.Run(d);
    }
}
