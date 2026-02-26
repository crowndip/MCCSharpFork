using Terminal.Gui;

namespace Mc.Ui.Dialogs;

public sealed class CopyMoveOptions
{
    public string DestinationPath { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
    public bool PreserveAttributes { get; set; } = true;
    public bool FollowSymlinks { get; set; }
    public bool OverwriteAll { get; set; }
    public bool SkipAll { get; set; }
}

/// <summary>
/// Copy/Move destination dialog.
/// Equivalent to the copy/move dialog in src/filemanager/filegui.c.
/// </summary>
public static class CopyMoveDialog
{
    public static CopyMoveOptions? Show(bool isMove, string sourceName, string defaultDest)
    {
        CopyMoveOptions? result = null;
        var title = isMove ? "Move" : "Copy";
        var prompt = isMove
            ? $"Move \"{sourceName}\" to:"
            : $"Copy \"{sourceName}\" to:";

        var d = new Dialog
        {
            Title = title,
            Width = 70,
            Height = 12,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = prompt });

        var destInput = new TextField
        {
            X = 1, Y = 3,
            Width = 66,
            Height = 1,
            Text = defaultDest,
            ColorScheme = McTheme.Dialog,
        };
        destInput.CursorPosition = defaultDest.Length;
        d.Add(destInput);

        var preserveCb = new CheckBox
        {
            X = 1, Y = 5,
            Text = "Preserve attributes",
            CheckedState = CheckState.Checked,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(preserveCb);

        var followSymCb = new CheckBox
        {
            X = 1, Y = 6,
            Text = "Follow symlinks",
            CheckedState = CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(followSymCb);

        var ok = new Button { X = Pos.Center() - 12, Y = 9, Text = isMove ? "Move" : "Copy", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            result = new CopyMoveOptions
            {
                DestinationPath = destInput.Text?.ToString() ?? defaultDest,
                Confirmed = true,
                PreserveAttributes = preserveCb.CheckedState == CheckState.Checked,
                FollowSymlinks = followSymCb.CheckedState == CheckState.Checked,
            };
            Application.RequestStop(d);
        };

        var cancel = new Button { X = Pos.Center() + 2, Y = 9, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(ok);
        d.AddButton(cancel);
        destInput.SetFocus();
        Application.Run(d);
        return result;
    }
}
