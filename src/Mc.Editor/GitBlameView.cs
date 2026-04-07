using Terminal.Gui;

namespace Mc.Editor;

/// <summary>
/// Git blame view showing commit info per line.
/// </summary>
public sealed class GitBlameView : View
{
    private readonly string[] _blameLines;
    private int _scrollLine;

    public event EventHandler? RequestClose;

    public GitBlameView(string filePath)
    {
        _blameLines = GitHelper.GetBlame(filePath);
        
        CanFocus = true;
        X = 0; Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
    }

    protected override bool OnKeyDown(Key keyEvent)
    {
        switch (keyEvent.KeyCode)
        {
            case KeyCode.CursorUp:   _scrollLine = Math.Max(0, _scrollLine - 1); SetNeedsDraw(); return true;
            case KeyCode.CursorDown: _scrollLine = Math.Min(_blameLines.Length - 1, _scrollLine + 1); SetNeedsDraw(); return true;
            case KeyCode.PageUp:     _scrollLine = Math.Max(0, _scrollLine - Viewport.Height + 2); SetNeedsDraw(); return true;
            case KeyCode.PageDown:   _scrollLine = Math.Min(_blameLines.Length - 1, _scrollLine + Viewport.Height - 2); SetNeedsDraw(); return true;
            case KeyCode.Home:       _scrollLine = 0; SetNeedsDraw(); return true;
            case KeyCode.End:        _scrollLine = Math.Max(0, _blameLines.Length - 1); SetNeedsDraw(); return true;
            case KeyCode.Esc:
            case KeyCode.F10:        RequestClose?.Invoke(this, EventArgs.Empty); return true;
        }
        return base.OnKeyDown(keyEvent);
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        var viewport = Viewport;
        int contentHeight = viewport.Height - 1;

        // Header
        Move(0, 0);
        Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
        var header = " Commit   Date       Author                                    Line ";
        if (header.Length > viewport.Width) header = header[..viewport.Width];
        Driver!.AddStr(header.PadRight(viewport.Width));

        // Blame lines
        for (int row = 0; row < contentHeight; row++)
        {
            int lineIdx = _scrollLine + row;
            Move(0, row + 1);
            
            if (lineIdx >= _blameLines.Length)
            {
                Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.White, Color.Black));
                Driver!.AddStr(new string(' ', viewport.Width));
                continue;
            }

            Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.White, Color.Black));
            var line = _blameLines[lineIdx];
            if (line.Length > viewport.Width) line = line[..viewport.Width];
            Driver!.AddStr(line.PadRight(viewport.Width));
        }

        return false;
    }
}
