namespace Mc.Core.Search;

/// <summary>
/// Abstraction over a search algorithm.
/// Equivalent to the mc_search_struct callback system in the original C codebase.
/// </summary>
public interface ISearchProvider
{
    SearchType SupportedType { get; }

    /// <summary>Search forward in <paramref name="data"/> starting at <paramref name="startOffset"/>.</summary>
    SearchResult Search(ReadOnlySpan<byte> data, SearchOptions options, long startOffset = 0);

    /// <summary>Search in a string (text mode).</summary>
    SearchResult Search(string text, SearchOptions options, int startIndex = 0);

    /// <summary>Replace first occurrence in text and return the modified string.</summary>
    string? Replace(string text, SearchOptions options, int startIndex = 0);

    /// <summary>Replace all occurrences in text.</summary>
    string ReplaceAll(string text, SearchOptions options);

    /// <summary>Validate that the pattern in <paramref name="options"/> is syntactically correct.</summary>
    bool IsPatternValid(SearchOptions options, out string? error);
}
