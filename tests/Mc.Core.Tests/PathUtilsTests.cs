using Mc.Core.Utilities;
using Xunit;

namespace Mc.Core.Tests;

public sealed class PathUtilsTests
{
    private static readonly string Home =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // --- GetDisplayPath ---

    [Fact]
    public void GetDisplayPath_ShortPath_ReturnsUnchanged()
    {
        const string path = "/home/user/file.txt";
        Assert.Equal(path, PathUtils.GetDisplayPath(path, 100));
    }

    [Fact]
    public void GetDisplayPath_LongPath_TruncatesFromLeft()
    {
        var path = new string('A', 30);  // 30 chars
        var result = PathUtils.GetDisplayPath(path, 10);
        Assert.Equal(10, result.Length);
        Assert.StartsWith("...", result);
        Assert.EndsWith(path[^7..], result);
    }

    [Fact]
    public void GetDisplayPath_ExactMaxLength_ReturnsUnchanged()
    {
        const string path = "1234567890";  // exactly 10 chars
        Assert.Equal(path, PathUtils.GetDisplayPath(path, 10));
    }

    // --- TildePath ---

    [Fact]
    public void TildePath_PathUnderHome_SubstitutedWithTilde()
    {
        var path = Home + "/documents/report.txt";
        var result = PathUtils.TildePath(path);
        Assert.StartsWith("~/", result);
        Assert.EndsWith("report.txt", result);
    }

    [Fact]
    public void TildePath_PathNotUnderHome_ReturnsUnchanged()
    {
        const string path = "/etc/hosts";
        Assert.Equal(path, PathUtils.TildePath(path));
    }

    // --- ExpandTilde ---

    [Fact]
    public void ExpandTilde_TildeOnly_ReturnsHome()
        => Assert.Equal(Home, PathUtils.ExpandTilde("~"));

    [Fact]
    public void ExpandTilde_TildeSlashPath_ExpandsToHome()
    {
        var result = PathUtils.ExpandTilde("~/documents");
        Assert.Equal(Home + "/documents", result);
    }

    [Fact]
    public void ExpandTilde_PathWithoutTilde_ReturnsUnchanged()
        => Assert.Equal("/etc/hosts", PathUtils.ExpandTilde("/etc/hosts"));

    // --- IsHidden ---

    [Theory]
    [InlineData(".bashrc",  true)]
    [InlineData(".hidden",  true)]
    [InlineData("visible",  false)]
    [InlineData("file.txt", false)]
    [InlineData(".",        false)]   // dot itself is not hidden
    [InlineData("..",       false)]   // double-dot is not hidden
    public void IsHidden_VariousNames(string name, bool expected)
        => Assert.Equal(expected, PathUtils.IsHidden(name));

    // --- NormalizePath ---

    [Fact]
    public void NormalizePath_BackslashesConverted()
        => Assert.Equal("a/b/c", PathUtils.NormalizePath("a\\b\\c"));

    [Fact]
    public void NormalizePath_DoubleSlashesCollapsed()
        => Assert.Equal("/a/b", PathUtils.NormalizePath("//a//b"));

    [Fact]
    public void NormalizePath_EmptyString_ReturnsEmpty()
        => Assert.Equal(string.Empty, PathUtils.NormalizePath(string.Empty));

    [Fact]
    public void NormalizePath_AlreadyNormal_Unchanged()
        => Assert.Equal("/usr/local/bin", PathUtils.NormalizePath("/usr/local/bin"));

    // --- GetPathComponents ---

    [Fact]
    public void GetPathComponents_AbsolutePath_StartsWithRoot()
    {
        var parts = PathUtils.GetPathComponents("/a/b/c").ToList();
        Assert.Equal("/", parts[0]);
        Assert.Equal(new[] { "/", "a", "b", "c" }, parts);
    }

    [Fact]
    public void GetPathComponents_RootOnly_JustRoot()
    {
        var parts = PathUtils.GetPathComponents("/").ToList();
        Assert.Single(parts);
        Assert.Equal("/", parts[0]);
    }
}
