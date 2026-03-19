using System.Text;
using Mc.Core.Search;

namespace Mc.Editor;

/// <summary>
/// Virtual text buffer for large files.
///
/// INDEX DESIGN — two-level block index
///   BLOCK_SIZE = 4096 lines per block.
///   _blockByteOffsets[b] = byte offset of the first byte of line (b * BLOCK_SIZE).
///   _blockCharOffsets[b] = cumulative char count of all chars before line (b * BLOCK_SIZE).
///
///   Memory for 1 billion lines:
///     1 000 000 000 / 4096 ≈ 244 141 blocks
///     244 141 × 8 bytes × 2 arrays ≈ 3.9 MB  (vs 16 GB for a dense index)
///
/// WINDOW
///   Only ~3000 lines are loaded into a TextBuffer at a time.
///   All ITextBuffer int offsets are WINDOW-RELATIVE — the start of the window is offset 0.
///   Absolute char position = _windowFirstChar + windowOffset.
///
/// ASYNC INDEX BUILD
///   BuildIndex() runs on a background thread via Task.Run.
///   Call EnsureReady() (blocks) or check IsReady before calling any method that
///   needs the index.  IndexingComplete fires when the background task finishes.
/// </summary>
public sealed class LargeFileBuffer : ITextBuffer
{
    /// <summary>Files larger than this are opened in large-file mode.</summary>
    public const long LargeFileThresholdBytes = 10L * 1024 * 1024; // 10 MB

    // Number of lines per index block.
    private const int BlockSize = 4096;

    // Window: how many lines to keep loaded at once.
    private const int WindowLines = 3000;

    // --- File info ---
    private readonly string _filePath;
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    // --- Two-level block index (built on background thread) ---
    // _blockByteOffsets[b] = byte offset of line  (b * BlockSize)
    // _blockCharOffsets[b] = char offset of line  (b * BlockSize)
    private long[] _blockByteOffsets = [];
    private long[] _blockCharOffsets = [];
    private long _totalLines;
    private long _totalChars;

    // --- BOM ---
    private bool _hasBom;

    // --- Async build ---
    private Task? _buildTask;
    private volatile bool _isReady;

    /// <summary>True once the background index build has finished.</summary>
    public bool IsReady => _isReady;

    /// <summary>Block until the index is fully built.</summary>
    public void EnsureReady() => _buildTask?.Wait();

    /// <summary>Fired on the thread-pool when indexing completes.</summary>
    public event EventHandler? IndexingComplete;

    // --- Loaded window ---
    private long _windowFirstLine;  // absolute index of window's first line
    private long _windowLastLine;   // absolute index of window's last line (inclusive)
    private long _windowFirstChar;  // cumulative char count before the window start

    // Editable gap-buffer holding the loaded window content
    private TextBuffer _window = new();

    // For 3-part save: original byte range that the loaded window covers
    private long _origWindowStartByte;
    private long _origWindowEndByte;

    // --- ITextBuffer ---
    public string LineEnding { get; private set; } = "\n";
    public bool IsModified => _window.IsModified;

    /// <summary>Size of the loaded window in characters (not the full file).</summary>
    public int Length => _window.Length;

    // char[windowOffset] — offset is relative to the start of the loaded window
    public char this[int windowOffset] => _window[windowOffset];

    private LargeFileBuffer(string filePath) { _filePath = filePath; }

    // ── Factory ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Open a large file.  The index is built synchronously (fast enough for up to ~100 MB;
    /// for truly huge files the caller should show a progress indicator and call
    /// EnsureReady() or await the task).
    /// The first window is loaded synchronously so the file is immediately usable.
    /// </summary>
    public static LargeFileBuffer Open(string filePath)
    {
        var buf = new LargeFileBuffer(filePath);
        // Build index synchronously for now; convert to async if startup latency matters.
        buf.BuildIndex();
        buf._isReady = true;
        buf.LoadWindow(0);
        return buf;
    }

    /// <summary>
    /// Open a large file with asynchronous index building.
    /// The caller should subscribe to IndexingComplete and call EnsureReady()
    /// before navigating past the first window.
    /// </summary>
    public static LargeFileBuffer OpenAsync(string filePath)
    {
        var buf = new LargeFileBuffer(filePath);
        buf._buildTask = Task.Run(() =>
        {
            buf.BuildIndex();
            buf._isReady = true;
            buf.IndexingComplete?.Invoke(buf, EventArgs.Empty);
        });
        // Load a small first-window using a temporary line scan so the file is
        // immediately visible while the full index builds.
        buf.LoadWindowRaw(0, Math.Min(WindowLines, 10000));
        return buf;
    }

