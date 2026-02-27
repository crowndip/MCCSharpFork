namespace Mc.Core.Config;

/// <summary>
/// Standard paths for mc configuration and data files.
/// Equivalent to lib/mcconfig/paths.c
/// </summary>
public static class ConfigPaths
{
    private static readonly string HomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string ConfigDir =>
        Environment.GetEnvironmentVariable("MC_CONFIG_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mc");

    public static string CacheDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mc", "cache");

    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mc");

    public static string MainConfigFile => Path.Combine(ConfigDir, "ini");
    public static string HotlistFile => Path.Combine(ConfigDir, "hotlist");
    public static string HistoryFile => Path.Combine(ConfigDir, "history");
    public static string ExtFile           => Path.Combine(ConfigDir, "mc.ext.ini");
    public static string MenuFile          => Path.Combine(ConfigDir, "mc.menu");
    public static string FileHighlightFile => Path.Combine(ConfigDir, "mc.filehighlight.ini");
    public static string KeymapFile        => Path.Combine(ConfigDir, "mc.keymap");
    public static string SkinsDir => Path.Combine(DataDir, "skins");
    public static string SyntaxDir => Path.Combine(DataDir, "syntax");
    public static string PanelsStateFile => Path.Combine(CacheDir, "panels.ini");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(SkinsDir);
        Directory.CreateDirectory(SyntaxDir);
    }
}
