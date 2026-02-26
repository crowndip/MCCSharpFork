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
        $"View: {(_viewer.FilePath != null ? Path.GetFileName(_viewer.FilePath) : "?")} ({_viewer.FileSize:N0} bytes) [{(_viewer.Mode == ViewMode.Hex ? "HEX" : "TEXT")}]";

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
        for (int row = 0; row < contentHeight; row++)
        {
            Move(0, row);
            Driver.SetAttribute(ColorScheme.Normal);
            if (row < lines.Count)
            {
                var line = lines[row];
                if (line.Length > viewport.Width) line = line[..viewport.Width];
                Driver.AddStr(line.PadRight(viewport.Width));
            }
            else
            {
                Driver.AddStr("~".PadRight(viewport.Width));
            }
        }
    }

    private void DrawHex(System.Drawing.Rectangle viewport, int contentHeight)
    {
        var lines = _viewer.GetHexLines(_viewer.ScrollLine, contentHeight);
        for (int row = 0; row < contentHeight; row++)
        {
            Move(0, row);
            Driver.SetAttribute(ColorScheme.Normal);
            if (row < lines.Count)
            {
                var line = lines[row];
                if (line.Length > viewport.Width) line = line[..viewport.Width];
                Driver.AddStr(line.PadRight(viewport.Width));
            }
            else
            {
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
            ? (int)((double)_viewer.ScrollLine / totalLines * 100)
            : 100;
        var status = $" {_viewer.FilePath ?? "?"} | {_viewer.FileSize:N0} bytes | {pct}% | {(_viewer.WrapLines ? "Wrap" : "No wrap")} | F4=Hex F7=Search q=Quit";
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
                RequestClose?.Invoke(this, EventArgs.Empty); return true;

            case KeyCode.F4:
                _viewer.Mode = _viewer.Mode == ViewMode.Hex ? ViewMode.Text : ViewMode.Hex;
                SetNeedsDraw(); return true;

            case KeyCode.F2:
                _viewer.WrapLines = !_viewer.WrapLines; SetNeedsDraw(); return true;

            case KeyCode.F7:
                ShowSearch(); return true;

            case KeyCode.N:
                if (_viewer.LastSearch.Pattern.Length > 0)
                { _viewer.FindNext(_viewer.LastSearch); SetNeedsDraw(); }
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
            case KeyCode.B:
                _viewer.ScrollUp(Viewport.Height - 2); return true;

            case KeyCode.Home:
                _viewer.GoToStart(); return true;

            case KeyCode.End:
                _viewer.GoToEnd(Viewport.Height - 2, Viewport.Width); return true;

            case KeyCode.CursorRight:
                _viewer.ScrollRight(); return true;

            case KeyCode.CursorLeft:
                _viewer.ScrollLeft(); return true;

            default:
                return base.OnKeyDown(keyEvent);
        }
    }

    private void ShowSearch()
    {
        var pattern = PromptInput("Find", "Search for:", _viewer.LastSearch.Pattern);
        if (pattern == null) return;
        var opts = new SearchOptions
        {
            Pattern = pattern,
            Type = _viewer.Mode == ViewMode.Hex ? SearchType.Hex : SearchType.Normal,
        };
        var result = _viewer.FindNext(opts);
        if (!result.Found)
            MessageBox.Query("Find", "Pattern not found", "OK");
        SetNeedsDraw();
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
