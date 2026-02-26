namespace Mc.Core.Models;

public sealed class FilterOptions
{
    public string? Pattern { get; set; }
    public bool ShowHidden { get; set; }
    public bool ShowBackups { get; set; } = true;   // Files ending with ~
    public bool CaseSensitive { get; set; } = OperatingSystem.IsLinux();

    public bool IsDefault => Pattern is null && !ShowHidden && ShowBackups;

    public bool Matches(string fileName)
    {
        if (!ShowHidden && fileName.StartsWith('.') && fileName != "..")
            return false;

        if (!ShowBackups && fileName.EndsWith('~'))
            return false;

        if (Pattern is null)
            return true;

        return MatchGlob(fileName, Pattern, CaseSensitive);
    }

    private static bool MatchGlob(string name, string pattern, bool caseSensitive)
    {
        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        // Simple glob: * matches anything, ? matches one char
        return MatchGlobCore(name.AsSpan(), pattern.AsSpan(), comparison);
    }

    private static bool MatchGlobCore(ReadOnlySpan<char> name, ReadOnlySpan<char> pattern,
        StringComparison comparison)
    {
        while (true)
        {
            if (pattern.IsEmpty) return name.IsEmpty;
            if (pattern[0] == '*')
            {
                pattern = pattern[1..];
                if (pattern.IsEmpty) return true;
                for (int i = 0; i <= name.Length; i++)
                    if (MatchGlobCore(name[i..], pattern, comparison)) return true;
                return false;
            }
            if (name.IsEmpty) return false;
            if (pattern[0] != '?' &&
                !name[..1].Equals(pattern[..1], comparison)) return false;
            name = name[1..];
            pattern = pattern[1..];
        }
    }
}
