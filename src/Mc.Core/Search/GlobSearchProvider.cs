using Mc.Core.Models;

namespace Mc.Core.Search;

public sealed class GlobSearchProvider : ISearchProvider
{
    public SearchType SupportedType => SearchType.Glob;

    public SearchResult Search(ReadOnlySpan<byte> data, SearchOptions options, long startOffset = 0)
        => SearchResult.NotFound; // Glob is for file names, not content

    public SearchResult Search(string text, SearchOptions options, int startIndex = 0)
    {
        // For glob, match full line by line
        if (string.IsNullOrEmpty(options.Pattern)) return SearchResult.NotFound;

        var filter = new FilterOptions
        {
            Pattern = options.Pattern,
            CaseSensitive = options.CaseSensitive
        };

        var lines = text.Split('\n');
        long offset = startIndex;
        bool pastStart = startIndex == 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (!pastStart)
            {
                if (offset >= startIndex) pastStart = true;
                else { offset += rawLine.Length + 1; continue; }
            }

            if (filter.Matches(line))
                return SearchResult.Match(offset, line.Length, line);

            offset += rawLine.Length + 1;
        }

        return SearchResult.NotFound;
    }

    public string? Replace(string text, SearchOptions options, int startIndex = 0) => null;
    public string ReplaceAll(string text, SearchOptions options) => text;

    public bool IsPatternValid(SearchOptions options, out string? error)
    {
        error = null;
        return !string.IsNullOrEmpty(options.Pattern);
    }
}
