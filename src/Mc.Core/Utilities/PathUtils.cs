namespace Mc.Core.Utilities;

/// <summary>
/// Path manipulation helpers. Cross-platform shims for Unix path conventions.
/// </summary>
public static class PathUtils
{
    public static string GetDisplayPath(string path, int maxLength)
    {
        if (path.Length <= maxLength) return path;
        // Truncate from the left, keeping the end: "...path/to/file"
        return "..." + path[^(maxLength - 3)..];
    }

    public static string TildePath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.StartsWith(home, StringComparison.Ordinal)
            ? "~" + path[home.Length..]
            : path;
    }

    public static string ExpandTilde(string path)
    {
        if (!path.StartsWith('~')) return path;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.Length == 1 ? home : home + path[1..];
    }

    public static bool IsHidden(string name)
        => name.StartsWith('.') && name != "." && name != "..";

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        path = path.Replace('\\', '/');
        // Collapse double slashes
        while (path.Contains("//"))
            path = path.Replace("//", "/");
        return path;
    }

    public static IEnumerable<string> GetPathComponents(string path)
    {
        var parts = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        yield return "/";
        foreach (var p in parts) yield return p;
    }
}
