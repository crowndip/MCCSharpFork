namespace Mc.Ui.Helpers;

/// <summary>
/// Helpers for launching external processes (terminal emulators, diff tools).
/// Ported from MCCompanion's ProcessHelper.
/// </summary>
internal static class ProcessHelper
{
    /// <summary>Try to launch an external program with the given arguments.</summary>
    public static bool TryLaunchArgs(string executable, params string[] args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            var proc = System.Diagnostics.Process.Start(psi);
            return proc != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Open a terminal emulator in <paramref name="directory"/>.
    /// Tries: gnome-terminal, konsole, xfce4-terminal, xterm.
    /// </summary>
    public static bool OpenTerminal(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            return TryLaunchArgs("cmd", "/K", $"cd /d \"{directory}\"");
        }

        // Ordered preference list: (executable, working-dir-flag)
        (string exe, string[] args)[] candidates =
        [
            ("gnome-terminal", ["--working-directory=" + directory]),
            ("konsole",        ["--workdir", directory]),
            ("xfce4-terminal", ["--working-directory=" + directory]),
            ("mate-terminal",  ["--working-directory=" + directory]),
            ("xterm",          ["-e", $"cd '{directory}' && exec bash"]),
        ];

        foreach (var (exe, args) in candidates)
        {
            if (TryLaunchArgs(exe, args))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Open an external diff viewer for two files.
    /// Tries: meld, kdiff3, code (VSCode --diff), vimdiff.
    /// </summary>
    public static bool OpenDiff(string leftPath, string rightPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return TryLaunchArgs("code", "--diff", leftPath, rightPath);
        }

        (string exe, string[] extra)[] candidates =
        [
            ("meld",   []),
            ("kdiff3", []),
            ("code",   ["--diff"]),
            ("bcompare", []),
            ("vimdiff", []),
        ];

        foreach (var (exe, extra) in candidates)
        {
            var args = extra.Append(leftPath).Append(rightPath).ToArray();
            if (TryLaunchArgs(exe, args))
                return true;
        }
        return false;
    }
}
