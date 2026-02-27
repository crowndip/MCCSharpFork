using Mc.Core.Utilities;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Combined owner/group + permissions dialog.
/// Equivalent to src/filemanager/achown.c in the original MC —
/// shows owner/group listboxes (left) and rwx permission checkboxes (right)
/// in a single dialog, applying both in one action.
/// </summary>
public static class AdvancedChownDialog
{
    public sealed record Result(string Owner, string Group, UnixFileMode Mode, bool ApplyToAll);

    public static Result? Show(string fileName, string currentOwner, string currentGroup,
                               UnixFileMode currentMode, int markedCount = 1)
    {
        var users  = ReadNames("/etc/passwd");
        var groups = ReadNames("/etc/group");

        Result? result = null;

        var d = new Dialog
        {
            Title       = $"Advanced Chown: {TruncateName(fileName, 40)}",
            Width       = 76,
            Height      = 22,
            ColorScheme = McTheme.Dialog,
        };

        // ── Left column: Owner ──────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 1, Text = "Owner name:" });
        var ownerInput = new TextField
        {
            X = 1, Y = 2, Width = 18, Height = 1,
            Text = currentOwner, ColorScheme = McTheme.Dialog,
        };
        d.Add(ownerInput);

        var ownerLv = new ListView
        {
            X = 1, Y = 3, Width = 18, Height = 8,
            ColorScheme = McTheme.Panel,
        };
        ownerLv.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(users));
        var ownerIdx = users.IndexOf(currentOwner);
        if (ownerIdx >= 0) ownerLv.SelectedItem = ownerIdx;
        d.Add(ownerLv);

        ownerLv.SelectedItemChanged += (_, a) =>
        {
            var idx = (int)a.Value;
            if (idx >= 0 && idx < users.Count) ownerInput.Text = users[idx];
        };

        // ── Middle column: Group ────────────────────────────────────────
        d.Add(new Label { X = 21, Y = 1, Text = "Group name:" });
        var groupInput = new TextField
        {
            X = 21, Y = 2, Width = 18, Height = 1,
            Text = currentGroup, ColorScheme = McTheme.Dialog,
        };
        d.Add(groupInput);

