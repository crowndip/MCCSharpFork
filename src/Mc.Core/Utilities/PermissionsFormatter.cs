namespace Mc.Core.Utilities;

/// <summary>
/// Formats Unix file permission bits as strings.
/// Equivalent to string_perm() in the original C codebase.
/// </summary>
public static class PermissionsFormatter
{
    public static string Format(UnixFileMode mode, bool isDirectory, bool isSymlink)
    {
        char type = isSymlink ? 'l' : isDirectory ? 'd' : '-';
        char[] p = [
            type,
            (mode & UnixFileMode.UserRead)    != 0 ? 'r' : '-',
            (mode & UnixFileMode.UserWrite)   != 0 ? 'w' : '-',
            (mode & UnixFileMode.UserExecute) != 0 ? 'x' : '-',
            (mode & UnixFileMode.GroupRead)   != 0 ? 'r' : '-',
            (mode & UnixFileMode.GroupWrite)  != 0 ? 'w' : '-',
            (mode & UnixFileMode.GroupExecute)!= 0 ? 'x' : '-',
            (mode & UnixFileMode.OtherRead)   != 0 ? 'r' : '-',
            (mode & UnixFileMode.OtherWrite)  != 0 ? 'w' : '-',
            (mode & UnixFileMode.OtherExecute)!= 0 ? 'x' : '-',
        ];
        // SetUID / SetGID / Sticky
        if ((mode & UnixFileMode.SetUser) != 0) p[3] = p[3] == 'x' ? 's' : 'S';
        if ((mode & UnixFileMode.SetGroup) != 0) p[6] = p[6] == 'x' ? 's' : 'S';
        if ((mode & UnixFileMode.StickyBit) != 0) p[9] = p[9] == 'x' ? 't' : 'T';
        return new string(p);
    }

    public static string FormatOctal(UnixFileMode mode)
        => Convert.ToString((int)mode, 8).PadLeft(4, '0');

    public static UnixFileMode ParseOctal(string octal)
        => (UnixFileMode)Convert.ToInt32(octal, 8);
}