    // ── Index building ───────────────────────────────────────────────────────

    private void BuildIndex()
    {
        var blockByteOffsets = new List<long>(256) { 0L };
        var blockCharOffsets = new List<long>(256) { 0L };

        using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 65536, useAsync: false);

        // Detect and skip UTF-8 BOM (EF BB BF).
        long startByte = 0;
        {
            var bom = new byte[3];
            int bomRead = stream.Read(bom, 0, 3);
            if (bomRead == 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            {
                _hasBom = true;
                startByte = 3;
                blockByteOffsets[0] = 3L; // Block 0 / line 0 starts after the BOM
            }
            else
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
        }

        var buf = new byte[65536];
        long bytePos = startByte;
        long charPos = 0;
        long lineCount = 0;  // number of newlines seen so far (= next line index - 1)
        bool sawCr = false;
        bool lineEndingDetected = false;
        int read;

        while ((read = stream.Read(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                byte b = buf[i];
                // Count Unicode scalar values: skip UTF-8 continuation bytes (0x80–0xBF).
                if ((b & 0xC0) != 0x80) charPos++;

                if (b == (byte)'\n')
                {
                    if (!lineEndingDetected)
                    {
                        LineEnding = sawCr ? "\r\n" : "\n";
                        lineEndingDetected = true;
                    }
                    sawCr = false;
                    lineCount++;

                    // Every BlockSize-th line boundary → save a block entry.
                    if (lineCount % BlockSize == 0)
                    {
                        blockByteOffsets.Add(bytePos + 1);   // byte offset of line lineCount
                        blockCharOffsets.Add(charPos);        // char offset of line lineCount
                    }
                }
                else
                {
                    sawCr = b == (byte)'\r';
                }
                bytePos++;
            }
        }

        if (!lineEndingDetected && sawCr) LineEnding = "\r";

        _totalChars = charPos;
        _totalLines = lineCount + 1;  // number of lines = number of newlines + 1
        _blockByteOffsets = blockByteOffsets.ToArray();
        _blockCharOffsets = blockCharOffsets.ToArray();
    }

    // ── Block-index helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Return the byte offset and cumulative char offset of the start of the given
    /// absolute line by: (1) jumping to its block entry, then (2) scanning forward
    /// through at most BlockSize-1 newlines.
    /// </summary>
    private (long ByteOffset, long CharOffset) LineStartPosition(long absoluteLine)
    {
        if (absoluteLine < 0) absoluteLine = 0;
        if (absoluteLine >= _totalLines) absoluteLine = _totalLines - 1;

        int block = (int)(absoluteLine / BlockSize);
        long bytePos = _blockByteOffsets[block];
        long charPos = _blockCharOffsets[block];
        long linesToSkip = absoluteLine - (long)block * BlockSize;

        if (linesToSkip == 0) return (bytePos, charPos);

        // Scan forward from the block start, counting newlines.
        using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 16384, useAsync: false);
        stream.Seek(bytePos, SeekOrigin.Begin);

        var scanBuf = new byte[8192];
        long skipped = 0;
        int r;
        while (skipped < linesToSkip && (r = stream.Read(scanBuf, 0, scanBuf.Length)) > 0)
        {
            for (int i = 0; i < r && skipped < linesToSkip; i++)
            {
                byte b = scanBuf[i];
                if ((b & 0xC0) != 0x80) charPos++;
                bytePos++;
                if (b == (byte)'\n') skipped++;
            }
        }
        return (bytePos, charPos);
    }

