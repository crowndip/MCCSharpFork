using Mc.Core.Search;

namespace Mc.Editor;

/// <summary>
/// Business logic for the text editor.
/// Manages the text buffer, undo stack, search, and clipboard.
/// Equivalent to src/editor/edit.c and editcmd.c in the original C codebase.
/// </summary>
public sealed class EditorController
{
    private readonly TextBuffer _buffer;
    private readonly Stack<EditOperation> _undoStack = new();
    private readonly Stack<EditOperation> _redoStack = new();
    private string? _filePath;
    private int _cursorOffset;
    private int _selectionStart = -1;
    private int _selectionEnd = -1;

    // Search state (persists between F7 presses)
    public SearchOptions LastSearch { get; set; } = new();
    private int _lastSearchOffset;

    public TextBuffer Buffer => _buffer;
    public string? FilePath => _filePath;
    public bool IsModified => _buffer.IsModified;
    public int CursorOffset => _cursorOffset;
    public (int Line, int Column) CursorPosition => _buffer.OffsetToLineCol(_cursorOffset);
    public bool HasSelection => _selectionStart >= 0 && _selectionEnd > _selectionStart;

    // Editor display options (from McSettings) (#23 #42)
    public bool ShowLineNumbers { get; set; }
    public int  TabWidth        { get; set; } = 4;
    public bool ExpandTabs      { get; set; }

    public SyntaxHighlighter? Highlighter { get; private set; }

    public event EventHandler? Changed;

    public EditorController(string? filePath = null)
    {
        _filePath = filePath;
        string? content = null;
        if (filePath != null && File.Exists(filePath))
            content = File.ReadAllText(filePath);
        _buffer = new TextBuffer(content);
        if (filePath != null)
            Highlighter = SyntaxHighlighter.ForFile(filePath);
    }

    // --- Cursor movement ---

    public void MoveCursor(int newOffset)
    {
        _cursorOffset = Math.Clamp(newOffset, 0, _buffer.Length);
    }

    public void MoveRight() => MoveCursor(_cursorOffset + 1);
    public void MoveLeft() => MoveCursor(_cursorOffset - 1);
    public void MoveDown() => MoveVertical(1);
    public void MoveUp() => MoveVertical(-1);

    public void MoveToLineStart()
    {
        while (_cursorOffset > 0 && _buffer[_cursorOffset - 1] != '\n')
            _cursorOffset--;
    }

    public void MoveToLineEnd()
    {
        while (_cursorOffset < _buffer.Length && _buffer[_cursorOffset] != '\n')
            _cursorOffset++;
    }

    public void MoveToStart() => _cursorOffset = 0;
    public void MoveToEnd() => _cursorOffset = _buffer.Length;

    public void MoveWordRight()
    {
        while (_cursorOffset < _buffer.Length && !char.IsLetterOrDigit(_buffer[_cursorOffset]))
            _cursorOffset++;
        while (_cursorOffset < _buffer.Length && char.IsLetterOrDigit(_buffer[_cursorOffset]))
            _cursorOffset++;
    }

    public void MoveWordLeft()
    {
        while (_cursorOffset > 0 && !char.IsLetterOrDigit(_buffer[_cursorOffset - 1]))
            _cursorOffset--;
        while (_cursorOffset > 0 && char.IsLetterOrDigit(_buffer[_cursorOffset - 1]))
            _cursorOffset--;
    }

    public void PageDown(int linesPerPage) => MoveVertical(linesPerPage);
    public void PageUp(int linesPerPage) => MoveVertical(-linesPerPage);

    // --- Editing ---

