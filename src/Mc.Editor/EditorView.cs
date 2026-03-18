using System.Text;
using Mc.Core.Search;
using Terminal.Gui;

namespace Mc.Editor;

/// <summary>
/// Terminal.Gui view that hosts the text editor.
/// Equivalent to editwidget.c + editdraw.c in the original C codebase.
/// </summary>
public sealed class EditorView : View
{
    private readonly EditorController _editor;
    private int _topLine;
    private int _leftCol;
    private bool _insertMode = true;
    private string? _clipboardText;
    private string[]? _clipboardColBlock;

    // Block-selection state
    private bool _selecting;
    private int  _selectionAnchor = -1;

    // Column / rectangular block mode (Alt+B)
    private bool _colBlock;
    private int  _colBlockAnchorLine;
    private int  _colBlockAnchorCol;

    // Syntax-highlighting toggle
    private bool _syntaxHighlightingOn = true;

    // Quote-next: insert next keystroke as a literal character (Ctrl+Q)
    private bool _quoteNext = false;

    // Macro recording / playback
    private bool _isRecordingMacro = false;
    private bool _isPlayingMacro   = false;
    private readonly List<Key> _macroKeys = [];

    // Display options
    private bool _showLineNumbers;
    private bool _showRightMargin;
    private int  _rightMarginColumn = 72;
    private bool _showTabTws;       // visible tabs/trailing whitespace

    // Scroll-without-cursor (Ctrl+Up / Ctrl+Down)
    private bool _lockScroll;

    // Line-number gutter width
    private int GutterWidth => _showLineNumbers ? _editor.Buffer.GetLineCount().ToString().Length + 1 : 0;

    public event EventHandler? RequestClose;
    /// <summary>Raised when the editor title should be refreshed (e.g. after save / open). </summary>
    public event EventHandler? EditorTitleChanged;

