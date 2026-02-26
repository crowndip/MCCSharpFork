using Mc.Core.Vfs;
using Xunit;

namespace Mc.Core.Tests;

public sealed class VfsPathTests
{
    [Theory]
    [InlineData("/home/user/file.txt", "local", null, "/home/user/file.txt")]
    [InlineData("/",                   "local", null, "/")]
    [InlineData("ftp://user@host/path/file.txt", "ftp", "host", "/path/file.txt")]
    [InlineData("sftp://user@host:22/path",      "sftp", "host", "/path")]
    public void Parse_ValidPaths_CorrectParsing(string raw, string expectedScheme, string? expectedHost, string expectedPath)
    {
        var path = VfsPath.Parse(raw);
        Assert.Equal(expectedScheme, path.Scheme);
        Assert.Equal(expectedHost, path.Host);
        Assert.Equal(expectedPath, path.Path);
    }

    [Fact]
    public void Parse_FtpWithCredentials_ExtractsUserAndPassword()
    {
        var path = VfsPath.Parse("ftp://admin:secret@server.com/pub");
        Assert.Equal("ftp", path.Scheme);
        Assert.Equal("admin", path.User);
        Assert.Equal("secret", path.Password);
        Assert.Equal("server.com", path.Host);
        Assert.Equal("/pub", path.Path);
    }

    [Fact]
    public void Parse_EncodingHint_ExtractsEncoding()
    {
        var path = VfsPath.Parse("/home/user/file.txt#enc:UTF-8");
        Assert.Equal("local", path.Scheme);
        Assert.Equal("/home/user/file.txt", path.Path);
        Assert.Equal("UTF-8", path.Encoding);
    }

    [Fact]
    public void Parent_LocalPath_ReturnsParentDirectory()
    {
        var path = VfsPath.FromLocal("/home/user/documents/file.txt");
        var parent = path.Parent();
        Assert.Equal("/home/user/documents", parent.Path);
    }

    [Fact]
    public void Combine_AddsChildToPath()
    {
        var dir = VfsPath.FromLocal("/home/user");
        var child = dir.Combine("file.txt");
        Assert.Equal("/home/user/file.txt", child.Path);
    }

    [Fact]
    public void FileName_ReturnsLastPathComponent()
    {
        var path = VfsPath.FromLocal("/home/user/documents/report.pdf");
        Assert.Equal("report.pdf", path.FileName);
    }

    [Fact]
    public void Equality_SamePaths_AreEqual()
    {
        var a = VfsPath.Parse("/home/user/file.txt");
        var b = VfsPath.Parse("/home/user/file.txt");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentPaths_NotEqual()
    {
        var a = VfsPath.FromLocal("/home/user/a.txt");
        var b = VfsPath.FromLocal("/home/user/b.txt");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void LocalPath_IsLocalTrue()
    {
        Assert.True(VfsPath.FromLocal("/tmp").IsLocal);
        Assert.False(VfsPath.Parse("ftp://host/path").IsLocal);
    }

    [Theory]
    [InlineData("ftp://user@host/path/file.txt", "ftp://user@host/path/file.txt")]
    [InlineData("/home/user/file.txt",            "/home/user/file.txt")]
    public void ToString_RoundTrip(string original, string expected)
    {
        Assert.Equal(expected, VfsPath.Parse(original).ToString());
    }
}
