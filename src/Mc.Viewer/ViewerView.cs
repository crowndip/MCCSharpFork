using Mc.Core.Search;
using Terminal.Gui;

namespace Mc.Viewer;

/// <summary>
/// Terminal.Gui view that hosts the file viewer.
/// Equivalent to src/viewer/mcviewer.c + display.c + hex.c.
/// </summary>
public sealed class ViewerView : View
{
    private readonly ViewerController _viewer;

    // Next/previous file cycling (Ctrl+F / Ctrl+B). (#5)
    private IReadOnlyList<string>? _fileList;
    private int _fileListIndex = -1;

    // Viewer state toggles
    private bool _showRuler;   // Alt+R (#18)
    private bool _nroffMode;   // F9 (#13)

    // 10 bookmarks accessed by digit prefix (#20)
    private readonly long[] _bookmarks = new long[10];
    private bool _digitPrefix;
    private int  _digitValue;

    public event EventHandler? RequestClose;

    public ViewerView(string filePath, IReadOnlyList<string>? fileList = null, int fileListIndex = -1)
    {
        _viewer = new ViewerController();
        _viewer.LoadFile(filePath);
        _viewer.Changed += (_, _) => SetNeedsDraw();
        _fileList      = fileList;
        _fileListIndex = fileListIndex;
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

    public string Title =>
        $"View: {(_viewer.FilePath != null ? Path.GetFileName(_viewer.FilePath) : "?")} ({_viewer.FileSize:N0} bytes) [{ModeLabel}]";

    private string ModeLabel => _viewer.Mode switch
    {
        ViewMode.Hex => "HEX",
        ViewMode.Raw => "RAW",
        _            => _nroffMode ? "NROFF" : "TEXT",
    };

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        var viewport = Viewport;
        int reservedRows = 1; // status bar
        if (_showRuler) reservedRows++; // ruler row
        int contentHeight = viewport.Height - reservedRows;

        if (_viewer.Mode == ViewMode.Hex)
            DrawHex(viewport, contentHeight);
        else
            DrawText(viewport, contentHeight);

        if (_showRuler) DrawRuler(viewport, contentHeight);
        DrawStatusBar(viewport);
        return false;
    }

    private void DrawText(System.Drawing.Rectangle viewport, int contentHeight)
    {
        var lines = _viewer.GetLines(_viewer.ScrollLine, contentHeight, viewport.Width);
        // Compute which (line, col) range corresponds to the last match for highlighting (#15)
        var matchLine = -1;
        var matchCol  = 0;
        var matchLen  = 0;
        if (_viewer.LastMatchOffset >= 0)
        {
            var offset = (int)_viewer.LastMatchOffset;
            matchLine = _viewer.OffsetToLine(offset, viewport.Width) - _viewer.ScrollLine;
            // column within that line
            var text = _viewer.GetText();
            if (offset < text.Length)
            {
                var lineStart = text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1;
                matchCol = offset - lineStart;
                matchLen = _viewer.LastMatchLength;
            }
        }

        for (int row = 0; row < contentHeight; row++)
        {
            Move(0, row);
            Driver.SetAttribute(ColorScheme.Normal);
            if (row < lines.Count)
            {
                var line = lines[row];
                if (_viewer.Mode == ViewMode.Raw)
                {
                    // Raw: show control chars as printable (#43)
                    var sb = new System.Text.StringBuilder(line.Length);
                    foreach (var c in line) sb.Append(c < 32 ? '.' : c);
                    line = sb.ToString();
                }
                else if (_nroffMode)
                {
                    line = StripNroff(line); // F9 nroff mode (#13)
                }
                if (line.Length > viewport.Width) line = line[..viewport.Width];

                if (row == matchLine && matchLen > 0)
                {
                    // Draw with highlight (#15)
                    DrawLineWithHighlight(line, matchCol, matchLen, viewport.Width);
                }
                else
                {
                    Driver.AddStr(line.PadRight(viewport.Width));
                }
            }
            else
            {
                Driver.AddStr("~".PadRight(viewport.Width));
            }
        }
    }

