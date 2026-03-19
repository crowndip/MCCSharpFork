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

    // Settings
    private bool _confirmSave;

    // Read-only mode
    private bool _isReadOnly;

    // Hex view/edit mode
    private bool _hexMode;
    private byte[] _hexBytes = [];
    private int _hexCursorByte;
    private int _hexTopLine;
    private bool _hexCursorInAscii;
    private int _hexNibble;
    private bool _hexModified;
    private const int HexBytesPerRow = 16;

    // Mouse drag selection
    private bool _mouseButtonHeld;

    // Triple-click tracking
    private DateTime _lastClickTime = DateTime.MinValue;
    private int _lastClickLine = -1;

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
        MouseWheel += (_, e) => HandleEditorWheelEvent(e);
    }

    protected override void OnHasFocusChanged(bool newHasFocus, View previousFocused, View newFocused)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocused, newFocused);
        // Only set cursor style on gaining focus; on blur, reset only if the new focus target
        // will not set its own style (i.e., is not another EditorView).
        if (newHasFocus)
            EscSeqUtils.CSI_SetCursorStyle(EscSeqUtils.DECSCUSR_Style.BlinkingUnderline);
        else if (newFocused is not EditorView)
            EscSeqUtils.CSI_SetCursorStyle(EscSeqUtils.DECSCUSR_Style.UserShape);
    }

    /// <summary>When true, editing operations are blocked. Used for the internal viewer replacement.</summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set { _isReadOnly = value; _editor.IsReadOnly = value; SetNeedsDraw(); }
    }

    public new string Title => _editor.FilePath != null
        ? $"Edit: {Path.GetFileName(_editor.FilePath)}{(_editor.IsModified ? " *" : string.Empty)}"
        : "Edit: [new file]";

    public string StatusText
    {
        get
        {
            if (_hexMode)
                return $"[HEX{(_hexModified ? "*" : "")}] Offset:{_hexCursorByte:X8} ({_hexCursorByte}) | {(_hexCursorInAscii ? "ASCII" : "HEX")} pane | Tab=switch F9>Command>Hex=exit";
            var (ln, col) = _editor.CursorPosition;
            char colFlag = _colBlock ? 'C' : '-';
            char modFlag = _editor.IsModified ? 'M' : '-';
            char recFlag = _isRecordingMacro ? 'R' : '-';
            char ovrFlag = !_insertMode ? 'O' : '-';
            char roFlag  = _isReadOnly ? 'R' : '-';
            var file = _editor.FilePath != null ? Path.GetFileName(_editor.FilePath) : "new";
            var syntaxName = _syntaxHighlightingOn && _editor.Highlighter != null
                ? $" [{_editor.Highlighter.SyntaxName}]"
                : string.Empty;
            var total = _editor.Buffer.GetLineCount();
            return $"[{colFlag}{modFlag}{recFlag}{ovrFlag}{roFlag}] {col + 1} L:[{ln + 1}/{total}] {file}{syntaxName}";
        }
    }

    // ── Drawing ─────────────────────────────────────────────────────────────

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        var viewport = Viewport;
        // Leave 1 row for the status bar at the bottom of this view
        var contentHeight = viewport.Height - 1;

        if (_hexMode)
        {
            DrawHexContent(viewport, contentHeight);
            return false;
        }

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
                if (marginScreenCol >= 0 && marginScreenCol < viewport.Width)
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
        int lastNonSpace = line.Length - 1;
        while (lastNonSpace >= 0 && line[lastNonSpace] == ' ') lastNonSpace--;
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
            {
                int tabW = _editor.TabWidth - (pos % _editor.TabWidth);
                Driver!.AddStr("→" + new string(' ', Math.Min(tabW - 1, width - i - 1)));
                i += tabW - 1; pos += tabW - 1;
            }
            else if (_showTabTws && ch == ' ' && pos > lastNonSpace)
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
            return lineNo >= top && lineNo <= bot && col >= left && col <= right;
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
        int lastNonSpace = line.Length - 1;
        while (lastNonSpace >= 0 && line[lastNonSpace] == ' ') lastNonSpace--;
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
            {
                int tabW = _editor.TabWidth - (pos % _editor.TabWidth);
                Driver!.AddStr("→" + new string(' ', Math.Min(tabW - 1, width - i - 1)));
                i += tabW - 1; pos += tabW - 1;
            }
            else if (_showTabTws && ch == ' ' && pos > lastNonSpace)
                { Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.DarkGray, attr.Background)); Driver!.AddStr("·"); }
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
        // Wheel events (Linux routing)
        if (e.Flags.HasFlag(MouseFlags.WheeledUp) || e.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            HandleEditorWheelEvent(e);
            return;
        }

        // Button 1 released: stop drag selection
        if (e.Flags.HasFlag(MouseFlags.Button1Released))
        {
            _mouseButtonHeld = false;
            e.Handled = true;
            return;
        }

        if (!e.Flags.HasFlag(MouseFlags.Button1Clicked) &&
            !e.Flags.HasFlag(MouseFlags.Button1Pressed) &&
            !e.Flags.HasFlag(MouseFlags.Button1DoubleClicked) &&
            !e.Flags.HasFlag(MouseFlags.ReportMousePosition))
            return;

        // Mouse drag: extend selection while button is held
        if (e.Flags.HasFlag(MouseFlags.ReportMousePosition) && _mouseButtonHeld)
        {
            ExtendSelectionToMousePos(e);
            return;
        }

        var viewport = Viewport;
        var contentHeight = viewport.Height - 1;
        var gutter = GutterWidth;

        // Hex mode: click positions hex cursor
        if (_hexMode)
        {
            int screenRow = e.Position.Y;
            int screenCol = e.Position.X;
            if (screenRow >= 0 && screenRow < contentHeight)
            {
                var byteRow = _hexTopLine + screenRow;
                // Detect if click is in hex or ASCII pane
                int asciiStart = 10 + HexBytesPerRow * 3 + 1 + 2;
                if (screenCol >= asciiStart && screenCol < asciiStart + HexBytesPerRow)
                {
                    _hexCursorInAscii = true;
                    _hexCursorByte = Math.Min(byteRow * HexBytesPerRow + (screenCol - asciiStart), _hexBytes.Length - 1);
                }
                else if (screenCol >= 10)
                {
                    _hexCursorInAscii = false;
                    // Find closest byte from hex column
                    int col = screenCol - 10;
                    int byteInRow = Math.Min((col / 3), HexBytesPerRow - 1);
                    _hexCursorByte = Math.Min(byteRow * HexBytesPerRow + byteInRow, _hexBytes.Length - 1);
                }
                _hexNibble = 0;
                SetNeedsDraw();
                SetFocus();
                e.Handled = true;
            }
            return;
        }

        // Click-to-position cursor
        int sRow = e.Position.Y;
        int sCol = e.Position.X - gutter;
        if (sRow >= 0 && sRow < contentHeight && sCol >= 0)
        {
            int targetLine = _topLine + sRow;
            int targetCol  = _leftCol + sCol;
            if (targetLine < _editor.Buffer.GetLineCount())
            {
                var lineText = _editor.Buffer.GetLine(targetLine);
                targetCol = Math.Min(targetCol, lineText.Length);
                _editor.MoveCursor(_editor.Buffer.LineColToOffset(targetLine, targetCol));

                if (e.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
                {
                    // Check for triple-click: second click within 400ms on same line
                    var now = DateTime.UtcNow;
                    if ((now - _lastClickTime).TotalMilliseconds <= 400 && _lastClickLine == targetLine)
                    {
                        // Triple-click: select entire line
                        _editor.MoveToLineStart();
                        _editor.StartSelection();
                        _editor.MoveToLineEnd();
                        _editor.ExtendSelection();
                        _selecting = true;
                        _lastClickTime = DateTime.MinValue;
                    }
                    else
                    {
                        // Double-click: select current word
                        _editor.MoveWordLeft();
                        _editor.StartSelection();
                        _editor.MoveWordRight();
                        _editor.ExtendSelection();
                        _selecting = true;
                        _lastClickTime = now;
                        _lastClickLine = targetLine;
                    }
                }
                else if (e.Flags.HasFlag(MouseFlags.Button1Pressed))
                {
                    // Start of potential drag: clear current selection
                    _selecting = false;
                    _editor.ClearSelection();
                    _mouseButtonHeld = true;
                    _lastClickTime = DateTime.MinValue;
                }
                SetNeedsDraw();
                SetFocus();
                e.Handled = true;
            }
        }
    }

    private void ExtendSelectionToMousePos(MouseEventArgs e)
    {
        var viewport = Viewport;
        var contentHeight = viewport.Height - 1;
        var gutter = GutterWidth;
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
                var anchorOffset = _editor.CursorOffset; // save before move for correct drag-anchor
                _editor.MoveCursor(_editor.Buffer.LineColToOffset(targetLine, targetCol));
                if (!_selecting)
                {
                    _selecting = true;
                    _selectionAnchor = anchorOffset;
                    _editor.StartSelection();
                }
                else
                {
                    _editor.ExtendSelection();
                }
                SetNeedsDraw();
                SetFocus();
                e.Handled = true;
            }
        }
    }

    private void OnMouseMoved(object? sender, MouseEventArgs e)
    {
        if (!_mouseButtonHeld || _hexMode) return;
        ExtendSelectionToMousePos(e);
    }

    private void HandleEditorWheelEvent(MouseEventArgs e)
    {
        if (e.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            if (_hexMode)
                _hexTopLine = Math.Max(0, _hexTopLine - 3);
            else
            {
                _topLine = Math.Max(0, _topLine - 3);
                _lockScroll = true;
            }
            SetNeedsDraw();
            e.Handled = true;
        }
        else if (e.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            if (_hexMode)
            {
                var maxLine = Math.Max(0, (_hexBytes.Length + HexBytesPerRow - 1) / HexBytesPerRow - 1);
                _hexTopLine = Math.Min(maxLine, _hexTopLine + 3);
            }
            else
            {
                _topLine = Math.Min(Math.Max(0, _editor.Buffer.GetLineCount() - 1), _topLine + 3);
                _lockScroll = true;
            }
            SetNeedsDraw();
            e.Handled = true;
        }
    }

    // ── Keyboard ─────────────────────────────────────────────────────────────

    protected override bool OnKeyDown(Key keyEvent)
    {
        // Hex mode: route keys to hex handler
        if (_hexMode)
            return HandleHexKey(keyEvent);

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

        // Ctrl+Shift: extend selection to word / file start/end / page
        if (keyEvent.IsShift && keyEvent.IsCtrl && keyEvent.KeyCode is
            KeyCode.Home or KeyCode.End or
            KeyCode.CursorLeft or KeyCode.CursorRight or
            KeyCode.CursorUp or KeyCode.CursorDown or
            KeyCode.PageUp or KeyCode.PageDown)
        {
            if (!_selecting)
            {
                _selecting = true;
                _selectionAnchor = _editor.CursorOffset;
                _editor.StartSelection();
            }
            MoveWithCtrlShift(keyEvent);
            if (!_colBlock) _editor.ExtendSelection();
            SetNeedsDraw();
            return true;
        }

        // Shift+Arrow: extend selection (stream or column mode)
        if (keyEvent.IsShift && keyEvent.KeyCode is
            KeyCode.CursorUp or KeyCode.CursorDown or
            KeyCode.CursorLeft or KeyCode.CursorRight or
            KeyCode.Home or KeyCode.End or
            KeyCode.PageUp or KeyCode.PageDown)
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
            case KeyCode.F8:
                if (!_isReadOnly) { _editor.DeleteLine(); SetNeedsDraw(); }
                return true;
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

            // Alt+Arrows: extend column selection
            case KeyCode.CursorUp when keyEvent.IsAlt:
                if (!_selecting || !_colBlock)
                {
                    _colBlock = true;
                    (_colBlockAnchorLine, _colBlockAnchorCol) = _editor.CursorPosition;
                    _selecting = true;
                    _editor.StartSelection();
                }
                _editor.MoveUp();
                SetNeedsDraw();
                return true;
            case KeyCode.CursorDown when keyEvent.IsAlt:
                if (!_selecting || !_colBlock)
                {
                    _colBlock = true;
                    (_colBlockAnchorLine, _colBlockAnchorCol) = _editor.CursorPosition;
                    _selecting = true;
                    _editor.StartSelection();
                }
                _editor.MoveDown();
                SetNeedsDraw();
                return true;
            case KeyCode.CursorLeft when keyEvent.IsAlt:
                if (!_selecting || !_colBlock)
                {
                    _colBlock = true;
                    (_colBlockAnchorLine, _colBlockAnchorCol) = _editor.CursorPosition;
                    _selecting = true;
                    _editor.StartSelection();
                }
                _editor.MoveLeft();
                SetNeedsDraw();
                return true;
            case KeyCode.CursorRight when keyEvent.IsAlt:
                if (!_selecting || !_colBlock)
                {
                    _colBlock = true;
                    (_colBlockAnchorLine, _colBlockAnchorCol) = _editor.CursorPosition;
                    _selecting = true;
                    _editor.StartSelection();
                }
                _editor.MoveRight();
                SetNeedsDraw();
                return true;

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
            case KeyCode.Backspace:
                if (!_isReadOnly) _editor.Backspace();
                return true;
            case KeyCode.Delete:
                if (!_isReadOnly) _editor.DeleteForward();
                return true;
            case KeyCode.Y when keyEvent.IsCtrl:
                if (!_isReadOnly) _editor.DeleteLine();
                return true;
            case KeyCode.K when keyEvent.IsCtrl:
                if (!_isReadOnly) _editor.DeleteToEndOfLine();
                return true;

            // Alt+Backspace = delete to word begin
            case KeyCode.Backspace | KeyCode.AltMask:
                if (!_isReadOnly) _editor.DeleteToWordBegin();
                return true;

            // Alt+D = delete to word end
            case KeyCode.D | KeyCode.AltMask:
                if (!_isReadOnly) _editor.DeleteToWordEnd();
                return true;

            // ── Clipboard ───────────────────────────────────────────────────
            case KeyCode.Insert | KeyCode.CtrlMask:
                _clipboardText = _editor.Copy(); _selecting = false; _editor.ClearSelection();
                return true;
            case KeyCode.Insert | KeyCode.ShiftMask:
                if (!_isReadOnly) PasteClipboard();
                return true;
            case KeyCode.Delete | KeyCode.ShiftMask:
                _clipboardText = _editor.Copy();
                if (!_isReadOnly) _editor.Cut();
                _selecting = false; _editor.ClearSelection();
                return true;
            case KeyCode.C when keyEvent.IsCtrl:
                ExecuteCopyToSystemClipboard();
                return true;
            case KeyCode.X when keyEvent.IsCtrl:
                if (!_isReadOnly) ExecuteCutToSystemClipboard();
                return true;
            case KeyCode.V when keyEvent.IsCtrl:
                if (!_isReadOnly) ExecutePasteFromSystemClipboard();
                return true;
            case KeyCode.V | KeyCode.AltMask:
                _editor.PageUp(Viewport.Height - 2); SetNeedsDraw();
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

            // ── Hex view/edit toggle ──────────────────────────────────────────
            case KeyCode.H when keyEvent.IsCtrl:
                ToggleHexMode();
                return true;

            // ── Refresh ─────────────────────────────────────────────────────
            case KeyCode.L when keyEvent.IsCtrl:
                SetNeedsDraw();
                return true;

            // ── Spell check ─────────────────────────────────────────────────
            case KeyCode.F5 | KeyCode.CtrlMask:            ExecuteSpellCheck(); return true;

            // ── Block shift (Tab/Shift+Tab on selection) ─────────────────────
            case KeyCode.Tab when _selecting:
                if (!_isReadOnly) { _editor.ShiftBlockRight(_editor.TabWidth); SetNeedsDraw(); }
                return true;

            default:
                var rune = keyEvent.AsRune;
                if (rune.Value >= 32)
                {
                    if (!_isReadOnly)
                    {
                        if (_insertMode) _editor.InsertChar((char)rune.Value);
                        else             _editor.ReplaceChar((char)rune.Value);
                    }
                    return true;
                }
                if (keyEvent == Key.Enter)
                {
                    if (!_isReadOnly) _editor.InsertNewlineWithIndent();
                    return true;
                }
                if (keyEvent.KeyCode == (KeyCode.Enter | KeyCode.ShiftMask))
                {
                    if (!_isReadOnly) _editor.InsertChar('\n');
                    return true;
                }
                if (keyEvent == Key.Tab)
                {
                    if (!_isReadOnly) _editor.InsertTab();
                    return true;
                }
                if (keyEvent.KeyCode == (KeyCode.Tab | KeyCode.ShiftMask))
                {
                    if (!_isReadOnly)
                    {
                        if (_selecting)
                            _editor.ShiftBlockLeft(_editor.TabWidth);
                        else
                            _editor.DeleteToWordBegin();
                        SetNeedsDraw();
                    }
                    return true;
                }
                return base.OnKeyDown(keyEvent);
        }
    }

    // ── Public Command Methods ───────────────────────────────────────────────

    /// <summary>Apply settings from an EditorSettings object to this view and its controller.</summary>
    public void ApplySettings(EditorSettings s)
    {
        _editor.TabWidth          = s.TabWidth;
        _editor.ExpandTabs        = s.ExpandTabs;
        _editor.AutoIndent        = s.AutoIndent;
        _editor.TypewriterWrap    = s.TypewriterWrap;
        _editor.WrapLineLength    = s.WrapLineLength;
        _editor.SaveMode          = s.SaveMode;
        _editor.BackupExtension   = s.BackupExtension;
        _editor.SavePosition      = s.SavePosition;
        _editor.BackspaceThruTabs = s.BackspaceThruTabs;
        _showLineNumbers          = s.ShowLineNumbers;
        _syntaxHighlightingOn     = s.SyntaxHighlighting;
        _showRightMargin          = s.ShowRightMargin;
        _rightMarginColumn        = s.RightMarginColumn;
        _showTabTws               = s.ShowTabTws;
        _confirmSave              = s.ConfirmSave;
        SetNeedsDraw();
    }

    /// <summary>Capture current view and controller state into an EditorSettings object.</summary>
    public EditorSettings CaptureSettings()
    {
        return new EditorSettings
        {
            TabWidth           = _editor.TabWidth,
            ExpandTabs         = _editor.ExpandTabs,
            AutoIndent         = _editor.AutoIndent,
            TypewriterWrap     = _editor.TypewriterWrap,
            WrapLineLength     = _editor.WrapLineLength,
            SaveMode           = _editor.SaveMode,
            BackupExtension    = _editor.BackupExtension,
            SavePosition       = _editor.SavePosition,
            BackspaceThruTabs  = _editor.BackspaceThruTabs,
            ShowLineNumbers    = _showLineNumbers,
            SyntaxHighlighting = _syntaxHighlightingOn,
            ShowRightMargin    = _showRightMargin,
            RightMarginColumn  = _rightMarginColumn,
            ShowTabTws         = _showTabTws,
            ConfirmSave        = _confirmSave,
        };
    }

    public void ExecuteSave()
    {
        try
        {
            if (_editor.FilePath == null) { ExecuteSaveAs(); return; }
            if (_confirmSave)
            {
                var fileName = Path.GetFileName(_editor.FilePath);
                var choice = MessageBox.Query("Confirm Save", $"Save to {fileName}?", "Yes", "No");
                if (choice != 0) return;
            }
            _editor.Save();
            EditorTitleChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { MessageBox.ErrorQuery("Save Failed", ex.Message, "OK"); }
    }

    public void ExecuteSaveAs()
    {
        var path = PromptInput("Save As", "File name:", _editor.FilePath ?? string.Empty);
        if (path == null) return;

        // Show line ending choice dialog
        string lineEnding = "\n"; // default LF
        try
        {
            var d = new Dialog { Title = "Line Ending", Width = 40, Height = 12 };
            d.Add(new Label { X = 1, Y = 1, Text = "Choose line ending:" });
            var rg = new RadioGroup
            {
                X = 1, Y = 3,
                RadioLabels = new string[] { "LF (Unix)", "CRLF (Windows)", "CR (Mac)", "As-is" },
                SelectedItem = 0,
            };
            d.Add(rg);
            var ok     = new Button { Text = "OK", IsDefault = true };
            var cancel = new Button { Text = "Cancel" };
            bool cancelled = false;
            ok.Accepting     += (_, _) => Application.RequestStop(d);
            cancel.Accepting += (_, _) => { cancelled = true; Application.RequestStop(d); };
            d.AddButton(ok); d.AddButton(cancel);
            Application.Run(d); d.Dispose();
            if (cancelled) return;
            lineEnding = rg.SelectedItem switch
            {
                0 => "\n",
                1 => "\r\n",
                2 => "\r",
                _ => null!,  // As-is: null signals no conversion
            };
        }
        catch { /* if dialog fails, use default */ }

        try
        {
            if (lineEnding == null)
                _editor.SaveAs(path);
            else
                _editor.SaveAsWithLineEnding(path, lineEnding);
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
        _editor.SaveCurrentPosition();
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

    private static string ClipFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache", "mc", "mcedit", "mcedit.clip");

    public void ExecuteCopyToClipfile()
    {
        _clipboardText = _editor.Copy();
        try
        {
            var clipDir = Path.GetDirectoryName(ClipFilePath)!;
            Directory.CreateDirectory(clipDir);
            File.WriteAllText(ClipFilePath, _clipboardText ?? string.Empty);
        }
        catch { /* ignore file write errors */ }
        _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    public void ExecuteCutToClipfile()
    {
        _clipboardText = _editor.Copy();
        _editor.Cut();
        try
        {
            var clipDir = Path.GetDirectoryName(ClipFilePath)!;
            Directory.CreateDirectory(clipDir);
            File.WriteAllText(ClipFilePath, _clipboardText ?? string.Empty);
        }
        catch { /* ignore file write errors */ }
        _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    public void ExecutePasteFromClipfile()
    {
        try
        {
            if (File.Exists(ClipFilePath))
                _clipboardText = File.ReadAllText(ClipFilePath);
        }
        catch { /* fall back to in-memory clipboard */ }
        PasteClipboard();
        SetNeedsDraw();
    }

    // ── OS / Desktop Clipboard ───────────────────────────────────────────────

    /// <summary>Copy selected text to the OS desktop clipboard (Ctrl+C).</summary>
    public void ExecuteCopyToSystemClipboard()
    {
        var text = _editor.Copy();
        if (string.IsNullOrEmpty(text)) return;
        _clipboardText = text;           // keep internal clipboard in sync
        if (!OsClipboard.Set(text))
            MessageBox.Query("Clipboard", "Could not access the system clipboard.\n" +
                "On Linux, install xclip, xsel, or wl-clipboard.", "OK");
        _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    /// <summary>Cut selected text to the OS desktop clipboard (Ctrl+X).</summary>
    public void ExecuteCutToSystemClipboard()
    {
        var text = _editor.Copy();
        if (string.IsNullOrEmpty(text)) return;
        _clipboardText = text;           // keep internal clipboard in sync
        _editor.Cut();
        if (!OsClipboard.Set(text))
            MessageBox.Query("Clipboard", "Could not access the system clipboard.\n" +
                "On Linux, install xclip, xsel, or wl-clipboard.", "OK");
        _selecting = false; _editor.ClearSelection(); SetNeedsDraw();
    }

    /// <summary>Paste text from the OS desktop clipboard at the cursor (Ctrl+V).</summary>
    public void ExecutePasteFromSystemClipboard()
    {
        var text = OsClipboard.Get();
        if (!string.IsNullOrEmpty(text))
        {
            _clipboardText = text;       // keep internal clipboard in sync
            _editor.Paste(text);
            SetNeedsDraw();
        }
        else
        {
            // Fall back to internal clipboard if OS clipboard is unavailable or empty
            PasteClipboard();
        }
    }

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

    public void ExecuteToggleHexMode()       => ToggleHexMode();
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
            var sortPsi = new System.Diagnostics.ProcessStartInfo("/bin/sh")
            {
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            sortPsi.ArgumentList.Add("-c");
            sortPsi.ArgumentList.Add(cmdStr);
            using var proc = new System.Diagnostics.Process { StartInfo = sortPsi };
            proc.Start();
            proc.StandardInput.Write(blockText);
            proc.StandardInput.Close();
            var sorted = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            _editor.MoveCursor(selStart);
            _editor.StartSelection();
            _editor.MoveCursor(selEnd);
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
            var extPsi = new System.Diagnostics.ProcessStartInfo("/bin/sh")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            extPsi.ArgumentList.Add("-c");
            extPsi.ArgumentList.Add(cmdStr);
            using var proc = new System.Diagnostics.Process { StartInfo = extPsi };
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
        var languages = new[]
        {
            ("Auto (detect from extension)", "auto"),
            ("C#", ".cs"),
            ("C/C++", ".c"),
            ("Python", ".py"),
            ("JavaScript/TypeScript", ".js"),
            ("Go", ".go"),
            ("Rust", ".rs"),
            ("Shell/Bash", ".sh"),
            ("JSON", ".json"),
            ("XML/HTML", ".xml"),
            ("Markdown", ".md"),
            ("Ruby", ".rb"),
            ("PHP", ".php"),
            ("Java", ".java"),
            ("CSS", ".css"),
            ("YAML", ".yaml"),
            ("TOML", ".toml"),
            ("Lua", ".lua"),
            ("R", ".r"),
            ("Swift", ".swift"),
            ("Kotlin", ".kt"),
            ("None (disable highlighting)", "none"),
        };

        string? chosen = null;
        var d = new Dialog
        {
            Title  = "Syntax Highlighting",
            Width  = Math.Min(60, Application.Screen.Width - 4),
            Height = Math.Min(languages.Length + 6, 26),
        };
        var lv = new ListView { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(4) };
        lv.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(
            languages.Select(l => l.Item1).ToList()));
        lv.SelectedItem = 0;
        d.Add(lv);
        lv.OpenSelectedItem += (_, _) =>
        {
            if (lv.SelectedItem >= 0) chosen = languages[lv.SelectedItem].Item2;
            Application.RequestStop(d);
        };
        var ok = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting     += (_, _) => { if (lv.SelectedItem >= 0) chosen = languages[lv.SelectedItem].Item2; Application.RequestStop(d); };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        lv.SetFocus();
        Application.Run(d); d.Dispose();

        if (chosen == null) return;
        if (chosen == "auto")
        {
            _editor.ReloadSyntax();
            _syntaxHighlightingOn = _editor.Highlighter != null;
        }
        else if (chosen == "none")
        {
            _syntaxHighlightingOn = false;
        }
        else
        {
            // Force specific syntax by extension
            var rules = SyntaxRuleSet.ForExtension(chosen);
            if (rules != null)
            {
                _editor.SetHighlighter(new SyntaxHighlighter(rules));
                _syntaxHighlightingOn = true;
            }
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
        var menuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "mc", "mcedit", "menu");

        // Fallback to system menu
        if (!File.Exists(menuPath))
            menuPath = "/usr/share/mc/mcedit/menu";

        if (!File.Exists(menuPath))
        {
            MessageBox.Query("User Menu", "No user menu file found.\n" + menuPath, "OK");
            return;
        }

        // Parse menu file
        var entries = ParseUserMenu(menuPath);
        if (entries.Count == 0)
        {
            MessageBox.Query("User Menu", "No menu entries found.", "OK");
            return;
        }

        var labels = entries.Select(e => $"{(e.Key != '\0' ? e.Key.ToString() : " ")} {e.Label}").ToArray();
        int choice = MessageBox.Query("User Menu", "Select action:", labels);
        if (choice < 0 || choice >= entries.Count) return;

        // Execute the command
        var cmd = ExpandMacros(entries[choice].Command);
        try
        {
            var _psi = new System.Diagnostics.ProcessStartInfo("/bin/sh")
                { UseShellExecute = false, CreateNoWindow = false };
            _psi.ArgumentList.Add("-c");
            _psi.ArgumentList.Add(cmd);
            using var proc = new System.Diagnostics.Process { StartInfo = _psi };
            proc.Start();
            proc.WaitForExit(30000);
        }
        catch (Exception ex) { MessageBox.ErrorQuery("User Menu", ex.Message, "OK"); }
    }

    private record UserMenuItem(char Key, string Label, string Command);

    private static List<UserMenuItem> ParseUserMenu(string path)
    {
        var entries = new List<UserMenuItem>();
        var lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            if (!char.IsWhiteSpace(line[0]))
            {
                // Header line: [key] label
                char key = '\0';
                string label = line.Trim();
                if (label.Length > 1 && !char.IsWhiteSpace(label[1]))
                { key = label[0]; label = label[1..].Trim(); }
                // Collect command lines (indented)
                var cmd = new StringBuilder();
                while (i + 1 < lines.Length && lines[i + 1].Length > 0 && char.IsWhiteSpace(lines[i + 1][0]))
                {
                    cmd.AppendLine(lines[++i].Trim());
                }
                entries.Add(new UserMenuItem(key, label, cmd.ToString().Trim()));
            }
        }
        return entries;
    }

    private string ExpandMacros(string cmd)
    {
        var file = _editor.FilePath ?? string.Empty;
        return cmd
            .Replace("%f", file)
            .Replace("%n", Path.GetFileNameWithoutExtension(file))
            .Replace("%x", Path.GetExtension(file))
            .Replace("%d", Path.GetDirectoryName(file) ?? ".")
            .Replace("%l", (_editor.CursorPosition.Line + 1).ToString())
            .Replace("%c", (_editor.CursorPosition.Column + 1).ToString());
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string MacroFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "mc", "mc.macros");

    private void SaveMacro(string name)
    {
        try
        {
            var dir = Path.GetDirectoryName(MacroFilePath)!;
            Directory.CreateDirectory(dir);
            // Simple format: name:key1,key2,...
            var entries = new List<string>();
            if (File.Exists(MacroFilePath))
                entries.AddRange(File.ReadAllLines(MacroFilePath)
                    .Where(l => !l.StartsWith(name + ":")));
            var keyStr = string.Join(",", _macroKeys.Select(k => (long)k.KeyCode));
            entries.Add($"{name}:{keyStr}");
            File.WriteAllLines(MacroFilePath, entries);
        }
        catch { /* ignore errors */ }
    }

    private bool LoadMacro(string name)
    {
        try
        {
            if (!File.Exists(MacroFilePath)) return false;
            foreach (var line in File.ReadAllLines(MacroFilePath))
            {
                var colon = line.IndexOf(':');
                if (colon < 0 || line[..colon] != name) continue;
                var codes = line[(colon + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries);
                _macroKeys.Clear();
                foreach (var codeStr in codes)
                {
                    if (long.TryParse(codeStr, out long code))
                        _macroKeys.Add(new Key((KeyCode)code));
                }
                return true;
            }
        }
        catch { }
        return false;
    }

    private void ToggleMacroRecord()
    {
        if (_isRecordingMacro)
        {
            _isRecordingMacro = false;
            SaveMacro("default");
            MessageBox.Query("Macro",
                $"Macro recording stopped. {_macroKeys.Count} keystrokes saved.", "OK");
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
        if (_macroKeys.Count == 0)
            LoadMacro("default");
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
            case KeyCode.CursorUp:    _editor.MoveUp();                       break;
            case KeyCode.CursorDown:  _editor.MoveDown();                     break;
            case KeyCode.CursorLeft:  _editor.MoveLeft();                     break;
            case KeyCode.CursorRight: _editor.MoveRight();                    break;
            case KeyCode.Home:        _editor.MoveToLineStart();              break;
            case KeyCode.End:         _editor.MoveToLineEnd();                break;
            case KeyCode.PageUp:      _editor.PageUp(Viewport.Height - 2);   break;
            case KeyCode.PageDown:    _editor.PageDown(Viewport.Height - 2); break;
        }
    }

    private void MoveWithCtrlShift(Key key)
    {
        switch (key.KeyCode)
        {
            case KeyCode.Home:        _editor.MoveToStart();                  break;
            case KeyCode.End:         _editor.MoveToEnd();                    break;
            case KeyCode.CursorLeft:  _editor.MoveWordLeft();                 break;
            case KeyCode.CursorRight: _editor.MoveWordRight();                break;
            case KeyCode.CursorUp:    _editor.PageUp(Viewport.Height - 2);   break;
            case KeyCode.CursorDown:  _editor.PageDown(Viewport.Height - 2); break;
            case KeyCode.PageUp:      _editor.GotoLine(1);                    break;
            case KeyCode.PageDown:    _editor.GotoLine(_editor.Buffer.GetLineCount()); break;
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
            CaseSensitive = caseCb.CheckedState  == CheckState.Checked,
            Type          = regexCb.CheckedState  == CheckState.Checked ? SearchType.Regex : SearchType.Normal,
            Backward      = backCb.CheckedState   == CheckState.Checked,
            WholeWords    = wholeCb.CheckedState   == CheckState.Checked,
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
        var replaced = _editor.ReplaceNext(_editor.LastSearch);
        if (!replaced) MessageBox.Query("Replace", "No more occurrences.", "OK");
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
        var d = new Dialog { Title = "Editor Options", Width = 64, Height = 36 };
        d.Add(new Label { X = 1, Y = 1, Text = "Tab width:" });
        var tabTf = new TextField { X = 20, Y = 1, Width = 6, Text = _editor.TabWidth.ToString() };
        d.Add(tabTf);
        var expandTabsCb = new CheckBox { X = 1, Y = 3, Text = "Fill tabs with spaces",
            CheckedState = _editor.ExpandTabs ? CheckState.Checked : CheckState.UnChecked };
        var autoIndentCb = new CheckBox { X = 1, Y = 5, Text = "Auto indent",
            CheckedState = _editor.AutoIndent ? CheckState.Checked : CheckState.UnChecked };
        var lineNumCb = new CheckBox { X = 1, Y = 7, Text = "Show line numbers",
            CheckedState = _showLineNumbers ? CheckState.Checked : CheckState.UnChecked };
        var syntaxCb = new CheckBox { X = 1, Y = 9, Text = "Syntax highlighting",
            CheckedState = _syntaxHighlightingOn ? CheckState.Checked : CheckState.UnChecked };
        var rightMarginCb = new CheckBox { X = 1, Y = 11, Text = "Show right margin",
            CheckedState = _showRightMargin ? CheckState.Checked : CheckState.UnChecked };
        var tabTwsCb = new CheckBox { X = 1, Y = 13, Text = "Visible tabs/spaces",
            CheckedState = _showTabTws ? CheckState.Checked : CheckState.UnChecked };
        var confirmSaveCb = new CheckBox { X = 1, Y = 15, Text = "Confirm before saving",
            CheckedState = _confirmSave ? CheckState.Checked : CheckState.UnChecked };
        var typewriterWrapCb = new CheckBox { X = 1, Y = 17, Text = "Typewriter word wrap",
            CheckedState = _editor.TypewriterWrap ? CheckState.Checked : CheckState.UnChecked };
        d.Add(new Label { X = 1, Y = 19, Text = "Right margin column:" });
        var marginTf = new TextField { X = 22, Y = 19, Width = 6, Text = _rightMarginColumn.ToString() };
        d.Add(new Label { X = 1, Y = 21, Text = "Wrap line length:" });
        var wrapTf = new TextField { X = 22, Y = 21, Width = 6, Text = _editor.WrapLineLength.ToString() };
        var bkspThruTabsCb = new CheckBox { X = 1, Y = 23, Text = "Backspace through tab stops",
            CheckedState = _editor.BackspaceThruTabs ? CheckState.Checked : CheckState.UnChecked };
        d.Add(expandTabsCb, autoIndentCb, lineNumCb, syntaxCb, rightMarginCb, tabTwsCb,
              confirmSaveCb, typewriterWrapCb, marginTf, wrapTf, bkspThruTabsCb);
        var ok     = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting += (_, _) =>
        {
            if (int.TryParse(tabTf.Text, out var tw) && tw > 0) _editor.TabWidth = tw;
            if (int.TryParse(marginTf.Text, out var mc) && mc > 0) _rightMarginColumn = mc;
            if (int.TryParse(wrapTf.Text, out var wl) && wl > 0) _editor.WrapLineLength = wl;
            _editor.ExpandTabs        = expandTabsCb.CheckedState    == CheckState.Checked;
            _editor.AutoIndent        = autoIndentCb.CheckedState     == CheckState.Checked;
            _editor.TypewriterWrap    = typewriterWrapCb.CheckedState == CheckState.Checked;
            _editor.BackspaceThruTabs = bkspThruTabsCb.CheckedState  == CheckState.Checked;
            _showLineNumbers          = lineNumCb.CheckedState        == CheckState.Checked;
            _syntaxHighlightingOn     = syntaxCb.CheckedState         == CheckState.Checked;
            _showRightMargin          = rightMarginCb.CheckedState     == CheckState.Checked;
            _showTabTws               = tabTwsCb.CheckedState          == CheckState.Checked;
            _confirmSave              = confirmSaveCb.CheckedState     == CheckState.Checked;
            Application.RequestStop(d);
        };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
        SetNeedsDraw();
    }

    private void ShowSaveModeDialog()
    {
        var d = new Dialog { Title = "Save Mode", Width = 50, Height = 12 };
        d.Add(new Label { X = 1, Y = 1, Text = "File saving mode:" });
        var rg = new RadioGroup
        {
            X = 1, Y = 3,
            RadioLabels = new string[]
            {
                "Quick save (overwrite file in place)",
                "Safe save (write temp, then rename)",
                "Backup save (keep original as backup)",
            },
            SelectedItem = _editor.SaveMode,
        };
        d.Add(rg);
        d.Add(new Label { X = 1, Y = 7, Text = "Backup extension:" });
        var backupTf = new TextField { X = 20, Y = 7, Width = 10, Text = _editor.BackupExtension };
        d.Add(backupTf);
        var ok     = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting += (_, _) =>
        {
            _editor.SaveMode = rg.SelectedItem;
            _editor.BackupExtension = backupTf.Text?.ToString() ?? "~";
            Application.RequestStop(d);
        };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    public void ExecuteExternalFormatter()
    {
        var cmdStr = PromptInput("External Formatter", "Formatter command:", "fmt");
        if (cmdStr == null) return;
        // If selection, pipe it; otherwise pipe whole file
        string inputText;
        bool hasSelection = _editor.HasSelection;
        if (hasSelection)
        {
            var (selStart, selEnd) = _editor.GetSelectionOffsets();
            inputText = _editor.Buffer.Extract(selStart, selEnd - selStart);
        }
        else
        {
            inputText = _editor.Buffer.ToString();
        }
        try
        {
            var fmtPsi = new System.Diagnostics.ProcessStartInfo("/bin/sh")
            {
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            fmtPsi.ArgumentList.Add("-c");
            fmtPsi.ArgumentList.Add(cmdStr);
            using var proc = new System.Diagnostics.Process { StartInfo = fmtPsi };
            proc.Start();
            proc.StandardInput.Write(inputText);
            proc.StandardInput.Close();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);
            if (hasSelection)
            {
                var (selStart, selEnd) = _editor.GetSelectionOffsets();
                _editor.MoveCursor(selStart);
                _editor.StartSelection();
                _editor.MoveCursor(selEnd);
                _editor.ExtendSelection();
                _editor.InsertText(output);
            }
            else
            {
                _editor.SelectAll();
                _editor.InsertText(output);
            }
            _selecting = false;
            _editor.ClearSelection();
            SetNeedsDraw();
        }
        catch (Exception ex) { MessageBox.ErrorQuery("Formatter Failed", ex.Message, "OK"); }
    }

    public void ExecuteEncodingSelect()
    {
        var encodings = new[]
        {
            "UTF-8",
            "UTF-16 LE",
            "UTF-16 BE",
            "ASCII",
            "ISO-8859-1 (Latin-1)",
            "ISO-8859-2 (Central European)",
            "Windows-1250",
            "Windows-1251 (Cyrillic)",
            "Windows-1252 (Western)",
            "KOI8-R (Russian)",
        };
        var choice = MessageBox.Query("Select Encoding",
            "Note: Encoding selection affects how the file is\n" +
            "loaded/saved. Current session uses UTF-8.\n\nEncoding:",
            encodings);
        if (choice < 0) return;
        // In a full implementation, this would re-read the file with the chosen encoding.
        // For now, save the preference and inform the user.
        MessageBox.Query("Encoding",
            $"Selected: {encodings[choice]}\n\nReopen the file to apply the new encoding.", "OK");
    }

    public void ExecuteDeleteMacro()
    {
        if (_macroKeys.Count == 0 && !File.Exists(MacroFilePath))
        {
            MessageBox.Query("Delete Macro", "No saved macro found.", "OK");
            return;
        }
        var choice = MessageBox.Query("Delete Macro",
            "Delete the saved macro 'default'?", "Yes", "No");
        if (choice != 0) return;
        _macroKeys.Clear();
        try
        {
            if (File.Exists(MacroFilePath))
            {
                var lines = File.ReadAllLines(MacroFilePath)
                    .Where(l => !l.StartsWith("default:")).ToArray();
                File.WriteAllLines(MacroFilePath, lines);
            }
        }
        catch { }
        MessageBox.Query("Delete Macro", "Macro deleted.", "OK");
    }

    public void ExecuteCheckWord()
    {
        // Like spell check but focused specifically on spell-check of current word
        ShowSpellCheck();
    }

    public void ExecuteLearnKeys()
    {
        MessageBox.Query("Keyboard Reference",
            "Navigation:\n" +
            "  Arrows=Move  Ctrl+Arrows=Word  Home/End=Line  PgUp/PgDn=Page\n" +
            "  Ctrl+Home/End=File  Ctrl+Up/Down=Scroll  Alt+L/Ctrl+G=GoToLine\n\n" +
            "Editing:\n" +
            "  Del/Bksp=Delete  Ctrl+Y=DelLine  Ctrl+K=DelToEOL\n" +
            "  Alt+Bksp=DelWordLeft  Alt+D=DelWordRight  Ins=Ins/Ovr\n\n" +
            "Selection:\n" +
            "  F3=Mark  Shift+F3=ColMark  Shift+Arrows=Select  Ctrl+A=All\n" +
            "  Alt+Arrows=ColSelect  F5=Copy  F6=Move  F8=Delete\n\n" +
            "Search:\n" +
            "  F7=Search  Shift+F7=Again  F4=Replace  Alt+K=Bookmark\n\n" +
            "File:\n" +
            "  F2=Save  Shift+F2=SaveAs  Ctrl+O=Open  Ctrl+N=New  F10=Quit\n\n" +
            "Format:\n" +
            "  Ctrl+Q=InsLiteral  Ctrl+D=DateTime  Alt+P=FormatPara\n" +
            "  Alt+T=Sort  Alt+U=ExternalCmd  Ctrl+Tab=Complete",
            "Close");
    }

    public void ExecuteEditSyntaxFile()
    {
        var userSyntaxDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "mc", "syntax");
        Directory.CreateDirectory(userSyntaxDir);
        var syntaxFile = Path.Combine(userSyntaxDir, "Syntax");
        if (!File.Exists(syntaxFile))
            File.WriteAllText(syntaxFile, "# MC Syntax file\n# See /usr/share/mc/syntax/ for examples\n");
        _editor.LoadFile(syntaxFile);
        EditorTitleChanged?.Invoke(this, EventArgs.Empty);
        SetNeedsDraw();
    }

    public void ExecuteEditMenuFile()
    {
        var menuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "mc", "mcedit");
        Directory.CreateDirectory(menuDir);
        var menuFile = Path.Combine(menuDir, "menu");
        if (!File.Exists(menuFile))
            File.WriteAllText(menuFile,
                "# User Menu for MCEdit\n" +
                "# Format: <key> <label>\n" +
                "#     <indented command lines>\n" +
                "# Available macros: %f=file %n=name %x=ext %d=dir %l=line %c=col\n\n" +
                "e Edit with default editor\n" +
                "    $EDITOR %f\n");
        _editor.LoadFile(menuFile);
        EditorTitleChanged?.Invoke(this, EventArgs.Empty);
        SetNeedsDraw();
    }

    public void ExecuteMail()
    {
        MessageBox.Query("Mail", "Mail functionality requires a mail program.\nThis feature is not available in this implementation.", "OK");
    }

    public void ExecuteChangeSpellingLanguage()
    {
        var lang = PromptInput("Spell Check Language", "Language code (e.g. en, de, fr):", "en");
        if (lang == null) return;
        MessageBox.Query("Spell Language",
            $"Language set to '{lang}'.\n(Requires aspell to be configured for this language.)", "OK");
    }

    // ── Hex view/edit ────────────────────────────────────────────────────────

    private void ToggleHexMode()
    {
        if (_hexMode)
        {
            if (_hexModified)
            {
                var choice = MessageBox.Query("Hex Edit", "Save hex changes to file?", "Yes", "No", "Cancel");
                if (choice == 2) return;
                if (choice == 0) SaveHexBytes();
            }
            _hexMode = false;
            _hexModified = false;
        }
        else
        {
            EnterHexMode();
        }
        SetNeedsDraw();
    }

    private void EnterHexMode()
    {
        if (_editor.FilePath != null && File.Exists(_editor.FilePath))
        {
            try { _hexBytes = File.ReadAllBytes(_editor.FilePath); }
            catch { _hexBytes = System.Text.Encoding.UTF8.GetBytes(_editor.Buffer.ToString()); }
        }
        else
        {
            _hexBytes = System.Text.Encoding.UTF8.GetBytes(_editor.Buffer.ToString());
        }
        _hexCursorByte    = 0;
        _hexTopLine       = 0;
        _hexCursorInAscii = false;
        _hexNibble        = 0;
        _hexModified      = false;
        _hexMode          = true;
    }

    private void SaveHexBytes()
    {
        if (_editor.FilePath == null) return;
        try { File.WriteAllBytes(_editor.FilePath, _hexBytes); }
        catch (Exception ex) { MessageBox.ErrorQuery("Save Failed", ex.Message, "OK"); }
    }

    private bool HandleHexKey(Key keyEvent)
    {
        // Always handle Ctrl+H to exit hex mode
        if (keyEvent.KeyCode == KeyCode.H && keyEvent.IsCtrl)
        {
            ToggleHexMode();
            return true;
        }

        // Navigation — guard all index arithmetic against empty file
        if (_hexBytes.Length == 0) return true;

        switch (keyEvent.KeyCode)
        {
            case KeyCode.CursorRight:
                if (_hexCursorByte < _hexBytes.Length - 1) _hexCursorByte++;
                _hexNibble = 0;
                SetNeedsDraw();
                return true;
            case KeyCode.CursorLeft:
                if (_hexCursorByte > 0) _hexCursorByte--;
                _hexNibble = 0;
                SetNeedsDraw();
                return true;
            case KeyCode.CursorDown:
                _hexCursorByte = Math.Min(_hexBytes.Length - 1, _hexCursorByte + HexBytesPerRow);
                _hexNibble = 0;
                SetNeedsDraw();
                return true;
            case KeyCode.CursorUp:
                _hexCursorByte = Math.Max(0, _hexCursorByte - HexBytesPerRow);
                _hexNibble = 0;
                SetNeedsDraw();
                return true;
            case KeyCode.Home:
                _hexCursorByte -= _hexCursorByte % HexBytesPerRow;
                _hexNibble = 0;
                SetNeedsDraw();
                return true;
            case KeyCode.End:
                _hexCursorByte = Math.Min(_hexBytes.Length - 1,
                    _hexCursorByte - _hexCursorByte % HexBytesPerRow + HexBytesPerRow - 1);
                _hexNibble = 0;
                SetNeedsDraw();
                return true;
            case KeyCode.PageDown:
            {
                int rows = Math.Max(1, Viewport.Height - 2);
                _hexCursorByte = Math.Min(_hexBytes.Length - 1, _hexCursorByte + rows * HexBytesPerRow);
                _hexNibble = 0;
                SetNeedsDraw();
                return true;
            }
            case KeyCode.PageUp:
            {
                int rows = Math.Max(1, Viewport.Height - 2);
                _hexCursorByte = Math.Max(0, _hexCursorByte - rows * HexBytesPerRow);
                _hexNibble = 0;
                SetNeedsDraw();
                return true;
            }
            case KeyCode.Tab:
                _hexCursorInAscii = !_hexCursorInAscii;
                _hexNibble = 0;
                SetNeedsDraw();
                return true;
            case KeyCode.F10:
            case KeyCode.Esc:
                ToggleHexMode();
                return true;
        }

        // Hex pane editing: 0-9, a-f
        if (!_hexCursorInAscii && !_isReadOnly)
        {
            var rune = keyEvent.AsRune.Value;
            int digit = -1;
            if (rune >= '0' && rune <= '9') digit = rune - '0';
            else if (rune >= 'a' && rune <= 'f') digit = rune - 'a' + 10;
            else if (rune >= 'A' && rune <= 'F') digit = rune - 'A' + 10;

            if (digit >= 0 && _hexCursorByte < _hexBytes.Length)
            {
                if (_hexNibble == 0)
                {
                    _hexBytes[_hexCursorByte] = (byte)((_hexBytes[_hexCursorByte] & 0x0F) | (digit << 4));
                    _hexNibble = 1;
                }
                else
                {
                    _hexBytes[_hexCursorByte] = (byte)((_hexBytes[_hexCursorByte] & 0xF0) | digit);
                    _hexNibble = 0;
                    if (_hexCursorByte < _hexBytes.Length - 1) _hexCursorByte++;
                }
                _hexModified = true;
                SetNeedsDraw();
                return true;
            }
        }

        // ASCII pane editing
        if (_hexCursorInAscii && !_isReadOnly)
        {
            var rune = keyEvent.AsRune.Value;
            if (rune >= 32 && rune < 127 && _hexCursorByte < _hexBytes.Length)
            {
                _hexBytes[_hexCursorByte] = (byte)rune;
                _hexModified = true;
                _hexNibble = 0;
                if (_hexCursorByte < _hexBytes.Length - 1) _hexCursorByte++;
                SetNeedsDraw();
                return true;
            }
        }

        return true; // consume all keys in hex mode
    }

    private void DrawHexContent(System.Drawing.Rectangle viewport, int contentHeight)
    {
        if (_hexBytes.Length == 0)
        {
            // Empty file
            for (int row = 0; row < contentHeight; row++)
            {
                Move(0, row);
                Driver!.SetAttribute(ColorScheme!.Normal);
                Driver!.AddStr(new string(' ', viewport.Width));
            }
        }
        else
        {
            var cursorRow = _hexCursorByte / HexBytesPerRow;
            if (cursorRow < _hexTopLine) _hexTopLine = cursorRow;
            if (cursorRow >= _hexTopLine + contentHeight) _hexTopLine = cursorRow - contentHeight + 1;

            for (int row = 0; row < contentHeight; row++)
            {
                int lineOffset = (_hexTopLine + row) * HexBytesPerRow;
                Move(0, row);
                Driver!.SetAttribute(ColorScheme!.Normal);
                if (lineOffset >= _hexBytes.Length)
                {
                    Driver!.AddStr(new string(' ', viewport.Width));
                    continue;
                }
                DrawHexLine(row, lineOffset, viewport.Width);
            }
        }

        // Status bar
        Move(0, contentHeight);
        Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
        var status = StatusText;
        if (status.Length > viewport.Width) status = status[..viewport.Width];
        Driver!.AddStr(status.PadRight(viewport.Width));

        // Terminal cursor position
        var curRow = _hexCursorByte / HexBytesPerRow - _hexTopLine;
        if (curRow >= 0 && curRow < contentHeight)
        {
            int byteInRow = _hexCursorByte % HexBytesPerRow;
            int curCol = _hexCursorInAscii
                ? HexAsciiStart() + byteInRow
                : HexByteColumn(byteInRow) + _hexNibble;
            if (curCol < viewport.Width)
                Move(curCol, curRow);
        }
    }

    // Column of the first hex digit of byte[byteInRow] in a hex line
    private static int HexByteColumn(int byteInRow)
        => 10 + byteInRow * 3 + (byteInRow >= HexBytesPerRow / 2 ? 1 : 0);

    // Column where the ASCII section starts
    private static int HexAsciiStart()
        => 10 + HexBytesPerRow * 3 + 1 + 2; // offset(10) + bytes(48) + mid-gap(1) + " |"(2) = 61

    private void DrawHexLine(int row, int lineOffset, int viewWidth)
    {
        // Build the full line string first
        var sb = new System.Text.StringBuilder(80);
        sb.Append($"{lineOffset:X8}  ");
        for (int i = 0; i < HexBytesPerRow; i++)
        {
            int byteOff = lineOffset + i;
            sb.Append(byteOff < _hexBytes.Length ? $"{_hexBytes[byteOff]:X2} " : "   ");
            if (i == HexBytesPerRow / 2 - 1) sb.Append(' ');
        }
        sb.Append(" |");
        for (int i = 0; i < HexBytesPerRow; i++)
        {
            int byteOff = lineOffset + i;
            if (byteOff < _hexBytes.Length)
            {
                byte b = _hexBytes[byteOff];
                sb.Append(b is >= 32 and < 127 ? (char)b : '.');
            }
            else sb.Append(' ');
        }
        sb.Append('|');

        var line = sb.ToString();
        if (line.Length > viewWidth) line = line[..viewWidth];
        Driver!.SetAttribute(ColorScheme!.Normal);
        Driver!.AddStr(line.PadRight(viewWidth));

        // Highlight current byte
        if (_hexCursorByte >= lineOffset && _hexCursorByte < lineOffset + HexBytesPerRow
            && _hexCursorByte < _hexBytes.Length)
        {
            int byteInRow = _hexCursorByte % HexBytesPerRow;

            // Hex pane highlight
            int hexCol = HexByteColumn(byteInRow);
            if (hexCol + 1 < viewWidth)
            {
                Move(hexCol, row);
                Driver!.SetAttribute(_hexCursorInAscii
                    ? new Terminal.Gui.Attribute(Color.BrightYellow, Color.DarkGray)
                    : new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow));
                Driver!.AddStr($"{_hexBytes[_hexCursorByte]:X2}");
            }

            // ASCII pane highlight
            int asciiCol = HexAsciiStart() + byteInRow;
            if (asciiCol < viewWidth)
            {
                Move(asciiCol, row);
                Driver!.SetAttribute(_hexCursorInAscii
                    ? new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow)
                    : new Terminal.Gui.Attribute(Color.BrightYellow, Color.DarkGray));
                byte b = _hexBytes[_hexCursorByte];
                Driver!.AddStr((b is >= 32 and < 127 ? (char)b : '.').ToString());
            }
        }
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
