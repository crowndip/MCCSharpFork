using Mc.Core.Utilities;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Change permissions (chmod) dialog.
/// Equivalent to src/filemanager/chmod.c.
/// </summary>
public static class ChmodDialog
{
    public static UnixFileMode? Show(string fileName, UnixFileMode currentMode)
    {
        UnixFileMode? result = null;
        var octal = PermissionsFormatter.FormatOctal(currentMode);

        var d = new Dialog
        {
            Title = "Permissions",
            Width = 50,
            Height = 16,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = $"File: {fileName}" });
        d.Add(new Label { X = 1, Y = 2, Text = $"Current: {PermissionsFormatter.Format(currentMode, false, false)}" });
        d.Add(new Label { X = 1, Y = 4, Text = "Octal permissions:" });

        var octalInput = new TextField
        {
            X = 20, Y = 4, Width = 6, Height = 1,
            Text = octal, ColorScheme = McTheme.Dialog,
        };
        d.Add(octalInput);

        // Permission checkboxes
        var perms = new (string Label, UnixFileMode Bit)[]
        {
            ("User  read",  UnixFileMode.UserRead),
            ("User  write", UnixFileMode.UserWrite),
            ("User  exec",  UnixFileMode.UserExecute),
            ("Group read",  UnixFileMode.GroupRead),
            ("Group write", UnixFileMode.GroupWrite),
            ("Group exec",  UnixFileMode.GroupExecute),
            ("Other read",  UnixFileMode.OtherRead),
            ("Other write", UnixFileMode.OtherWrite),
            ("Other exec",  UnixFileMode.OtherExecute),
        };

        var checkboxes = new CheckBox[perms.Length];
        for (int i = 0; i < perms.Length; i++)
        {
            checkboxes[i] = new CheckBox
            {
                X = 1, Y = 6 + i,
                Text = perms[i].Label,
                CheckedState = (currentMode & perms[i].Bit) != 0 ? CheckState.Checked : CheckState.UnChecked,
                ColorScheme = McTheme.Dialog,
            };
            d.Add(checkboxes[i]);
        }

        var ok = new Button { X = Pos.Center() - 8, Y = 13, Text = "Set", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            // Try octal input first
            var octalStr = octalInput.Text?.ToString() ?? string.Empty;
            if (octalStr.Length > 0 && octalStr.All(c => c >= '0' && c <= '7'))
            {
                result = PermissionsFormatter.ParseOctal(octalStr);
            }
            else
            {
                // Build from checkboxes
                var mode = UnixFileMode.None;
                for (int i = 0; i < perms.Length; i++)
                    if (checkboxes[i].CheckedState == CheckState.Checked) mode |= perms[i].Bit;
                result = mode;
            }
            Application.RequestStop(d);
        };

        var cancel = new Button { X = Pos.Center() + 2, Y = 13, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(ok);
        d.AddButton(cancel);
        Application.Run(d);
        return result;
    }
}
