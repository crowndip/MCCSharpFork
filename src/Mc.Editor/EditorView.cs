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

    // Block-selection state (#3)
    private bool _selecting;
    private int  _selectionAnchor = -1;

    // Syntax-highlighting toggle (Ctrl+T). (#51)
    private bool _syntaxHighlightingOn = true;

    // Line-number gutter width (#23)
    private int GutterWidth => _showLineNumbers ? _editor.Buffer.GetLineCount().ToString().Length + 1 : 0;

    private bool _showLineNumbers;  // #23

    public event EventHandler? RequestClose;

    public EditorView(string? filePath = null)
    {
        _editor = new EditorController(filePath);
        _editor.Changed += (_, _) => SetNeedsDraw();
        CanFocus = true;
        ColorScheme = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.White,       Color.Black),
            Focus     = new Terminal.Gui.Attribute(Color.White,       Color.Black),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow,Color.Black),
            HotFocus  = new Terminal.Gui.Attribute(Color.BrightYellow,Color.Black),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray,        Color.Black),
        };
    }

    public string Title => _editor.FilePath != null
        ? $"Edit: {Path.GetFileName(_editor.FilePath)}{(_editor.IsModified ? " *" : string.Empty)}"
        : "Edit: [new file]";

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        var viewport = Viewport;
        var contentHeight = viewport.Height - 1; // leave 1 line for status
        var (cursorLine, cursorCol) = _editor.CursorPosition;
        var gutter = GutterWidth;

        // Scroll viewport to keep cursor visible
        if (cursorLine < _topLine) _topLine = cursorLine;
        if (cursorLine >= _topLine + contentHeight) _topLine = cursorLine - contentHeight + 1;
        var textWidth = viewport.Width - gutter;
        if (cursorCol < _leftCol) _leftCol = cursorCol;
        if (cursorCol >= _leftCol + textWidth) _leftCol = cursorCol - textWidth + 1;

        for (int row = 0; row < contentHeight; row++)
        {
            int lineNo = _topLine + row;
            Move(0, row);

            // Line-number gutter (#23)
            if (gutter > 0)
            {
                Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Gray, Color.Black));
                if (lineNo < _editor.Buffer.GetLineCount())
                    Driver.AddStr((lineNo + 1).ToString().PadLeft(gutter - 1) + " ");
                else
                    Driver.AddStr(new string(' ', gutter));
            }

            if (lineNo >= _editor.Buffer.GetLineCount())
            {
                Driver.SetAttribute(ColorScheme.Normal);
                Driver.AddStr(new string(' ', textWidth));
                continue;
            }

            var line = _editor.Buffer.GetLine(lineNo);
            var lineOffset = _editor.Buffer.LineColToOffset(lineNo, 0);

            if (_syntaxHighlightingOn && _editor.Highlighter != null)
            {
                var tokens = _editor.Highlighter.Tokenize(line);
                DrawLineWithSyntaxAndSelection(row, line, tokens, _leftCol, textWidth, lineOffset);
            }
            else
            {
                DrawLineWithSelection(row, line, _leftCol, textWidth, lineOffset);
            }
        }

        // Status bar
        Move(0, contentHeight);
        Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
        var (ln, col) = _editor.CursorPosition;
        var mode = _insertMode ? "INS" : "OVR";
        var status = $" {_editor.FilePath ?? "new"} | Ln {ln + 1}, Col {col + 1} | {mode} | {(_editor.IsModified ? "Modified" : "Saved")}";
        if (_showLineNumbers) status += " | Nums";
        if (!_syntaxHighlightingOn) status += " | NoHL";
        if (status.Length > viewport.Width) status = status[..viewport.Width];
        Driver.AddStr(status.PadRight(viewport.Width));

        // Position the terminal cursor
        var screenLine = cursorLine - _topLine;
        var screenCol = gutter + cursorCol - _leftCol;
        if (screenLine >= 0 && screenLine < contentHeight &&
            screenCol >= gutter && screenCol < viewport.Width)
        {
            Move(screenCol, screenLine);
        }
        return false;
    }

    private void DrawLineWithSelection(int row, string line, int leftCol, int width, int lineStartOffset)
    {
        var (selStart, selEnd) = _editor.GetSelectionOffsets();
        var pos = leftCol;
        Move(GutterWidth, row);
        for (int i = 0; i < width; i++, pos++)
        {
            int charOffset = lineStartOffset + pos;
            char ch = pos < line.Length ? line[pos] : ' ';
            bool inSel = selStart >= 0 && charOffset >= selStart && charOffset < selEnd;
            Driver.SetAttribute(inSel
                ? new Terminal.Gui.Attribute(Color.Black, Color.Cyan)
                : ColorScheme.Normal);
            Driver.AddStr(ch.ToString());
        }
    }

    private void DrawLineWithSyntaxAndSelection(int row, string line, IReadOnlyList<SyntaxToken> tokens, int leftCol, int width, int lineStartOffset)
    {
        var (selStart, selEnd) = _editor.GetSelectionOffsets();
        var gutter = GutterWidth;
        Move(gutter, row);
        for (int i = 0; i < width; i++)
        {
            int pos = leftCol + i;
            int charOffset = lineStartOffset + pos;
            char ch = pos < line.Length ? line[pos] : ' ';
            bool inSel = selStart >= 0 && charOffset >= selStart && charOffset < selEnd;
            if (inSel)
            {
                Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
            }
            else
            {
                var tok = FindToken(tokens, pos);
                Driver.SetAttribute(tok != null ? GetTokenColor(tok.Type) : ColorScheme.Normal);
            }
            Driver.AddStr(ch.ToString());
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
        TokenType.Keyword      => new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
        TokenType.Comment      => new Terminal.Gui.Attribute(Color.Gray,         Color.Black),
        TokenType.String       => new Terminal.Gui.Attribute(Color.BrightCyan,   Color.Black),
        TokenType.Number       => new Terminal.Gui.Attribute(Color.BrightMagenta,Color.Black),
        TokenType.Preprocessor => new Terminal.Gui.Attribute(Color.BrightGreen,  Color.Black),
        TokenType.Type         => new Terminal.Gui.Attribute(Color.BrightGreen,  Color.Black),
        _                      => new Terminal.Gui.Attribute(Color.White,        Color.Black),
    };

    protected override bool OnKeyDown(Key keyEvent)
    {
        // Shift+Arrow: extend block selection (#3)
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
            }
            MoveWithShift(keyEvent);
            _editor.ExtendSelection();
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
            case KeyCode.F2:  SaveFile(); return true;
            case KeyCode.F10: OnRequestClose(); return true;
            case KeyCode.F7:  ShowFind(); return true;
            case KeyCode.F7 | KeyCode.ShiftMask: FindAgain(); return true;   // (#17)
            case KeyCode.F9:  ShowEditorMenu(); return true;                 // (#18)
            case KeyCode.Esc: OnRequestClose(); return true;

            // F4 = Find+Replace (#1)
            case KeyCode.F4: ShowFindReplace(); return true;

            // F3 = start/extend block mark (#3)
            case KeyCode.F3:
                if (!_selecting)
                {
                    _selecting = true;
                    _editor.StartSelection();
                }
                else
                {
                    _selecting = false;
                    _editor.ExtendSelection();
                }
                SetNeedsDraw();
                return true;

            // F5 = copy block (#3)
            case KeyCode.F5:
                _clipboardText = _editor.Copy();
                _selecting = false;
                _editor.ClearSelection();
                SetNeedsDraw();
                return true;

            // F6 = move block (#3)
            case KeyCode.F6:
                _clipboardText = _editor.Copy();
                _editor.Cut();
                _selecting = false;
                _editor.ClearSelection();
                SetNeedsDraw();
                return true;

            // F8 = delete line (#4)
            case KeyCode.F8:
                _editor.DeleteLine();
                return true;

            // F5 when no block = go-to-line (#14)  (overridden above when selecting)
            // Ctrl+G = go-to-line (#14)
            case KeyCode.G when keyEvent.IsCtrl:
                ShowGotoLine();
                return true;

            // Shift+F2 = save as (#48)
            case KeyCode.F2 | KeyCode.ShiftMask:
                SaveAs();
                return true;

            case KeyCode.CursorUp:   _editor.MoveUp(); return true;
            case KeyCode.CursorDown: _editor.MoveDown(); return true;
            case KeyCode.CursorLeft  when !keyEvent.IsCtrl: _editor.MoveLeft(); return true;
            case KeyCode.CursorRight when !keyEvent.IsCtrl: _editor.MoveRight(); return true;
            case KeyCode.Home when !keyEvent.IsCtrl: _editor.MoveToLineStart(); return true;
            case KeyCode.End  when !keyEvent.IsCtrl: _editor.MoveToLineEnd(); return true;
            case KeyCode.PageUp:   _editor.PageUp(Viewport.Height - 2); return true;
            case KeyCode.PageDown: _editor.PageDown(Viewport.Height - 2); return true;
            case KeyCode.Home when keyEvent.IsCtrl: _editor.MoveToStart(); return true;
            case KeyCode.End  when keyEvent.IsCtrl: _editor.MoveToEnd(); return true;
            case KeyCode.CursorRight when keyEvent.IsCtrl: _editor.MoveWordRight(); return true;
            case KeyCode.CursorLeft  when keyEvent.IsCtrl: _editor.MoveWordLeft(); return true;

            case KeyCode.Backspace: _editor.Backspace(); return true;

            // Shift+Delete = cut (#36)
            case KeyCode.Delete | KeyCode.ShiftMask:
                _clipboardText = _editor.Copy();
                _editor.Cut();
                _selecting = false;
                _editor.ClearSelection();
                return true;

            case KeyCode.Delete:    _editor.DeleteForward(); return true;
            case KeyCode.Y when keyEvent.IsCtrl: _editor.DeleteLine(); return true;

            // Ctrl+Insert = copy, Shift+Insert = paste (#36)
            case KeyCode.Insert | KeyCode.CtrlMask:
                _clipboardText = _editor.Copy();
                _selecting = false;
                _editor.ClearSelection();
                return true;
            case KeyCode.Insert | KeyCode.ShiftMask:
                if (_clipboardText != null) _editor.Paste(_clipboardText);
                return true;

            case KeyCode.Insert:
                _insertMode = !_insertMode;
                SetNeedsDraw();
                return true;

            // Undo Ctrl+Z / Ctrl+U (#33), Redo Ctrl+R (#2)
            case KeyCode.Z when keyEvent.IsCtrl: _editor.Undo(); return true;
            case KeyCode.U when keyEvent.IsCtrl: _editor.Undo(); return true;  // (#33)
            case KeyCode.R when keyEvent.IsCtrl: _editor.Redo(); return true;

            // Alt+L = go to line (#37)
            case KeyCode.L | KeyCode.AltMask: ShowGotoLine(); return true;

            // Ctrl+D = insert date/time (#50)
            case KeyCode.D when keyEvent.IsCtrl: InsertDateTime(); return true;

            // Ctrl+T = toggle syntax highlighting (#51)
            case KeyCode.T when keyEvent.IsCtrl:
                _syntaxHighlightingOn = !_syntaxHighlightingOn;
                SetNeedsDraw();
                return true;

            case KeyCode.C when keyEvent.IsCtrl:
                _clipboardText = _editor.Copy();
                _selecting = false;
                _editor.ClearSelection();
                return true;
            case KeyCode.X when keyEvent.IsCtrl:
                _clipboardText = _editor.Copy();
                _editor.Cut();
                _selecting = false;
                _editor.ClearSelection();
                return true;
            case KeyCode.V when keyEvent.IsCtrl:
                if (_clipboardText != null) _editor.Paste(_clipboardText);
                return true;
            case KeyCode.A when keyEvent.IsCtrl:
                _editor.SelectAll();
                _selecting = true;
                SetNeedsDraw();
                return true;

            default:
                var rune = keyEvent.AsRune;
                if (rune.Value >= 32)
                {
                    if (_insertMode)
                        _editor.InsertChar((char)rune.Value);
                    else
                        _editor.ReplaceChar((char)rune.Value); // #22 overwrite mode
                    return true;
                }
                if (keyEvent == Key.Enter)
                {
                    _editor.InsertNewlineWithIndent(); // #24 auto-indent
                    return true;
                }
                // Shift+Enter = newline without auto-indent (#53)
                if (keyEvent.KeyCode == (KeyCode.Enter | KeyCode.ShiftMask))
                {
                    _editor.InsertChar('\n');
                    return true;
                }
                if (keyEvent == Key.Tab)
                {
                    _editor.InsertTab(); // #42 tab handling
                    return true;
                }
                return base.OnKeyDown(keyEvent);
        }
    }

    private void MoveWithShift(Key key)
    {
        switch (key.KeyCode)
        {
            case KeyCode.CursorUp:    _editor.MoveUp(); break;
            case KeyCode.CursorDown:  _editor.MoveDown(); break;
            case KeyCode.CursorLeft:  _editor.MoveLeft(); break;
            case KeyCode.CursorRight: _editor.MoveRight(); break;
            case KeyCode.Home:        _editor.MoveToLineStart(); break;
            case KeyCode.End:         _editor.MoveToLineEnd(); break;
        }
    }

    private void SaveFile()
    {
        try
        {
            if (_editor.FilePath == null) { SaveAs(); return; }
            _editor.Save();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Save Failed", ex.Message, "OK");
        }
    }

    private void SaveAs()
    {
        var path = PromptInput("Save As", "File name:", _editor.FilePath ?? string.Empty);
        if (path == null) return;
        try { _editor.SaveAs(path); }
        catch (Exception ex) { MessageBox.ErrorQuery("Save Failed", ex.Message, "OK"); }
    }

    /// <summary>Find dialog with case-sensitive and regex options (F7). (#16, #63)</summary>
    private void ShowFind()
    {
        string? pattern = null;
        bool caseSensitive = _editor.LastSearch.CaseSensitive;
        bool useRegex      = _editor.LastSearch.Type == SearchType.Regex;

        var d = new Dialog { Title = "Search", Width = 60, Height = 11 };
        d.Add(new Label { X = 1, Y = 1, Text = "Search for:" });
        var tf = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Text = _editor.LastSearch.Pattern };
        d.Add(tf);
        var caseCb  = new CheckBox { X = 1, Y = 4, Text = "Case sensitive",
            CheckedState = caseSensitive ? CheckState.Checked : CheckState.UnChecked };
        var regexCb = new CheckBox { X = 1, Y = 5, Text = "Regular expression",
            CheckedState = useRegex     ? CheckState.Checked : CheckState.UnChecked };
        d.Add(caseCb, regexCb);
        var ok     = new Button { X = Pos.Center() - 5, Y = 8, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { pattern = tf.Text?.ToString(); Application.RequestStop(d); };
        var cancel = new Button { X = Pos.Center() + 3, Y = 8, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (string.IsNullOrEmpty(pattern)) return;
        var opts = new SearchOptions
        {
            Pattern       = pattern,
            CaseSensitive = caseCb.CheckedState  == CheckState.Checked,
            Type          = regexCb.CheckedState == CheckState.Checked ? SearchType.Regex : SearchType.Normal,
        };
        var result = _editor.FindNext(opts);
        if (!result.Found)
            MessageBox.Query("Find", "Pattern not found", "OK");
        else
            SetNeedsDraw();
    }

    /// <summary>Repeat the last search without showing a dialog (Shift+F7). (#17)</summary>
    private void FindAgain()
    {
        if (string.IsNullOrEmpty(_editor.LastSearch.Pattern)) { ShowFind(); return; }
        var result = _editor.FindNext(_editor.LastSearch);
        if (!result.Found)
            MessageBox.Query("Find", "Pattern not found", "OK");
        else
            SetNeedsDraw();
    }

    /// <summary>Show a simple editor actions menu (F9). (#18)</summary>
    private void ShowEditorMenu()
    {
        var items = new[]
        {
            "Save (F2)", "Save As (Shift+F2)", "Find (F7)", "Find+Replace (F4)",
            "Go to line (Ctrl+G)", "Toggle line numbers", "Toggle syntax highlighting", "Close (F10)",
        };
        var choice = MessageBox.Query("Editor", "Select action:", items);
        switch (choice)
        {
            case 0: SaveFile(); break;
            case 1: SaveAs(); break;
            case 2: ShowFind(); break;
            case 3: ShowFindReplace(); break;
            case 4: ShowGotoLine(); break;
            case 5: _showLineNumbers = !_showLineNumbers; SetNeedsDraw(); break;
            case 6: _syntaxHighlightingOn = !_syntaxHighlightingOn; SetNeedsDraw(); break;
            case 7: OnRequestClose(); break;
        }
    }

    /// <summary>Inserts the current date+time at the cursor (Ctrl+D). (#50)</summary>
    private void InsertDateTime() =>
        _editor.InsertText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

    /// <summary>Find+Replace dialog. (#1)</summary>
    private void ShowFindReplace()
    {
        string? findPat = null, replPat = null;
        bool replaceAll = false;

        var d = new Dialog { Title = "Find and Replace", Width = 60, Height = 14 };
        d.Add(new Label { X = 1, Y = 1, Text = "Search for:" });
        var tfFind = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Text = _editor.LastSearch.Pattern };
        d.Add(tfFind);
        d.Add(new Label { X = 1, Y = 4, Text = "Replace with:" });
        var tfRepl = new TextField { X = 1, Y = 5, Width = Dim.Fill(1) };
        d.Add(tfRepl);
        var caseCb = new CheckBox { X = 1, Y = 7, Text = "Case sensitive" };
        var regexCb = new CheckBox { X = 1, Y = 8, Text = "Regular expression" };
        d.Add(caseCb, regexCb);

        var btnFind = new Button { X = Pos.Center() - 22, Y = 10, Text = "Find next" };
        btnFind.Accepting += (_, _) =>
        {
            findPat = tfFind.Text?.ToString();
            replPat = tfRepl.Text?.ToString();
            Application.RequestStop(d);
        };
        var btnAll = new Button { X = Pos.Center() - 8, Y = 10, Text = "Replace all" };
        btnAll.Accepting += (_, _) =>
        {
            findPat = tfFind.Text?.ToString();
            replPat = tfRepl.Text?.ToString();
            replaceAll = true;
            Application.RequestStop(d);
        };
        var btnCancel = new Button { X = Pos.Center() + 8, Y = 10, Text = "Cancel", IsDefault = true };
        btnCancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(btnFind); d.AddButton(btnAll); d.AddButton(btnCancel);
        Application.Run(d); d.Dispose();

        if (string.IsNullOrEmpty(findPat)) return;
        var opts = new SearchOptions
        {
            Pattern = findPat,
            Replacement = replPat ?? string.Empty,
            CaseSensitive = caseCb.CheckedState == CheckState.Checked,
            Type = regexCb.CheckedState == CheckState.Checked ? SearchType.Regex : SearchType.Normal,
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

    /// <summary>Go-to-line dialog (Ctrl+G). (#14)</summary>
    private void ShowGotoLine()
    {
        var input = PromptInput("Go to line", $"Line number (1-{_editor.Buffer.GetLineCount()}):", string.Empty);
        if (int.TryParse(input, out var line) && line >= 1)
            _editor.GotoLine(line);
        SetNeedsDraw();
    }

    private void OnRequestClose()
    {
        if (_editor.IsModified)
        {
            if (MessageBox.Query("Unsaved Changes", "File has unsaved changes. Discard?", "Discard", "Cancel") != 0)
                return;
        }
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private static string? PromptInput(string title, string prompt, string defaultValue)
    {
        string? result = null;
        var d = new Dialog { Title = title, Width = 60, Height = 8 };
        d.Add(new Label { X = 1, Y = 1, Text = prompt });
        var tf = new TextField { X = 1, Y = 3, Width = Dim.Fill(1), Text = defaultValue };
        d.Add(tf);
        var ok = new Button { X = Pos.Center() - 5, Y = 5, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { result = tf.Text?.ToString(); Application.RequestStop(d); };
        var cancel = new Button { X = Pos.Center() + 3, Y = 5, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok);
        d.AddButton(cancel);
        Application.Run(d);
        d.Dispose();
        return result;
    }
}
