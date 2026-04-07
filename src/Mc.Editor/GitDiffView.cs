using Terminal.Gui;

namespace Mc.Editor;

/// <summary>
/// Git diff view showing changes.
/// </summary>
public sealed class GitDiffView : View
{
    private readonly string[] _diffLines;
    private int _scrollLine;

    public event EventHandler? RequestClose;

    public GitDiffView(string filePath)
    {
        var diff = GitHelper.GetDiff(filePath);
        _diffLines = diff.Split('\n');
        
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
            case KeyCode.CursorDown: _scrollLine = Math.Min(_diffLines.Length - 1, _scrollLine + 1); SetNeedsDraw(); return true;
            case KeyCode.PageUp:     _scrollLine = Math.Max(0, _scrollLine - Viewport.Height + 2); SetNeedsDraw(); return true;
            case KeyCode.PageDown:   _scrollLine = Math.Min(_diffLines.Length - 1, _scrollLine + Viewport.Height - 2); SetNeedsDraw(); return true;
            case KeyCode.Home:       _scrollLine = 0; SetNeedsDraw(); return true;
            case KeyCode.End:        _scrollLine = Math.Max(0, _diffLines.Length - 1); SetNeedsDraw(); return true;
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
        var header = " Git Diff ";
        if (header.Length > viewport.Width) header = header[..viewport.Width];
        Driver!.AddStr(header.PadRight(viewport.Width));

        // Diff lines
        for (int row = 0; row < contentHeight; row++)
        {
            int lineIdx = _scrollLine + row;
            Move(0, row + 1);
            
            if (lineIdx >= _diffLines.Length)
            {
                Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.White, Color.Black));
                Driver!.AddStr(new string(' ', viewport.Width));
                continue;
            }

            var line = _diffLines[lineIdx];
            Terminal.Gui.Attribute attr;

            if (line.StartsWith("+"))
                attr = new Terminal.Gui.Attribute(Color.BrightGreen, Color.Black);
            else if (line.StartsWith("-"))
                attr = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black);
            else if (line.StartsWith("@@"))
                attr = new Terminal.Gui.Attribute(Color.Cyan, Color.Black);
            else if (line.StartsWith("diff ") || line.StartsWith("index "))
                attr = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black);
            else
                attr = new Terminal.Gui.Attribute(Color.White, Color.Black);

            Driver!.SetAttribute(attr);
            if (line.Length > viewport.Width) line = line[..viewport.Width];
            Driver!.AddStr(line.PadRight(viewport.Width));
        }

        return false;
    }
}
