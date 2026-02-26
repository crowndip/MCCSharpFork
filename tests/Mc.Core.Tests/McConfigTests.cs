using Mc.Core.Config;
using Xunit;

namespace Mc.Core.Tests;

public sealed class McConfigTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"mc_test_{Guid.NewGuid()}.ini");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    // --- Load from string content ---

    private static McConfig ParseContent(string iniContent)
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, iniContent);
            return McConfig.Load(file);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void GetString_ExistingKey_ReturnsValue()
    {
        var cfg = ParseContent("[Section]\nkey=value");
        Assert.Equal("value", cfg.GetString("Section", "key"));
    }

    [Fact]
    public void GetString_MissingKey_ReturnsDefault()
    {
        var cfg = ParseContent("[Section]\nkey=value");
        Assert.Equal("fallback", cfg.GetString("Section", "missing", "fallback"));
    }

    [Fact]
    public void GetString_MissingSection_ReturnsDefault()
    {
        var cfg = ParseContent("[Other]\nkey=value");
        Assert.Equal("default", cfg.GetString("Nowhere", "key", "default"));
    }

    [Theory]
    [InlineData("1",    true)]
    [InlineData("true", true)]
    [InlineData("yes",  true)]
    [InlineData("on",   true)]
    [InlineData("0",    false)]
    [InlineData("no",   false)]
    [InlineData("off",  false)]
    public void GetBool_TruthyAndFalsy_Values(string raw, bool expected)
    {
        var cfg = ParseContent($"[S]\nflag={raw}");
        Assert.Equal(expected, cfg.GetBool("S", "flag"));
    }

    [Fact]
    public void GetBool_MissingKey_ReturnsDefault()
    {
        var cfg = ParseContent("[S]\n");
        Assert.True(cfg.GetBool("S", "absent", defaultValue: true));
    }

    [Fact]
    public void GetInt_ValidNumber_Parsed()
    {
        var cfg = ParseContent("[S]\ncount=42");
        Assert.Equal(42, cfg.GetInt("S", "count"));
    }

    [Fact]
    public void GetInt_InvalidNumber_ReturnsDefault()
    {
        var cfg = ParseContent("[S]\ncount=notanumber");
        Assert.Equal(99, cfg.GetInt("S", "count", 99));
    }

    [Fact]
    public void GetDouble_ValidDecimal_Parsed()
    {
        var cfg = ParseContent("[S]\nratio=3.14");
        Assert.Equal(3.14, cfg.GetDouble("S", "ratio"), precision: 5);
    }

    [Fact]
    public void GetStringList_SemicolonSeparated_ReturnsList()
    {
        var cfg = ParseContent("[S]\nfiles=a.txt;b.txt;c.txt");
        var list = cfg.GetStringList("S", "files");
        Assert.Equal(3, list.Count);
        Assert.Contains("a.txt", list);
        Assert.Contains("c.txt", list);
    }

    [Fact]
    public void GetStringList_EmptyValue_ReturnsEmptyList()
    {
        var cfg = ParseContent("[S]\nfiles=");
        Assert.Empty(cfg.GetStringList("S", "files"));
    }

    [Fact]
    public void Parse_CommentsIgnored()
    {
        var cfg = ParseContent("# This is a comment\n[S]\n; Also ignored\nkey=value");
        Assert.Equal("value", cfg.GetString("S", "key"));
    }

    [Fact]
    public void HasKey_ExistingKey_True()
    {
        var cfg = ParseContent("[S]\nkey=v");
        Assert.True(cfg.HasKey("S", "key"));
    }

    [Fact]
    public void HasKey_MissingKey_False()
    {
        var cfg = ParseContent("[S]\nkey=v");
        Assert.False(cfg.HasKey("S", "other"));
    }

    [Fact]
    public void GetSections_ReturnsParsedSections()
    {
        var cfg = ParseContent("[Alpha]\nx=1\n[Beta]\ny=2");
        var sections = cfg.GetSections();
        Assert.Contains("Alpha", sections);
        Assert.Contains("Beta", sections);
    }

    [Fact]
    public void GetKeys_ReturnsKeysInSection()
    {
        var cfg = ParseContent("[S]\na=1\nb=2\nc=3");
        var keys = cfg.GetKeys("S");
        Assert.Equal(3, keys.Count);
    }

    [Fact]
    public void Set_ThenGet_RoundTrip()
    {
        var cfg = new McConfig();
        cfg.Set("Sec", "name", "world");
        Assert.Equal("world", cfg.GetString("Sec", "name"));
    }

    [Fact]
    public void Save_And_Load_RoundTrip()
    {
        var cfg = new McConfig();
        cfg.Set("Panel", "show_hidden", true);
        cfg.Set("Panel", "columns", 80);
        cfg.Set("Colors", "theme", "classic");
        cfg.Save(_tempFile);

        var loaded = McConfig.Load(_tempFile);
        Assert.True(loaded.GetBool("Panel", "show_hidden"));
        Assert.Equal(80, loaded.GetInt("Panel", "columns"));
        Assert.Equal("classic", loaded.GetString("Colors", "theme"));
    }
}
