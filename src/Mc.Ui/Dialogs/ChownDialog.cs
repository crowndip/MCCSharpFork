using System.Collections.ObjectModel;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Change owner / group dialog.
/// Equivalent to src/filemanager/chown.c chown_cmd().
/// Shows listboxes populated from /etc/passwd and /etc/group so the user can
/// pick an existing user/group — matching the original MC design.
/// Also has "Set all" for multi-file operations.
/// </summary>
public static class ChownDialog
{
    public sealed record Result(string Owner, string Group, bool ApplyToAll);

    public static Result? Show(string fileName, string currentOwner, string currentGroup, int markedCount = 1)
    {
        Result? result = null;

        // Read system users and groups
        var users  = ReadNames("/etc/passwd");
        var groups = ReadNames("/etc/group");

        const int W = 64;
        const int LBH = 8;   // listbox height

        var d = new Dialog
        {
            Title = "Chown",
            Width = W,
            Height = LBH * 2 + 12,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 0, Text = $"File: {TruncateName(fileName, W - 8)}" });

        // Owner listbox
        d.Add(new Label { X = 1, Y = 2, Text = "User name" });
        var ownerLv = new ListView
        {
            X = 1, Y = 3, Width = W / 2 - 2, Height = LBH,
            ColorScheme = McTheme.Panel,
        };
        ownerLv.SetSource(new ObservableCollection<string>(users));
        int ownerIdx = users.IndexOf(currentOwner);
        if (ownerIdx >= 0) ownerLv.SelectedItem = ownerIdx;
        d.Add(ownerLv);

        // Group listbox
        d.Add(new Label { X = W / 2 + 1, Y = 2, Text = "Group name" });
        var groupLv = new ListView
        {
            X = W / 2 + 1, Y = 3, Width = W / 2 - 3, Height = LBH,
            ColorScheme = McTheme.Panel,
        };
        groupLv.SetSource(new ObservableCollection<string>(groups));
        int groupIdx = groups.IndexOf(currentGroup);
        if (groupIdx >= 0) groupLv.SelectedItem = groupIdx;
        d.Add(groupLv);

        // Current owner/group text fields (editable fallback if name not in list)
        int row = LBH + 4;
        d.Add(new Label { X = 1, Y = row, Text = "Owner:" });
        var ownerInput = new TextField
        {
            X = 8, Y = row, Width = W / 2 - 9, Height = 1,
            Text = currentOwner, ColorScheme = McTheme.Dialog,
        };
        d.Add(ownerInput);

        d.Add(new Label { X = W / 2 + 1, Y = row, Text = "Group:" });
        var groupInput = new TextField
        {
            X = W / 2 + 8, Y = row, Width = W / 2 - 9, Height = 1,
            Text = currentGroup, ColorScheme = McTheme.Dialog,
        };
        d.Add(groupInput);

        // Selecting from listbox updates the text field
        ownerLv.SelectedItemChanged += (_, a) =>
        {
            var idx = (int)a.Value;
            if (idx >= 0 && idx < users.Count) ownerInput.Text = users[idx];
        };
        groupLv.SelectedItemChanged += (_, a) =>
        {
            var idx = (int)a.Value;
            if (idx >= 0 && idx < groups.Count) groupInput.Text = groups[idx];
        };

        if (markedCount > 1)
        {
            var btnAll = new Button { Text = "Set all" };
            btnAll.Accepting += (_, _) =>
            {
                result = new Result(
                    ownerInput.Text?.ToString() ?? currentOwner,
                    groupInput.Text?.ToString() ?? currentGroup,
                    ApplyToAll: true);
                Application.RequestStop(d);
            };
            d.AddButton(btnAll);
        }

        var btnSet = new Button { Text = "Set", IsDefault = true };
        btnSet.Accepting += (_, _) =>
        {
            result = new Result(
                ownerInput.Text?.ToString() ?? currentOwner,
                groupInput.Text?.ToString() ?? currentGroup,
                ApplyToAll: false);
            Application.RequestStop(d);
        };
        var btnCancel = new Button { Text = "Cancel" };
        btnCancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(btnSet);
        d.AddButton(btnCancel);

        ownerLv.SetFocus();
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
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                var name = line.Split(':')[0];
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }
        }
        catch { /* permission or missing — fall back to empty list */ }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static string TruncateName(string name, int max)
        => name.Length <= max ? name : "..." + name[^(max - 3)..];
}
