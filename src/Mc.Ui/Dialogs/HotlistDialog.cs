using System.Collections.ObjectModel;
using Mc.FileManager;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>Directory hotlist (bookmarks) dialog. Equivalent to src/filemanager/hotlist.c.</summary>
public static class HotlistDialog
{
    public static string? Show(HotlistManager hotlist, string currentPath)
    {
        string? selected = null;
        var d = new Dialog
        {
            Title = "Directory Hotlist",
            Width = 70,
            Height = 20,
            ColorScheme = McTheme.Dialog,
        };

        var entries = hotlist.Entries;
        var items = entries.Select(e => $"{e.Label,-30} {e.Path}").ToList();

        var listView = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(5),
            ColorScheme = McTheme.Panel,
        };
        listView.SetSource(new ObservableCollection<string>(items));
        d.Add(listView);

        var goto_ = new Button { X = Pos.Center() - 20, Y = Pos.Bottom(listView) + 1, Text = "Go to", IsDefault = true };
        goto_.Accepting += (_, _) =>
        {
            if (listView.SelectedItem >= 0 && listView.SelectedItem < entries.Count)
            {
                selected = entries[listView.SelectedItem].Path;
                Application.RequestStop(d);
            }
        };

        var add = new Button { X = Pos.Center() - 8, Y = Pos.Bottom(listView) + 1, Text = "Add" };
        add.Accepting += (_, _) =>
        {
            var label = InputDialog.Show("Add to Hotlist", "Label:", System.IO.Path.GetFileName(currentPath));
            if (label != null) hotlist.Add(label, currentPath);
            listView.SetSource(new ObservableCollection<string>(hotlist.Entries.Select(e => $"{e.Label,-30} {e.Path}")));
        };

        var remove = new Button { X = Pos.Center() + 4, Y = Pos.Bottom(listView) + 1, Text = "Remove" };
        remove.Accepting += (_, _) =>
        {
            if (listView.SelectedItem >= 0 && listView.SelectedItem < entries.Count)
            {
                hotlist.Remove(entries[listView.SelectedItem].Path);
                listView.SetSource(new ObservableCollection<string>(hotlist.Entries.Select(e => $"{e.Label,-30} {e.Path}")));
            }
        };

        var cancel = new Button { X = Pos.Center() + 14, Y = Pos.Bottom(listView) + 1, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(goto_); d.AddButton(add); d.AddButton(remove); d.AddButton(cancel);
        Application.Run(d);
        return selected;
    }
}
