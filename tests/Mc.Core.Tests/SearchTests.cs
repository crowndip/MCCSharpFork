using Mc.Core.Search;
using Xunit;

namespace Mc.Core.Tests;

public sealed class NormalSearchTests
{
    private readonly ISearchProvider _provider = new NormalSearchProvider();

    [Fact]
    public void Search_ExactMatch_ReturnsCorrectOffset()
    {
        var opts = new SearchOptions { Pattern = "hello", CaseSensitive = false };
        var result = _provider.Search("say hello world", opts);
        Assert.True(result.Found);
        Assert.Equal(4, result.Offset);
        Assert.Equal(5, result.Length);
    }

    [Fact]
    public void Search_NoMatch_ReturnsNotFound()
    {
        var opts = new SearchOptions { Pattern = "xyz" };
        var result = _provider.Search("hello world", opts);
        Assert.False(result.Found);
    }

    [Fact]
    public void Search_CaseInsensitive_FindsMatch()
    {
        var opts = new SearchOptions { Pattern = "HELLO", CaseSensitive = false };
        var result = _provider.Search("say hello world", opts);
        Assert.True(result.Found);
    }

    [Fact]
    public void Search_CaseSensitive_DoesNotFindMismatch()
    {
        var opts = new SearchOptions { Pattern = "HELLO", CaseSensitive = true };
        var result = _provider.Search("say hello world", opts);
        Assert.False(result.Found);
    }

    [Fact]
    public void Search_WholeWords_MatchesOnlyWhole()
    {
        var opts = new SearchOptions { Pattern = "he", WholeWords = true };
        var result = _provider.Search("he said hello", opts);
        Assert.True(result.Found);
        Assert.Equal(0, result.Offset);
    }

    [Fact]
    public void Search_WholeWords_DoesNotMatchPartial()
    {
        var opts = new SearchOptions { Pattern = "ell", WholeWords = true };
        var result = _provider.Search("hello world", opts);
        Assert.False(result.Found);
    }

    [Fact]
    public void ReplaceAll_ReplacesAllOccurrences()
    {
        var opts = new SearchOptions { Pattern = "cat", Replacement = "dog", CaseSensitive = false };
        var result = _provider.ReplaceAll("the cat sat on the cat mat", opts);
        Assert.Equal("the dog sat on the dog mat", result);
    }
}

public sealed class RegexSearchTests
{
    private readonly ISearchProvider _provider = new RegexSearchProvider();

    [Fact]
    public void Search_ValidRegex_Matches()
    {
        var opts = new SearchOptions { Pattern = @"\d+", Type = SearchType.Regex };
        var result = _provider.Search("hello 123 world", opts);
        Assert.True(result.Found);
        Assert.Equal(6, result.Offset);
        Assert.Equal("123", result.MatchedText);
    }

    [Fact]
    public void Search_InvalidRegex_ReturnsFalse()
    {
        var opts = new SearchOptions { Pattern = "[invalid", Type = SearchType.Regex };
        var result = _provider.Search("text", opts);
        Assert.False(result.Found);
    }

    [Fact]
    public void IsPatternValid_InvalidPattern_ReturnsFalseWithError()
    {
        var opts = new SearchOptions { Pattern = "(unclosed", Type = SearchType.Regex };
        Assert.False(_provider.IsPatternValid(opts, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void ReplaceAll_WithGroups_ReplacesCorrectly()
    {
        var opts = new SearchOptions
        {
            Pattern = @"(\w+)\s(\w+)",
            Replacement = "$2 $1",
            Type = SearchType.Regex,
        };
        var result = _provider.ReplaceAll("hello world", opts);
        Assert.Equal("world hello", result);
    }
}

public sealed class HexSearchTests
{
    private readonly ISearchProvider _provider = new HexSearchProvider();

    [Fact]
    public void Search_ValidHexPattern_Matches()
    {
        var opts = new SearchOptions { Pattern = "48 65 6C 6C 6F", Type = SearchType.Hex };
        var data = "Hello World"u8.ToArray();
        var result = _provider.Search(data.AsSpan(), opts);
        Assert.True(result.Found);
        Assert.Equal(0, result.Offset);
    }

    [Fact]
    public void IsPatternValid_InvalidHex_ReturnsFalse()
    {
        var opts = new SearchOptions { Pattern = "GG HH" };
        Assert.False(_provider.IsPatternValid(opts, out var error));
        Assert.NotNull(error);
    }
}
