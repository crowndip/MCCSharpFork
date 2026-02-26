using Mc.Core.Config;
using Mc.FileManager;
using Mc.Ui;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;

namespace Mc.App;

/// <summary>
/// Application entry point.
/// Equivalent to src/main.c in the original C codebase.
/// </summary>
internal sealed class Program
{
    private static int Main(string[] args)
    {
        try
        {
            // Handle --help
            if (args.Any(a => a is "--help" or "-h"))
            {
                PrintUsage();
                return 0;
            }

            // Handle --version
            if (args.Any(a => a is "--version" or "-V"))
            {
                Console.WriteLine("Midnight Commander for .NET 8");
                Console.WriteLine("A .NET rewrite of GNU Midnight Commander");
                return 0;
            }

            // Build DI container
            var services = AppSetup.BuildServiceProvider(args);

            // Initialize Terminal.Gui
            Application.Init();
            McTheme.ApplyDefault();

            // Get required services
            var controller = services.GetRequiredService<FileManagerController>();
            var settings   = services.GetRequiredService<McSettings>();

            // Create and run the main application
            var app = new McApplication(controller, settings);
            Application.Run(app);
            Application.Shutdown();

            return 0;
        }
        catch (Exception ex)
        {
            // Ensure terminal is restored before printing error
            try { Application.Shutdown(); } catch { }
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"mc: fatal error: {ex.Message}");
            Console.ResetColor();
            if (Environment.GetEnvironmentVariable("MC_DEBUG") != null)
                Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Usage: mc [OPTIONS] [LEFTDIR] [RIGHTDIR]

            Midnight Commander for .NET — a visual file manager.

            OPTIONS:
              -h, --help         Show this help message
              -V, --version      Show version information
              -d, --nodebug      Disable debug output
              -b, --nocolor      Start in black-and-white mode
              -P, --printwd      Print last directory on exit (for shell integration)

            ARGUMENTS:
              LEFTDIR            Initial directory for left panel (default: last saved)
              RIGHTDIR           Initial directory for right panel (default: last saved)

            KEY BINDINGS:
              F1       Help
              F3       View file
              F4       Edit file
              F5       Copy
              F6       Move/Rename
              F7       Create directory
              F8       Delete
              F9       Menu
              F10      Quit
              Tab      Switch panels
              Ctrl+R   Refresh panels
              Ctrl+U   Swap panels
              Ctrl+O   Shell

            CONFIG:
              Config files: ~/.config/mc/ini
              Skins:        ~/.local/share/mc/skins/
              Hotlist:      ~/.config/mc/hotlist
            """);
    }
}
