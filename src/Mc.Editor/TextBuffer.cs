using System.Text;

namespace Mc.Editor;

/// <summary>
/// Gap buffer text storage for interactive editing.
/// A gap buffer keeps a "gap" at the cursor position so insertions/deletions
/// near the cursor are O(1). Equivalent to editbuffer.c in the original C codebase.
/// </summary>
public sealed class TextBuffer
{
    private char[] _buf;
    private int _gapStart;    // index of first gap char
    private int _gapEnd;      // index of first char after gap
    private const int InitialGapSize = 1024;

    public int Length => _buf.Length - (_gapEnd - _gapStart);
    public bool IsModified { get; private set; }
    public string LineEnding { get; private set; } = "\n";

    public TextBuffer(string? initialContent = null)
    {
        var content = initialContent ?? string.Empty;
        _buf = new char[content.Length + InitialGapSize];
        content.CopyTo(0, _buf, 0, content.Length);
        _gapStart = content.Length;
        _gapEnd = _buf.Length;
        DetectLineEnding(content);
    }

    // --- Reading ---

    public char this[int index]
    {
        get
        {
            if (index < 0 || index >= Length) throw new ArgumentOutOfRangeException(nameof(index));
            return index < _gapStart ? _buf[index] : _buf[index + (_gapEnd - _gapStart)];
        }
    }

    public string GetText() => ToString();

    public override string ToString()
    {
        var sb = new StringBuilder(Length);
        sb.Append(_buf, 0, _gapStart);
        sb.Append(_buf, _gapEnd, _buf.Length - _gapEnd);
        return sb.ToString();
    }

    public string GetLine(int lineNumber)
    {
        var text = ToString();
        var lines = text.Split('\n');
        return lineNumber >= 0 && lineNumber < lines.Length ? lines[lineNumber] : string.Empty;
    }

    public int GetLineCount()
    {
        int count = 1;
        for (int i = 0; i < Length; i++)
            if (this[i] == '\n') count++;
        return count;
    }

    public (int Line, int Column) OffsetToLineCol(int offset)
    {
        int line = 0, col = 0;
        for (int i = 0; i < offset && i < Length; i++)
        {
            if (this[i] == '\n') { line++; col = 0; }
            else col++;
        }
        return (line, col);
    }

    public int LineColToOffset(int line, int col)
    {
        int currentLine = 0, offset = 0;
        while (offset < Length && currentLine < line)
        {
            if (this[offset] == '\n') currentLine++;
            offset++;
        }
        int lineStart = offset;
        while (offset < Length && this[offset] != '\n' && offset - lineStart < col)
            offset++;
        return offset;
    }

    // --- Editing ---

    public void Insert(int position, char ch)
    {
        MoveGap(position);
        EnsureGap(1);
        _buf[_gapStart++] = ch;
        IsModified = true;
    }

    public void Insert(int position, string text)
    {
        MoveGap(position);
        EnsureGap(text.Length);
        text.CopyTo(0, _buf, _gapStart, text.Length);
        _gapStart += text.Length;
        IsModified = true;
    }

    public void Delete(int position, int count = 1)
    {
        if (count <= 0) return;
        MoveGap(position);
        _gapEnd = Math.Min(_gapEnd + count, _buf.Length);
        IsModified = true;
    }

    public void Replace(int position, int length, string text)
    {
        Delete(position, length);
        Insert(position, text);
    }

    public string Extract(int start, int length)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length && start + i < Length; i++)
            sb.Append(this[start + i]);
        return sb.ToString();
    }

    // --- Loading / Saving ---

    public static TextBuffer LoadFile(string path)
    {
        var text = File.ReadAllText(path);
        return new TextBuffer(text);
    }

    public void SaveFile(string path)
    {
        File.WriteAllText(path, ToString());
        IsModified = false;
    }

    // --- Private helpers ---

    private void MoveGap(int position)
    {
        if (position == _gapStart) return;

        if (position < _gapStart)
        {
            int moveCount = _gapStart - position;
            Array.Copy(_buf, position, _buf, _gapEnd - moveCount, moveCount);
            _gapStart = position;
            _gapEnd -= moveCount;
        }
        else
        {
            int moveCount = position - _gapStart;
            Array.Copy(_buf, _gapEnd, _buf, _gapStart, moveCount);
            _gapStart += moveCount;
            _gapEnd += moveCount;
        }
    }

    private void EnsureGap(int needed)
    {
        int gapSize = _gapEnd - _gapStart;
        if (gapSize >= needed) return;

        int newGapSize = Math.Max(needed + InitialGapSize, gapSize * 2);
        var newBuf = new char[_buf.Length + newGapSize - gapSize];
        Array.Copy(_buf, 0, newBuf, 0, _gapStart);
        int newGapEnd = _gapStart + newGapSize;
        int tailLen = _buf.Length - _gapEnd;
        Array.Copy(_buf, _gapEnd, newBuf, newGapEnd, tailLen);
        _buf = newBuf;
        _gapEnd = newGapEnd;
    }

    private void DetectLineEnding(string text)
    {
        if (text.Contains("\r\n")) LineEnding = "\r\n";
        else if (text.Contains('\r')) LineEnding = "\r";
        else LineEnding = "\n";
    }
}
