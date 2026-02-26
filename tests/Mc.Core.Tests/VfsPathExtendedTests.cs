using Mc.Core.Vfs;
using Xunit;

namespace Mc.Core.Tests;

public sealed class VfsPathExtendedTests
{
    // --- Extension property ---

    [Theory]
    [InlineData("/home/user/file.txt",  ".txt")]
    [InlineData("/home/user/archive.tar.gz", ".gz")]
    [InlineData("/home/user/Makefile",  "")]
    [InlineData("/home/user/.hidden",   ".hidden")]
    public void Extension_VariousFiles(string raw, string expected)
        => Assert.Equal(expected, VfsPath.FromLocal(raw).Extension);

    // --- IsRoot ---

    [Theory]
    [InlineData("/",     true)]
    [InlineData("/home", false)]
    [InlineData("",      true)]   // empty path is treated as root
    public void IsRoot_LocalPaths(string path, bool expected)
        => Assert.Equal(expected, VfsPath.FromLocal(path).IsRoot);

    // --- Parent of root stays at root ---

    [Fact]
    public void Parent_OfRootLocal_StaysAtRoot()
    {
        var root = VfsPath.FromLocal("/");
        var parent = root.Parent();
        Assert.True(parent.IsRoot);
    }

    // --- Remote path Combine uses forward slash ---

    [Fact]
    public void Combine_RemotePath_UsesForwardSlash()
    {
        var dir = VfsPath.Parse("ftp://user@host/pub");
        var child = dir.Combine("file.tar.gz");
        Assert.Equal("/pub/file.tar.gz", child.Path);
        Assert.Equal("ftp", child.Scheme);
        Assert.Equal("host", child.Host);
    }

    // --- Remote path Parent ---

    [Fact]
    public void Parent_RemotePath_PreservesSchemeAndHost()
    {
        var path = VfsPath.Parse("sftp://user@server/home/user/file.txt");
        var parent = path.Parent();
        Assert.Equal("sftp", parent.Scheme);
        Assert.Equal("server", parent.Host);
        Assert.Equal("user", parent.User);
    }

    // --- WithPath ---

    [Fact]
    public void WithPath_PreservesAllFieldsExceptPath()
    {
        var original = VfsPath.Parse("ftp://admin:pass@host:2121/old/path");
        var updated = original.WithPath("/new/path");
        Assert.Equal("ftp", updated.Scheme);
        Assert.Equal("host", updated.Host);
        Assert.Equal(2121, updated.Port);
        Assert.Equal("admin", updated.User);
        Assert.Equal("pass", updated.Password);
        Assert.Equal("/new/path", updated.Path);
    }

    // --- Parse edge cases ---

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var path = VfsPath.Parse(string.Empty);
        Assert.Equal(VfsPath.Empty, path);
    }

    [Fact]
    public void Parse_FileUri_MapsToLocal()
    {
        var path = VfsPath.Parse("file:///home/user/doc.txt");
        Assert.True(path.IsLocal);
        Assert.Equal("/home/user/doc.txt", path.Path);
    }

    [Fact]
    public void Parse_NonDefaultPort_Preserved()
    {
        var path = VfsPath.Parse("sftp://host:2222/path");
        Assert.Equal(2222, path.Port);
    }

    // --- ToString round-trip ---

    [Fact]
    public void ToString_FtpWithNonDefaultPort_RoundTrips()
    {
        const string raw = "ftp://user@host:2121/path/file.txt";
        Assert.Equal(raw, VfsPath.Parse(raw).ToString());
    }
}
