using Mc.Core.Utilities;
using Xunit;

namespace Mc.Core.Tests;

public sealed class PermissionsFormatterTests
{
    // UnixFileMode bit layout (from .NET 8 docs):
    //   OtherExecute=1, OtherWrite=2, OtherRead=4
    //   GroupExecute=8, GroupWrite=16, GroupRead=32
    //   UserExecute=64, UserWrite=128, UserRead=256
    //   StickyBit=512, SetGroup=1024, SetUser=2048

    private static readonly UnixFileMode Mode644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead;       // rw-r--r--

    private static readonly UnixFileMode Mode755 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;   // rwxr-xr-x

    [Fact]
    public void Format_RegularFile_rw_r__r__()
        => Assert.Equal("-rw-r--r--", PermissionsFormatter.Format(Mode644, isDirectory: false, isSymlink: false));

    [Fact]
    public void Format_Directory_drwxr_xr_x()
        => Assert.Equal("drwxr-xr-x", PermissionsFormatter.Format(Mode755, isDirectory: true, isSymlink: false));

    [Fact]
    public void Format_Symlink_PrefixedWithL()
        => Assert.Equal("lrwxr-xr-x", PermissionsFormatter.Format(Mode755, isDirectory: false, isSymlink: true));

    [Fact]
    public void Format_NoPermissions_AllDashes()
        => Assert.Equal("----------", PermissionsFormatter.Format(UnixFileMode.None, isDirectory: false, isSymlink: false));

    [Fact]
    public void Format_SetUID_WithExecute_ShowsLowercaseS()
    {
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                   UnixFileMode.SetUser;   // -rws------
        Assert.Equal("-rws------", PermissionsFormatter.Format(mode, isDirectory: false, isSymlink: false));
    }

    [Fact]
    public void Format_SetUID_WithoutExecute_ShowsUppercaseS()
    {
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.SetUser;   // -rwS------
        Assert.Equal("-rwS------", PermissionsFormatter.Format(mode, isDirectory: false, isSymlink: false));
    }

    [Fact]
    public void Format_SetGID_WithGroupExecute_ShowsLowercaseS()
    {
        var mode = UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.SetGroup;
        var result = PermissionsFormatter.Format(mode, isDirectory: false, isSymlink: false);
        Assert.Equal('s', result[6]);
    }

    [Fact]
    public void Format_StickyBit_WithOtherExecute_ShowsLowercaseT()
    {
        var mode = Mode755 | UnixFileMode.StickyBit;   // drwxr-xr-t
        var result = PermissionsFormatter.Format(mode, isDirectory: true, isSymlink: false);
        Assert.Equal('t', result[9]);
    }

    [Fact]
    public void Format_StickyBit_WithoutOtherExecute_ShowsUppercaseT()
    {
        var mode = UnixFileMode.OtherRead | UnixFileMode.StickyBit;
        var result = PermissionsFormatter.Format(mode, isDirectory: true, isSymlink: false);
        Assert.Equal('T', result[9]);
    }

    [Theory]
    [InlineData(420,  "0644")]   // 0644
    [InlineData(493,  "0755")]   // 0755
    [InlineData(511,  "0777")]   // 0777
    [InlineData(0,    "0000")]   // 0000
    public void FormatOctal_CorrectRepresentation(int modeValue, string expected)
        => Assert.Equal(expected, PermissionsFormatter.FormatOctal((UnixFileMode)modeValue));

    [Theory]
    [InlineData("0644", 420)]
    [InlineData("0755", 493)]
    [InlineData("0777", 511)]
    public void ParseOctal_CorrectValue(string octal, int expectedValue)
        => Assert.Equal((UnixFileMode)expectedValue, PermissionsFormatter.ParseOctal(octal));

    [Fact]
    public void ParseOctal_FormatOctal_RoundTrip()
    {
        var original = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;
        var roundTripped = PermissionsFormatter.ParseOctal(PermissionsFormatter.FormatOctal(original));
        Assert.Equal(original, roundTripped);
    }
}
