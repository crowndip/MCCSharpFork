using Mc.Core.Utilities;
using Xunit;

namespace Mc.Core.Tests;

public sealed class FileSizeFormatterTests
{
    [Theory]
    [InlineData(0,    "0")]
    [InlineData(1,    "1")]
    [InlineData(1023, "1023")]
    public void Format_BelowOneKib_ReturnsRawBytes(long bytes, string expected)
        => Assert.Equal(expected, FileSizeFormatter.Format(bytes));

    [Theory]
    [InlineData(1024,        "1.0K")]
    [InlineData(1536,        "1.5K")]
    [InlineData(10 * 1024,   "10.0K")]
    [InlineData(100 * 1024,  "100K")]   // >= 100 → no decimal
    public void Format_Kibibytes_FormatCorrectly(long bytes, string expected)
        => Assert.Equal(expected, FileSizeFormatter.Format(bytes));

    [Theory]
    [InlineData(1024L * 1024,            "1.0M")]
    [InlineData(100L * 1024 * 1024,      "100M")]
    [InlineData(1024L * 1024 * 1024,     "1.0G")]
    [InlineData(1024L * 1024 * 1024 * 1024, "1.0T")]
    public void Format_LargerUnits_AreRecognised(long bytes, string expected)
        => Assert.Equal(expected, FileSizeFormatter.Format(bytes));

    [Fact]
    public void Format_HumanReadableFalse_ReturnsRawNumber()
        => Assert.Equal("1048576", FileSizeFormatter.Format(1048576L, humanReadable: false));

    [Theory]
    [InlineData(0,         "0")]
    [InlineData(1234,      "1,234")]
    [InlineData(1234567,   "1,234,567")]
    public void FormatExact_AddsSeparators(long bytes, string expected)
        => Assert.Equal(expected, FileSizeFormatter.FormatExact(bytes));

    [Fact]
    public void FormatPanelSize_Directory_ReturnsDirTag()
        => Assert.Equal("<DIR>", FileSizeFormatter.FormatPanelSize(0, isDirectory: true));

    [Fact]
    public void FormatPanelSize_File_UsesHumanFormat()
        => Assert.Equal(FileSizeFormatter.Format(2048), FileSizeFormatter.FormatPanelSize(2048, isDirectory: false));
}
