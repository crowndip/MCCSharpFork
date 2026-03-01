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

    // Date/size filters (#7, #8)
    public int NewerThanDays { get; set; }   // 0 = disabled; files newer than N days
    public int OlderThanDays { get; set; }   // 0 = disabled; files older than N days
    public long MinSizeKB { get; set; }      // 0 = disabled; minimum size in KB
    public long MaxSizeKB { get; set; }      // 0 = disabled; maximum size in KB

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

        // ── Date filters (#7) ─────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 18, Text = "Newer than (days, 0=any):" });
        var newerThanInput = new TextField { X = 26, Y = 18, Width = 6, Text = "0", ColorScheme = McTheme.Dialog };
        d.Add(new Label { X = 34, Y = 18, Text = "Older than (days, 0=any):" });
        var olderThanInput = new TextField { X = 59, Y = 18, Width = 6, Text = "0", ColorScheme = McTheme.Dialog };
        d.Add(newerThanInput, olderThanInput);

        // ── Size filters (#8) ─────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 20, Text = "Min size KB (0=any):" });
        var minSizeInput = new TextField { X = 21, Y = 20, Width = 10, Text = "0", ColorScheme = McTheme.Dialog };
        d.Add(new Label { X = 34, Y = 20, Text = "Max size KB (0=any):" });
        var maxSizeInput = new TextField { X = 55, Y = 20, Width = 10, Text = "0", ColorScheme = McTheme.Dialog };
        d.Add(minSizeInput, maxSizeInput);

        d.Height = 24; // expand dialog to fit new rows

        var ok = new Button { Text = "Find", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            int.TryParse(newerThanInput.Text?.ToString(), out var newerDays);
            int.TryParse(olderThanInput.Text?.ToString(), out var olderDays);
            long.TryParse(minSizeInput.Text?.ToString(), out var minSizeKB);
            long.TryParse(maxSizeInput.Text?.ToString(), out var maxSizeKB);

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
                NewerThanDays   = newerDays,   // #7
                OlderThanDays   = olderDays,   // #7
                MinSizeKB       = minSizeKB,   // #8
                MaxSizeKB       = maxSizeKB,   // #8
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
