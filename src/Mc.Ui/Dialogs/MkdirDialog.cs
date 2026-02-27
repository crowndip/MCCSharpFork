namespace Mc.Ui.Dialogs;

/// <summary>Create directory dialog. Equivalent to query_dialog() + input in the C code.</summary>
public static class MkdirDialog
{
    public static string? Show()
        => InputDialog.Show("Create a new Directory", "Enter directory name:", string.Empty);
}
