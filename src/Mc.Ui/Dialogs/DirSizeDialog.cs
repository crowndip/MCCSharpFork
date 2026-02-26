using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Computes the total size (bytes, files, subdirectories) of a directory tree.
/// Ported from MCCompanion's DirSizeCommand.
/// </summary>
public static class DirSizeDialog
{
    public static void Show(string directoryPath)
    {
        var dirName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar)) ?? directoryPath;

        var d = new Dialog
        {
            Title  = "Directory Size",
            Width  = 52,
            Height = 12,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = $"Directory: {dirName}" });
        d.Add(new Label { X = 1, Y = 3, Text = "Size:  " });
        d.Add(new Label { X = 1, Y = 4, Text = "Files: " });
        d.Add(new Label { X = 1, Y = 5, Text = "Dirs:  " });

        var lblSize  = new Label { X = 9, Y = 3, Text = "Scanning…" };
        var lblFiles = new Label { X = 9, Y = 4, Text = "" };
        var lblDirs  = new Label { X = 9, Y = 5, Text = "" };
        d.Add(lblSize, lblFiles, lblDirs);

        var btnClose = new Button { X = Pos.Center(), Y = 7, Text = "Close", IsDefault = true };
        btnClose.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(btnClose);

        var cts = new CancellationTokenSource();

        _ = Task.Run(() =>
        {
            try
            {
                long totalBytes = 0;
                long fileCount  = 0;
                long dirCount   = 0;

                foreach (var entry in Directory.EnumerateFileSystemEntries(
                    directoryPath, "*", SearchOption.AllDirectories))
                {
                    if (cts.IsCancellationRequested) return;

                    if (File.Exists(entry))
                    {
                        totalBytes += new FileInfo(entry).Length;
                        fileCount++;
                    }
                    else
                    {
                        dirCount++;
                    }

                    // Live update every 500 entries
                    if ((fileCount + dirCount) % 500 == 0)
                    {
                        var bytesSnapshot = totalBytes;
                        var fcSnap = fileCount;
                        var dcSnap = dirCount;
                        Application.Invoke(() =>
                        {
                            lblSize.Text  = FormatBytes(bytesSnapshot);
                            lblFiles.Text = fcSnap.ToString("N0");
                            lblDirs.Text  = dcSnap.ToString("N0");
                            d.SetNeedsDraw();
                        });
                    }
                }

                Application.Invoke(() =>
                {
                    lblSize.Text  = FormatBytes(totalBytes);
                    lblFiles.Text = fileCount.ToString("N0");
                    lblDirs.Text  = dirCount.ToString("N0");
                    d.SetNeedsDraw();
                });
            }
            catch (Exception ex)
            {
                Application.Invoke(() =>
                {
                    lblSize.Text = $"Error: {ex.Message}";
                    d.SetNeedsDraw();
                });
            }
        }, cts.Token);

        Application.Run(d);
        cts.Cancel();
        d.Dispose();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0
            ? $"{bytes:N0} B"
            : $"{value:F2} {units[unit]}  ({bytes:N0} bytes)";
    }
}