    public void InsertChar(char ch)
    {
        DeleteSelection();
        RecordUndo(new InsertOp(_cursorOffset, ch.ToString()));
        _buffer.Insert(_cursorOffset, ch);
        _cursorOffset++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void InsertText(string text)
    {
        DeleteSelection();
        RecordUndo(new InsertOp(_cursorOffset, text));
        _buffer.Insert(_cursorOffset, text);
        _cursorOffset += text.Length;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Overwrite mode: replace the character under the cursor. (#22)</summary>
    public void ReplaceChar(char ch)
    {
        if (_cursorOffset < _buffer.Length && _buffer[_cursorOffset] != '\n')
        {
            var old = _buffer[_cursorOffset].ToString();
            RecordUndo(new DeleteOp(_cursorOffset, old));
            _buffer.Delete(_cursorOffset);
            RecordUndo(new InsertOp(_cursorOffset, ch.ToString()));
            _buffer.Insert(_cursorOffset, ch);
            _cursorOffset++;
        }
        else
        {
            InsertChar(ch);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Insert newline and reproduce leading whitespace from the current line (auto-indent). (#24)</summary>
    public void InsertNewlineWithIndent()
    {
        var (line, _) = _buffer.OffsetToLineCol(_cursorOffset);
        var lineText  = _buffer.GetLine(line);
        int indent    = 0;
        while (indent < lineText.Length && (lineText[indent] == ' ' || lineText[indent] == '\t'))
            indent++;
        var whitespace = lineText[..indent];
        InsertChar('\n');
        InsertText(whitespace);
    }

    /// <summary>Insert tab as spaces when ExpandTabs is on, otherwise literal tab. (#42)</summary>
    public void InsertTab()
    {
        if (ExpandTabs)
        {
            var (_, col) = _buffer.OffsetToLineCol(_cursorOffset);
            int spaces   = TabWidth - (col % TabWidth);
            InsertText(new string(' ', spaces));
        }
        else
        {
            InsertChar('\t');
        }
    }

    public void Backspace()
    {
        if (HasSelection) { DeleteSelection(); return; }
        if (_cursorOffset == 0) return;
        var deleted = _buffer[_cursorOffset - 1].ToString();
        RecordUndo(new DeleteOp(_cursorOffset - 1, deleted));
        _buffer.Delete(_cursorOffset - 1);
        _cursorOffset--;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteForward()
    {
        if (HasSelection) { DeleteSelection(); return; }
        if (_cursorOffset >= _buffer.Length) return;
        var deleted = _buffer[_cursorOffset].ToString();
        RecordUndo(new DeleteOp(_cursorOffset, deleted));
        _buffer.Delete(_cursorOffset);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteLine()
    {
        MoveToLineStart();
        int start = _cursorOffset;
        MoveToLineEnd();
        if (_cursorOffset < _buffer.Length) _cursorOffset++; // include newline
        var deleted = _buffer.Extract(start, _cursorOffset - start);
        RecordUndo(new DeleteOp(start, deleted));
        _buffer.Delete(start, _cursorOffset - start);
        _cursorOffset = start;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Delete from the cursor to the end of the current line (Emacs Ctrl+K).
    /// If the cursor is already at the end of the line, delete the newline instead
    /// (joining with the next line), matching original MC editor behaviour.
    /// </summary>
    public void DeleteToEndOfLine()
    {
        int start = _cursorOffset;
        MoveToLineEnd();
        if (start == _cursorOffset)
        {
            // Already at end — delete the newline (join with next line)
            if (_cursorOffset < _buffer.Length)
            {
                var nl = _buffer[_cursorOffset].ToString();
                RecordUndo(new DeleteOp(_cursorOffset, nl));
                _buffer.Delete(_cursorOffset);
                Changed?.Invoke(this, EventArgs.Empty);
            }
            return;
        }
        var deleted = _buffer.Extract(start, _cursorOffset - start);
        RecordUndo(new DeleteOp(start, deleted));
        _buffer.Delete(start, _cursorOffset - start);
        _cursorOffset = start;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // --- Selection ---

    public void SelectAll()
    {
        _selectionStart = 0;
        _selectionEnd = _buffer.Length;
    }

    public void StartSelection() => _selectionStart = _cursorOffset;
    public void ExtendSelection() => _selectionEnd = _cursorOffset;
    public void ClearSelection() { _selectionStart = -1; _selectionEnd = -1; }

    /// <summary>Returns (start, end) byte offsets of the current selection, or (-1,-1). (#3)</summary>
    public (int Start, int End) GetSelectionOffsets()
    {
        if (!HasSelection) return (-1, -1);
        return (Math.Min(_selectionStart, _selectionEnd), Math.Max(_selectionStart, _selectionEnd));
    }

    // --- Column / rectangular block operations (#24) ---

    /// <summary>
    /// Copy a rectangular block defined by two (line, col) corners.
    /// Returns one string per line; lines shorter than the rectangle are padded with spaces.
    /// </summary>
    public string[] CopyColumnBlock(int line1, int col1, int line2, int col2)
    {
        int top    = Math.Min(line1, line2);
        int bot    = Math.Max(line1, line2);
        int left   = Math.Min(col1,  col2);
        int right  = Math.Max(col1,  col2);
        var result = new List<string>();
        for (int ln = top; ln <= bot; ln++)
        {
            var lineText = ln < _buffer.GetLineCount() ? _buffer.GetLine(ln) : string.Empty;
            var seg = lineText.Length <= left
                ? new string(' ', right - left)
                : lineText.Length >= right
                    ? lineText[left..right]
                    : lineText[left..] + new string(' ', right - lineText.Length);
            result.Add(seg);
        }
        return result.ToArray();
    }

    /// <summary>
    /// Delete a rectangular block defined by two (line, col) corners.
    /// Each affected line has the column range removed.
    /// </summary>
    public void DeleteColumnBlock(int line1, int col1, int line2, int col2)
    {
        int top   = Math.Min(line1, line2);
        int bot   = Math.Max(line1, line2);
        int left  = Math.Min(col1,  col2);
        int right = Math.Max(col1,  col2);
        for (int ln = bot; ln >= top; ln--)
        {
            if (ln >= _buffer.GetLineCount()) continue;
            var lineText = _buffer.GetLine(ln);
            if (lineText.Length <= left) continue; // nothing to delete in this line
            int delEnd  = Math.Min(right, lineText.Length);
            int delLen  = delEnd - left;
            int lineOff = _buffer.LineColToOffset(ln, left);
            var deleted = _buffer.Extract(lineOff, delLen);
            RecordUndo(new DeleteOp(lineOff, deleted));
            _buffer.Delete(lineOff, delLen);
        }
        _cursorOffset = _buffer.LineColToOffset(Math.Min(top, _buffer.GetLineCount() - 1), left);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Paste a column block (array of row strings) at the given (line, col) position.
    /// Each row is inserted at the given column of consecutive lines.
    /// </summary>
    public void PasteColumnBlock(string[] rows, int atLine, int atCol)
    {
        for (int i = rows.Length - 1; i >= 0; i--)
        {
            int ln = atLine + i;
            // Ensure line exists
            while (_buffer.GetLineCount() <= ln)
                InsertTextAt(_buffer.Length, "\n");

            var lineText = _buffer.GetLine(ln);
            int insOff;
            string insertion;
            if (lineText.Length <= atCol)
            {
                // Pad the line with spaces, then insert
                insOff    = _buffer.LineColToOffset(ln, lineText.Length) + (atCol - lineText.Length);
                insertion = new string(' ', atCol - lineText.Length) + rows[i];
                insOff    = _buffer.LineColToOffset(ln, lineText.Length);
                insertion = new string(' ', atCol - lineText.Length) + rows[i];
            }
            else
            {
                insOff    = _buffer.LineColToOffset(ln, atCol);
                insertion = rows[i];
            }
            InsertTextAt(insOff, insertion);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void InsertTextAt(int offset, string text)
    {
        RecordUndo(new InsertOp(offset, text));
        _buffer.Insert(offset, text);
    }

    // --- Clipboard ---

    public string Copy()
    {
        if (!HasSelection) return string.Empty;
        return _buffer.Extract(_selectionStart, _selectionEnd - _selectionStart);
    }

    public void Cut()
    {
        if (!HasSelection) return;
        DeleteSelection();
    }

    public void Paste(string text) => InsertText(text);

    // --- Undo / Redo ---

    public void Undo()
    {
        if (!_undoStack.TryPop(out var op)) return;
        op.Undo(_buffer);
        _cursorOffset = op.Offset;
        _redoStack.Push(op);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!_redoStack.TryPop(out var op)) return;
        op.Redo(_buffer);
        _cursorOffset = op.Offset + op.Text.Length;
        _undoStack.Push(op);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // --- Search ---

    public SearchResult FindNext(SearchOptions opts)
    {
        var text = _buffer.ToString();
        var provider = opts.Type switch
        {
            SearchType.Regex => (ISearchProvider)new RegexSearchProvider(),
            _ => new NormalSearchProvider(),
        };
        var result = provider.Search(text, opts, _lastSearchOffset + 1);
        if (result.Found)
        {
            _lastSearchOffset = (int)result.Offset;
            MoveCursor((int)result.Offset);
            _selectionStart = (int)result.Offset;
            _selectionEnd = (int)result.Offset + result.Length;
        }
        LastSearch = opts;
        return result;
    }

    public int ReplaceAll(SearchOptions opts)
    {
        if (string.IsNullOrEmpty(opts.Pattern)) return 0;
        var text = _buffer.ToString();
        var provider = opts.Type switch
        {
            SearchType.Regex => (ISearchProvider)new RegexSearchProvider(),
            _ => new NormalSearchProvider(),
        };
        var newText = provider.ReplaceAll(text, opts);
        if (newText == text) return 0;

        var countBefore = CountOccurrences(text, opts.Pattern);
        _buffer.Replace(0, _buffer.Length, newText);
        Changed?.Invoke(this, EventArgs.Empty);
        return countBefore;
    }

    // --- Save ---

    /// <summary>Load a new file into the editor, replacing current content. (#10)</summary>
    public void LoadFile(string path)
    {
        _filePath = string.IsNullOrEmpty(path) ? null : path;
        var content = (_filePath != null && File.Exists(_filePath)) ? File.ReadAllText(_filePath) : string.Empty;
        _buffer.SetContent(content);
        _cursorOffset = 0;
        _selectionStart = -1;
        _selectionEnd = -1;
        _undoStack.Clear();
        _redoStack.Clear();
        Highlighter = _filePath != null ? SyntaxHighlighter.ForFile(_filePath) : null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ReloadSyntax()
    {
        Highlighter = _filePath != null ? SyntaxHighlighter.ForFile(_filePath) : null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        if (_filePath == null) throw new InvalidOperationException("No file path set");
        _buffer.SaveFile(_filePath);
    }

    public void SaveAs(string newPath)
    {
        _filePath = newPath;
        _buffer.SaveFile(newPath);
        Highlighter = SyntaxHighlighter.ForFile(newPath);
    }

    // --- Goto ---

    public void GotoLine(int line)
    {
        var offset = _buffer.LineColToOffset(line - 1, 0);
        MoveCursor(offset);
    }

    // --- Private helpers ---

    private void DeleteSelection()
    {
        if (!HasSelection) return;
        var start = Math.Min(_selectionStart, _selectionEnd);
        var end = Math.Max(_selectionStart, _selectionEnd);
        var deleted = _buffer.Extract(start, end - start);
        RecordUndo(new DeleteOp(start, deleted));
        _buffer.Delete(start, end - start);
        _cursorOffset = start;
        ClearSelection();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RecordUndo(EditOperation op)
    {
        _undoStack.Push(op);
        _redoStack.Clear();
    }

    private void MoveVertical(int lines)
    {
        var (line, col) = _buffer.OffsetToLineCol(_cursorOffset);
        var newLine = Math.Clamp(line + lines, 0, _buffer.GetLineCount() - 1);
        _cursorOffset = _buffer.LineColToOffset(newLine, col);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++; idx++;
        }
        return count;
    }

    // --- Bookmarks (Alt+K / Alt+J / Alt+I / Alt+O) ---

    private readonly SortedSet<int> _bookmarks = new();

    public bool HasBookmarkAt(int line) => _bookmarks.Contains(line);
    public IEnumerable<int> Bookmarks => _bookmarks;

    public void ToggleBookmark()
    {
        var (line, _) = _buffer.OffsetToLineCol(_cursorOffset);
        if (!_bookmarks.Remove(line))
            _bookmarks.Add(line);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void NextBookmark()
    {
        var (curLine, _) = _buffer.OffsetToLineCol(_cursorOffset);
        var after = _bookmarks.GetViewBetween(curLine + 1, int.MaxValue);
        int target = after.Count > 0 ? after.Min : (_bookmarks.Count > 0 ? _bookmarks.Min : -1);
        if (target >= 0) { _cursorOffset = _buffer.LineColToOffset(target, 0); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public void PrevBookmark()
    {
        var (curLine, _) = _buffer.OffsetToLineCol(_cursorOffset);
        var before = _bookmarks.GetViewBetween(0, curLine > 0 ? curLine - 1 : 0);
        int target = (curLine > 0 && before.Count > 0) ? before.Max : (_bookmarks.Count > 0 ? _bookmarks.Max : -1);
        if (target >= 0) { _cursorOffset = _buffer.LineColToOffset(target, 0); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public void FlushBookmarks()
    {
        _bookmarks.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // --- Delete-word operations (Alt+Backspace / Alt+D) ---

    public void DeleteToWordBegin()
    {
        int end = _cursorOffset;
        while (_cursorOffset > 0 && !IsWordChar(_buffer[_cursorOffset - 1]))
            _cursorOffset--;
        while (_cursorOffset > 0 && IsWordChar(_buffer[_cursorOffset - 1]))
            _cursorOffset--;
        int start = _cursorOffset;
        if (start < end)
        {
            var deleted = _buffer.Extract(start, end - start);
            RecordUndo(new DeleteOp(start, deleted));
            _buffer.Delete(start, end - start);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void DeleteToWordEnd()
    {
        int start = _cursorOffset;
        while (_cursorOffset < _buffer.Length && !IsWordChar(_buffer[_cursorOffset]))
            _cursorOffset++;
        while (_cursorOffset < _buffer.Length && IsWordChar(_buffer[_cursorOffset]))
            _cursorOffset++;
        int end = _cursorOffset;
        if (end > start)
        {
            var deleted = _buffer.Extract(start, end - start);
            _cursorOffset = start;
            RecordUndo(new DeleteOp(start, deleted));
            _buffer.Delete(start, end - start);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    // --- Insert / Save block ---

    public void InsertFile(string path)
    {
        var content = File.ReadAllText(path);
        InsertText(content);
    }

    public void SaveBlock(string path)
    {
        var (start, end) = GetSelectionOffsets();
        if (start < 0) return;
        var block = _buffer.Extract(start, end - start);
        File.WriteAllText(path, block);
    }

    // --- Block indentation (Tab / Shift+Tab on selection) ---

    public void ShiftBlockRight(int tabWidth)
    {
        if (!HasSelection) return;
        var (selStart, selEnd) = GetSelectionOffsets();
        var (startLine, _) = _buffer.OffsetToLineCol(selStart);
        var (endLine, endCol) = _buffer.OffsetToLineCol(selEnd);
        if (endCol == 0 && endLine > startLine) endLine--;
        for (int ln = endLine; ln >= startLine; ln--)
        {
            int lineOff = _buffer.LineColToOffset(ln, 0);
            string indent = new string(' ', tabWidth);
            RecordUndo(new InsertOp(lineOff, indent));
            _buffer.Insert(lineOff, indent);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ShiftBlockLeft(int tabWidth)
    {
        if (!HasSelection) return;
        var (selStart, selEnd) = GetSelectionOffsets();
        var (startLine, _) = _buffer.OffsetToLineCol(selStart);
        var (endLine, endCol) = _buffer.OffsetToLineCol(selEnd);
        if (endCol == 0 && endLine > startLine) endLine--;
        for (int ln = endLine; ln >= startLine; ln--)
        {
            var lineText = _buffer.GetLine(ln);
            int spaces = 0;
            while (spaces < tabWidth && spaces < lineText.Length &&
                   (lineText[spaces] == ' ' || lineText[spaces] == '\t'))
                spaces++;
            if (spaces > 0)
            {
                int lineOff = _buffer.LineColToOffset(ln, 0);
                var deleted = _buffer.Extract(lineOff, spaces);
                RecordUndo(new DeleteOp(lineOff, deleted));
                _buffer.Delete(lineOff, spaces);
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // --- Paragraph formatting (Alt+P) ---

    public void FormatParagraph(int wrapWidth = 72)
    {
        var (curLine, _) = _buffer.OffsetToLineCol(_cursorOffset);
        int lineCount = _buffer.GetLineCount();

        int paraStart = curLine;
        while (paraStart > 0 && !string.IsNullOrWhiteSpace(_buffer.GetLine(paraStart - 1)))
            paraStart--;
        int paraEnd = curLine;
        while (paraEnd < lineCount - 1 && !string.IsNullOrWhiteSpace(_buffer.GetLine(paraEnd + 1)))
            paraEnd++;

        var firstLine = paraStart < lineCount ? _buffer.GetLine(paraStart) : string.Empty;
        int indentLen = 0;
        while (indentLen < firstLine.Length && (firstLine[indentLen] == ' ' || firstLine[indentLen] == '\t'))
            indentLen++;
        var indent = indentLen > 0 ? firstLine[..indentLen] : string.Empty;

        var paraLines = Enumerable.Range(paraStart, paraEnd - paraStart + 1)
            .Select(ln => ln < lineCount ? _buffer.GetLine(ln).TrimEnd() : string.Empty)
            .ToList();
        var words = string.Join(" ", paraLines.Select(l => l.Trim()))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var resultLines = new List<string>();
        var current = new System.Text.StringBuilder(indent);
        foreach (var word in words)
        {
            int addLen = (current.Length == indent.Length ? 0 : 1) + word.Length;
            if (current.Length + addLen > wrapWidth && current.Length > indent.Length)
            {
                resultLines.Add(current.ToString());
                current.Clear(); current.Append(indent);
            }
            if (current.Length > indent.Length) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > indent.Length) resultLines.Add(current.ToString());
        if (resultLines.Count == 0) return;

        int startOff = _buffer.LineColToOffset(paraStart, 0);
        var lastLineText = paraEnd < lineCount ? _buffer.GetLine(paraEnd) : string.Empty;
        int endOff = _buffer.LineColToOffset(paraEnd, 0) + lastLineText.Length;
        var oldText = _buffer.Extract(startOff, endOff - startOff);
        var newText = string.Join("\n", resultLines);
        RecordUndo(new DeleteOp(startOff, oldText));
        _buffer.Delete(startOff, endOff - startOff);
        RecordUndo(new InsertOp(startOff, newText));
        _buffer.Insert(startOff, newText);
        _cursorOffset = startOff;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

// --- Undo operation types ---

internal abstract class EditOperation
{
    public int Offset { get; protected set; }
    public string Text { get; protected set; } = string.Empty;
    public abstract void Undo(TextBuffer buffer);
    public abstract void Redo(TextBuffer buffer);
}

internal sealed class InsertOp : EditOperation
{
    public InsertOp(int offset, string text) { Offset = offset; Text = text; }
    public override void Undo(TextBuffer b) => b.Delete(Offset, Text.Length);
    public override void Redo(TextBuffer b) => b.Insert(Offset, Text);
}

internal sealed class DeleteOp : EditOperation
{
    public DeleteOp(int offset, string text) { Offset = offset; Text = text; }
    public override void Undo(TextBuffer b) => b.Insert(Offset, Text);
    public override void Redo(TextBuffer b) => b.Delete(Offset, Text.Length);
}
