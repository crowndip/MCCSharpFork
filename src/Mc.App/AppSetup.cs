using Mc.Core.Config;
using Mc.Core.Skin;
using Mc.Core.Vfs;
using Mc.FileManager;
using Mc.Vfs.Archives;
using Mc.Vfs.Ftp;
using Mc.Vfs.Local;
using Mc.Vfs.Sftp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mc.App;

/// <summary>
/// Dependency injection and application bootstrap.
/// Registers all VFS providers, loads configuration, and sets up the service container.
/// Equivalent to src/main.c + plugins_init() in the original C codebase.
/// </summary>
public static class AppSetup
{
    public static IServiceProvider BuildServiceProvider(string[] args)
    {
        var services = new ServiceCollection();

        // Ensure config directories exist
        ConfigPaths.EnsureDirectoriesExist();

        // Configuration
        var config = McConfig.LoadDefault();
        var settings = new McSettings(config);
        services.AddSingleton(config);
        services.AddSingleton(settings);

        // Skin
        var skinManager = new SkinManager();
        skinManager.LoadDirectory(ConfigPaths.SkinsDir);
        skinManager.Activate(settings.ActiveSkin);
        services.AddSingleton(skinManager);

        // VFS registry with all providers
        var vfsRegistry = new VfsRegistry();
        vfsRegistry.Register(new LocalVfsProvider());
        vfsRegistry.Register(new FtpVfsProvider());
        vfsRegistry.Register(new SftpVfsProvider());
        vfsRegistry.Register(new TarVfsProvider());
        vfsRegistry.Register(new ZipVfsProvider());
        services.AddSingleton(vfsRegistry);

        // File manager
        var controller = new FileManagerController(vfsRegistry);
        services.AddSingleton(controller);

        // Parse command line: mc [leftdir] [rightdir]
        var leftPath  = args.Length > 0 ? args[0] : settings.LeftPanelPath;
        var rightPath = args.Length > 1 ? args[1] : settings.RightPanelPath;

        // Ensure paths exist
        if (!Directory.Exists(leftPath))  leftPath  = Environment.CurrentDirectory;
        if (!Directory.Exists(rightPath)) rightPath = Environment.CurrentDirectory;

        controller.Initialize(leftPath, rightPath);

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        return services.BuildServiceProvider();
    }
}
