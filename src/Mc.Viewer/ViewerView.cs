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

    public event EventHandler? RequestClose;

    public ViewerView(string filePath)
    {
        _viewer = new ViewerController();
        _viewer.LoadFile(filePath);
        _viewer.Changed += (_, _) => SetNeedsDraw();
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
        _            => "TEXT",
    };

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        var viewport = Viewport;
        int contentHeight = viewport.Height - 1;

        if (_viewer.Mode == ViewMode.Hex)
            DrawHex(viewport, contentHeight);
        else
            DrawText(viewport, contentHeight);

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

    private void DrawStatusBar(System.Drawing.Rectangle viewport)
    {
        Move(0, viewport.Height - 1);
        Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
        var totalLines = _viewer.Mode == ViewMode.Hex
            ? _viewer.TotalHexLineCount()
            : _viewer.TotalLineCount(viewport.Width);
        var pct = totalLines > 0
            ? (int)((double)_viewer.ScrollLine / Math.Max(1, totalLines - (viewport.Height - 1)) * 100)
            : 100;
        pct = Math.Clamp(pct, 0, 100);
        var status = $" {_viewer.FilePath ?? "?"} | {_viewer.FileSize:N0} bytes | {pct}% | {ModeLabel} | {(_viewer.WrapLines ? "Wrap" : "No wrap")} | F2=Wrap F4=Hex F5=Goto F7=Find F8=Raw n/N=Repeat q=Quit";
        if (status.Length > viewport.Width) status = status[..viewport.Width];
        Driver.AddStr(status.PadRight(viewport.Width));
    }

    protected override bool OnKeyDown(Key keyEvent)
    {
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

            case KeyCode.F2:
                _viewer.WrapLines = !_viewer.WrapLines; SetNeedsDraw(); return true;

            case KeyCode.F7:
                ShowSearch(backward: false); return true;

            case KeyCode.F5:  // Go to offset (#16)
                ShowGotoOffset(); return true;

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

            // Bookmarks (#34): Ctrl+B = set, Ctrl+P = goto
            case KeyCode.B when keyEvent.IsCtrl:
                _viewer.SetBookmark(0); return true;

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

            default:
                return base.OnKeyDown(keyEvent);
        }
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

    /// <summary>Go-to byte offset dialog (F5). (#16)</summary>
    private void ShowGotoOffset()
    {
        string? input = null;
        var d = new Dialog { Title = "Go to position", Width = 50, Height = 8 };
        d.Add(new Label { X = 1, Y = 1, Text = "Byte offset (decimal or 0x hex):" });
        var tf = new TextField { X = 1, Y = 3, Width = Dim.Fill(1) };
        d.Add(tf);
        var ok     = new Button { X = Pos.Center() - 5, Y = 5, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { input = tf.Text?.ToString(); Application.RequestStop(d); };
        var cancel = new Button { X = Pos.Center() + 3, Y = 5, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (string.IsNullOrEmpty(input)) return;
        long offset;
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            long.TryParse(input[2..], System.Globalization.NumberStyles.HexNumber, null, out offset);
        else
            long.TryParse(input, out offset);

        _viewer.GotoOffset(offset, Viewport.Height - 2, Viewport.Width);
        SetNeedsDraw();
    }
}
