using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Edit access / modification / creation timestamps for a file.
/// Ported from MCCompanion's TouchCommand.
/// </summary>
public static class TouchDialog
{
    public static void Show(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fi       = new FileInfo(filePath);

        var created  = fi.CreationTime;
        var modified = fi.LastWriteTime;
        var accessed = fi.LastAccessTime;

        var d = new Dialog
        {
            Title  = "Touch — Edit Timestamps",
            Width  = 58,
            Height = 16,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = $"File: {fileName}" });

        // Row helper: label + text field + "Now" button
        static (TextField tf, Button btn) MakeRow(Dialog dialog, string label, int y, DateTime value)
        {
            dialog.Add(new Label { X = 1, Y = y, Text = label });
            var tf = new TextField
            {
                X = 13, Y = y, Width = 26,
                Text = value.ToString("yyyy-MM-dd HH:mm:ss"),
                ColorScheme = McTheme.Panel,
            };
            dialog.Add(tf);
            var btn = new Button { X = 40, Y = y, Text = "Now" };
            dialog.Add(btn);
            return (tf, btn);
        }

        var (tfCreated, btnNowC)  = MakeRow(d, "Created:  ",  3, created);
        var (tfModified, btnNowM) = MakeRow(d, "Modified: ",  5, modified);
        var (tfAccessed, btnNowA) = MakeRow(d, "Accessed: ",  7, accessed);

        btnNowC.Accepting += (_, _) => { tfCreated.Text  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); };
        btnNowM.Accepting += (_, _) => { tfModified.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); };
        btnNowA.Accepting += (_, _) => { tfAccessed.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); };

        d.Add(new Label { X = 1, Y = 9, Text = "Format: yyyy-MM-dd HH:mm:ss" });

        var chkAllNow = new CheckBox { X = 1, Y = 10, Text = "Set all to current time on Apply" };
        d.Add(chkAllNow);

        var btnApply  = new Button { X = Pos.Center() - 12, Y = 12, Text = "Apply", IsDefault = true };
        var btnCancel = new Button { X = Pos.Center() + 3,  Y = 12, Text = "Cancel" };

        btnApply.Accepting += (_, _) =>
        {
            if (chkAllNow.CheckedState == CheckState.Checked)
            {
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                tfCreated.Text  = now;
                tfModified.Text = now;
                tfAccessed.Text = now;
            }

            if (!TryParseDate(tfCreated.Text?.ToString(),  out var c) ||
                !TryParseDate(tfModified.Text?.ToString(), out var m) ||
                !TryParseDate(tfAccessed.Text?.ToString(), out var a))
            {
                MessageDialog.Error("Invalid date format. Use yyyy-MM-dd HH:mm:ss");
                return;
            }

            try
            {
                File.SetCreationTime(filePath, c);
                File.SetLastWriteTime(filePath, m);
                File.SetLastAccessTime(filePath, a);
                Application.RequestStop(d);
            }
            catch (Exception ex)
            {
                MessageDialog.Error(ex.Message);
            }
        };
        btnCancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(btnApply);
        d.AddButton(btnCancel);

        Application.Run(d);
        d.Dispose();
    }

    private static bool TryParseDate(string? s, out DateTime result)
    {
        if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out result))
            return true;

        result = default;
        return false;
    }
}
