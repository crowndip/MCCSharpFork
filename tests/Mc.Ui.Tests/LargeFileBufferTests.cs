using System.Text;
using Mc.Editor;
using Mc.Core.Search;
using Xunit;

namespace Mc.Ui.Tests;

public sealed class LargeFileBufferTests : IDisposable
{
    private readonly string _tempFile;

    public LargeFileBufferTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    /// <summary>Write N lines to the temp file (no BOM) and return the line texts.</summary>
    private string[] WriteLines(int count, string prefix = "Line")
    {
        var lines = Enumerable.Range(0, count)
            .Select(i => $"{prefix} {i:D6}: content_here_{i}")
            .ToArray();
        // Write without BOM so line content is predictable
        File.WriteAllLines(_tempFile, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return lines;
    }

    [Fact]
    public void GetLineCount_ReturnsCorrectTotal()
    {
        var lines = WriteLines(200);
        var buf = LargeFileBuffer.Open(_tempFile);
        // File.WriteAllLines appends a trailing newline → 200 lines + 1 empty line = 201
        // This matches TextBuffer.GetLineCount() behaviour (counts 1 + number_of_newlines).
        Assert.Equal((long)(lines.Length + 1), buf.GetLineCount());
    }

    [Fact]
    public void GetLine_ReturnsCorrectContent()
    {
        var lines = WriteLines(100);
        var buf = LargeFileBuffer.Open(_tempFile);

        Assert.Equal(lines[0],  buf.GetLine(0L));
        Assert.Equal(lines[50], buf.GetLine(50L));
        Assert.Equal(lines[99], buf.GetLine(99L));
    }

    [Fact]
    public void OffsetToLineCol_ConvertsCorrectly()
    {
        WriteLines(10);
        var buf = LargeFileBuffer.Open(_tempFile);

        // Window-relative offset 0 → absolute line 0, col 0
        var (line, col) = buf.OffsetToLineCol(0);
        Assert.Equal(0L, line);
        Assert.Equal(0,  col);
    }

    [Fact]
    public void LineColToOffset_RoundTrips()
    {
        var lines = WriteLines(50);
        var buf = LargeFileBuffer.Open(_tempFile);

        for (int ln = 0; ln < 50; ln += 10)
        {
            int off = buf.LineColToOffset((long)ln, 0);
            var (gotLine, gotCol) = buf.OffsetToLineCol(off);
            Assert.Equal((long)ln, gotLine);
            Assert.Equal(0, gotCol);
        }
    }

    [Fact]
    public void GetLine_WorksAcrossWindowBoundary()
    {
        // Write more lines than the window size (3000) so we must cross windows
        var lines = WriteLines(6500);
        var buf = LargeFileBuffer.Open(_tempFile);

        // Lines at start, middle, and end should all return correctly
        Assert.Equal(lines[0],    buf.GetLine(0L));
        Assert.Equal(lines[3250], buf.GetLine(3250L));
        Assert.Equal(lines[6499], buf.GetLine(6499L));
    }

    [Fact]
    public void StreamSearch_FindsForward()
    {
        var lines = WriteLines(100, "TestLine");
        var buf = LargeFileBuffer.Open(_tempFile);

        var provider = new NormalSearchProvider();
        var opts = new SearchOptions { Pattern = "TestLine 000050", CaseSensitive = true };
        var result = buf.StreamSearch(provider, opts, 0L);

        Assert.True(result.Found);
        // Seek to the result (absolute char offset) and check it lands on line 50
        int windowOffset = buf.SeekToAbsChar(result.Offset);
        var (foundLine, _) = buf.OffsetToLineCol(windowOffset);
        Assert.Equal(50L, foundLine);
    }

    [Fact]
    public void StreamSearch_ReturnsNotFoundWhenPatternAbsent()
    {
        WriteLines(50);
        var buf = LargeFileBuffer.Open(_tempFile);

        var provider = new NormalSearchProvider();
        var opts = new SearchOptions { Pattern = "XYZZY_NOT_HERE", CaseSensitive = true };
        var result = buf.StreamSearch(provider, opts, 0L);

        Assert.False(result.Found);
    }

    [Fact]
    public void StreamSearch_FindsBackward()
    {
        WriteLines(100, "BackLine");
        var buf = LargeFileBuffer.Open(_tempFile);

        // First find forward to get a position
        var provider = new NormalSearchProvider();
        var opts = new SearchOptions { Pattern = "BackLine 000099", CaseSensitive = true };
        var fwd = buf.StreamSearch(provider, opts, 0L);
        Assert.True(fwd.Found);

        // Now search backward from the forward result's absolute offset
        var optsBack = new SearchOptions { Pattern = "BackLine 000050", CaseSensitive = true, Backward = true };
        var bwd = buf.StreamSearch(provider, optsBack, fwd.Offset);
        Assert.True(bwd.Found);
        int windowOffset = buf.SeekToAbsChar(bwd.Offset);
        var (ln, _) = buf.OffsetToLineCol(windowOffset);
        Assert.Equal(50L, ln);
    }

    [Fact]
    public void Length_ReturnsTotalCharsForSmallFile()
    {
        // For a file that fits within the window, Length == total window size ≈ total file chars
        var lines = WriteLines(50);
        var buf = LargeFileBuffer.Open(_tempFile);

        // Each line has its content + newline written by File.WriteAllLines
        long expectedChars = lines.Sum(l => (long)l.Length + 1);
        // Allow for final newline difference; window covers the whole file
        Assert.InRange(buf.Length, (int)(expectedChars - 2), (int)(expectedChars + 2));
    }

    [Fact]
    public void IndexerAccess_ReturnsCorrectChar()
    {
        WriteLines(10);
        var buf = LargeFileBuffer.Open(_tempFile);

        // The very first character should be 'L' (start of "Line")
        Assert.Equal('L', buf[0]);
        // LineColToOffset(1, 0) - 1 = last char of line 0 = '\n'
        int firstNewline = buf.LineColToOffset(1L, 0) - 1;
        Assert.Equal('\n', buf[firstNewline]);
    }
}
