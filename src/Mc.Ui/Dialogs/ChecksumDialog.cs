using System.Security.Cryptography;
using System.Text;
using Mc.Ui.Helpers;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Computes and displays MD5 / SHA-1 / SHA-256 checksums for a file.
/// Ported from MCCompanion's ChecksumCommand.
/// </summary>
public static class ChecksumDialog
{
    public static void Show(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        var d = new Dialog
        {
            Title  = "Checksum",
            Width  = 70,
            Height = 16,
            ColorScheme = McTheme.Dialog,
        };

        // Labels
        d.Add(new Label { X = 1, Y = 1, Text = $"File: {fileName}" });
        d.Add(new Label { X = 1, Y = 3, Text = "MD5:" });
        d.Add(new Label { X = 1, Y = 5, Text = "SHA-1:" });
        d.Add(new Label { X = 1, Y = 7, Text = "SHA-256:" });

        // Result fields (read-only)
        var md5Field = new TextField
        {
            X = 10, Y = 3, Width = 56, Height = 1,
            ReadOnly = true, Text = "Computing…",
            ColorScheme = McTheme.Panel,
        };
        var sha1Field = new TextField
        {
            X = 10, Y = 5, Width = 56, Height = 1,
            ReadOnly = true, Text = "Computing…",
            ColorScheme = McTheme.Panel,
        };
        var sha256Field = new TextField
        {
            X = 10, Y = 7, Width = 56, Height = 1,
            ReadOnly = true, Text = "Computing…",
            ColorScheme = McTheme.Panel,
        };

        d.Add(md5Field, sha1Field, sha256Field);

        // Buttons
        var btnCopyMd5 = new Button
        {
            X = 10, Y = 9, Text = "Copy MD5",
        };
        var btnCopySha1 = new Button
        {
            X = 22, Y = 9, Text = "Copy SHA-1",
        };
        var btnCopySha256 = new Button
        {
            X = 36, Y = 9, Text = "Copy SHA-256",
        };
        var btnClose = new Button
        {
            X = Pos.Center(), Y = 11, Text = "Close", IsDefault = true,
        };

        btnCopyMd5.Accepting    += (_, _) => ClipboardHelper.TrySet(md5Field.Text?.ToString() ?? "");
        btnCopySha1.Accepting   += (_, _) => ClipboardHelper.TrySet(sha1Field.Text?.ToString() ?? "");
        btnCopySha256.Accepting += (_, _) => ClipboardHelper.TrySet(sha256Field.Text?.ToString() ?? "");
        btnClose.Accepting      += (_, _) => Application.RequestStop(d);

        d.Add(btnCopyMd5, btnCopySha1, btnCopySha256);
        d.AddButton(btnClose);

        // Compute checksums asynchronously so UI renders first
        _ = Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(filePath);

                var md5Hash    = MD5.HashData(ReadFully(filePath));
                var sha1Hash   = SHA1.HashData(ReadFully(filePath));
                var sha256Hash = SHA256.HashData(ReadFully(filePath));

                Application.Invoke(() =>
                {
                    md5Field.Text    = ToHex(md5Hash);
                    sha1Field.Text   = ToHex(sha1Hash);
                    sha256Field.Text = ToHex(sha256Hash);
                    d.SetNeedsDraw();
                });
            }
            catch (Exception ex)
            {
                Application.Invoke(() =>
                {
                    md5Field.Text    = "Error";
                    sha1Field.Text   = ex.Message;
                    sha256Field.Text = "";
                    d.SetNeedsDraw();
                });
            }
        });

        Application.Run(d);
        d.Dispose();
    }

    private static byte[] ReadFully(string path) => File.ReadAllBytes(path);

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