    private void DrawLineWithHighlight(string line, int matchCol, int matchLen, int width)
    {
        // Before match
        if (matchCol > 0)
        {
            var pre = line[..Math.Min(matchCol, line.Length)];
            if (pre.Length > width) pre = pre[..width];
            Driver.SetAttribute(ColorScheme.Normal);
            Driver.AddStr(pre);
        }
        // Match highlight
        if (matchCol < width && matchLen > 0)
        {
            var start = matchCol;
            var end   = Math.Min(matchCol + matchLen, line.Length);
            if (end > start)
            {
                var mid = line[start..end];
                if (start + mid.Length > width) mid = mid[..(width - start)];
                Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan));
                Driver.AddStr(mid);
            }
        }
        // After match
        var afterStart = matchCol + matchLen;
        if (afterStart < line.Length && afterStart < width)
        {
            var post = line[afterStart..Math.Min(line.Length, width)];
            Driver.SetAttribute(ColorScheme.Normal);
            Driver.AddStr(post);
        }
        // Pad remainder
        int drawn = Math.Min(line.Length, width);
        if (drawn < width)
        {
            Driver.SetAttribute(ColorScheme.Normal);
            Driver.AddStr(new string(' ', width - drawn));
        }
    }

    private void DrawHex(System.Drawing.Rectangle viewport, int contentHeight)
    {
        var lines = _viewer.GetHexLines(_viewer.ScrollLine, contentHeight);
        for (int row = 0; row < contentHeight; row++)
        {
            Move(0, row);
            if (row < lines.Count)
            {
                var line = lines[row];
                if (line.Length > viewport.Width) line = line[..viewport.Width];
                Driver.SetAttribute(ColorScheme.Normal);
                Driver.AddStr(line.PadRight(viewport.Width));
            }
            else
            {
                Driver.SetAttribute(ColorScheme.Normal);
                Driver.AddStr(new string(' ', viewport.Width));
            }
        }
    }

    private void DrawRuler(System.Drawing.Rectangle viewport, int contentRow)
    {
        Move(0, contentRow);
        Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Gray));
        var ruler = new System.Text.StringBuilder(viewport.Width);
        for (int col = 0; col < viewport.Width; col++)
        {
            int c1 = (col + 1) % 10;
            ruler.Append(c1 == 0 ? (char)('0' + ((col + 1) / 10) % 10) : (c1 == 5 ? '+' : '-'));
        }
        Driver.AddStr(ruler.ToString());
    }

    private void DrawStatusBar(System.Drawing.Rectangle viewport)
    {
        Move(0, viewport.Height - 1);
        Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
        int reservedRows = 1 + (_showRuler ? 1 : 0);
        var totalLines = _viewer.Mode == ViewMode.Hex
            ? _viewer.TotalHexLineCount()
            : _viewer.TotalLineCount(viewport.Width);
        var pct = totalLines > 0
            ? (int)((double)_viewer.ScrollLine / Math.Max(1, totalLines - (viewport.Height - reservedRows)) * 100)
            : 100;
        pct = Math.Clamp(pct, 0, 100);
        var status = $" {_viewer.FilePath ?? "?"} | {_viewer.FileSize:N0} bytes | {pct}% | {ModeLabel} | {(_viewer.WrapLines ? "Wrap" : "No wrap")} | F2=Wrap F4=Hex F5=Goto F7=Find F8=Raw F9=Nroff Alt+R=Ruler q=Quit";
        if (status.Length > viewport.Width) status = status[..viewport.Width];
        Driver.AddStr(status.PadRight(viewport.Width));
    }

    protected override bool OnKeyDown(Key keyEvent)
    {
        // Digit prefix for bookmark selection (0-9 then m=set, r=goto) (#20)
        if (_digitPrefix)
        {
            _digitPrefix = false;
            switch (keyEvent.KeyCode)
            {
                case KeyCode.M:
                    _bookmarks[_digitValue] = _viewer.ScrollLine;
                    return true;
                case KeyCode.R:
                    _viewer.ScrollLine = (int)Math.Max(0, _bookmarks[_digitValue]);
                    SetNeedsDraw(); return true;
            }
        }

        // Check for digit key to start bookmark prefix (0-9) (#20)
        var rune = keyEvent.AsRune.Value;
        if (rune >= '0' && rune <= '9' && !keyEvent.IsCtrl && !keyEvent.IsAlt)
        {
            _digitPrefix = true;
            _digitValue  = rune - '0';
            return true;
        }

        // "/" key for search (#12) — rune-based, not a named KeyCode
        if (keyEvent.AsRune.Value == '/')
        {
            ShowSearch(backward: false);
            return true;
        }

        switch (keyEvent.KeyCode)
        {
            case KeyCode.Q:
            case KeyCode.F10:
            case KeyCode.Esc:
            case KeyCode.F3:
                RequestClose?.Invoke(this, EventArgs.Empty); return true;

            case KeyCode.F4:
                _viewer.Mode = _viewer.Mode == ViewMode.Hex ? ViewMode.Text : ViewMode.Hex;
                SetNeedsDraw(); return true;

            case KeyCode.F8:  // Raw mode toggle (#43)
                _viewer.Mode = _viewer.Mode == ViewMode.Raw ? ViewMode.Text : ViewMode.Raw;
                SetNeedsDraw(); return true;

            case KeyCode.F9:  // Nroff toggle (#13)
                _nroffMode = !_nroffMode;
                SetNeedsDraw(); return true;

            case KeyCode.F2:
                _viewer.WrapLines = !_viewer.WrapLines; SetNeedsDraw(); return true;

            case KeyCode.F7:
                ShowSearch(backward: false); return true;

            case KeyCode.F5:  // Go to line (text) / offset (hex) (#9)
                ShowGotoPosition(); return true;

            case KeyCode.F1:  // Viewer-specific help (#21)
                ShowViewerHelp(); return true;

            // n = repeat find next, N = find previous (#17)
            case KeyCode.N when !keyEvent.IsShift:
                if (!string.IsNullOrEmpty(_viewer.LastSearch.Pattern))
                { _viewer.FindNext(_viewer.LastSearch); SetNeedsDraw(); }
                return true;

            case KeyCode.N when keyEvent.IsShift:  // #17
                if (!string.IsNullOrEmpty(_viewer.LastSearch.Pattern))
                { _viewer.FindPrev(_viewer.LastSearch); SetNeedsDraw(); }
                return true;

            case KeyCode.CursorDown:
            case KeyCode.J:
                _viewer.ScrollDown(); return true;

            case KeyCode.CursorUp:
            case KeyCode.K:
                _viewer.ScrollUp(); return true;

            case KeyCode.PageDown:
            case KeyCode.Space:
                _viewer.ScrollDown(Viewport.Height - 2); return true;

            case KeyCode.PageUp:
                _viewer.ScrollUp(Viewport.Height - 2); return true;

            // Ctrl+F = next file, Ctrl+B = prev file (#5)
            case KeyCode.F when keyEvent.IsCtrl:
                NavigateFile(+1); return true;

            // Bookmarks (#20): Ctrl+B (no digit prefix) = set bookmark 0
            case KeyCode.B when keyEvent.IsCtrl:
                _bookmarks[0] = _viewer.ScrollLine; return true;

            case KeyCode.B:
                _viewer.ScrollUp(Viewport.Height - 2); return true;

            // g / Home = go to start, G / End = go to end (#44)
            case KeyCode.Home:
                _viewer.GoToStart(); return true;

            case KeyCode.End:
                _viewer.GoToEnd(Viewport.Height - 2, Viewport.Width); return true;

            case KeyCode.G:
                if (!keyEvent.IsShift) _viewer.GoToStart();
                else                   _viewer.GoToEnd(Viewport.Height - 2, Viewport.Width);
                return true;

            case KeyCode.CursorRight:
                _viewer.ScrollRight(); return true;

            case KeyCode.CursorLeft:
                _viewer.ScrollLeft(); return true;

            case KeyCode.P when keyEvent.IsCtrl:
                _viewer.GotoBookmark(0, Viewport.Height - 2, Viewport.Width);
                SetNeedsDraw();
                return true;

            // Alt+R = toggle ruler (#18)
            case KeyCode.R | KeyCode.AltMask:
                _showRuler = !_showRuler;
                SetNeedsDraw(); return true;

            // Alt+E = change encoding (#19)
            case KeyCode.E | KeyCode.AltMask:
                ShowEncodingDialog(); return true;

            default:
                return base.OnKeyDown(keyEvent);
        }
    }

    private void NavigateFile(int delta)
    {
        if (_fileList == null || _fileList.Count == 0 || _fileListIndex < 0) return;
        var newIndex = _fileListIndex + delta;
        if (newIndex < 0 || newIndex >= _fileList.Count) return;
        _fileListIndex = newIndex;
        _viewer.LoadFile(_fileList[_fileListIndex]);
        SetNeedsDraw();
    }

    private void ShowSearch(bool backward)
    {
        string? pattern = null;
        bool caseSensitive = _viewer.LastSearch.CaseSensitive;
        bool useRegex      = _viewer.LastSearch.Type == SearchType.Regex;

        var d = new Dialog { Title = backward ? "Search backward" : "Search forward", Width = 60, Height = 10 };
        d.Add(new Label { X = 1, Y = 1, Text = "Search for:" });
        var tf = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Text = _viewer.LastSearch.Pattern };
        d.Add(tf);
        var caseCb  = new CheckBox { X = 1, Y = 4, Text = "Case sensitive",     CheckedState = caseSensitive ? CheckState.Checked : CheckState.UnChecked };
        var regexCb = new CheckBox { X = 1, Y = 5, Text = "Regular expression", CheckedState = useRegex     ? CheckState.Checked : CheckState.UnChecked }; // #29
        d.Add(caseCb, regexCb);

        var ok     = new Button { X = Pos.Center() - 5, Y = 7, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { pattern = tf.Text?.ToString(); Application.RequestStop(d); };
        var cancel = new Button { X = Pos.Center() + 3, Y = 7, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (string.IsNullOrEmpty(pattern)) return;
        var opts = new SearchOptions
        {
            Pattern       = pattern,
            CaseSensitive = caseCb.CheckedState == CheckState.Checked,
            Type          = regexCb.CheckedState == CheckState.Checked ? SearchType.Regex
                          : _viewer.Mode == ViewMode.Hex ? SearchType.Hex
                          : SearchType.Normal,
            Backward      = backward,
        };

        var result = backward ? _viewer.FindPrev(opts) : _viewer.FindNext(opts);
        if (!result.Found)
            MessageBox.Query("Find", "Pattern not found", "OK");
        SetNeedsDraw();
    }

    /// <summary>Go-to line (text mode) or byte offset (hex mode). F5. (#9)</summary>
    private void ShowGotoPosition()
    {
        string? input = null;
        bool isHex = _viewer.Mode == ViewMode.Hex;
        var prompt = isHex ? "Byte offset (decimal or 0x hex):" : "Line number:";
        var title  = isHex ? "Go to byte offset" : "Go to line";

        var d = new Dialog { Title = title, Width = 50, Height = 8 };
        d.Add(new Label { X = 1, Y = 1, Text = prompt });
        var tf = new TextField { X = 1, Y = 3, Width = Dim.Fill(1) };
        d.Add(tf);
        var ok     = new Button { X = Pos.Center() - 5, Y = 5, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { input = tf.Text?.ToString(); Application.RequestStop(d); };
        var cancel = new Button { X = Pos.Center() + 3, Y = 5, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (string.IsNullOrEmpty(input)) return;

        if (isHex)
        {
            long offset;
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                long.TryParse(input[2..], System.Globalization.NumberStyles.HexNumber, null, out offset);
            else
                long.TryParse(input, out offset);
            _viewer.GotoOffset(offset, Viewport.Height - 2, Viewport.Width);
        }
        else
        {
            if (int.TryParse(input, out var lineNo) && lineNo >= 1)
                _viewer.ScrollLine = Math.Max(0, lineNo - 1);
        }
        SetNeedsDraw();
    }

    /// <summary>Strip nroff backspace sequences: char\bchar → char, _\bchar → char. (#13)</summary>
    private static string StripNroff(string line)
    {
        var sb = new System.Text.StringBuilder(line.Length);
        for (int i = 0; i < line.Length; i++)
        {
            if (i + 2 < line.Length && line[i + 1] == '\b')
            {
                // char\bchar = bold: keep char; _\bchar = underline: keep char after \b
                sb.Append(line[i] == '_' ? line[i + 2] : line[i]);
                i += 2; // skip \b and next char
            }
            else if (line[i] != '\b')
            {
                sb.Append(line[i]);
            }
        }
        return sb.ToString();
    }

    /// <summary>Show viewer-specific help (F1). (#21)</summary>
    private void ShowViewerHelp()
    {
        var help =
            "Viewer Key Bindings\n" +
            "──────────────────────────────────────────\n" +
            "q / F10 / Esc / F3  Close viewer\n" +
            "F2                  Toggle word wrap\n" +
            "F4                  Toggle hex mode\n" +
            "F5                  Go to line / byte offset\n" +
            "F7 / /              Search forward\n" +
            "F8                  Toggle raw mode\n" +
            "F9                  Toggle nroff formatting\n" +
            "n / N               Repeat search forward / backward\n" +
            "g / Home            Go to start\n" +
            "G / End             Go to end\n" +
            "Space / PgDn        Page down\n" +
            "PgUp                Page up\n" +
            "↑ / ↓ / k / j      Scroll one line\n" +
            "← / →              Scroll horizontally\n" +
            "Ctrl+F              Open next file in directory\n" +
            "Ctrl+B              Set bookmark 0 / open prev file with digit\n" +
            "0-9 then m          Set bookmark 0-9\n" +
            "0-9 then r          Go to bookmark 0-9\n" +
            "Alt+R               Toggle column ruler\n" +
            "Alt+E               Change character encoding\n" +
            "F1                  This help\n";

        var d = new Dialog { Title = "Viewer Help", Width = 60, Height = 28 };
        var tv = new TextView { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(2), Text = help, ReadOnly = true };
        d.Add(tv);
        var ok = new Button { X = Pos.Center(), Y = Pos.AnchorEnd(2), Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok);
        Application.Run(d); d.Dispose();
    }

    /// <summary>Encoding selection dialog (Alt+E). (#19)</summary>
    private void ShowEncodingDialog()
    {
        var encodings = System.Text.Encoding.GetEncodings()
            .OrderBy(e => e.Name)
            .Select(e => e.Name)
            .ToList();
        // Put common encodings first
        var preferred = new[] { "utf-8", "utf-16", "utf-32", "windows-1252", "iso-8859-1", "us-ascii" };
        encodings = preferred.Concat(encodings.Where(e => !preferred.Contains(e, StringComparer.OrdinalIgnoreCase))).ToList();

        string? selected = null;
        var d = new Dialog { Title = "Select Encoding", Width = 50, Height = 20 };
        d.Add(new Label { X = 1, Y = 1, Text = "Encoding:" });
        var tf = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Text = "utf-8" };
        var lv = new ListView { X = 1, Y = 3, Width = Dim.Fill(1), Height = Dim.Fill(3) };
        lv.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(encodings));
        d.Add(tf, lv);
        lv.SelectedItemChanged += (_, e) => { if (e.Item >= 0) tf.Text = encodings[e.Item]; };
        // Filter list as user types
        tf.TextChanged += (_, _) =>
        {
            var filter = tf.Text?.ToString() ?? string.Empty;
            var filtered = encodings.Where(e => e.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            lv.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(filtered));
        };
        var ok = new Button { X = Pos.Center() - 5, Y = Pos.AnchorEnd(2), Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { selected = tf.Text?.ToString(); Application.RequestStop(d); };
        var cancel = new Button { X = Pos.Center() + 3, Y = Pos.AnchorEnd(2), Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (string.IsNullOrEmpty(selected)) return;
        try
        {
            var enc = System.Text.Encoding.GetEncoding(selected);
            _viewer.Encoding = enc;
            _viewer.LoadFile(_viewer.FilePath ?? string.Empty);
            SetNeedsDraw();
        }
        catch { MessageBox.ErrorQuery("Encoding", $"Unknown encoding: {selected}", "OK"); }
    }
}
