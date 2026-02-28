using System.Collections.ObjectModel;
using Mc.FileManager;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Directory hotlist (bookmarks) dialog with hierarchical group navigation.
/// Equivalent to src/filemanager/hotlist.c in the original C codebase.
/// </summary>
public static class HotlistDialog
{
    public static string? Show(HotlistManager hotlist, string currentPath)
    {
        string? selected = null;
        // Navigation stack: each element is (group, selectedIndex)
        var stack = new Stack<(HotlistManager.HotlistGroup Group, int Index)>();
        var currentGroup = hotlist.Root;

        var d = new Dialog
        {
            Title = "Directory Hotlist",
            Width = 70,
            Height = 22,
            ColorScheme = McTheme.Dialog,
        };

        // Path label e.g. "Hotlist" or "Hotlist / Work"
        var pathLabel = new Label
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1),
            Text = "Hotlist",
        };
        d.Add(pathLabel);

        var listView = new ListView
        {
            X = 1, Y = 2,
            Width = Dim.Fill(1),
            Height = Dim.Fill(7),
            ColorScheme = McTheme.Panel,
        };
        d.Add(listView);

        // ── helpers ──────────────────────────────────────────────────────────

        void RefreshList()
        {
            var items = new List<string>();
            foreach (var item in currentGroup.Children)
            {
                if (item is HotlistManager.HotlistGroup g)
                    items.Add($"[/] {g.Label}");
                else if (item is HotlistManager.HotlistEntry e)
                    items.Add($"    {e.Label,-28} {e.Path}");
            }
            listView.SetSource(new ObservableCollection<string>(items));

            // Build breadcrumb
            var crumbs = new List<string> { "Hotlist" };
            foreach (var (grp, _) in stack.Reverse())
                crumbs.Add(grp.Label);
            if (stack.Count > 0) crumbs.Add(currentGroup.Label);
            pathLabel.Text = string.Join(" / ", crumbs);
        }

        HotlistManager.HotlistItem? SelectedItem()
        {
            var idx = listView.SelectedItem;
            if (idx < 0 || idx >= currentGroup.Children.Count) return null;
            return currentGroup.Children[idx];
        }

        // ── buttons ──────────────────────────────────────────────────────────

        var goto_ = new Button { Text = "Go to", IsDefault = true };
        goto_.Accepting += (_, _) =>
        {
            var item = SelectedItem();
            if (item is HotlistManager.HotlistGroup grp)
            {
                stack.Push((currentGroup, listView.SelectedItem));
                currentGroup = grp;
                RefreshList();
                listView.SelectedItem = 0;
            }
            else if (item is HotlistManager.HotlistEntry e)
            {
                selected = e.Path;
                Application.RequestStop(d);
            }
        };

        var up = new Button { Text = "Up" };
        up.Accepting += (_, _) =>
        {
            if (stack.Count == 0) return;
            var (parent, prevIdx) = stack.Pop();
            currentGroup = parent;
            RefreshList();
            listView.SelectedItem = prevIdx;
        };

        var add = new Button { Text = "Add" };
        add.Accepting += (_, _) =>
        {
            var label = InputDialog.Show("Add to Hotlist", "Label:", System.IO.Path.GetFileName(currentPath));
            if (label == null) return;
            var path = InputDialog.Show("Add to Hotlist", "Path:", currentPath);
            if (path == null) return;
            currentGroup.Children.Add(new HotlistManager.HotlistEntry(label, path));
            hotlist.Add(label, path); // triggers Save()
            RefreshList();
        };

        var newGroup = new Button { Text = "New group" };
        newGroup.Accepting += (_, _) =>
        {
            var label = InputDialog.Show("New Group", "Group name:", string.Empty);
            if (label == null) return;
            hotlist.AddGroup(currentGroup, label);
            RefreshList();
        };

        var remove = new Button { Text = "Remove" };
        remove.Accepting += (_, _) =>
        {
            var item = SelectedItem();
            if (item == null) return;
            if (item is HotlistManager.HotlistGroup g && g.Children.Count > 0)
            {
                MessageDialog.Show("Remove", "Cannot remove a non-empty group.");
                return;
            }
            hotlist.RemoveItem(currentGroup, item);
            RefreshList();
        };

        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        // Double-clicking or Enter key navigates into groups
        listView.KeyDown += (_, k) =>
        {
            if (k.KeyCode == KeyCode.Enter)
            {
                var item = SelectedItem();
                if (item is HotlistManager.HotlistGroup grp)
                {
                    stack.Push((currentGroup, listView.SelectedItem));
                    currentGroup = grp;
                    RefreshList();
                    listView.SelectedItem = 0;
                    k.Handled = true;
                }
            }
        };

        d.AddButton(goto_);
        d.AddButton(up);
        d.AddButton(add);
        d.AddButton(newGroup);
        d.AddButton(remove);
        d.AddButton(cancel);

        RefreshList();
        Application.Run(d);
        return selected;
    }
}
