using Mc.Core.Models;
using Mc.Core.Utilities;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>File information dialog. Equivalent to the info dialog in mc.</summary>
public static class InfoDialog
{
    public static void Show(FileEntry entry)
    {
        var d = new Dialog
        {
            Title = "File Info",
            Width = 60,
            Height = 16,
            ColorScheme = McTheme.Dialog,
        };

        var lines = new[]
        {
            $"Name:        {entry.Name}",
            $"Type:        {(entry.IsDirectory ? "Directory" : entry.IsSymlink ? "Symlink" : "Regular file")}",
            $"Size:        {FileSizeFormatter.FormatExact(entry.Size)} bytes ({FileSizeFormatter.Format(entry.Size)})",
            $"Permissions: {PermissionsFormatter.Format(entry.Permissions, entry.IsDirectory, entry.IsSymlink)}",
            $"Owner:       {entry.OwnerName ?? entry.DirEntry.OwnerUid.ToString()}",
            $"Group:       {entry.GroupName ?? entry.DirEntry.OwnerGid.ToString()}",
            $"Modified:    {entry.ModificationTime:yyyy-MM-dd HH:mm:ss}",
            $"Accessed:    {entry.DirEntry.AccessTime:yyyy-MM-dd HH:mm:ss}",
            $"Path:        {entry.FullPath}",
        };

        for (int i = 0; i < lines.Length; i++)
            d.Add(new Label { X = 1, Y = 1 + i, Text = lines[i] });

        if (entry.IsSymlink && entry.DirEntry.SymlinkTarget != null)
            d.Add(new Label { X = 1, Y = 1 + lines.Length, Text = $"Target:      {entry.DirEntry.SymlinkTarget}" });

        var ok = new Button { X = Pos.Center(), Y = 13, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok);
        Application.Run(d);
    }
}
