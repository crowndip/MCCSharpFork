using System.Text;
using System.Text.RegularExpressions;

namespace Mc.Core.Search;

public sealed class RegexSearchProvider : ISearchProvider
{
    public SearchType SupportedType => SearchType.Regex;

    public SearchResult Search(ReadOnlySpan<byte> data, SearchOptions options, long startOffset = 0)
    {
        var text = Encoding.UTF8.GetString(data[(int)startOffset..]);
        return Search(text, options);
    }

    public SearchResult Search(string text, SearchOptions options, int startIndex = 0)
    {
        if (!TryBuildRegex(options, out var regex, out _)) return SearchResult.NotFound;

        var match = regex!.Match(text, startIndex);
        if (!match.Success) return SearchResult.NotFound;

        var groups = new List<string>(match.Groups.Count);
        for (int i = 0; i < match.Groups.Count; i++)
            groups.Add(match.Groups[i].Value);

        return SearchResult.Match(match.Index, match.Length, match.Value, groups);
    }

    public string? Replace(string text, SearchOptions options, int startIndex = 0)
    {
        if (!TryBuildRegex(options, out var regex, out _)) return null;

        var match = regex!.Match(text, startIndex);
        if (!match.Success) return null;

        return regex.Replace(text, options.Replacement ?? string.Empty, 1, startIndex);
    }

    public string ReplaceAll(string text, SearchOptions options)
    {
        if (!TryBuildRegex(options, out var regex, out _)) return text;
        return regex!.Replace(text, options.Replacement ?? string.Empty);
    }

    public bool IsPatternValid(SearchOptions options, out string? error)
    {
        TryBuildRegex(options, out _, out error);
        return error == null;
    }

    private static bool TryBuildRegex(SearchOptions options, out Regex? regex, out string? error)
    {
        regex = null;
        error = null;
        if (string.IsNullOrEmpty(options.Pattern)) { error = "Empty pattern"; return false; }

        var flags = RegexOptions.None;
        if (!options.CaseSensitive) flags |= RegexOptions.IgnoreCase;
        if (options.EntireLine) flags |= RegexOptions.Singleline;

        var pattern = options.WholeWords ? $@"\b{options.Pattern}\b" : options.Pattern;

        try
        {
            regex = new Regex(pattern, flags, TimeSpan.FromSeconds(5));
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
