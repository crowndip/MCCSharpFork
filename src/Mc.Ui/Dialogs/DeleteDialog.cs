using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>Delete confirmation dialog.</summary>
public static class DeleteDialog
{
    public static bool Confirm(IReadOnlyList<string> fileNames)
    {
        if (fileNames.Count == 0) return false;

        string message = fileNames.Count == 1
            ? $"Delete \"{fileNames[0]}\"?"
            : $"Delete {fileNames.Count} marked files?";

        return MessageDialog.Confirm("Delete", message, "Delete", "Cancel");
    }
}
