using System.Diagnostics;

namespace Mc.Editor;

/// <summary>
/// Cross-platform OS clipboard integration for copy/paste with the desktop environment.
///
/// Priority order:
///   1. Terminal.Gui built-in clipboard (uses Win32 API on Windows, xclip/xsel on Linux)
///   2. Shell-command fallbacks (clip.exe / powershell on Windows; xclip / xsel / wl-clipboard on Linux)
///
/// Linux requirements (any one is sufficient):
///   X11 sessions  : xclip  OR  xsel
///   Wayland sessions : wl-copy / wl-paste  (from wl-clipboard package)
/// </summary>
internal static class OsClipboard
{
    // Cached Wayland / X11 detection (environment doesn't change during a session)
    private static readonly bool _isWayland =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    private static readonly bool _hasXclip  = !OperatingSystem.IsWindows() && HasCommand("xclip");
    private static readonly bool _hasXsel   = !OperatingSystem.IsWindows() && HasCommand("xsel");
    private static readonly bool _hasWlClip = !OperatingSystem.IsWindows() && HasCommand("wl-copy");

    /// <summary>Gets text from the OS clipboard. Returns empty string on failure.</summary>
    public static string Get()
    {
        // 1. Terminal.Gui built-in clipboard
        if (Terminal.Gui.Clipboard.TryGetClipboardData(out var tgText) && tgText != null)
            return tgText;

        // 2. Shell fallback
        return GetViaShell();
    }

    /// <summary>Sets text in the OS clipboard. Returns true on success.</summary>
    public static bool Set(string text)
    {
        // 1. Terminal.Gui built-in clipboard
        if (Terminal.Gui.Clipboard.TrySetClipboardData(text))
            return true;

        // 2. Shell fallback
        return SetViaShell(text);
    }

    // ── Shell-based fallbacks ────────────────────────────────────────────────

    private static string GetViaShell()
    {
        try
        {
            var (cmd, args) = GetReadArgs();
            if (cmd == null) return string.Empty;

            using var p = Process.Start(new ProcessStartInfo(cmd, args)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                CreateNoWindow         = true,
            });
            if (p == null) return string.Empty;
            var result = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            return result;
        }
        catch { return string.Empty; }
    }

    private static bool SetViaShell(string text)
    {
        try
        {
            var (cmd, args, stayRunning) = GetWriteArgs();
            if (cmd == null) return false;

            using var p = Process.Start(new ProcessStartInfo(cmd, args)
            {
                UseShellExecute       = false,
                RedirectStandardInput = true,
                CreateNoWindow        = true,
            });
            if (p == null) return false;
            p.StandardInput.Write(text);
            p.StandardInput.Close();
            // xclip and wl-copy serve clipboard requests while running — don't block on them.
            // xsel stores data internally and exits immediately.
            if (!stayRunning) p.WaitForExit(3000);
            return true;
        }
        catch { return false; }
    }

    // ── Command selection ────────────────────────────────────────────────────

    private static (string? cmd, string args) GetReadArgs()
    {
        if (OperatingSystem.IsWindows())
            return ("powershell", "-NoProfile -NonInteractive -Command \"Get-Clipboard\"");

        if (_isWayland && _hasWlClip)
            return ("wl-paste", "--no-newline");

        if (_hasXclip)
            return ("xclip", "-selection clipboard -out");

        if (_hasXsel)
            return ("xsel", "--clipboard --output");

        return (null, string.Empty);
    }

    /// <returns>(cmd, args, stayRunning) — stayRunning means do not WaitForExit.</returns>
    private static (string? cmd, string args, bool stayRunning) GetWriteArgs()
    {
        if (OperatingSystem.IsWindows())
            return ("clip.exe", string.Empty, false);

        if (_isWayland && _hasWlClip)
            return ("wl-copy", string.Empty, true);

        if (_hasXclip)
            return ("xclip", "-selection clipboard", true);

        if (_hasXsel)
            return ("xsel", "--clipboard --input", false);

        return (null, string.Empty, false);
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    private static bool HasCommand(string cmd)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("which", cmd)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                CreateNoWindow         = true,
            });
            p?.WaitForExit(500);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }
}