    /// <summary>Binary search in _blockCharOffsets to find the block containing absChar.</summary>
    private int BlockForAbsChar(long absChar)
    {
        int lo = 0, hi = _blockCharOffsets.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_blockCharOffsets[mid] <= absChar) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>Find the absolute line number of the line containing absChar.</summary>
    private long LineForAbsChar(long absChar)
    {
        if (absChar <= 0) return 0;
        int block = BlockForAbsChar(absChar);
        long bytePos = _blockByteOffsets[block];
        long charPos = _blockCharOffsets[block];
        long lineNo  = (long)block * BlockSize;

        // Scan forward to find the exact line.
        using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 16384, useAsync: false);
        stream.Seek(bytePos, SeekOrigin.Begin);
        var scanBuf = new byte[8192];
        int r;
        while (charPos < absChar && (r = stream.Read(scanBuf, 0, scanBuf.Length)) > 0)
        {
            for (int i = 0; i < r && charPos < absChar; i++)
            {
                byte b = scanBuf[i];
                if ((b & 0xC0) != 0x80) charPos++;
                if (b == (byte)'\n' && charPos <= absChar) lineNo++;
            }
        }
        return lineNo;
    }

    // ── Window management ────────────────────────────────────────────────────

    /// <summary>Load a window centred on the given absolute line number.</summary>
    private void LoadWindow(long centerLine)
    {
        EnsureReady();
        long startLine = Math.Max(0, centerLine - WindowLines / 2);
        long endLine   = Math.Min(_totalLines - 1, startLine + WindowLines - 1);
        if (endLine - startLine < Math.Min(WindowLines - 1, _totalLines - 1))
            startLine = Math.Max(0, endLine - WindowLines + 1);

        var (startByte, startChar) = LineStartPosition(startLine);
        var (endByte, _)           = endLine + 1 < _totalLines
            ? LineStartPosition(endLine + 1)
            : (new FileInfo(_filePath).Length, _totalChars);

        _windowFirstLine = startLine;
        _windowLastLine  = endLine;
        _windowFirstChar = startChar;
        _origWindowStartByte = startByte;
        _origWindowEndByte   = endByte;

        using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 65536, useAsync: false);
        stream.Seek(startByte, SeekOrigin.Begin);
        bool needBomDetect = _hasBom && startByte == 0;
        using var reader = new StreamReader(stream, Utf8NoBom,
            detectEncodingFromByteOrderMarks: needBomDetect, bufferSize: 65536, leaveOpen: true);

        var sb = new StringBuilder();
        for (long ln = startLine; ln <= endLine; ln++)
        {
            var lineText = reader.ReadLine();
            if (lineText == null) break;
            sb.Append(lineText);
            if (ln < endLine || endLine < _totalLines - 1)
                sb.Append('\n');
        }
        _window = new TextBuffer(sb.ToString());
    }

    /// <summary>
    /// Raw window load used before the index is ready (OpenAsync path).
    /// Reads up to maxLines lines from the start of the file.
    /// </summary>
    private void LoadWindowRaw(long startByteOffset, int maxLines)
    {
        using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 65536, useAsync: false);
        stream.Seek(startByteOffset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Utf8NoBom,
            detectEncodingFromByteOrderMarks: _hasBom && startByteOffset == 0,
            bufferSize: 65536, leaveOpen: true);
        var sb = new StringBuilder();
        int linesRead = 0;
        string? line;
        while (linesRead < maxLines && (line = reader.ReadLine()) != null)
        {
            if (linesRead > 0) sb.Append('\n');
            sb.Append(line);
            linesRead++;
        }
        _windowFirstLine = 0;
        _windowLastLine  = linesRead - 1;
        _windowFirstChar = 0;
        _origWindowStartByte = 0;
        _origWindowEndByte   = stream.Position;
        _window = new TextBuffer(sb.ToString());
    }

    /// <summary>Shift the window so that absoluteLine is inside it.</summary>
    private void EnsureWindowContainsLine(long absoluteLine)
    {
        if (absoluteLine >= _windowFirstLine && absoluteLine <= _windowLastLine) return;
        LoadWindow(absoluteLine);
    }

    // ── ITextBuffer implementation ───────────────────────────────────────────

    public string GetText() => _window.GetText();

    public string GetLine(long lineNumber)
    {
        if (lineNumber < 0 || lineNumber >= _totalLines) return string.Empty;
        EnsureWindowContainsLine(lineNumber);
        long relLine = lineNumber - _windowFirstLine;
        return _window.GetLine(relLine);
    }

    public long GetLineCount()
    {
        EnsureReady();
        return _totalLines;
    }

    /// <summary>
    /// Convert a window-relative offset to (absolute line, column).
    /// </summary>
    public (long Line, int Column) OffsetToLineCol(int windowOffset)
    {
        var (relLine, col) = _window.OffsetToLineCol(windowOffset);
        return (_windowFirstLine + relLine, col);
    }

    /// <summary>
    /// Convert (absolute line, column) to a window-relative offset.
    /// Shifts the window if the line is not currently loaded.
    /// </summary>
    public int LineColToOffset(long line, int col)
    {
        if (line < 0) line = 0;
        EnsureReady();
        if (line >= _totalLines) line = _totalLines - 1;
        EnsureWindowContainsLine(line);
        long relLine = line - _windowFirstLine;
        return _window.LineColToOffset(relLine, col);
    }

    public string Extract(int windowOffset, int length)
    {
        if (windowOffset < 0) windowOffset = 0;
        int end = Math.Min(windowOffset + length, _window.Length);
        return _window.Extract(windowOffset, end - windowOffset);
    }

    // --- Mutation (not supported in view-only mode) ---

    public void Insert(int position, char ch)     => throw new InvalidOperationException("Large-file view mode: load the full file to edit.");
    public void Insert(int position, string text) => throw new InvalidOperationException("Large-file view mode: load the full file to edit.");
    public void Delete(int position, int count = 1) => throw new InvalidOperationException("Large-file view mode: load the full file to edit.");
    public void Replace(int position, int length, string text) => throw new InvalidOperationException("Large-file view mode: load the full file to edit.");
    public void SetContent(string content) => throw new InvalidOperationException("Large-file view mode: load the full file to edit.");

    public void SaveFile(string path)
    {
        if (!IsModified)
        {
            if (!string.Equals(path, _filePath, StringComparison.Ordinal))
                File.Copy(_filePath, path, overwrite: true);
            return;
        }
        // 3-part save: pre-window (bytes) + window content (text) + post-window (bytes)
        var tmp = path + ".lfb_tmp";
        using (var outStream = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            if (_origWindowStartByte > 0)
            {
                using var inStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                CopyBytes(inStream, outStream, _origWindowStartByte);
            }
            var windowBytes = Utf8NoBom.GetBytes(_window.GetText());
            outStream.Write(windowBytes, 0, windowBytes.Length);
            using (var inStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                inStream.Seek(_origWindowEndByte, SeekOrigin.Begin);
                CopyBytesToEnd(inStream, outStream);
            }
        }
        File.Move(tmp, path, overwrite: true);
    }

    // ── Absolute position helpers ────────────────────────────────────────────

    /// <summary>
    /// Move the window to contain absChar and return the window-relative offset.
    /// Used by search result placement in EditorController.
    /// </summary>
    public int SeekToAbsChar(long absChar)
    {
        EnsureReady();
        if (absChar < 0) absChar = 0;
        if (absChar > _totalChars) absChar = _totalChars;
        long line = LineForAbsChar(absChar);
        EnsureWindowContainsLine(line);
        // absChar relative to window start
        int windowOffset = (int)(absChar - _windowFirstChar);
        return Math.Clamp(windowOffset, 0, _window.Length);
    }

    /// <summary>Absolute char position of a window-relative offset.</summary>
    public long AbsoluteChar(int windowOffset) => _windowFirstChar + windowOffset;

    // ── Streaming search ─────────────────────────────────────────────────────

    /// <summary>
    /// Search the entire file by streaming it in chunks.
    /// <paramref name="startAbsChar"/> is an absolute char offset into the file.
    /// Returns a result whose Offset is also an absolute char offset (pass to SeekToAbsChar).
    /// </summary>
    public SearchResult StreamSearch(ISearchProvider provider, SearchOptions options, long startAbsChar)
    {
        if (options.Backward)
            return StreamSearchBackward(provider, options, startAbsChar);
        return StreamSearchForward(provider, options, startAbsChar);
    }

    private SearchResult StreamSearchForward(ISearchProvider provider, SearchOptions options, long fromAbsChar)
    {
        const int ChunkChars = 512 * 1024;
        int overlap = Math.Max(options.Pattern?.Length ?? 0, 1024);

        // Seek to the block/line that contains fromAbsChar
        long startLine = LineForAbsChar(fromAbsChar);
        var (startByte, startLineChar) = LineStartPosition(startLine);

        using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 65536, useAsync: false);
        stream.Seek(startByte, SeekOrigin.Begin);
        bool detectBom = _hasBom && startByte == 0;
        using var reader = new StreamReader(stream, Utf8NoBom,
            detectEncodingFromByteOrderMarks: detectBom, bufferSize: 65536, leaveOpen: true);

        // Skip chars within the start line to reach fromAbsChar
        long toSkip = fromAbsChar - startLineChar;
        if (toSkip > 0)
        {
            var skipBuf = new char[4096];
            long remaining = toSkip;
            while (remaining > 0)
            {
                int n = reader.Read(skipBuf, 0, (int)Math.Min(remaining, skipBuf.Length));
                if (n == 0) break;
                remaining -= n;
            }
        }

        var chunkBuf = new char[ChunkChars + overlap];
        var tail     = new char[overlap];
        int tailLen  = 0;
        long currentChar = fromAbsChar;

        while (true)
        {
            Array.Copy(tail, 0, chunkBuf, 0, tailLen);
            int freshRead = reader.Read(chunkBuf, tailLen, ChunkChars);
            if (freshRead == 0) break;

            int totalLen = tailLen + freshRead;
            var searchText = new string(chunkBuf, 0, totalLen);

            var result = provider.Search(searchText, options, 0);
            if (result.Found)
            {
                long absOffset = currentChar + (result.Offset - tailLen);
                if (absOffset >= fromAbsChar)
                    return SearchResult.Match(absOffset, result.Length, result.MatchedText!, result.Groups);

                int nextIdx = (int)result.Offset + 1;
                while (nextIdx < totalLen)
                {
                    result = provider.Search(searchText, options, nextIdx);
                    if (!result.Found) break;
                    absOffset = currentChar + (result.Offset - tailLen);
                    if (absOffset >= fromAbsChar)
                        return SearchResult.Match(absOffset, result.Length, result.MatchedText!, result.Groups);
                    nextIdx = (int)result.Offset + 1;
                }
            }

            int advance = Math.Max(0, totalLen - overlap);
            currentChar += advance;
            tailLen = Math.Min(overlap, totalLen);
            Array.Copy(chunkBuf, totalLen - tailLen, tail, 0, tailLen);
        }

        return SearchResult.NotFound;
    }

    private SearchResult StreamSearchBackward(ISearchProvider provider, SearchOptions options, long fromAbsChar)
    {
        var forwardOpts = new SearchOptions
        {
            Pattern = options.Pattern, Type = options.Type,
            CaseSensitive = options.CaseSensitive, WholeWords = options.WholeWords,
            EntireLine = options.EntireLine, Backward = false,
            Replacement = options.Replacement,
        };

        const int ChunkChars = 512 * 1024;
        int overlap = Math.Max(options.Pattern?.Length ?? 0, 1024);

        using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 65536, useAsync: false);
        using var reader = new StreamReader(stream, Utf8NoBom,
            detectEncodingFromByteOrderMarks: _hasBom, bufferSize: 65536, leaveOpen: true);

        var chunkBuf = new char[ChunkChars + overlap];
        var tail     = new char[overlap];
        int tailLen  = 0;
        long currentChar = 0;
        SearchResult lastFound = SearchResult.NotFound;

        while (true)
        {
            Array.Copy(tail, 0, chunkBuf, 0, tailLen);
            int freshRead = reader.Read(chunkBuf, tailLen, ChunkChars);
            if (freshRead == 0) break;

            int totalLen = tailLen + freshRead;
            var searchText = new string(chunkBuf, 0, totalLen);

            int idx = 0;
            while (idx < totalLen)
            {
                var result = provider.Search(searchText, forwardOpts, idx);
                if (!result.Found) break;
                long absOffset = currentChar + (result.Offset - tailLen);
                if (absOffset < fromAbsChar)
                    lastFound = SearchResult.Match(absOffset, result.Length, result.MatchedText!, result.Groups);
                else
                    goto done;
                idx = (int)result.Offset + 1;
            }

            long endOfThisChunk = currentChar + (totalLen - tailLen);
            if (endOfThisChunk >= fromAbsChar) break;

            int advance = Math.Max(0, totalLen - overlap);
            currentChar += advance;
            tailLen = Math.Min(overlap, totalLen);
            Array.Copy(chunkBuf, totalLen - tailLen, tail, 0, tailLen);
        }
        done:
        return lastFound;
    }

    // ── Static helpers ───────────────────────────────────────────────────────

    private static void CopyBytes(FileStream src, FileStream dst, long count)
    {
        var buf = new byte[65536];
        long remaining = count;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(remaining, buf.Length);
            int read = src.Read(buf, 0, toRead);
            if (read == 0) break;
            dst.Write(buf, 0, read);
            remaining -= read;
        }
    }

    private static void CopyBytesToEnd(FileStream src, FileStream dst)
    {
        var buf = new byte[65536];
        int read;
        while ((read = src.Read(buf, 0, buf.Length)) > 0)
            dst.Write(buf, 0, read);
    }
}
