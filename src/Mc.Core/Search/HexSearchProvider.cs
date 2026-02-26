namespace Mc.Core.Search;

public sealed class HexSearchProvider : ISearchProvider
{
    public SearchType SupportedType => SearchType.Hex;

    public SearchResult Search(ReadOnlySpan<byte> data, SearchOptions options, long startOffset = 0)
    {
        if (!TryParseHexPattern(options.Pattern, out var pattern, out _))
            return SearchResult.NotFound;

        var searchIn = data[(int)startOffset..];
        int idx = FindBytes(searchIn, pattern);
        if (idx < 0) return SearchResult.NotFound;

        return SearchResult.Match(startOffset + idx, pattern.Length,
            BitConverter.ToString(pattern).Replace("-", " "));
    }

    public SearchResult Search(string text, SearchOptions options, int startIndex = 0)
        => SearchResult.NotFound; // Hex search operates on bytes only

    public string? Replace(string text, SearchOptions options, int startIndex = 0) => null;

    public string ReplaceAll(string text, SearchOptions options) => text;

    public bool IsPatternValid(SearchOptions options, out string? error)
    {
        TryParseHexPattern(options.Pattern, out _, out error);
        return error == null;
    }

    private static bool TryParseHexPattern(string pattern, out byte[] bytes, out string? error)
    {
        bytes = [];
        error = null;
        var tokens = pattern.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var result = new List<byte>(tokens.Length);
        foreach (var t in tokens)
        {
            if (!byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                error = $"Invalid hex token: {t}";
                return false;
            }
            result.Add(b);
        }
        if (result.Count == 0) { error = "Empty pattern"; return false; }
        bytes = [.. result];
        return true;
    }

    private static int FindBytes(ReadOnlySpan<byte> haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { found = false; break; }
            }
            if (found) return i;
        }
        return -1;
    }
}
