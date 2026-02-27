using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Change owner / group dialog.
/// Equivalent to src/filemanager/chown.c chown_cmd() in the original C codebase.
/// </summary>
public static class ChownDialog
{
    public sealed record Result(string Owner, string Group);

    public static Result? Show(string fileName, string currentOwner, string currentGroup)
    {
        Result? result = null;

        var d = new Dialog
        {
            Title = "Change Owner",
            Width = 52,
            Height = 12,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = $"File: {fileName}" });

        d.Add(new Label { X = 1, Y = 3, Text = "Owner:" });
        var ownerInput = new TextField
        {
            X = 9, Y = 3,
            Width = 38, Height = 1,
            Text = currentOwner,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(ownerInput);

        d.Add(new Label { X = 1, Y = 5, Text = "Group:" });
        var groupInput = new TextField
        {
            X = 9, Y = 5,
            Width = 38, Height = 1,
            Text = currentGroup,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(groupInput);

        var ok = new Button { X = Pos.Center() - 8, Y = 8, Text = "Set", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            result = new Result(
                ownerInput.Text?.ToString() ?? string.Empty,
                groupInput.Text?.ToString() ?? string.Empty);
            Application.RequestStop(d);
        };

        var cancel = new Button { X = Pos.Center() + 2, Y = 8, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(ok);
        d.AddButton(cancel);
        ownerInput.SetFocus();
        Application.Run(d);
        d.Dispose();
        return result;
    }
}
