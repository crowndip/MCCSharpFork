using Terminal.Gui;

namespace Mc.Ui.Dialogs;

public sealed class CopyMoveOptions
{
    public string DestinationPath { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
    public bool PreserveAttributes { get; set; } = true;
    public bool FollowSymlinks { get; set; }
    public bool DiveIntoSubdir { get; set; }
    public bool StableSymlinks { get; set; }
    public bool OverwriteAll { get; set; }
    public bool SkipAll { get; set; }
}

/// <summary>
/// Copy / Move destination dialog.
/// Equivalent to the file mask dialog in src/filemanager/filegui.c.
/// Matches original MC fields:
///   - Destination "to:" input (pre-filled)
///   - Preserve attributes checkbox
///   - Follow symlinks checkbox
///   - Dive into subdirectory if exists checkbox
///   - Stable symlinks checkbox
/// </summary>
public static class CopyMoveDialog
{
    public static CopyMoveOptions? Show(bool isMove, string sourceName, string defaultDest)
    {
        CopyMoveOptions? result = null;
        var title  = isMove ? "Move" : "Copy";
        var prompt = isMove
            ? $"Move \"{sourceName}\" to:"
            : $"Copy \"{sourceName}\" to:";

        var d = new Dialog
        {
            Title = title,
            Width = 70,
            Height = 14,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = prompt });

        var destInput = new TextField
        {
            X = 1, Y = 2, Width = 66, Height = 1,
            Text = defaultDest,
            ColorScheme = McTheme.Dialog,
        };
        destInput.CursorPosition = defaultDest.Length;
        d.Add(destInput);

        // Checkboxes matching original MC file mask dialog options
        var preserveCb = new CheckBox
        {
            X = 1, Y = 4, Text = "Preserve attributes",
            CheckedState = CheckState.Checked, ColorScheme = McTheme.Dialog,
        };
        var followSymCb = new CheckBox
        {
            X = 1, Y = 5, Text = "Follow symlinks",
            CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog,
        };
        var diveCb = new CheckBox
        {
            X = 1, Y = 6, Text = "Dive into subdir if exists",
            CheckedState = CheckState.Checked, ColorScheme = McTheme.Dialog,
        };
        var stableCb = new CheckBox
        {
            X = 1, Y = 7, Text = "Stable symlinks",
            CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog,
        };
        // "Stable symlinks" only relevant for Copy
        stableCb.Enabled = !isMove;

        d.Add(preserveCb, followSymCb, diveCb, stableCb);

        var ok = new Button { Text = isMove ? "Move" : "Copy", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            result = new CopyMoveOptions
            {
                DestinationPath  = destInput.Text?.ToString() ?? defaultDest,
                Confirmed        = true,
                PreserveAttributes = preserveCb.CheckedState == CheckState.Checked,
                FollowSymlinks   = followSymCb.CheckedState == CheckState.Checked,
                DiveIntoSubdir   = diveCb.CheckedState == CheckState.Checked,
                StableSymlinks   = stableCb.CheckedState == CheckState.Checked,
            };
            Application.RequestStop(d);
        };

        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(ok);
        d.AddButton(cancel);
        destInput.SetFocus();
        Application.Run(d);
        d.Dispose();
        return result;
    }
}
