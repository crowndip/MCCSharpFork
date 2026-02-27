using Mc.Core.Utilities;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Change permissions (chmod) dialog.
/// Equivalent to src/filemanager/chmod.c.
/// Shows special bits (setuid/setgid/sticky) plus owner/group/other r/w/x.
/// When markedCount > 1 a "Set all" button is shown matching MC multi-file behaviour.
/// The octal field syncs live with the checkboxes.
/// </summary>
public static class ChmodDialog
{
    public sealed record Result(UnixFileMode Mode, bool ApplyToAll);

    public static Result? Show(string fileName, UnixFileMode currentMode, int markedCount = 1)
    {
        Result? result = null;

        const int W = 54;
        const int COL = 28;   // right column x-offset

        // Permission bit table: label, bit, row
        var perms = new (string Label, UnixFileMode Bit, int Row)[]
        {
            // Special bits (row 1-3)
            ("set uid",      UnixFileMode.SetUser,       1),
            ("set gid",      UnixFileMode.SetGroup,      2),
            ("sticky bit",   UnixFileMode.StickyBit,     3),
            // Owner (row 5-7)
            ("owner read",   UnixFileMode.UserRead,      5),
            ("owner write",  UnixFileMode.UserWrite,     6),
            ("owner exec",   UnixFileMode.UserExecute,   7),
            // Group (row 9-11)
            ("group read",   UnixFileMode.GroupRead,     9),
            ("group write",  UnixFileMode.GroupWrite,    10),
            ("group exec",   UnixFileMode.GroupExecute,  11),
            // Other (row 13-15)
            ("other read",   UnixFileMode.OtherRead,     13),
            ("other write",  UnixFileMode.OtherWrite,    14),
            ("other exec",   UnixFileMode.OtherExecute,  15),
        };

        const int DLG_H = 20;
        var d = new Dialog
        {
            Title = "Permissions",
            Width = W,
            Height = DLG_H,
            ColorScheme = McTheme.Dialog,
        };

        // Right column: file info
        d.Add(new Label { X = COL, Y = 1, Text = "File:" });
        var shortName = fileName.Length > W - COL - 2
            ? "..." + fileName[^(W - COL - 3)..] : fileName;
        d.Add(new Label { X = COL, Y = 2, Text = shortName, ColorScheme = McTheme.Dialog });

        d.Add(new Label { X = COL, Y = 4, Text = "Permissions:" });
        var permLabel = new Label
        {
            X = COL, Y = 5,
            Text = PermissionsFormatter.Format(currentMode, false, false),
            ColorScheme = McTheme.Dialog,
        };
        d.Add(permLabel);

        d.Add(new Label { X = COL, Y = 7, Text = "Octal:" });
        var octalInput = new TextField
        {
            X = COL + 7, Y = 7, Width = 5, Height = 1,
            Text = PermissionsFormatter.FormatOctal(currentMode),
            ColorScheme = McTheme.Dialog,
        };
        d.Add(octalInput);

        // Left column: section headers + checkboxes
        d.Add(new Label { X = 1, Y = 0,  Text = "[ Special ]", ColorScheme = McTheme.Dialog });
        d.Add(new Label { X = 1, Y = 4,  Text = "[ Owner ]",   ColorScheme = McTheme.Dialog });
        d.Add(new Label { X = 1, Y = 8,  Text = "[ Group ]",   ColorScheme = McTheme.Dialog });
        d.Add(new Label { X = 1, Y = 12, Text = "[ Other ]",   ColorScheme = McTheme.Dialog });

        var checkboxes = new CheckBox[perms.Length];
        for (int i = 0; i < perms.Length; i++)
        {
            checkboxes[i] = new CheckBox
            {
                X = 2, Y = perms[i].Row,
                Text = perms[i].Label,
                CheckedState = (currentMode & perms[i].Bit) != 0
                    ? CheckState.Checked : CheckState.UnChecked,
                ColorScheme = McTheme.Dialog,
            };
            d.Add(checkboxes[i]);
        }

        // Sync octal field and permission string whenever a checkbox changes
        void SyncDisplay()
        {
            var m = UnixFileMode.None;
            for (int i = 0; i < perms.Length; i++)
                if (checkboxes[i].CheckedState == CheckState.Checked) m |= perms[i].Bit;
            octalInput.Text = PermissionsFormatter.FormatOctal(m);
            permLabel.Text  = PermissionsFormatter.Format(m, false, false);
        }
        foreach (var cb in checkboxes)
            cb.CheckedStateChanging += (_, _) => Application.AddIdle(() => { SyncDisplay(); return false; });

        // Build mode from UI — prefer octal field if valid
        UnixFileMode BuildMode()
        {
            var octalStr = octalInput.Text?.ToString() ?? string.Empty;
            if (octalStr.Length > 0 && octalStr.All(c => c >= '0' && c <= '7'))
                return PermissionsFormatter.ParseOctal(octalStr);
            var m = UnixFileMode.None;
            for (int i = 0; i < perms.Length; i++)
                if (checkboxes[i].CheckedState == CheckState.Checked) m |= perms[i].Bit;
            return m;
        }

        // "Set all" shown only when multiple files are marked (matches original MC)
        if (markedCount > 1)
        {
            var btnAll = new Button { Text = "Set all" };
            btnAll.Accepting += (_, _) =>
            {
                result = new Result(BuildMode(), ApplyToAll: true);
                Application.RequestStop(d);
            };
            d.AddButton(btnAll);
        }

        var btnSet = new Button { Text = "Set", IsDefault = true };
        btnSet.Accepting += (_, _) =>
        {
            result = new Result(BuildMode(), ApplyToAll: false);
            Application.RequestStop(d);
        };
        var btnCancel = new Button { Text = "Cancel" };
        btnCancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(btnSet);
        d.AddButton(btnCancel);

        Application.Run(d);
        d.Dispose();
        return result;
    }
}
