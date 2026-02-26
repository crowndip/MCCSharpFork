namespace Mc.Core.Search;

public enum SearchType
{
    Normal,
    Regex,
    Hex,
    Glob,
}

public sealed class SearchOptions
{
    public string Pattern { get; set; } = string.Empty;
    public SearchType Type { get; set; } = SearchType.Normal;
    public bool CaseSensitive { get; set; }
    public bool WholeWords { get; set; }
    public bool EntireLine { get; set; }
    public bool Backward { get; set; }
    public string? Replacement { get; set; }
}

public sealed class SearchResult
{
    public bool Found { get; init; }
    public long Offset { get; init; }
    public int Length { get; init; }
    public string? MatchedText { get; init; }
    public IReadOnlyList<string>? Groups { get; init; }

    public static SearchResult NotFound => new() { Found = false };
    public static SearchResult Match(long offset, int length, string matched, IReadOnlyList<string>? groups = null)
        => new() { Found = true, Offset = offset, Length = length, MatchedText = matched, Groups = groups };
}
