namespace Mc.Core.Utilities;

/// <summary>
/// Human-readable file size formatting. Equivalent to size_trunc_sep() etc. in the C codebase.
/// </summary>
public static class FileSizeFormatter
{
    private static readonly string[] SizeSuffixes = ["", "K", "M", "G", "T", "P"];

    public static string Format(long bytes, bool humanReadable = true)
    {
        if (!humanReadable) return bytes.ToString();

        if (bytes < 1024) return $"{bytes}";

        double size = bytes;
        int order = 0;
        while (size >= 1024 && order < SizeSuffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return size >= 100
            ? $"{(long)size}{SizeSuffixes[order]}"
            : $"{size:F1}{SizeSuffixes[order]}";
    }

    public static string FormatExact(long bytes)
    {
        // Format with thousand separators: 1,234,567
        return bytes.ToString("N0");
    }

    public static string FormatPanelSize(long bytes, bool isDirectory)
    {
        if (isDirectory) return "<DIR>";
        return Format(bytes);
    }
}
