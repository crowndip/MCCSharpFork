using Mc.Core.Search;
using Xunit;

namespace Mc.Core.Tests;

public sealed class GlobSearchTests
{
    private readonly ISearchProvider _provider = new GlobSearchProvider();

    [Fact]
    public void Search_GlobStarPattern_MatchesFirstLine()
    {
        var opts = new SearchOptions { Pattern = "*.txt", Type = SearchType.Glob };
        var result = _provider.Search("hello.txt\nworld.png", opts);
        Assert.True(result.Found);
        Assert.Equal(0, result.Offset);
        Assert.Equal("hello.txt", result.MatchedText);
    }

    [Fact]
    public void Search_GlobStarPattern_SkipsNonMatchingLines()
    {
        var opts = new SearchOptions { Pattern = "*.py", Type = SearchType.Glob };
        var result = _provider.Search("readme.md\nscript.py", opts);
        Assert.True(result.Found);
        Assert.Equal("script.py", result.MatchedText);
        // offset = length of "readme.md" + 1 ('\n') = 10
        Assert.Equal(10, result.Offset);
    }

    [Fact]
    public void Search_NoMatch_ReturnsNotFound()
    {
        var opts = new SearchOptions { Pattern = "*.java", Type = SearchType.Glob };
        var result = _provider.Search("readme.md\nscript.py", opts);
        Assert.False(result.Found);
    }

    [Fact]
    public void Search_QuestionMarkWildcard_MatchesSingleChar()
    {
        var opts = new SearchOptions { Pattern = "file?.txt", Type = SearchType.Glob };
        var result = _provider.Search("file1.txt\nfile22.txt", opts);
        Assert.True(result.Found);
        Assert.Equal("file1.txt", result.MatchedText);
    }

    [Fact]
    public void Search_ExactMatch_NoWildcards()
    {
        var opts = new SearchOptions { Pattern = "Makefile", Type = SearchType.Glob, CaseSensitive = false };
        var result = _provider.Search("Makefile\nREADME", opts);
        Assert.True(result.Found);
        Assert.Equal("Makefile", result.MatchedText);
    }

    [Fact]
    public void Search_BinaryData_AlwaysReturnsNotFound()
    {
        var opts = new SearchOptions { Pattern = "*", Type = SearchType.Glob };
        var result = _provider.Search(new byte[] { 0x00, 0xFF, 0x42 }.AsSpan(), opts);
        Assert.False(result.Found);
    }

    [Fact]
    public void IsPatternValid_EmptyPattern_ReturnsFalse()
    {
        var opts = new SearchOptions { Pattern = string.Empty };
        Assert.False(_provider.IsPatternValid(opts, out var error));
    }

    [Fact]
    public void IsPatternValid_NonEmptyPattern_ReturnsTrue()
    {
        var opts = new SearchOptions { Pattern = "*.cs" };
        Assert.True(_provider.IsPatternValid(opts, out _));
    }

    [Fact]
    public void Replace_AlwaysReturnsNull()
    {
        var opts = new SearchOptions { Pattern = "*.txt", Replacement = "new" };
        Assert.Null(_provider.Replace("hello.txt", opts));
    }

    [Fact]
    public void ReplaceAll_ReturnsTextUnchanged()
    {
        var opts = new SearchOptions { Pattern = "*.txt", Replacement = "new" };
        const string text = "hello.txt\nworld.txt";
        Assert.Equal(text, _provider.ReplaceAll(text, opts));
    }
}
