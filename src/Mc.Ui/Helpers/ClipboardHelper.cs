using Terminal.Gui;

namespace Mc.Ui.Helpers;

/// <summary>
/// Wraps Terminal.Gui clipboard access with fallback to xclip/xsel/wl-copy on Linux.
/// Ported from MCCompanion's ClipboardHelper.
/// </summary>
internal static class ClipboardHelper
{
    /// <summary>Set clipboard text, returns true on success.</summary>
    public static bool TrySet(string text)
    {
        if (Clipboard.IsSupported)
        {
            Clipboard.Contents = text;
            return true;
        }

        // Fallback for Linux: try wl-copy, xclip, xsel in order
        if (!OperatingSystem.IsLinux()) return false;

        string[][] tools =
        [
            ["wl-copy",   text],
            ["xclip",     "-selection", "clipboard"],
            ["xsel",      "--clipboard", "--input"],
        ];

        foreach (var args in tools)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName  = args[0],
                    UseShellExecute = false,
                    RedirectStandardInput = args.Length > 1,
                };
                // For tools that read from stdin (xclip, xsel), pass text via stdin
                if (args.Length > 1)
                    foreach (var a in args[1..]) psi.ArgumentList.Add(a);

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) continue;
                if (psi.RedirectStandardInput)
                {
                    proc.StandardInput.Write(text);
                    proc.StandardInput.Close();
                }
                proc.WaitForExit(2000);
                if (proc.ExitCode == 0) return true;
            }
            catch { /* try next */ }
        }

        // wl-copy takes text as argument
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wl-copy",
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(text);
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(2000);
            if (proc?.ExitCode == 0) return true;
        }
        catch { }

        return false;
    }
}
