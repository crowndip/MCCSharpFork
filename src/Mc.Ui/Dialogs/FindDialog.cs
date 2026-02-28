using Terminal.Gui;

namespace Mc.Ui.Dialogs;

public sealed class FindOptions
{
    public string StartDirectory { get; set; } = string.Empty;
    public string FilePattern { get; set; } = "*";
    public string ContentPattern { get; set; } = string.Empty;
    public bool SearchInSubdirs { get; set; } = true;
    public bool FollowSymlinks { get; set; }
    public bool SkipHiddenDirs { get; set; }
    public bool CaseSensitive { get; set; }
    public bool ContentRegex { get; set; }
    public string IgnoreDirs { get; set; } = string.Empty;  // #33
    public bool Confirmed { get; set; }
}

/// <summary>
/// Find file dialog. Equivalent to find.c in the original C codebase.
/// Adds: editable start directory, Follow symlinks, Skip hidden dirs options.
/// </summary>
public static class FindDialog
{
    public static FindOptions? Show(string startDir)
    {
        FindOptions? result = null;

        var d = new Dialog
        {
            Title = "Find File",
            Width = 70,
            Height = 18,
            ColorScheme = McTheme.Dialog,
        };

        // ── Start directory (editable) ────────────────────────────────
        d.Add(new Label { X = 1, Y = 1, Text = "Start at:" });
        var startInput = new TextField
        {
            X = 1, Y = 2, Width = 66, Height = 1,
            Text = startDir, ColorScheme = McTheme.Dialog,
        };
        startInput.CursorPosition = startDir.Length;
        d.Add(startInput);

        // ── File name pattern ────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 4, Text = "File name pattern:" });
        var filePatternInput = new TextField
        {
            X = 1, Y = 5, Width = 66, Height = 1,
            Text = "*", ColorScheme = McTheme.Dialog,
        };
        d.Add(filePatternInput);

        // ── Content search ────────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 7, Text = "Content (leave blank to skip):" });
        var contentInput = new TextField
        {
            X = 1, Y = 8, Width = 66, Height = 1,
            Text = string.Empty, ColorScheme = McTheme.Dialog,
        };
        d.Add(contentInput);

        // ── Options ───────────────────────────────────────────────────
        var subDirsCb     = new CheckBox { X = 1,  Y = 10, Text = "Search in subdirectories", CheckedState = CheckState.Checked,   ColorScheme = McTheme.Dialog };
        var followSymCb   = new CheckBox { X = 1,  Y = 11, Text = "Follow symlinks",          CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog };
        var skipHiddenCb  = new CheckBox { X = 1,  Y = 12, Text = "Skip hidden directories",  CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog };
        var caseCb        = new CheckBox { X = 1,  Y = 13, Text = "Case sensitive",            CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog };
        var regexCb       = new CheckBox { X = 35, Y = 13, Text = "Use regex",                 CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog };
        d.Add(subDirsCb, followSymCb, skipHiddenCb, caseCb, regexCb);

        // ── Ignore dirs (#33) ─────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 15, Text = "Ignore dirs (colon-separated):" });
        var ignoreDirsInput = new TextField { X = 1, Y = 16, Width = 66, Height = 1, Text = string.Empty, ColorScheme = McTheme.Dialog };
        d.Add(ignoreDirsInput);

        d.Height = 20; // expand dialog to fit new row

        var ok = new Button { Text = "Find", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            result = new FindOptions
            {
                StartDirectory  = startInput.Text?.ToString() ?? startDir,
                FilePattern     = filePatternInput.Text?.ToString() ?? "*",
                ContentPattern  = contentInput.Text?.ToString() ?? string.Empty,
                SearchInSubdirs = subDirsCb.CheckedState    == CheckState.Checked,
                FollowSymlinks  = followSymCb.CheckedState  == CheckState.Checked,
                SkipHiddenDirs  = skipHiddenCb.CheckedState == CheckState.Checked,
                CaseSensitive   = caseCb.CheckedState       == CheckState.Checked,
                ContentRegex    = regexCb.CheckedState      == CheckState.Checked,
                IgnoreDirs      = ignoreDirsInput.Text?.ToString() ?? string.Empty, // #33
                Confirmed = true,
            };
            Application.RequestStop(d);
        };

        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(ok);
        d.AddButton(cancel);
        filePatternInput.SetFocus();
        Application.Run(d);
        d.Dispose();
        return result;
    }
}
