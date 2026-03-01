using Mc.Core.Models;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>Sort order selection dialog.</summary>
public static class SortDialog
{
    public static SortOptions? Show(SortOptions current)
    {
        SortOptions? result = null;
        var d = new Dialog
        {
            Title = "Sort Order",
            Width = 40,
            Height = 16,
            ColorScheme = McTheme.Dialog,
        };

        var fields = Enum.GetValues<SortField>();
        // Human-readable labels matching original MC sort dialog (#45)
        static string Label(SortField f) => f switch
        {
            SortField.Name             => "Name",
            SortField.Extension        => "Extension",
            SortField.Size             => "Size",
            SortField.ModificationTime => "Modify time",
            SortField.AccessTime       => "Access time",
            SortField.CreationTime     => "Change time",
            SortField.Permissions      => "Permissions",
            SortField.Owner            => "Owner",
            SortField.Group            => "Group",
            SortField.Inode            => "Inode",
            SortField.Unsorted         => "Unsorted",
            _                          => f.ToString(),
        };
        var radioGroup = new RadioGroup
        {
            X = 1, Y = 1,
            RadioLabels = fields.Select(Label).ToArray(),
            SelectedItem = Array.IndexOf(fields, current.Field),
            ColorScheme = McTheme.Dialog,
        };
        d.Add(radioGroup);

        var reverseCb = new CheckBox
        {
            X = 1, Y = fields.Length + 2,
            Text = "Reverse order",
            CheckedState = current.Descending ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        var dirFirstCb = new CheckBox
        {
            X = 1, Y = fields.Length + 3,
            Text = "Directories first",
            CheckedState = current.DirectoriesFirst ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        var caseCb = new CheckBox
        {
            X = 1, Y = fields.Length + 4,
            Text = "Case sensitive",
            CheckedState = current.CaseSensitive ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(reverseCb, dirFirstCb, caseCb);

        var ok = new Button { X = Pos.Center() - 8, Y = fields.Length + 6, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            result = new SortOptions
            {
                Field = fields[Math.Max(0, radioGroup.SelectedItem)],
                Descending = reverseCb.CheckedState == CheckState.Checked,
                DirectoriesFirst = dirFirstCb.CheckedState == CheckState.Checked,
                CaseSensitive = caseCb.CheckedState == CheckState.Checked,
            };
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 2, Y = fields.Length + 6, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d);
        return result;
    }
}
