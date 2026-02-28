using Terminal.Gui;

namespace Mc.Ui.Dialogs;

public sealed class CopyMoveOptions
{
    public string SourceMask { get; set; } = "*";
    public string DestinationPath { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
    public bool UseShellPatterns { get; set; } = true;
    public bool PreserveAttributes { get; set; } = true;
    public bool FollowSymlinks { get; set; }
    public bool DiveIntoSubdir { get; set; }
    public bool StableSymlinks { get; set; }
    public bool OverwriteAll { get; set; }
    public bool SkipAll { get; set; }
    public bool RunInBackground { get; set; }
}

/// <summary>
/// Copy / Move destination dialog.
/// Equivalent to the file mask dialog in src/filemanager/filegui.c.
/// Matches original MC fields:
///   - Source "From:" mask input
///   - Destination "To:" input (pre-filled)
///   - Using shell patterns checkbox
///   - Preserve attributes checkbox
///   - Follow symlinks checkbox
///   - Dive into subdirectory if exists checkbox
///   - Stable symlinks checkbox (Copy only)
/// </summary>
public static class CopyMoveDialog
{
    /// <param name="defaultSource">Default source mask ("*" for multi-file, filename for single-file rename).</param>
    public static CopyMoveOptions? Show(bool isMove, string sourceName, string defaultDest,
                                        string defaultSource = "*")
    {
        CopyMoveOptions? result = null;
        var title = isMove ? "Move" : "Copy";

        var d = new Dialog
        {
            Title = title,
            Width = 70,
            Height = 17,
            ColorScheme = McTheme.Dialog,
        };

        // ── From: (source mask) ─────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 1, Text = $"{title} \"{sourceName}\"" });
        d.Add(new Label { X = 1, Y = 3, Text = "From:" });

        var sourceInput = new TextField
        {
            X = 1, Y = 4, Width = 66, Height = 1,
            Text = defaultSource,
            ColorScheme = McTheme.Dialog,
        };
        sourceInput.CursorPosition = defaultSource.Length;
        d.Add(sourceInput);

        // ── To: (destination) ────────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 6, Text = "To:" });

        var destInput = new TextField
        {
            X = 1, Y = 7, Width = 66, Height = 1,
            Text = defaultDest,
            ColorScheme = McTheme.Dialog,
        };
        destInput.CursorPosition = defaultDest.Length;
        d.Add(destInput);

        // ── Checkboxes ───────────────────────────────────────────────────
        var shellPatternsCb = new CheckBox
        {
            X = 1, Y = 9, Text = "Using shell patterns",
            CheckedState = CheckState.Checked, ColorScheme = McTheme.Dialog,
        };
        var preserveCb = new CheckBox
        {
            X = 1, Y = 10, Text = "Preserve attributes",
            CheckedState = CheckState.Checked, ColorScheme = McTheme.Dialog,
        };
        var followSymCb = new CheckBox
        {
            X = 1, Y = 11, Text = "Follow symlinks",
            CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog,
        };
        var diveCb = new CheckBox
        {
            X = 1, Y = 12, Text = "Dive into subdir if exists",
            CheckedState = CheckState.Checked, ColorScheme = McTheme.Dialog,
        };
        var stableCb = new CheckBox
        {
            X = 1, Y = 13, Text = "Stable symlinks",
            CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog,
        };
        // "Stable symlinks" only relevant for Copy
        stableCb.Enabled = !isMove;

        d.Add(shellPatternsCb, preserveCb, followSymCb, diveCb, stableCb);

        var ok = new Button { Text = isMove ? "Move" : "Copy", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            result = new CopyMoveOptions
            {
                SourceMask         = sourceInput.Text?.ToString() ?? defaultSource,
                DestinationPath    = destInput.Text?.ToString() ?? defaultDest,
                Confirmed          = true,
                UseShellPatterns   = shellPatternsCb.CheckedState == CheckState.Checked,
                PreserveAttributes = preserveCb.CheckedState == CheckState.Checked,
                FollowSymlinks     = followSymCb.CheckedState == CheckState.Checked,
                DiveIntoSubdir     = diveCb.CheckedState == CheckState.Checked,
                StableSymlinks     = stableCb.CheckedState == CheckState.Checked,
            };
            Application.RequestStop(d);
        };

        var background = new Button { Text = "Background" };
        background.Accepting += (_, _) =>
        {
            result = new CopyMoveOptions
            {
                SourceMask         = sourceInput.Text?.ToString() ?? defaultSource,
                DestinationPath    = destInput.Text?.ToString() ?? defaultDest,
                Confirmed          = true,
                RunInBackground    = true,
                UseShellPatterns   = shellPatternsCb.CheckedState == CheckState.Checked,
                PreserveAttributes = preserveCb.CheckedState == CheckState.Checked,
                FollowSymlinks     = followSymCb.CheckedState == CheckState.Checked,
                DiveIntoSubdir     = diveCb.CheckedState == CheckState.Checked,
                StableSymlinks     = stableCb.CheckedState == CheckState.Checked,
            };
            Application.RequestStop(d);
        };

        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(ok);
        d.AddButton(background);
        d.AddButton(cancel);
        destInput.SetFocus();
        Application.Run(d);
        d.Dispose();
        return result;
    }
}
