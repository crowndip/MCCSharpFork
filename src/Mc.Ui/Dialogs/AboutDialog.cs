using Mc.Core.Utilities;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// About menu dialogs — License, GitHub, Fork From, Why Forked, New Functions.
/// </summary>
public static class AboutDialog
{
    private const string GitHubUrl = "https://github.com/crowndip/MCCSharpFork";

    public static void ShowLicense()     => ShowDoc("License",       LicenseText);
    public static void ShowGitHub()      => ShowGitHubDialog();
    public static void ShowForkFrom()    => ShowDoc("Fork From",      ForkFromText);
    public static void ShowWhyForked()   => ShowDoc("Why Forked",     WhyForkedText);
    public static void ShowNewFunctions()=> ShowDoc("New Functions",  NewFunctionsText);
    public static void ShowSystemInfo()  => ShowDoc("System Info",    SystemInfoBuilder.Build());

    // ── Document viewer ──────────────────────────────────────────────────────

    private static void ShowDoc(string title, string text)
    {
        int cols = Application.Driver?.Cols ?? 80;
        int rows = Application.Driver?.Rows ?? 24;
        int w = Math.Min(78, cols - 2);
        int h = Math.Clamp(rows - 4, 12, 28);

        var d = new Dialog
        {
            Title       = title,
            Width       = w,
            Height      = h,
            ColorScheme = McTheme.Dialog,
        };
        var tv = new TextView
        {
            X           = 1,
            Y           = 1,
            Width       = Dim.Fill(1),
            Height      = Dim.Fill(3),
            Text        = text,
            ReadOnly    = true,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(tv);
        var close = new Button { Text = "Close", IsDefault = true };
        close.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(close);
        Application.Run(d);
        d.Dispose();
    }

    private static void ShowGitHubDialog()
    {
        var d = new Dialog
        {
            Title       = "GitHub",
            Width       = 64,
            Height      = 10,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(new Label { X = 1, Y = 1, Text = "MCCSharpFork on GitHub:" });
        d.Add(new Label { X = 1, Y = 2, Text = GitHubUrl });
        d.Add(new Label { X = 1, Y = 4, Text = "Press Open to launch the page in your browser," });
        d.Add(new Label { X = 1, Y = 5, Text = "or Close to dismiss." });

        var open  = new Button { Text = "Open" };
        var close = new Button { Text = "Close", IsDefault = true };
        open.Accepting  += (_, _) => { TryOpenUrl(GitHubUrl); Application.RequestStop(d); };
        close.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(open);
        d.AddButton(close);
        Application.Run(d);
        d.Dispose();
    }

    private static void TryOpenUrl(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", url);
            else
                System.Diagnostics.Process.Start("xdg-open", url);
        }
        catch { }
    }

    // ── Content ──────────────────────────────────────────────────────────────

    private const string LicenseText =
        "Midnight Commander for .NET\n" +
        "GNU General Public License, version 3 (GPL-3.0)\n" +
        "\n" +
        "Copyright (C) 2024-2026  MCCSharpFork Contributors\n" +
        "\n" +
        "This program is free software: you can redistribute it and/or modify\n" +
        "it under the terms of the GNU General Public License as published by\n" +
        "the Free Software Foundation, either version 3 of the License, or\n" +
        "(at your option) any later version.\n" +
        "\n" +
        "This program is distributed in the hope that it will be useful,\n" +
        "but WITHOUT ANY WARRANTY; without even the implied warranty of\n" +
        "MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the\n" +
        "GNU General Public License for more details.\n" +
        "\n" +
        "You should have received a copy of the GNU General Public License\n" +
        "along with this program.  If not, see <https://www.gnu.org/licenses/>.\n" +
        "\n" +
        "This project is a clean-room C# rewrite and is NOT derived from the\n" +
        "GNU Midnight Commander C source code.\n" +
        "\n" +
        "Full license text:  https://www.gnu.org/licenses/gpl-3.0.txt\n" +
        "Source code:        https://github.com/crowndip/MCCSharpFork";

    private const string ForkFromText =
        "GNU Midnight Commander (mc)\n" +
        "https://midnight-commander.org/\n" +
        "\n" +
        "Original author:    Miguel de Icaza (1994)\n" +
        "Current maintainer: GNU MC team\n" +
        "License:            GNU GPL v2+\n" +
        "\n" +
        "GNU Midnight Commander is a free cross-platform orthodox file manager.\n" +
        "It provides a two-panel interface for managing files in the terminal,\n" +
        "inspired by the classic Norton Commander.\n" +
        "\n" +
        "MCCSharpFork is a clean-room reimplementation of GNU mc in C#/.NET.\n" +
        "It is NOT derived from the GNU mc C source code — it was written from\n" +
        "scratch to reproduce the documented behaviour, key bindings, and feature\n" +
        "set of GNU Midnight Commander, while adding new capabilities.";

    private const string WhyForkedText =
        "Why was this project created?\n" +
        "\n" +
        "GNU Midnight Commander is a mature and well-loved tool, but its C codebase\n" +
        "dates back to the early 1990s.  MCCSharpFork was created to bring the mc\n" +
        "experience to modern platforms and developer workflows:\n" +
        "\n" +
        "1. Cross-platform parity\n" +
        "   GNU mc is primarily a Linux/Unix tool.  This rewrite runs equally well\n" +
        "   on Linux, macOS, and Windows without any compatibility layer.\n" +
        "\n" +
        "2. Modern .NET ecosystem\n" +
        "   Written in C#/.NET 8, the codebase benefits from strong typing,\n" +
        "   async/await, NuGet packages, and modern IDE tooling (Visual Studio,\n" +
        "   VS Code, Rider).\n" +
        "\n" +
        "3. Terminal.Gui v2\n" +
        "   Built on the Terminal.Gui v2 framework, which provides a clean widget\n" +
        "   model, mouse support, and 256-colour rendering across all major\n" +
        "   terminal emulators.\n" +
        "\n" +
        "4. Layered, extensible architecture\n" +
        "   VFS, FileManager, UI, Editor, and Viewer are separate projects.\n" +
        "   New VFS providers, themes, or tools can be added without touching\n" +
        "   core logic.\n" +
        "\n" +
        "5. Clean-room implementation\n" +
        "   A fresh rewrite avoids legacy technical debt and is easier to\n" +
        "   understand, test, and contribute to.";

    private const string NewFunctionsText =
        "New and extended features in MCCSharpFork\n" +
        "(beyond standard GNU Midnight Commander)\n" +
        "\n" +
        "FILE MANAGER\n" +
        "  • Batch rename — rename multiple files with pattern placeholders:\n" +
        "      [N] name  [E] extension  [C] counter  [Y]/[M]/[D] date parts\n" +
        "  • Folder size analyser — visual breakdown of disk usage per entry\n" +
        "  • Checksum calculator — MD5, SHA-1, SHA-256 for selected files\n" +
        "  • Touch dialog — edit file timestamps interactively\n" +
        "  • Clipboard tools — copy path / name / directory to system clipboard\n" +
        "  • Copy tagged paths / names to clipboard in bulk\n" +
        "  • Open with default application (xdg-open / ShellExecute)\n" +
        "  • 'Open in file manager' — Nautilus, Dolphin, Explorer\n" +
        "  • Open terminal emulator in current directory (Ctrl+T)\n" +
        "  • Compare files / directories with external diff tool\n" +
        "    (meld, kdiff3, VS Code diff, vimdiff)\n" +
        "\n" +
        "VIRTUAL FILE SYSTEM\n" +
        "  • FTP and SFTP VFS providers (browse remote servers as local dirs)\n" +
        "  • ZIP and TAR archive VFS — browse and extract archives in-place\n" +
        "  • Large-file streaming for files > 10 MB (LargeFileBuffer)\n" +
        "\n" +
        "EDITOR (MCEDIT)\n" +
        "  • Syntax highlighting for 30+ languages including Perl, Rust, TOML\n" +
        "  • Mouse drag text selection (cross-platform, including Windows cmd)\n" +
        "  • Column / rectangular block selection (Alt+B)\n" +
        "  • Hex view and edit mode\n" +
        "  • Right-margin indicator\n" +
        "  • Spell-check integration\n" +
        "  • Macro recording and playback (Ctrl+R)\n" +
        "  • Configurable tab display\n" +
        "\n" +
        "VIEWER\n" +
        "  • Hex + text dual modes with in-viewer search\n" +
        "  • Streaming viewer for very large files\n" +
        "\n" +
        "DIFF VIEWER\n" +
        "  • Side-by-side diff with line-by-line navigation\n" +
        "\n" +
        "UI / APPEARANCE\n" +
        "  • 256-colour skin support with McTheme engine\n" +
        "  • Mouse support everywhere (click, scroll, drag)\n" +
        "  • Favourites menu (bookmarked directories with icons)\n" +
        "  • Drives menu (auto-detected local and network drives)";
}
