using System.Text;

namespace Mc.Core.Search;

public sealed class NormalSearchProvider : ISearchProvider
{
    public SearchType SupportedType => SearchType.Normal;

    public SearchResult Search(ReadOnlySpan<byte> data, SearchOptions options, long startOffset = 0)
    {
        var encoding = Encoding.UTF8;
        var text = encoding.GetString(data[(int)startOffset..]);
        var result = Search(text, options);
        return result.Found
            ? SearchResult.Match(startOffset + encoding.GetByteCount(text[..(int)result.Offset]), result.Length, result.MatchedText!)
            : SearchResult.NotFound;
    }

    public SearchResult Search(string text, SearchOptions options, int startIndex = 0)
    {
        if (string.IsNullOrEmpty(options.Pattern)) return SearchResult.NotFound;

        var comparison = options.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var searchIn = startIndex > 0 && startIndex < text.Length ? text[startIndex..] : text;
        var startOff = startIndex > 0 ? startIndex : 0;

        if (options.EntireLine)
        {
            var lines = searchIn.Split('\n');
            long lineOffset = startOff;
            foreach (var line in lines)
            {
                var stripped = line.TrimEnd('\r');
                if (string.Equals(stripped, options.Pattern, comparison))
                    return SearchResult.Match(lineOffset, stripped.Length, stripped);
                lineOffset += line.Length + 1;
            }
            return SearchResult.NotFound;
        }

        int idx;
        if (options.WholeWords)
        {
            idx = startOff;
            while (idx < text.Length)
            {
                int found = text.IndexOf(options.Pattern, idx, comparison);
                if (found < 0) return SearchResult.NotFound;

                bool leftBound = found == 0 || !char.IsLetterOrDigit(text[found - 1]);
                bool rightBound = found + options.Pattern.Length >= text.Length ||
                                  !char.IsLetterOrDigit(text[found + options.Pattern.Length]);

                if (leftBound && rightBound)
                    return SearchResult.Match(found, options.Pattern.Length, text.Substring(found, options.Pattern.Length));

                idx = found + 1;
            }
            return SearchResult.NotFound;
        }

        idx = text.IndexOf(options.Pattern, startOff, comparison);
        return idx >= 0
            ? SearchResult.Match(idx, options.Pattern.Length, text.Substring(idx, options.Pattern.Length))
            : SearchResult.NotFound;
    }

    public string? Replace(string text, SearchOptions options, int startIndex = 0)
    {
        var result = Search(text, options, startIndex);
        if (!result.Found) return null;
        var repStart = (int)result.Offset;
        return text[..repStart] + (options.Replacement ?? string.Empty) + text[(repStart + result.Length)..];
    }

    public string ReplaceAll(string text, SearchOptions options)
    {
        if (string.IsNullOrEmpty(options.Pattern)) return text;
        var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return text.Replace(options.Pattern, options.Replacement ?? string.Empty, comparison);
    }

    public bool IsPatternValid(SearchOptions options, out string? error)
    {
        error = null;
        return !string.IsNullOrEmpty(options.Pattern);
    }
}
