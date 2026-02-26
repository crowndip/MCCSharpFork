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

        // Scroll viewport to keep cursor visible
        if (cursorLine < _topLine) _topLine = cursorLine;
        if (cursorLine >= _topLine + contentHeight) _topLine = cursorLine - contentHeight + 1;
        if (cursorCol < _leftCol) _leftCol = cursorCol;
        if (cursorCol >= _leftCol + viewport.Width) _leftCol = cursorCol - viewport.Width + 1;

        // Draw lines
        for (int row = 0; row < contentHeight; row++)
        {
            int lineNo = _topLine + row;
            Move(0, row);

            if (lineNo >= _editor.Buffer.GetLineCount())
            {
                Driver.SetAttribute(ColorScheme.Normal);
                Driver.AddStr(new string(' ', viewport.Width));
                continue;
            }

            var line = _editor.Buffer.GetLine(lineNo);
            var visibleLine = _leftCol < line.Length ? line[_leftCol..] : string.Empty;
            if (visibleLine.Length > viewport.Width)
                visibleLine = visibleLine[..viewport.Width];

            if (_editor.Highlighter != null)
            {
                var tokens = _editor.Highlighter.Tokenize(line);
                DrawLineWithSyntax(row, visibleLine, tokens, _leftCol, viewport.Width);
            }
            else
            {
                Driver.SetAttribute(ColorScheme.Normal);
                Driver.AddStr(visibleLine.PadRight(viewport.Width));
            }
        }

        // Status bar
        Move(0, contentHeight);
        Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
        var (ln, col) = _editor.CursorPosition;
        var status = $" {_editor.FilePath ?? "new"} | Ln {ln + 1}, Col {col + 1} | {(_insertMode ? "INS" : "OVR")} | {(_editor.IsModified ? "Modified" : "Saved")}";
        if (status.Length > viewport.Width) status = status[..viewport.Width];
        Driver.AddStr(status.PadRight(viewport.Width));

        // Position the cursor
        var screenLine = cursorLine - _topLine;
        var screenCol = cursorCol - _leftCol;
        if (screenLine >= 0 && screenLine < contentHeight &&
            screenCol >= 0 && screenCol < viewport.Width)
        {
            Move(screenCol, screenLine);
        }
        return false;
    }

    private void DrawLineWithSyntax(int row, string line, IReadOnlyList<SyntaxToken> tokens, int leftCol, int width)
    {
        var pos = 0;
        foreach (var token in tokens)
        {
            if (token.Start + token.Length <= leftCol) { pos = token.Start + token.Length; continue; }
            if (token.Start > pos)
            {
                var gap = line.Substring(Math.Max(0, pos - leftCol), Math.Min(token.Start - pos, line.Length - Math.Max(0, pos - leftCol)));
                Driver.SetAttribute(ColorScheme.Normal);
                Driver.AddStr(gap);
            }
            Driver.SetAttribute(GetTokenColor(token.Type));
            var start = Math.Max(0, token.Start - leftCol);
            var len = Math.Min(token.Length, line.Length - start);
            if (start < line.Length && len > 0)
                Driver.AddStr(line.Substring(start, len));
            pos = token.Start + token.Length;
        }
        if (pos - leftCol < width)
        {
            Driver.SetAttribute(ColorScheme.Normal);
            Driver.AddStr(new string(' ', Math.Max(0, width - (pos - leftCol))));
        }
    }

    private static Terminal.Gui.Attribute GetTokenColor(TokenType type) => type switch
    {
        TokenType.Keyword     => new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
        TokenType.Comment     => new Terminal.Gui.Attribute(Color.Gray,         Color.Black),
        TokenType.String      => new Terminal.Gui.Attribute(Color.BrightCyan,   Color.Black),
        TokenType.Number      => new Terminal.Gui.Attribute(Color.BrightMagenta,Color.Black),
        TokenType.Preprocessor=> new Terminal.Gui.Attribute(Color.BrightGreen,  Color.Black),
        TokenType.Type        => new Terminal.Gui.Attribute(Color.BrightGreen,  Color.Black),
        _                     => new Terminal.Gui.Attribute(Color.White,        Color.Black),
    };

    protected override bool OnKeyDown(Key keyEvent)
    {
        switch (keyEvent.KeyCode)
        {
            case KeyCode.F2:                                SaveFile(); return true;
            case KeyCode.F10:                               OnRequestClose(); return true;
            case KeyCode.F7:                                ShowFind(); return true;
            case KeyCode.Esc:                               OnRequestClose(); return true;

            case KeyCode.CursorUp:                          _editor.MoveUp(); return true;
            case KeyCode.CursorDown:                        _editor.MoveDown(); return true;
            case KeyCode.CursorLeft when !keyEvent.IsCtrl:  _editor.MoveLeft(); return true;
            case KeyCode.CursorRight when !keyEvent.IsCtrl: _editor.MoveRight(); return true;
            case KeyCode.Home when !keyEvent.IsCtrl:        _editor.MoveToLineStart(); return true;
            case KeyCode.End when !keyEvent.IsCtrl:         _editor.MoveToLineEnd(); return true;
            case KeyCode.PageUp:                            _editor.PageUp(Viewport.Height - 2); return true;
            case KeyCode.PageDown:                          _editor.PageDown(Viewport.Height - 2); return true;
            case KeyCode.Home when keyEvent.IsCtrl:         _editor.MoveToStart(); return true;
            case KeyCode.End when keyEvent.IsCtrl:          _editor.MoveToEnd(); return true;
            case KeyCode.CursorRight when keyEvent.IsCtrl:  _editor.MoveWordRight(); return true;
            case KeyCode.CursorLeft when keyEvent.IsCtrl:   _editor.MoveWordLeft(); return true;

            case KeyCode.Backspace:                         _editor.Backspace(); return true;
            case KeyCode.Delete:                            _editor.DeleteForward(); return true;
            case KeyCode.Y when keyEvent.IsCtrl:            _editor.DeleteLine(); return true;

            case KeyCode.Insert:                            _insertMode = !_insertMode; SetNeedsDraw(); return true;

            case KeyCode.Z when keyEvent.IsCtrl:            _editor.Undo(); return true;

            case KeyCode.C when keyEvent.IsCtrl:
                _clipboardText = _editor.Copy();
                return true;
            case KeyCode.X when keyEvent.IsCtrl:
                _clipboardText = _editor.Copy();
                _editor.Cut();
                return true;
            case KeyCode.V when keyEvent.IsCtrl:
                if (_clipboardText != null) _editor.Paste(_clipboardText);
                return true;
            case KeyCode.A when keyEvent.IsCtrl:
                _editor.SelectAll();
                return true;

            default:
                var rune = keyEvent.AsRune;
                if (rune.Value >= 32)
                {
                    _editor.InsertChar((char)rune.Value);
                    return true;
                }
                if (keyEvent == Key.Enter) { _editor.InsertChar('\n'); return true; }
                if (keyEvent == Key.Tab)   { _editor.InsertChar('\t'); return true; }
                return base.OnKeyDown(keyEvent);
        }
    }

    private void SaveFile()
    {
        try
        {
            if (_editor.FilePath == null)
            {
                var path = PromptInput("Save As", "File name:", string.Empty);
                if (path == null) return;
                _editor.SaveAs(path);
            }
            else
            {
                _editor.Save();
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Save Failed", ex.Message, "OK");
        }
    }

    private void ShowFind()
    {
        var pattern = PromptInput("Find", "Search for:", _editor.LastSearch.Pattern);
        if (pattern == null) return;
        var opts = new SearchOptions { Pattern = pattern, CaseSensitive = false };
        var result = _editor.FindNext(opts);
        if (!result.Found)
            MessageBox.Query("Find", "Pattern not found", "OK");
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
        var d = new Dialog { Title = title, Width = 50, Height = 8 };
        d.Add(new Label { X = 1, Y = 1, Text = prompt });
        var tf = new TextField { X = 1, Y = 3, Width = Dim.Fill(1), Text = defaultValue };
        d.Add(tf);
        var ok = new Button { X = Pos.Center() - 5, Y = 5, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { result = tf.Text; Application.RequestStop(d); };
        var cancel = new Button { X = Pos.Center() + 3, Y = 5, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok);
        d.AddButton(cancel);
        Application.Run(d);
        d.Dispose();
        return result;
    }
}