        var groupLv = new ListView
        {
            X = 21, Y = 3, Width = 18, Height = 8,
            ColorScheme = McTheme.Panel,
        };
        groupLv.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(groups));
        var groupIdx = groups.IndexOf(currentGroup);
        if (groupIdx >= 0) groupLv.SelectedItem = groupIdx;
        d.Add(groupLv);

        groupLv.SelectedItemChanged += (_, a) =>
        {
            var idx = (int)a.Value;
            if (idx >= 0 && idx < groups.Count) groupInput.Text = groups[idx];
        };

        // ── Right column: Permissions ────────────────────────────────────
        d.Add(new Label { X = 41, Y = 1, Text = "[ Special ]" });
        var suCb  = new CheckBox { X = 41, Y = 2,  Text = "Set UID",  ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.SetUser)    ? CheckState.Checked : CheckState.UnChecked };
        var sgCb  = new CheckBox { X = 41, Y = 3,  Text = "Set GID",  ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.SetGroup)   ? CheckState.Checked : CheckState.UnChecked };
        var stCb  = new CheckBox { X = 41, Y = 4,  Text = "Sticky",   ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.StickyBit)  ? CheckState.Checked : CheckState.UnChecked };

        d.Add(new Label { X = 41, Y = 5,  Text = "[ Owner ]" });
        var urCb  = new CheckBox { X = 41, Y = 6,  Text = "Read",    ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.UserRead)    ? CheckState.Checked : CheckState.UnChecked };
        var uwCb  = new CheckBox { X = 41, Y = 7,  Text = "Write",   ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.UserWrite)   ? CheckState.Checked : CheckState.UnChecked };
        var uxCb  = new CheckBox { X = 41, Y = 8,  Text = "Execute", ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.UserExecute) ? CheckState.Checked : CheckState.UnChecked };

        d.Add(new Label { X = 57, Y = 5,  Text = "[ Group ]" });
        var grCb  = new CheckBox { X = 57, Y = 6,  Text = "Read",    ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.GroupRead)    ? CheckState.Checked : CheckState.UnChecked };
        var gwCb  = new CheckBox { X = 57, Y = 7,  Text = "Write",   ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.GroupWrite)   ? CheckState.Checked : CheckState.UnChecked };
        var gxCb  = new CheckBox { X = 57, Y = 8,  Text = "Execute", ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.GroupExecute) ? CheckState.Checked : CheckState.UnChecked };

        d.Add(new Label { X = 41, Y = 9,  Text = "[ Other ]" });
        var orCb  = new CheckBox { X = 41, Y = 10, Text = "Read",    ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.OtherRead)    ? CheckState.Checked : CheckState.UnChecked };
        var owCb  = new CheckBox { X = 41, Y = 11, Text = "Write",   ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.OtherWrite)   ? CheckState.Checked : CheckState.UnChecked };
        var oxCb  = new CheckBox { X = 41, Y = 12, Text = "Execute", ColorScheme = McTheme.Dialog, CheckedState = currentMode.HasFlag(UnixFileMode.OtherExecute) ? CheckState.Checked : CheckState.UnChecked };

        d.Add(suCb, sgCb, stCb, urCb, uwCb, uxCb, grCb, gwCb, gxCb, orCb, owCb, oxCb);

        // Octal display
        d.Add(new Label { X = 41, Y = 14, Text = "Octal:", ColorScheme = McTheme.Dialog });
        var octalInput = new TextField
        {
            X = 48, Y = 14, Width = 6, Height = 1,
            Text = PermissionsFormatter.FormatOctal(currentMode),
            ColorScheme = McTheme.Dialog,
        };
        d.Add(octalInput);

        // Live sync: checkboxes → octal
        UnixFileMode ReadCheckboxes() =>
            (suCb.CheckedState  == CheckState.Checked ? UnixFileMode.SetUser      : 0) |
            (sgCb.CheckedState  == CheckState.Checked ? UnixFileMode.SetGroup     : 0) |
            (stCb.CheckedState  == CheckState.Checked ? UnixFileMode.StickyBit    : 0) |
            (urCb.CheckedState  == CheckState.Checked ? UnixFileMode.UserRead     : 0) |
            (uwCb.CheckedState  == CheckState.Checked ? UnixFileMode.UserWrite    : 0) |
            (uxCb.CheckedState  == CheckState.Checked ? UnixFileMode.UserExecute  : 0) |
            (grCb.CheckedState  == CheckState.Checked ? UnixFileMode.GroupRead    : 0) |
            (gwCb.CheckedState  == CheckState.Checked ? UnixFileMode.GroupWrite   : 0) |
            (gxCb.CheckedState  == CheckState.Checked ? UnixFileMode.GroupExecute : 0) |
            (orCb.CheckedState  == CheckState.Checked ? UnixFileMode.OtherRead    : 0) |
            (owCb.CheckedState  == CheckState.Checked ? UnixFileMode.OtherWrite   : 0) |
            (oxCb.CheckedState  == CheckState.Checked ? UnixFileMode.OtherExecute : 0);

        void OnCbChanged(object? s, EventArgs _)
        {
            Application.AddIdle(() =>
            {
                octalInput.Text = PermissionsFormatter.FormatOctal(ReadCheckboxes());
                return false;
            });
        }
        foreach (var cb in new[] { suCb, sgCb, stCb, urCb, uwCb, uxCb, grCb, gwCb, gxCb, orCb, owCb, oxCb })
            cb.CheckedStateChanging += OnCbChanged;

        // ── Buttons ─────────────────────────────────────────────────────
        var ok = new Button { Text = "Set", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            var mode = PermissionsFormatter.ParseOctal(octalInput.Text?.ToString() ?? string.Empty);
            result = new Result(
                ownerInput.Text?.ToString() ?? currentOwner,
                groupInput.Text?.ToString() ?? currentGroup,
                mode,
                false);
            Application.RequestStop(d);
        };

        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        if (markedCount > 1)
        {
            var allBtn = new Button { Text = "Set all" };
            allBtn.Accepting += (_, _) =>
            {
                var mode = PermissionsFormatter.ParseOctal(octalInput.Text?.ToString() ?? string.Empty);
                result = new Result(
                    ownerInput.Text?.ToString() ?? currentOwner,
                    groupInput.Text?.ToString() ?? currentGroup,
                    mode,
                    true);
                Application.RequestStop(d);
            };
            d.AddButton(allBtn);
        }

        d.AddButton(ok);
        d.AddButton(cancel);
        ownerInput.SetFocus();
        Application.Run(d);
        d.Dispose();
        return result;
    }

    private static List<string> ReadNames(string path)
    {
        var names = new List<string>();
        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split(':');
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                    names.Add(parts[0]);
            }
        }
        catch { /* file unreadable on some systems */ }
        return names;
    }

    private static string TruncateName(string name, int max)
        => name.Length > max ? name[..(max - 1)] + "…" : name;
}
