using Terminal.Gui;

namespace Mc.Editor;

/// <summary>
/// Side-by-side binary comparison with hex view.
/// </summary>
public sealed class BinaryCompareView : View
{
    private readonly string _leftPath;
    private readonly string _rightPath;
    private readonly byte[] _leftBytes;
    private readonly byte[] _rightBytes;
    private int _scrollOffset;
    private const int BytesPerRow = 16;

    public event EventHandler? RequestClose;

    public BinaryCompareView(string leftPath, string rightPath)
    {
        _leftPath = leftPath;
        _rightPath = rightPath;
        _leftBytes = File.Exists(leftPath) ? File.ReadAllBytes(leftPath) : [];
        _rightBytes = File.Exists(rightPath) ? File.ReadAllBytes(rightPath) : [];

        CanFocus = true;
        X = 0; Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
    }

    protected override bool OnKeyDown(Key keyEvent)
    {
        int maxOffset = Math.Max(_leftBytes.Length, _rightBytes.Length);
        switch (keyEvent.KeyCode)
        {
            case KeyCode.CursorUp:   _scrollOffset = Math.Max(0, _scrollOffset - BytesPerRow); SetNeedsDraw(); return true;
            case KeyCode.CursorDown: _scrollOffset = Math.Min(maxOffset - BytesPerRow, _scrollOffset + BytesPerRow); SetNeedsDraw(); return true;
            case KeyCode.PageUp:     _scrollOffset = Math.Max(0, _scrollOffset - BytesPerRow * (Viewport.Height - 2)); SetNeedsDraw(); return true;
            case KeyCode.PageDown:   _scrollOffset = Math.Min(maxOffset - BytesPerRow, _scrollOffset + BytesPerRow * (Viewport.Height - 2)); SetNeedsDraw(); return true;
            case KeyCode.Home:       _scrollOffset = 0; SetNeedsDraw(); return true;
            case KeyCode.End:        _scrollOffset = Math.Max(0, (maxOffset / BytesPerRow) * BytesPerRow); SetNeedsDraw(); return true;
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
        var header = $" {Path.GetFileName(_leftPath)} ({_leftBytes.Length} bytes) | {Path.GetFileName(_rightPath)} ({_rightBytes.Length} bytes) ";
        if (header.Length > viewport.Width) header = header[..viewport.Width];
        Driver!.AddStr(header.PadRight(viewport.Width));

        // Hex rows
        for (int row = 0; row < contentHeight; row++)
        {
            int offset = _scrollOffset + row * BytesPerRow;
            Move(0, row + 1);

            if (offset >= Math.Max(_leftBytes.Length, _rightBytes.Length))
            {
                Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.White, Color.Black));
                Driver!.AddStr(new string(' ', viewport.Width));
                continue;
            }

            // Offset
            Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Cyan, Color.Black));
            Driver!.AddStr($"{offset:X8}: ");

            // Left hex
            for (int i = 0; i < BytesPerRow; i++)
            {
                int idx = offset + i;
                if (idx < _leftBytes.Length)
                {
                    bool diff = idx >= _rightBytes.Length || _leftBytes[idx] != _rightBytes[idx];
                    Driver!.SetAttribute(diff ? 
                        new Terminal.Gui.Attribute(Color.BrightRed, Color.Black) :
                        new Terminal.Gui.Attribute(Color.White, Color.Black));
                    Driver!.AddStr($"{_leftBytes[idx]:X2} ");
                }
                else
                {
                    Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Gray, Color.Black));
                    Driver!.AddStr("   ");
                }
            }

            // Separator
            Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Cyan, Color.Black));
            Driver!.AddStr("| ");

            // Right hex
            for (int i = 0; i < BytesPerRow; i++)
            {
                int idx = offset + i;
                if (idx < _rightBytes.Length)
                {
                    bool diff = idx >= _leftBytes.Length || _leftBytes[idx] != _rightBytes[idx];
                    Driver!.SetAttribute(diff ?
                        new Terminal.Gui.Attribute(Color.BrightGreen, Color.Black) :
                        new Terminal.Gui.Attribute(Color.White, Color.Black));
                    Driver!.AddStr($"{_rightBytes[idx]:X2} ");
                }
                else
                {
                    Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Gray, Color.Black));
                    Driver!.AddStr("   ");
                }
            }

            // ASCII preview
            Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Gray, Color.Black));
            Driver!.AddStr(" ");
            for (int i = 0; i < BytesPerRow; i++)
            {
                int idx = offset + i;
                if (idx < _leftBytes.Length)
                {
                    char c = _leftBytes[idx] >= 32 && _leftBytes[idx] < 127 ? (char)_leftBytes[idx] : '.';
                    Driver!.AddStr(c.ToString());
                }
            }
            Driver!.AddStr("|");
            for (int i = 0; i < BytesPerRow; i++)
            {
                int idx = offset + i;
                if (idx < _rightBytes.Length)
                {
                    char c = _rightBytes[idx] >= 32 && _rightBytes[idx] < 127 ? (char)_rightBytes[idx] : '.';
                    Driver!.AddStr(c.ToString());
                }
            }
        }

        return false;
    }
}
