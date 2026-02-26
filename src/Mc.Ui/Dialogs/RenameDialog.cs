namespace Mc.Ui.Dialogs;

/// <summary>Rename/move a single file dialog.</summary>
public static class RenameDialog
{
    public static string? Show(string currentName)
        => InputDialog.Show("Rename", $"Rename \"{currentName}\" to:", currentName);
}