    public EditorView(string? filePath = null)
    {
        _editor = new EditorController(filePath);
        _editor.Changed += (_, _) => SetNeedsDraw();
        CanFocus = true;
        ColorScheme = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.White,        Color.Black),
            Focus     = new Terminal.Gui.Attribute(Color.White,        Color.Black),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
            HotFocus  = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,         Color.Black),
        };
        // Mouse support
        MouseClick += OnMouseClicked;
    }

    public new string Title => _editor.FilePath != null
        ? $"Edit: {Path.GetFileName(_editor.FilePath)}{(_editor.IsModified ? " *" : string.Empty)}"
        : "Edit: [new file]";

    public string StatusText
    {
        get
        {
            var (ln, col) = _editor.CursorPosition;
            var mode = _insertMode ? "INS" : "OVR";
            var modified = _editor.IsModified ? "Modified" : "Saved";
            var file = _editor.FilePath != null ? Path.GetFileName(_editor.FilePath) : "new";
            var extra = new StringBuilder();
            if (_colBlock) extra.Append(" COL");
            if (_showLineNumbers) extra.Append(" NUMS");
            if (!_syntaxHighlightingOn) extra.Append(" NOHL");
            if (_quoteNext) extra.Append(" QUOT");
            if (_isRecordingMacro) extra.Append(" REC");
            return $" {file} | Ln {ln + 1}, Col {col + 1} | {mode} | {modified}{extra}";
        }
    }

    // ── Drawing ─────────────────────────────────────────────────────────────

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        var viewport = Viewport;
        // Leave 1 row for the status bar at the bottom of this view
        var contentHeight = viewport.Height - 1;
        var (cursorLine, cursorCol) = _editor.CursorPosition;
        var gutter = GutterWidth;

        // Scroll viewport to keep cursor visible (unless locked by Ctrl+Up/Down)
        if (!_lockScroll)
        {
            if (cursorLine < _topLine) _topLine = cursorLine;
            if (cursorLine >= _topLine + contentHeight) _topLine = cursorLine - contentHeight + 1;
        }
        _lockScroll = false;
        var textWidth = viewport.Width - gutter;
        if (cursorCol < _leftCol) _leftCol = cursorCol;
        if (cursorCol >= _leftCol + textWidth) _leftCol = cursorCol - textWidth + 1;
        if (_leftCol < 0) _leftCol = 0;

        for (int row = 0; row < contentHeight; row++)
        {
            int lineNo = _topLine + row;
            Move(0, row);

            // Line-number gutter
            if (gutter > 0)
            {
                var bookmarkAttr = new Terminal.Gui.Attribute(Color.BrightYellow, Color.DarkGray);
                var gutterAttr   = new Terminal.Gui.Attribute(Color.Gray, Color.Black);
                Driver!.SetAttribute(lineNo < _editor.Buffer.GetLineCount() && _editor.HasBookmarkAt(lineNo)
                    ? bookmarkAttr : gutterAttr);
                if (lineNo < _editor.Buffer.GetLineCount())
                    Driver!.AddStr((lineNo + 1).ToString().PadLeft(gutter - 1) + " ");
                else
                    Driver!.AddStr(new string(' ', gutter));
            }

            if (lineNo >= _editor.Buffer.GetLineCount())
            {
                Driver!.SetAttribute(ColorScheme!.Normal);
                Driver!.AddStr(new string(' ', textWidth));
                continue;
            }

            var line = _editor.Buffer.GetLine(lineNo);
            var lineOffset = _editor.Buffer.LineColToOffset(lineNo, 0);

            // Bookmarked line highlight (when no line numbers)
            bool isBookmarked = _editor.HasBookmarkAt(lineNo);

            if (_syntaxHighlightingOn && _editor.Highlighter != null)
            {
                var tokens = _editor.Highlighter.Tokenize(line);
                DrawLineWithSyntaxAndSelection(row, line, tokens, _leftCol, textWidth, lineOffset, isBookmarked);
            }
            else
            {
                DrawLineWithSelection(row, line, _leftCol, textWidth, lineOffset, isBookmarked);
            }

            // Right margin indicator
            if (_showRightMargin)
            {
                int marginScreenCol = gutter + _rightMarginColumn - _leftCol;
                if (marginScreenCol >= gutter && marginScreenCol < viewport.Width)
                {
                    Move(marginScreenCol, row);
                    Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.DarkGray, Color.Black));
                    Driver!.AddRune(new Rune('│'));
                }
            }
        }

        // Status bar (last row of this view)
        Move(0, contentHeight);
        Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
        var status = StatusText;
        if (status.Length > viewport.Width) status = status[..viewport.Width];
        Driver!.AddStr(status.PadRight(viewport.Width));

        // Position the terminal cursor
        var screenLine = cursorLine - _topLine;
        var screenCol  = gutter + cursorCol - _leftCol;
        if (screenLine >= 0 && screenLine < contentHeight &&
            screenCol >= gutter && screenCol < viewport.Width)
        {
            Move(screenCol, screenLine);
        }
        return false;
    }

    private void DrawLineWithSelection(int row, string line, int leftCol, int width,
        int lineStartOffset, bool isBookmarked)
    {
        var gutter = GutterWidth;
        var pos = leftCol;
        Move(gutter, row);
        var bmAttr = new Terminal.Gui.Attribute(Color.White, Color.DarkGray);
        for (int i = 0; i < width; i++, pos++)
        {
            char ch = pos < line.Length ? line[pos] : ' ';
            bool inSel = IsInSelection(row + _topLine, pos, lineStartOffset + pos);
            Terminal.Gui.Attribute attr;
            if (inSel)
                attr = new Terminal.Gui.Attribute(Color.Black, Color.Cyan);
            else if (isBookmarked)
                attr = bmAttr;
            else
                attr = ColorScheme!.Normal;
            Driver!.SetAttribute(attr);
            if (_showTabTws && ch == '\t')
                Driver!.AddStr("→");
            else if (_showTabTws && ch == ' ' && pos == line.Length - 1)
                { Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.DarkGray, attr.Background)); Driver!.AddStr("·"); }
            else
                Driver!.AddStr(ch.ToString());
        }
    }

    private bool IsInSelection(int lineNo, int col, int charOffset)
    {
        if (_colBlock && _selecting)
        {
            var (curLine, curCol) = _editor.CursorPosition;
            int top   = Math.Min(_colBlockAnchorLine, curLine);
            int bot   = Math.Max(_colBlockAnchorLine, curLine);
            int left  = Math.Min(_colBlockAnchorCol,  curCol);
            int right = Math.Max(_colBlockAnchorCol,  curCol);
            return lineNo >= top && lineNo <= bot && col >= left && col < right;
        }
        var (selStart, selEnd) = _editor.GetSelectionOffsets();
        return selStart >= 0 && charOffset >= selStart && charOffset < selEnd;
    }

    private void DrawLineWithSyntaxAndSelection(int row, string line, IReadOnlyList<SyntaxToken> tokens,
        int leftCol, int width, int lineStartOffset, bool isBookmarked)
    {
        var gutter = GutterWidth;
        var bmAttr = new Terminal.Gui.Attribute(Color.White, Color.DarkGray);
        Move(gutter, row);
        for (int i = 0; i < width; i++)
        {
            int pos = leftCol + i;
            int charOffset = lineStartOffset + pos;
            char ch = pos < line.Length ? line[pos] : ' ';
            bool inSel = IsInSelection(row + _topLine, pos, charOffset);
            Terminal.Gui.Attribute attr;
            if (inSel)
                attr = new Terminal.Gui.Attribute(Color.Black, Color.Cyan);
            else if (isBookmarked)
                attr = bmAttr;
            else
            {
                var tok = FindToken(tokens, pos);
                attr = tok != null ? GetTokenColor(tok.Type) : ColorScheme!.Normal;
            }
            Driver!.SetAttribute(attr);
            if (_showTabTws && ch == '\t')
                Driver!.AddStr("→");
            else
                Driver!.AddStr(ch.ToString());
        }
    }

    private static SyntaxToken? FindToken(IReadOnlyList<SyntaxToken> tokens, int pos)
    {
        foreach (var t in tokens)
            if (pos >= t.Start && pos < t.Start + t.Length) return t;
        return null;
    }

    private static Terminal.Gui.Attribute GetTokenColor(TokenType type) => type switch
    {
        TokenType.Keyword      => new Terminal.Gui.Attribute(Color.BrightYellow,  Color.Black),
        TokenType.Comment      => new Terminal.Gui.Attribute(Color.Gray,           Color.Black),
        TokenType.String       => new Terminal.Gui.Attribute(Color.BrightCyan,     Color.Black),
        TokenType.Number       => new Terminal.Gui.Attribute(Color.BrightMagenta,  Color.Black),
        TokenType.Preprocessor => new Terminal.Gui.Attribute(Color.BrightGreen,    Color.Black),
        TokenType.Type         => new Terminal.Gui.Attribute(Color.BrightGreen,    Color.Black),
        _                      => new Terminal.Gui.Attribute(Color.White,           Color.Black),
    };

    // ── Mouse ────────────────────────────────────────────────────────────────

    private void OnMouseClicked(object? sender, MouseEventArgs e)
    {
        var viewport = Viewport;
        var contentHeight = viewport.Height - 1;
        var gutter = GutterWidth;

        if (e.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            _topLine = Math.Max(0, _topLine - 2);
            _lockScroll = true;
            SetNeedsDraw();
            e.Handled = true;
            return;
        }
        if (e.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            _topLine = Math.Min(Math.Max(0, _editor.Buffer.GetLineCount() - 1), _topLine + 2);
            _lockScroll = true;
            SetNeedsDraw();
            e.Handled = true;
            return;
        }

        if (!e.Flags.HasFlag(MouseFlags.Button1Clicked) &&
            !e.Flags.HasFlag(MouseFlags.Button1Pressed) &&
            !e.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
            return;

        // Click-to-position cursor
        int screenRow = e.Position.Y;
        int screenCol = e.Position.X - gutter;
        if (screenRow >= 0 && screenRow < contentHeight && screenCol >= 0)
        {
            int targetLine = _topLine + screenRow;
            int targetCol  = _leftCol + screenCol;
            if (targetLine < _editor.Buffer.GetLineCount())
            {
                var lineText = _editor.Buffer.GetLine(targetLine);
                targetCol = Math.Min(targetCol, lineText.Length);
                _editor.MoveCursor(_editor.Buffer.LineColToOffset(targetLine, targetCol));

                if (e.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
                {
                    // Double-click: select current word
                    _editor.MoveWordLeft();
                    _editor.StartSelection();
                    _editor.MoveWordRight();
                    _editor.ExtendSelection();
                    _selecting = true;
                }
                else if (e.Flags.HasFlag(MouseFlags.Button1Pressed))
                {
                    _selecting = false;
                    _editor.ClearSelection();
                }
                SetNeedsDraw();
                SetFocus();
                e.Handled = true;
            }
        }
    }

    // ── Keyboard ─────────────────────────────────────────────────────────────

    protected override bool OnKeyDown(Key keyEvent)
    {
        // Quote-next: insert any next keystroke literally
        if (_quoteNext)
        {
            _quoteNext = false;
            var rune = keyEvent.AsRune;
            var ch = rune.Value >= 1 && rune.Value <= 31
                ? (char)rune.Value
                : rune.Value >= 32 ? (char)rune.Value : '\0';
            if (ch != '\0') { _editor.InsertChar(ch); SetNeedsDraw(); }
            return true;
        }

        // Macro recording
        if (_isRecordingMacro && !_isPlayingMacro
            && keyEvent.KeyCode != (KeyCode.R | KeyCode.CtrlMask))
            _macroKeys.Add(keyEvent);

        // Alt+[ = go to matching bracket
        if (keyEvent.IsAlt && (char)((int)keyEvent.KeyCode & 0xFFFF) == '[')
        {
            ExecuteMatchBracket();
            return true;
        }

        // Shift+Arrow: extend selection (stream or column mode)
        if (keyEvent.IsShift && keyEvent.KeyCode is
            KeyCode.CursorUp or KeyCode.CursorDown or
            KeyCode.CursorLeft or KeyCode.CursorRight or
            KeyCode.Home or KeyCode.End)
        {
            if (!_selecting)
            {
                _selecting = true;
                _selectionAnchor = _editor.CursorOffset;
                _editor.StartSelection();
                if (_colBlock) (_colBlockAnchorLine, _colBlockAnchorCol) = _editor.CursorPosition;
            }
            MoveWithShift(keyEvent);
            if (!_colBlock) _editor.ExtendSelection();
            SetNeedsDraw();
            return true;
        }

        // Any non-shift move cancels selection
        if (_selecting && keyEvent.KeyCode is
            KeyCode.CursorUp or KeyCode.CursorDown or
            KeyCode.CursorLeft or KeyCode.CursorRight or
            KeyCode.Home or KeyCode.End or
            KeyCode.PageUp or KeyCode.PageDown)
        {
            _selecting = false;
            _editor.ClearSelection();
        }

        switch (keyEvent.KeyCode)
        {
            // ── File ────────────────────────────────────────────────────────
            case KeyCode.F2:                                ExecuteSave(); return true;
            case KeyCode.F2 | KeyCode.ShiftMask:            ExecuteSaveAs(); return true;
            case KeyCode.O when keyEvent.IsCtrl:            ExecuteOpenFile(); return true;
            case KeyCode.N when keyEvent.IsCtrl:            ExecuteNewFile(); return true;
            case KeyCode.F10: case KeyCode.Esc:             ExecuteClose(); return true;
            case KeyCode.F5 | KeyCode.ShiftMask:            ExecuteInsertFile(); return true;   // Shift+F5
            case KeyCode.F when keyEvent.IsCtrl:            ExecuteSaveBlock(); return true;    // Ctrl+F

            // ── Search ──────────────────────────────────────────────────────
            case KeyCode.F7:                                ExecuteSearch(); return true;
            case KeyCode.F7 | KeyCode.ShiftMask:            ExecuteSearchContinue(); return true;
            case KeyCode.F4:                                ExecuteReplace(); return true;
            case KeyCode.F4 | KeyCode.ShiftMask:            ExecuteReplaceContinue(); return true;

            // ── Edit ────────────────────────────────────────────────────────
            case KeyCode.F3:
                if (!_selecting) { _selecting = true; _editor.StartSelection(); }
                else { _selecting = false; _editor.ExtendSelection(); }
                SetNeedsDraw();
                return true;

            case KeyCode.F3 | KeyCode.ShiftMask:            // Shift+F3 = column mark
                _colBlock = !_colBlock;
                if (_colBlock) { (_colBlockAnchorLine, _colBlockAnchorCol) = _editor.CursorPosition; }
                SetNeedsDraw();
                return true;

            case KeyCode.F5:
                if (_selecting) { ExecuteCopyBlock(); } else { ExecuteGotoLine(); }
                return true;

            case KeyCode.F6:                                ExecuteMoveBlock(); return true;
            case KeyCode.F8:                                _editor.DeleteLine(); SetNeedsDraw(); return true;
            case KeyCode.F9:                                // F9 handled by EditorScreen menu bar
                return base.OnKeyDown(keyEvent);

            // ── Undo / Redo ──────────────────────────────────────────────────
            case KeyCode.Z when keyEvent.IsCtrl && !keyEvent.IsShift: _editor.Undo(); return true;
            case KeyCode.U when keyEvent.IsCtrl:            _editor.Undo(); return true;
            case KeyCode.Z | KeyCode.ShiftMask when keyEvent.IsCtrl: _editor.Redo(); return true;

            // ── Navigation: Ctrl variants must come before plain variants ───────
            case KeyCode.CursorLeft   when keyEvent.IsCtrl:  _editor.MoveWordLeft();  return true;
            case KeyCode.CursorRight  when keyEvent.IsCtrl:  _editor.MoveWordRight(); return true;
            case KeyCode.Home         when keyEvent.IsCtrl:  _editor.MoveToStart(); return true;
            case KeyCode.End          when keyEvent.IsCtrl:  _editor.MoveToEnd();   return true;

            // Ctrl+Up / Ctrl+Down = scroll display without moving cursor
            case KeyCode.CursorUp   when keyEvent.IsCtrl:
                _topLine = Math.Max(0, _topLine - 1);
                _lockScroll = true;
                SetNeedsDraw();
                return true;
            case KeyCode.CursorDown when keyEvent.IsCtrl:
                _topLine = Math.Min(Math.Max(0, _editor.Buffer.GetLineCount() - 1), _topLine + 1);
                _lockScroll = true;
                SetNeedsDraw();
                return true;

            // Ctrl+PgUp = move cursor to top of visible screen
            case KeyCode.PageUp when keyEvent.IsCtrl:
                _editor.GotoLine(_topLine + 1);
                SetNeedsDraw();
                return true;
            // Ctrl+PgDn = move cursor to bottom of visible screen
            case KeyCode.PageDown when keyEvent.IsCtrl:
                _editor.GotoLine(_topLine + Viewport.Height - 2);
                SetNeedsDraw();
                return true;

            // ── Navigation: plain variants ────────────────────────────────────
            case KeyCode.CursorUp:                          _editor.MoveUp();   _lockScroll = false; return true;
            case KeyCode.CursorDown:                        _editor.MoveDown(); _lockScroll = false; return true;
            case KeyCode.CursorLeft:                        _editor.MoveLeft(); return true;
            case KeyCode.CursorRight:                       _editor.MoveRight(); return true;
            case KeyCode.Home:                              _editor.MoveToLineStart(); return true;
            case KeyCode.End:                               _editor.MoveToLineEnd(); return true;
            case KeyCode.PageUp:                            _editor.PageUp(Viewport.Height - 2); return true;
            case KeyCode.PageDown:                          _editor.PageDown(Viewport.Height - 2); return true;

            // ── Deletion ────────────────────────────────────────────────────
            case KeyCode.Backspace:                         _editor.Backspace(); return true;
            case KeyCode.Delete:                            _editor.DeleteForward(); return true;
            case KeyCode.Y when keyEvent.IsCtrl:            _editor.DeleteLine(); return true;
            case KeyCode.K when keyEvent.IsCtrl:            _editor.DeleteToEndOfLine(); return true;

            // Alt+Backspace = delete to word begin
            case KeyCode.Backspace | KeyCode.AltMask:
                _editor.DeleteToWordBegin();
                return true;

            // Alt+D = delete to word end
            case KeyCode.D | KeyCode.AltMask:
                _editor.DeleteToWordEnd();
                return true;

            // ── Clipboard ───────────────────────────────────────────────────
            case KeyCode.Insert | KeyCode.CtrlMask:
                _clipboardText = _editor.Copy(); _selecting = false; _editor.ClearSelection();
                return true;
            case KeyCode.Insert | KeyCode.ShiftMask:
                PasteClipboard();
                return true;
            case KeyCode.Delete | KeyCode.ShiftMask:
                _clipboardText = _editor.Copy(); _editor.Cut();
                _selecting = false; _editor.ClearSelection();
                return true;
            case KeyCode.C when keyEvent.IsCtrl:
                _clipboardText = _editor.Copy(); _selecting = false; _editor.ClearSelection();
                return true;
            case KeyCode.X when keyEvent.IsCtrl:
                _clipboardText = _editor.Copy(); _editor.Cut();
                _selecting = false; _editor.ClearSelection();
                return true;
            case KeyCode.V when keyEvent.IsCtrl:
                PasteClipboard();
                return true;
            case KeyCode.A when keyEvent.IsCtrl:
                _editor.SelectAll(); _selecting = true; SetNeedsDraw();
                return true;

            // Insert/overwrite toggle
            case KeyCode.Insert:
                _insertMode = !_insertMode; SetNeedsDraw();
                return true;

            // ── Bookmarks ───────────────────────────────────────────────────
            case KeyCode.K | KeyCode.AltMask:              _editor.ToggleBookmark(); return true;
            case KeyCode.J | KeyCode.AltMask:              _editor.NextBookmark(); SetNeedsDraw(); return true;
            case KeyCode.I | KeyCode.AltMask:              _editor.PrevBookmark(); SetNeedsDraw(); return true;
            case KeyCode.O | KeyCode.AltMask:              _editor.FlushBookmarks(); return true;

            // ── Display toggles ─────────────────────────────────────────────
            case KeyCode.N | KeyCode.AltMask:
                _showLineNumbers = !_showLineNumbers; SetNeedsDraw();
                return true;
            case KeyCode.T when keyEvent.IsCtrl:
            case KeyCode.S when keyEvent.IsCtrl:
                _syntaxHighlightingOn = !_syntaxHighlightingOn; SetNeedsDraw();
                return true;
            case KeyCode.B | KeyCode.AltMask:
                _colBlock = !_colBlock;
                if (!_colBlock && _selecting) { _editor.StartSelection(); _editor.ExtendSelection(); }
                SetNeedsDraw();
                return true;

            // ── Go-to ───────────────────────────────────────────────────────
            case KeyCode.G when keyEvent.IsCtrl:
            case KeyCode.L | KeyCode.AltMask:
                ExecuteGotoLine();
                return true;

            // ── Format ──────────────────────────────────────────────────────
            case KeyCode.P | KeyCode.AltMask:              ExecuteFormatParagraph(); return true;
            case KeyCode.T | KeyCode.AltMask:              ExecuteSort(); return true;
            case KeyCode.U | KeyCode.AltMask:              ExecuteExternalCommand(); return true;
            case KeyCode.D when keyEvent.IsCtrl:           ExecuteInsertDateTime(); return true;

            // ── Macro ───────────────────────────────────────────────────────
            case KeyCode.R when keyEvent.IsCtrl:           ToggleMacroRecord(); return true;
            case KeyCode.E when keyEvent.IsCtrl:           PlayMacro(); return true;

            // ── Word completion ──────────────────────────────────────────────
            case KeyCode.Tab | KeyCode.CtrlMask:           ExecuteWordComplete(); return true;

            // ── Quote-next ──────────────────────────────────────────────────
            case KeyCode.Q when keyEvent.IsCtrl:
                _quoteNext = true; SetNeedsDraw();
                return true;

            // ── Refresh ─────────────────────────────────────────────────────
            case KeyCode.L when keyEvent.IsCtrl:
                SetNeedsDraw();
                return true;

            // ── Spell check ─────────────────────────────────────────────────
            case KeyCode.F5 | KeyCode.CtrlMask:            ExecuteSpellCheck(); return true;

            // ── Block shift (Tab/Shift+Tab on selection) ─────────────────────
            case KeyCode.Tab when _selecting:
                _editor.ShiftBlockRight(_editor.TabWidth); SetNeedsDraw();
                return true;

            default:
                var rune = keyEvent.AsRune;
                if (rune.Value >= 32)
                {
                    if (_insertMode) _editor.InsertChar((char)rune.Value);
                    else             _editor.ReplaceChar((char)rune.Value);
                    return true;
                }
                if (keyEvent == Key.Enter)
                {
                    _editor.InsertNewlineWithIndent();
                    return true;
                }
                if (keyEvent.KeyCode == (KeyCode.Enter | KeyCode.ShiftMask))
                {
                    _editor.InsertChar('\n');
                    return true;
                }
                if (keyEvent == Key.Tab)
                {
                    _editor.InsertTab();
                    return true;
                }
                if (keyEvent.KeyCode == (KeyCode.Tab | KeyCode.ShiftMask))
                {
                    // Shift+Tab: dedent or move back
                    if (_selecting)
                        _editor.ShiftBlockLeft(_editor.TabWidth);
                    else
                        _editor.DeleteToWordBegin();
                    SetNeedsDraw();
                    return true;
                }
                return base.OnKeyDown(keyEvent);
        }
    }

    // ── Public Command Methods ───────────────────────────────────────────────

    public void ExecuteSave()
    {
        try
        {
            if (_editor.FilePath == null) { ExecuteSaveAs(); return; }
            _editor.Save();
            EditorTitleChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { MessageBox.ErrorQuery("Save Failed", ex.Message, "OK"); }
    }

    public void ExecuteSaveAs()
    {
        var path = PromptInput("Save As", "File name:", _editor.FilePath ?? string.Empty);
        if (path == null) return;
        try
        {
            _editor.SaveAs(path);
            EditorTitleChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { MessageBox.ErrorQuery("Save Failed", ex.Message, "OK"); }
    }

    public void ExecuteOpenFile()
    {
        if (_editor.IsModified)
        {
            var choice = MessageBox.Query("Unsaved Changes",
                "Current file has unsaved changes.", "Save", "Discard", "Cancel");
            if (choice == 2) return;
            if (choice == 0) ExecuteSave();
        }
        var path = PromptInput("Open File", "File name:", _editor.FilePath ?? string.Empty);
        if (path == null) return;
        try
        {
            _editor.LoadFile(path);
            EditorTitleChanged?.Invoke(this, EventArgs.Empty);
            SetNeedsDraw();
        }
        catch (Exception ex) { MessageBox.ErrorQuery("Open Failed", ex.Message, "OK"); }
    }

    public void ExecuteNewFile()
    {
        if (_editor.IsModified)
        {
            var choice = MessageBox.Query("Unsaved Changes",
                "Current file has unsaved changes.", "Save", "Discard", "Cancel");
            if (choice == 2) return;
            if (choice == 0) ExecuteSave();
        }
        _editor.LoadFile(string.Empty);
        EditorTitleChanged?.Invoke(this, EventArgs.Empty);
        SetNeedsDraw();
    }

    public void ExecuteClose()
    {
        if (_editor.IsModified)
        {
            var choice = MessageBox.Query("Unsaved Changes",
                "File has unsaved changes.", "Save", "Discard", "Cancel");
            if (choice == 2) return;
            if (choice == 0) ExecuteSave();
        }
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public void ExecuteInsertFile()
    {
        var path = PromptInput("Insert File", "File name:", string.Empty);
        if (path == null) return;
        try { _editor.InsertFile(path); SetNeedsDraw(); }
        catch (Exception ex) { MessageBox.ErrorQuery("Insert Failed", ex.Message, "OK"); }
    }

    public void ExecuteSaveBlock()
    {
        if (!_editor.HasSelection)
        {
            MessageBox.Query("Save Block", "No block selected.", "OK");
            return;
        }
        var path = PromptInput("Save Block", "File name:", "block.txt");
        if (path == null) return;
        try { _editor.SaveBlock(path); }
        catch (Exception ex) { MessageBox.ErrorQuery("Save Block Failed", ex.Message, "OK"); }
    }

    public void ExecuteAbout()
    {
        MessageBox.Query("About MCEdit",
            "MCEdit — Midnight Commander Internal Editor\n" +
            "C# reimplementation based on mcedit specifications.\n\n" +
            "Original: https://midnight-commander.org/\n" +
            "License: GNU GPL v3+",
            "OK");
    }

    public void ExecuteUndo() { _editor.Undo(); SetNeedsDraw(); }
    public void ExecuteRedo() { _editor.Redo(); SetNeedsDraw(); }

    public void ExecuteToggleMark()
    {
        if (!_selecting) { _selecting = true; _editor.StartSelection(); }
        else { _selecting = false; _editor.ExtendSelection(); }
        SetNeedsDraw();
    }

    public void ExecuteMarkColumn()
    {
        _colBlock = true;
        (_colBlockAnchorLine, _colBlockAnchorCol) = _editor.CursorPosition;
        _selecting = true;
        _editor.StartSelection();
        SetNeedsDraw();
    }

    public void ExecuteMarkAll()
    {
        _editor.SelectAll();
        _selecting = true;
        SetNeedsDraw();
    }

    public void ExecuteUnmark()
    {
        _selecting = false;
        _editor.ClearSelection();
        SetNeedsDraw();
    }

    public void ExecuteCopyBlock()
    {
        if (_colBlock && _selecting)
        {
            var (curLine, curCol) = _editor.CursorPosition;
            _clipboardColBlock = _editor.CopyColumnBlock(_colBlockAnchorLine, _colBlockAnchorCol, curLine, curCol);
            _clipboardText = string.Join('\n', _clipboardColBlock);
        }
        else { _clipboardText = _editor.Copy(); }
        _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    public void ExecuteMoveBlock()
    {
        if (_colBlock && _selecting)
        {
            var (curLine, curCol) = _editor.CursorPosition;
            _clipboardColBlock = _editor.CopyColumnBlock(_colBlockAnchorLine, _colBlockAnchorCol, curLine, curCol);
            _clipboardText = string.Join('\n', _clipboardColBlock);
            _editor.DeleteColumnBlock(_colBlockAnchorLine, _colBlockAnchorCol, curLine, curCol);
        }
        else { _clipboardText = _editor.Copy(); _editor.Cut(); }
        _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    public void ExecuteDeleteBlock()
    {
        if (!_editor.HasSelection) return;
        _editor.Cut(); _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    public void ExecuteCopyToClipfile()
    {
        _clipboardText = _editor.Copy();
        _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    public void ExecuteCutToClipfile()
    {
        _clipboardText = _editor.Copy(); _editor.Cut();
        _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    public void ExecutePasteFromClipfile() { PasteClipboard(); SetNeedsDraw(); }

    public void ExecuteGotoTop()    { _editor.MoveToStart(); SetNeedsDraw(); }
    public void ExecuteGotoBottom() { _editor.MoveToEnd();   SetNeedsDraw(); }

    public void ExecuteSearch() => ShowFind();
    public void ExecuteSearchContinue() => FindAgain();
    public void ExecuteReplace() => ShowFindReplace();
    public void ExecuteReplaceContinue() => RepeatLastReplace();

    public void ExecuteToggleBookmark() { _editor.ToggleBookmark(); SetNeedsDraw(); }
    public void ExecuteNextBookmark()   { _editor.NextBookmark();   SetNeedsDraw(); }
    public void ExecutePrevBookmark()   { _editor.PrevBookmark();   SetNeedsDraw(); }
    public void ExecuteFlushBookmarks() { _editor.FlushBookmarks(); SetNeedsDraw(); }

    public void ExecuteGotoLine()
    {
        var input = PromptInput("Go to line", $"Line number (1-{_editor.Buffer.GetLineCount()}):", string.Empty);
        if (int.TryParse(input, out var line) && line >= 1)
            _editor.GotoLine(line);
        SetNeedsDraw();
    }

    public void ExecuteToggleLineNumbers()   { _showLineNumbers = !_showLineNumbers; SetNeedsDraw(); }
    public void ExecuteMatchBracket()        => GoToMatchingBracket();
    public void ExecuteToggleSyntax()        { _syntaxHighlightingOn = !_syntaxHighlightingOn; SetNeedsDraw(); }
    public void ExecuteToggleRightMargin()   { _showRightMargin = !_showRightMargin; SetNeedsDraw(); }
    public void ExecuteToggleShowTabs()      { _showTabTws = !_showTabTws; SetNeedsDraw(); }
    public void ExecuteToggleInsert()        { _insertMode = !_insertMode; SetNeedsDraw(); }
    public void ExecuteRefresh()             => SetNeedsDraw();
    public void ExecuteInsertDateTime()      => _editor.InsertText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    public void ExecuteInsertLiteral()
    {
        _quoteNext = true; SetNeedsDraw();
    }

    public void ExecuteFormatParagraph()
    {
        _editor.FormatParagraph(_rightMarginColumn);
        SetNeedsDraw();
    }

    public void ExecuteStartStopMacro() => ToggleMacroRecord();
    public void ExecuteSpellCheck()     => ShowSpellCheck();
    public void ExecuteWordComplete()   => WordComplete();

    public void ExecuteOptions() => ShowOptionsDialog();
    public void ExecuteSaveMode() => ShowSaveModeDialog();

    public void ExecuteSort()
    {
        if (!_editor.HasSelection)
        {
            MessageBox.Query("Sort", "Select a block first.", "OK");
            return;
        }
        var cmdStr = PromptInput("Sort", "Sort command:", "sort");
        if (cmdStr == null) return;
        try
        {
            var (selStart, selEnd) = _editor.GetSelectionOffsets();
            var blockText = _editor.Buffer.Extract(selStart, selEnd - selStart);
            using var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"-c \"{cmdStr}\"",
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };
            proc.Start();
            proc.StandardInput.Write(blockText);
            proc.StandardInput.Close();
            var sorted = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            _editor.StartSelection();
            _editor.MoveCursor(selStart);
            _editor.ExtendSelection();
            _editor.InsertText(sorted);
            _selecting = false; _editor.ClearSelection();
            SetNeedsDraw();
        }
        catch (Exception ex) { MessageBox.ErrorQuery("Sort Failed", ex.Message, "OK"); }
    }

    public void ExecuteExternalCommand()
    {
        var cmdStr = PromptInput("Paste Output", "Shell command:", string.Empty);
        if (cmdStr == null) return;
        try
        {
            using var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"-c \"{cmdStr}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);
            _editor.InsertText(output);
            SetNeedsDraw();
        }
        catch (Exception ex) { MessageBox.ErrorQuery("Command Failed", ex.Message, "OK"); }
    }

    public void ExecuteSyntaxChoose()
    {
        // Build list of available syntax highlighters
        var syntaxNames = new[] { "Auto", "C#", "C/C++", "Python", "JavaScript/TypeScript",
                                   "Go", "Rust", "Shell", "JSON", "XML/HTML", "Markdown", "None" };
        var choice = MessageBox.Query("Syntax Highlighting", "Choose syntax:", syntaxNames);
        if (choice < 0) return;
        // Apply choice (stub: reload based on file extension for "Auto", set None, or apply specific)
        if (choice == 0)
        {
            if (_editor.FilePath != null)
            {
                // Reload from file extension
                var h = SyntaxHighlighter.ForFile(_editor.FilePath);
                // Access via reload
                _editor.ReloadSyntax();
            }
        }
        else if (choice == syntaxNames.Length - 1)
        {
            _syntaxHighlightingOn = false;
        }
        else
        {
            _syntaxHighlightingOn = true;
        }
        SetNeedsDraw();
    }

    public void ExecuteHistory()
    {
        // File history — managed by EditorScreen; stub here
        MessageBox.Query("History", "File history is managed by the editor shell.", "OK");
    }

    public void ExecuteUserMenu()
    {
        var userMenuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "mc", "mcedit", "menu");
        if (!File.Exists(userMenuPath))
        {
            MessageBox.Query("User Menu", "No user menu file found.\n" + userMenuPath, "OK");
            return;
        }
        MessageBox.Query("User Menu", "User menu support not yet implemented.", "OK");
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void ToggleMacroRecord()
    {
        if (_isRecordingMacro)
        {
            _isRecordingMacro = false;
            MessageBox.Query("Macro",
                $"Macro recording stopped. {_macroKeys.Count} keystrokes saved.\nPress Ctrl+E to play back.", "OK");
        }
        else
        {
            _macroKeys.Clear();
            _isRecordingMacro = true;
            MessageBox.Query("Macro", "Macro recording started. Press Ctrl+R to stop.", "OK");
        }
        SetNeedsDraw();
    }

    private void PlayMacro()
    {
        if (_isPlayingMacro || _macroKeys.Count == 0)
        {
            if (_macroKeys.Count == 0)
                MessageBox.Query("Macro", "No macro recorded. Press Ctrl+R to start recording.", "OK");
            return;
        }
        _isPlayingMacro = true;
        var snapshot = _macroKeys.ToList();
        foreach (var key in snapshot) OnKeyDown(key);
        _isPlayingMacro = false;
        SetNeedsDraw();
    }

    private void WordComplete()
    {
        var text   = _editor.Buffer.ToString();
        var cursor = _editor.CursorOffset;
        var wordStart = cursor;
        while (wordStart > 0 && (char.IsLetterOrDigit(text[wordStart - 1]) || text[wordStart - 1] == '_'))
            wordStart--;
        var prefix = text[wordStart..cursor];
        if (prefix.Length == 0) return;

        var pattern = new System.Text.RegularExpressions.Regex(
            @"\b" + System.Text.RegularExpressions.Regex.Escape(prefix) + @"[\w]+");
        var matches = pattern.Matches(text)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Value)
            .Where(w => w.Length > prefix.Length)
            .Distinct().OrderBy(w => w).ToList();
        if (matches.Count == 0) return;
        if (matches.Count == 1) { _editor.InsertText(matches[0][prefix.Length..]); }
        else                    { ShowWordCompletePopup(matches, prefix); }
        SetNeedsDraw();
    }

    private void ShowWordCompletePopup(IReadOnlyList<string> matches, string prefix)
    {
        string? chosen = null;
        var d = new Dialog
        {
            Title  = "Word completion",
            Width  = Math.Min(50, Application.Screen.Width - 4),
            Height = Math.Min(matches.Count + 5, 18),
        };
        var lv = new ListView { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(4) };
        lv.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(matches));
        lv.SelectedItem = 0;
        d.Add(lv);
        lv.OpenSelectedItem += (_, _) => { if (lv.SelectedItem >= 0) chosen = matches[lv.SelectedItem]; Application.RequestStop(d); };
        var ok     = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting     += (_, _) => { if (lv.SelectedItem >= 0) chosen = matches[lv.SelectedItem]; Application.RequestStop(d); };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        lv.SetFocus();
        Application.Run(d); d.Dispose();
        if (chosen != null) _editor.InsertText(chosen[prefix.Length..]);
    }

    private void PasteClipboard()
    {
        if (_colBlock && _clipboardColBlock != null)
        {
            var (atLine, atCol) = _editor.CursorPosition;
            _editor.PasteColumnBlock(_clipboardColBlock, atLine, atCol);
        }
        else if (_clipboardText != null)
        {
            _editor.Paste(_clipboardText);
        }
    }

    private void MoveWithShift(Key key)
    {
        switch (key.KeyCode)
        {
            case KeyCode.CursorUp:    _editor.MoveUp();           break;
            case KeyCode.CursorDown:  _editor.MoveDown();         break;
            case KeyCode.CursorLeft:  _editor.MoveLeft();         break;
            case KeyCode.CursorRight: _editor.MoveRight();        break;
            case KeyCode.Home:        _editor.MoveToLineStart();  break;
            case KeyCode.End:         _editor.MoveToLineEnd();    break;
        }
    }

    private void ShowFind()
    {
        string? pattern = null;
        bool caseSensitive = _editor.LastSearch.CaseSensitive;
        bool useRegex      = _editor.LastSearch.Type == SearchType.Regex;

        var d = new Dialog { Title = "Search", Width = 60, Height = 13 };
        d.Add(new Label { X = 1, Y = 1, Text = "Search for:" });
        var tf = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Text = _editor.LastSearch.Pattern };
        d.Add(tf);
        var caseCb    = new CheckBox { X = 1, Y = 4, Text = "Case sensitive",
            CheckedState = caseSensitive ? CheckState.Checked : CheckState.UnChecked };
        var regexCb   = new CheckBox { X = 1, Y = 5, Text = "Regular expression",
            CheckedState = useRegex ? CheckState.Checked : CheckState.UnChecked };
        var backCb    = new CheckBox { X = 1, Y = 6, Text = "Backwards" };
        var wholeCb   = new CheckBox { X = 1, Y = 7, Text = "Whole words" };
        d.Add(caseCb, regexCb, backCb, wholeCb);
        var ok     = new Button { X = Pos.Center() - 5, Y = 9, Text = "OK", IsDefault = true };
        var cancel = new Button { X = Pos.Center() + 3, Y = 9, Text = "Cancel" };
        ok.Accepting     += (_, _) => { pattern = tf.Text?.ToString(); Application.RequestStop(d); };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (string.IsNullOrEmpty(pattern)) return;
        var opts = new SearchOptions
        {
            Pattern       = pattern,
            CaseSensitive = caseCb.CheckedState == CheckState.Checked,
            Type          = regexCb.CheckedState == CheckState.Checked ? SearchType.Regex : SearchType.Normal,
        };
        var result = _editor.FindNext(opts);
        if (!result.Found) MessageBox.Query("Find", "Pattern not found", "OK");
        else SetNeedsDraw();
    }

    private void FindAgain()
    {
        if (string.IsNullOrEmpty(_editor.LastSearch.Pattern)) { ShowFind(); return; }
        var result = _editor.FindNext(_editor.LastSearch);
        if (!result.Found) MessageBox.Query("Find", "Pattern not found", "OK");
        else SetNeedsDraw();
    }

    private void ShowFindReplace()
    {
        string? findPat = null, replPat = null;
        bool replaceAll = false;

        var d = new Dialog { Title = "Find and Replace", Width = 62, Height = 16 };
        d.Add(new Label { X = 1, Y = 1, Text = "Search for:" });
        var tfFind = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Text = _editor.LastSearch.Pattern };
        d.Add(tfFind);
        d.Add(new Label { X = 1, Y = 4, Text = "Replace with:" });
        var tfRepl = new TextField { X = 1, Y = 5, Width = Dim.Fill(1), Text = _editor.LastSearch.Replacement };
        d.Add(tfRepl);
        var caseCb   = new CheckBox { X = 1, Y = 7,  Text = "Case sensitive" };
        var regexCb  = new CheckBox { X = 1, Y = 8,  Text = "Regular expression" };
        var wholeCb  = new CheckBox { X = 1, Y = 9,  Text = "Whole words" };
        d.Add(caseCb, regexCb, wholeCb);

        var btnFind   = new Button { X = 1,              Y = 11, Text = "Find next" };
        var btnAll    = new Button { X = 14,             Y = 11, Text = "Replace all" };
        var btnCancel = new Button { X = 28,             Y = 11, Text = "Cancel", IsDefault = true };
        btnFind.Accepting   += (_, _) => { findPat = tfFind.Text?.ToString(); replPat = tfRepl.Text?.ToString(); Application.RequestStop(d); };
        btnAll.Accepting    += (_, _) => { findPat = tfFind.Text?.ToString(); replPat = tfRepl.Text?.ToString(); replaceAll = true; Application.RequestStop(d); };
        btnCancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(btnFind); d.AddButton(btnAll); d.AddButton(btnCancel);
        Application.Run(d); d.Dispose();

        if (string.IsNullOrEmpty(findPat)) return;
        var opts = new SearchOptions
        {
            Pattern       = findPat,
            Replacement   = replPat ?? string.Empty,
            CaseSensitive = caseCb.CheckedState == CheckState.Checked,
            Type          = regexCb.CheckedState == CheckState.Checked ? SearchType.Regex : SearchType.Normal,
        };
        if (replaceAll)
        {
            var count = _editor.ReplaceAll(opts);
            MessageBox.Query("Replace all", $"Replaced {count} occurrence(s).", "OK");
        }
        else
        {
            var result = _editor.FindNext(opts);
            if (!result.Found) MessageBox.Query("Find", "Pattern not found", "OK");
        }
        SetNeedsDraw();
    }

    private void RepeatLastReplace()
    {
        if (string.IsNullOrEmpty(_editor.LastSearch.Pattern) ||
            string.IsNullOrEmpty(_editor.LastSearch.Replacement))
        { ShowFindReplace(); return; }
        var result = _editor.FindNext(_editor.LastSearch);
        if (!result.Found) MessageBox.Query("Replace", "No more occurrences.", "OK");
        else { _editor.ReplaceAll(_editor.LastSearch); MessageBox.Query("Replace", "Replacement applied.", "OK"); }
        SetNeedsDraw();
    }

    private void ShowSpellCheck()
    {
        var text   = _editor.Buffer.ToString();
        var cursor = _editor.CursorOffset;
        int ws = cursor; while (ws > 0 && char.IsLetter(text[ws - 1])) ws--;
        int we = cursor; while (we < text.Length && char.IsLetter(text[we])) we++;
        if (ws >= we) { MessageBox.Query("Spell check", "No word at cursor.", "OK"); return; }
        var word = text[ws..we];
        string[] suggestions;
        try
        {
            using var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo("aspell", "-a")
                {
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };
            proc.Start();
            proc.StandardInput.WriteLine(word);
            proc.StandardInput.Close();
            var lines = proc.StandardOutput.ReadToEnd().Split('\n');
            proc.WaitForExit(3000);
            suggestions = [];
            foreach (var ln in lines)
            {
                if (ln.StartsWith("*")) return;
                if (ln.StartsWith("&"))
                {
                    var colonIdx = ln.IndexOf(':');
                    if (colonIdx >= 0) suggestions = ln[(colonIdx + 2)..].Split(", ");
                    break;
                }
                if (ln.StartsWith("#")) break;
            }
        }
        catch { MessageBox.ErrorQuery("Spell check", "aspell is not installed or failed to run.", "OK"); return; }
        var items = new List<string> { $"Skip  [{word}]", "Add to dictionary" };
        items.AddRange(suggestions.Take(10).Select(s => s.Trim()));
        var choice = MessageBox.Query("Spell check", $"Word: {word}", items.ToArray());
        if (choice <= 0) return;
        if (choice == 1)
        {
            try
            {
                using var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo("aspell", "-a")
                    { RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true }
                };
                proc.Start();
                proc.StandardInput.WriteLine($"*{word}"); proc.StandardInput.WriteLine("#");
                proc.StandardInput.Close(); proc.WaitForExit(2000);
            }
            catch { }
            return;
        }
        var replacement = suggestions[choice - 2].Trim();
        _editor.MoveCursor(ws); _editor.StartSelection(); _editor.MoveCursor(we); _editor.ExtendSelection();
        _editor.InsertText(replacement);
        _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    private void GoToMatchingBracket()
    {
        var text = _editor.Buffer.ToString();
        var pos  = _editor.CursorOffset;
        if (pos >= text.Length) return;
        var ch = text[pos];
        char open, close; bool forward;
        switch (ch)
        {
            case '(': open = '('; close = ')'; forward = true;  break;
            case '[': open = '['; close = ']'; forward = true;  break;
            case '{': open = '{'; close = '}'; forward = true;  break;
            case '<': open = '<'; close = '>'; forward = true;  break;
            case ')': open = '('; close = ')'; forward = false; break;
            case ']': open = '['; close = ']'; forward = false; break;
            case '}': open = '{'; close = '}'; forward = false; break;
            case '>': open = '<'; close = '>'; forward = false; break;
            default:  MessageBox.Query("Bracket match", "No bracket at cursor.", "OK"); return;
        }
        int depth = 0;
        if (forward)
        {
            for (int i = pos; i < text.Length; i++)
            {
                if (text[i] == open) depth++;
                else if (text[i] == close) { depth--; if (depth == 0) { _editor.MoveCursor(i); SetNeedsDraw(); return; } }
            }
        }
        else
        {
            for (int i = pos; i >= 0; i--)
            {
                if (text[i] == close) depth++;
                else if (text[i] == open) { depth--; if (depth == 0) { _editor.MoveCursor(i); SetNeedsDraw(); return; } }
            }
        }
        MessageBox.Query("Bracket match", "No matching bracket found.", "OK");
    }

    private void ShowOptionsDialog()
    {
        var d = new Dialog { Title = "Editor Options", Width = 60, Height = 22 };
        d.Add(new Label { X = 1, Y = 1, Text = "Tab width:" });
        var tabTf = new TextField { X = 20, Y = 1, Width = 6, Text = _editor.TabWidth.ToString() };
        d.Add(tabTf);
        var expandTabsCb = new CheckBox { X = 1, Y = 3, Text = "Fill tabs with spaces",
            CheckedState = _editor.ExpandTabs ? CheckState.Checked : CheckState.UnChecked };
        var lineNumCb = new CheckBox { X = 1, Y = 5, Text = "Show line numbers",
            CheckedState = _showLineNumbers ? CheckState.Checked : CheckState.UnChecked };
        var syntaxCb = new CheckBox { X = 1, Y = 7, Text = "Syntax highlighting",
            CheckedState = _syntaxHighlightingOn ? CheckState.Checked : CheckState.UnChecked };
        var rightMarginCb = new CheckBox { X = 1, Y = 9, Text = "Show right margin",
            CheckedState = _showRightMargin ? CheckState.Checked : CheckState.UnChecked };
        var tabTwsCb = new CheckBox { X = 1, Y = 11, Text = "Visible tabs/spaces",
            CheckedState = _showTabTws ? CheckState.Checked : CheckState.UnChecked };
        d.Add(new Label { X = 1, Y = 13, Text = "Right margin column:" });
        var marginTf = new TextField { X = 22, Y = 13, Width = 6, Text = _rightMarginColumn.ToString() };
        d.Add(expandTabsCb, lineNumCb, syntaxCb, rightMarginCb, tabTwsCb, marginTf);
        var ok     = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting += (_, _) =>
        {
            if (int.TryParse(tabTf.Text, out var tw) && tw > 0) _editor.TabWidth = tw;
            if (int.TryParse(marginTf.Text, out var mc) && mc > 0) _rightMarginColumn = mc;
            _editor.ExpandTabs      = expandTabsCb.CheckedState == CheckState.Checked;
            _showLineNumbers        = lineNumCb.CheckedState    == CheckState.Checked;
            _syntaxHighlightingOn   = syntaxCb.CheckedState     == CheckState.Checked;
            _showRightMargin        = rightMarginCb.CheckedState == CheckState.Checked;
            _showTabTws             = tabTwsCb.CheckedState      == CheckState.Checked;
            Application.RequestStop(d);
        };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
        SetNeedsDraw();
    }

    private void ShowSaveModeDialog()
    {
        MessageBox.Query("Save Mode",
            "Save modes:\n• Quick save (default)\n• Safe save (write to temp then rename)\n• Backup (create backup before overwrite)\n\nSave mode configuration not yet persisted.",
            "OK");
    }

    private static string? PromptInput(string title, string prompt, string defaultValue)
    {
        string? result = null;
        var d = new Dialog { Title = title, Width = 60, Height = 8 };
        d.Add(new Label { X = 1, Y = 1, Text = prompt });
        var tf = new TextField { X = 1, Y = 3, Width = Dim.Fill(1), Text = defaultValue };
        d.Add(tf);
        var ok     = new Button { X = Pos.Center() - 5, Y = 5, Text = "OK", IsDefault = true };
        var cancel = new Button { X = Pos.Center() + 3, Y = 5, Text = "Cancel" };
        ok.Accepting     += (_, _) => { result = tf.Text?.ToString(); Application.RequestStop(d); };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
        return result;
    }
}
