using System.Text;
using Mc.Core.Search;

namespace Mc.Viewer;

public enum ViewMode { Text, Hex, Raw }

/// <summary>
/// Business logic for the file viewer.
/// Supports text, hex, and raw modes with search.
/// Equivalent to src/viewer/ in the original C codebase.
/// </summary>
public sealed class ViewerController : IDisposable
{
    private byte[] _data = [];
    private string? _textCache;
    private string? _filePath;
    private Encoding _encoding = Encoding.UTF8;

    public ViewMode Mode { get; set; } = ViewMode.Text;
    public int ScrollLine { get; private set; }
    public int ScrollCol { get; private set; }
    public bool WrapLines { get; set; } = true;
    public string? FilePath => _filePath;
    public long FileSize => _data.Length;
    public Encoding Encoding
    {
        get => _encoding;
        set { _encoding = value; _textCache = null; }
    }

    // Search
    public SearchOptions LastSearch { get; set; } = new();
    private long _lastSearchOffset;

    public event EventHandler? Changed;

    public void LoadFile(string path)
    {
        _filePath = path;
        _data = File.ReadAllBytes(path);
        _textCache = null;
        ScrollLine = 0;
        ScrollCol = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void LoadStream(Stream stream, string? name = null)
    {
        _filePath = name;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        _data = ms.ToArray();
        _textCache = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string GetText()
    {
        _textCache ??= _encoding.GetString(_data);
        return _textCache;
    }

    public ReadOnlyMemory<byte> GetBytes() => _data;

    public IReadOnlyList<string> GetLines(int startLine, int count, int viewWidth)
    {
        var text = GetText();
        var allLines = SplitLines(text, viewWidth);
        return allLines.Skip(startLine).Take(count).ToList();
    }

    public int TotalLineCount(int viewWidth)
    {
        return SplitLines(GetText(), viewWidth).Count;
    }

    public void ScrollDown(int lines = 1)
    {
        ScrollLine += lines;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollUp(int lines = 1)
    {
        ScrollLine = Math.Max(0, ScrollLine - lines);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollRight(int cols = 8) { ScrollCol += cols; Changed?.Invoke(this, EventArgs.Empty); }
    public void ScrollLeft(int cols = 8) { ScrollCol = Math.Max(0, ScrollCol - cols); Changed?.Invoke(this, EventArgs.Empty); }

    public void GoToStart() { ScrollLine = 0; ScrollCol = 0; Changed?.Invoke(this, EventArgs.Empty); }
    public void GoToEnd(int viewHeight, int viewWidth)
    {
        ScrollLine = Math.Max(0, TotalLineCount(viewWidth) - viewHeight);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // --- Hex mode ---

    public IReadOnlyList<string> GetHexLines(int startLine, int count, int bytesPerRow = 16)
    {
        var lines = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            int offset = (startLine + i) * bytesPerRow;
            if (offset >= _data.Length) break;
            lines.Add(FormatHexLine(offset, bytesPerRow));
        }
        return lines;
    }

    public int TotalHexLineCount(int bytesPerRow = 16)
        => (_data.Length + bytesPerRow - 1) / bytesPerRow;

    private string FormatHexLine(int offset, int bytesPerRow)
    {
        var sb = new StringBuilder();
        sb.Append($"{offset:X8}  ");

        // Hex bytes
        for (int i = 0; i < bytesPerRow; i++)
        {
            if (offset + i < _data.Length)
                sb.Append($"{_data[offset + i]:X2} ");
            else
                sb.Append("   ");

            if (i == bytesPerRow / 2 - 1) sb.Append(' ');
        }

        sb.Append(" |");

        // ASCII representation
        for (int i = 0; i < bytesPerRow && offset + i < _data.Length; i++)
        {
            byte b = _data[offset + i];
            sb.Append(b is >= 32 and < 127 ? (char)b : '.');
        }
        sb.Append('|');

        return sb.ToString();
    }

    // --- Search ---

    public SearchResult FindNext(SearchOptions opts)
    {
        ISearchProvider provider = opts.Type switch
        {
            SearchType.Hex => new HexSearchProvider(),
            SearchType.Regex => new RegexSearchProvider(),
            _ => new NormalSearchProvider(),
        };

        SearchResult result;
        if (opts.Type == SearchType.Hex)
        {
            result = provider.Search(_data.AsSpan(), opts, _lastSearchOffset + 1);
        }
        else
        {
            var text = GetText();
            result = provider.Search(text, opts, (int)(_lastSearchOffset + 1));
        }

        if (result.Found)
        {
            _lastSearchOffset = result.Offset;
            // Scroll to the match
        }
        LastSearch = opts;
        return result;
    }

    private List<string> SplitLines(string text, int maxWidth)
    {
        if (!WrapLines)
            return [.. text.Split('\n')];

        var lines = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length <= maxWidth) { lines.Add(line); continue; }
            int pos = 0;
            while (pos < line.Length)
            {
                lines.Add(line[pos..Math.Min(pos + maxWidth, line.Length)]);
                pos += maxWidth;
            }
        }
        return lines;
    }

    public void Dispose() { _data = []; }
}
