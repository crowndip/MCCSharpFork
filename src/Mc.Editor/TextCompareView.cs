using Terminal.Gui;

namespace Mc.Editor;

/// <summary>
/// Side-by-side text comparison with syntax highlighting.
/// </summary>
public sealed class TextCompareView : View
{
    private readonly string _leftPath;
    private readonly string _rightPath;
    private readonly string[] _leftLines;
    private readonly string[] _rightLines;
    private readonly DiffLine[] _diffLines;
    private int _scrollLine;
    private readonly SyntaxHighlighter? _leftHighlighter;
    private readonly SyntaxHighlighter? _rightHighlighter;

    public event EventHandler? RequestClose;

    public TextCompareView(string leftPath, string rightPath)
    {
        _leftPath = leftPath;
        _rightPath = rightPath;
        _leftLines = File.Exists(leftPath) ? File.ReadAllLines(leftPath) : [];
        _rightLines = File.Exists(rightPath) ? File.ReadAllLines(rightPath) : [];
        _diffLines = ComputeDiff(_leftLines, _rightLines);

        _leftHighlighter = SyntaxHighlighter.ForFile(leftPath, _leftLines.Length > 0 ? _leftLines[0] : null);
        _rightHighlighter = SyntaxHighlighter.ForFile(rightPath, _rightLines.Length > 0 ? _rightLines[0] : null);

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
        int halfWidth = viewport.Width / 2 - 1;
        int contentHeight = viewport.Height - 1;

        // Header
        Move(0, 0);
        Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
        var leftHeader = $" {Path.GetFileName(_leftPath)} ".PadRight(halfWidth);
        var rightHeader = $" {Path.GetFileName(_rightPath)} ".PadRight(halfWidth);
        Driver!.AddStr(leftHeader[..Math.Min(halfWidth, leftHeader.Length)]);
        Driver!.AddStr("|");
        Driver!.AddStr(rightHeader[..Math.Min(halfWidth, rightHeader.Length)]);

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
            var attr = line.Type switch
            {
                DiffType.Added => new Terminal.Gui.Attribute(Color.BrightGreen, Color.Black),
                DiffType.Removed => new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
                DiffType.Changed => new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                _ => new Terminal.Gui.Attribute(Color.White, Color.Black),
            };

            // Left side
            Driver!.SetAttribute(attr);
            var leftText = (line.LeftText ?? "").PadRight(halfWidth);
            if (leftText.Length > halfWidth) leftText = leftText[..halfWidth];
            Driver!.AddStr(leftText);

            // Separator
            Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
            Driver!.AddStr("|");

            // Right side
            Driver!.SetAttribute(attr);
            var rightText = (line.RightText ?? "").PadRight(halfWidth);
            if (rightText.Length > halfWidth) rightText = rightText[..halfWidth];
            Driver!.AddStr(rightText);
        }

        return false;
    }

    private static DiffLine[] ComputeDiff(string[] left, string[] right)
    {
        var result = new List<DiffLine>();
        int i = 0, j = 0;

        while (i < left.Length || j < right.Length)
        {
            if (i >= left.Length)
            {
                result.Add(new DiffLine(DiffType.Added, null, right[j++]));
            }
            else if (j >= right.Length)
            {
                result.Add(new DiffLine(DiffType.Removed, left[i++], null));
            }
            else if (left[i] == right[j])
            {
                result.Add(new DiffLine(DiffType.Same, left[i], right[j]));
                i++; j++;
            }
            else
            {
                // Simple heuristic: if next lines match, one was added/removed
                if (i + 1 < left.Length && left[i + 1] == right[j])
                {
                    result.Add(new DiffLine(DiffType.Removed, left[i++], null));
                }
                else if (j + 1 < right.Length && left[i] == right[j + 1])
                {
                    result.Add(new DiffLine(DiffType.Added, null, right[j++]));
                }
                else
                {
                    result.Add(new DiffLine(DiffType.Changed, left[i++], right[j++]));
                }
            }
        }

        return result.ToArray();
    }

    private record DiffLine(DiffType Type, string? LeftText, string? RightText);
    private enum DiffType { Same, Added, Removed, Changed }
}
