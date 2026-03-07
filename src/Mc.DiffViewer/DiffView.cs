using Terminal.Gui;

namespace Mc.DiffViewer;

/// <summary>
/// Terminal.Gui view for side-by-side diff display.
/// Equivalent to src/diffviewer/ydiff.c in the original C codebase.
/// </summary>
public sealed class DiffView : View
{
    private readonly DiffController _diff;
    private readonly Terminal.Gui.Attribute _addedAttr    = new(Color.BrightGreen,  Color.Black);
    private readonly Terminal.Gui.Attribute _removedAttr  = new(Color.BrightRed,    Color.Black);
    private readonly Terminal.Gui.Attribute _changedAttr  = new(Color.BrightYellow, Color.Black);
    private readonly Terminal.Gui.Attribute _contextAttr  = new(Color.White,        Color.Black);
    private readonly Terminal.Gui.Attribute _headerAttr   = new(Color.Black,        Color.Cyan);

    public event EventHandler? RequestClose;

    public DiffView(string leftPath, string rightPath)
    {
        _diff = new DiffController();
        _diff.LoadFiles(leftPath, rightPath);
        _diff.Changed += (_, _) => SetNeedsDraw();
        CanFocus = true;
        ColorScheme = new ColorScheme
        {
            Normal    = new Terminal.Gui.Attribute(Color.White, Color.Black),
            Focus     = new Terminal.Gui.Attribute(Color.White, Color.Black),
            HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
            HotFocus  = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
            Disabled  = new Terminal.Gui.Attribute(Color.Gray, Color.Black),
        };
    }

    public new string Title =>
        $"Diff: {Path.GetFileName(_diff.LeftPath ?? "?")} <> {Path.GetFileName(_diff.RightPath ?? "?")} ({_diff.TotalChanges} changes)";

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        var viewport = Viewport;
        int contentHeight = viewport.Height - 2; // header + status
        int halfWidth = viewport.Width / 2 - 1;

        // Header
        Move(0, 0);
        Driver!.SetAttribute(_headerAttr);
        var leftHeader = $" {_diff.LeftPath ?? "Left"} ".PadRight(halfWidth);
        var rightHeader = $" {_diff.RightPath ?? "Right"} ".PadRight(halfWidth);
        Driver!.AddStr(leftHeader[..Math.Min(halfWidth, leftHeader.Length)]);
        Driver!.AddStr(" ");
        Driver!.AddStr(rightHeader[..Math.Min(halfWidth, rightHeader.Length)]);

        // Diff lines
        var visibleLines = _diff.GetVisibleLines(_diff.ScrollLine, contentHeight);
        for (int row = 0; row < contentHeight; row++)
        {
            Move(0, row + 1);
            if (row >= visibleLines.Count)
            {
                Driver!.SetAttribute(_contextAttr);
                Driver!.AddStr(new string(' ', viewport.Width));
                continue;
            }

            var line = visibleLines[row];
            var attr = line.Type switch
            {
                DiffLineType.Added   => _addedAttr,
                DiffLineType.Removed => _removedAttr,
                DiffLineType.Changed => _changedAttr,
                _                   => _contextAttr,
            };

            Driver!.SetAttribute(attr);

            // Left side
            var prefix = line.Type switch
            {
                DiffLineType.Added   => "  ",
                DiffLineType.Removed => "- ",
                DiffLineType.Changed => "~ ",
                _                   => "  ",
            };
            var leftText = (prefix + (line.LeftText ?? string.Empty)).PadRight(halfWidth);
            if (leftText.Length > halfWidth) leftText = leftText[..halfWidth];
            Driver!.AddStr(leftText);

            // Separator
            Driver!.SetAttribute(_headerAttr);
            Driver!.AddStr("|");

            // Right side
            var rightPrefix = line.Type switch
            {
                DiffLineType.Added   => "+ ",
                DiffLineType.Removed => "  ",
                DiffLineType.Changed => "~ ",
                _                   => "  ",
            };
            Driver!.SetAttribute(attr);
            var rightText = (rightPrefix + (line.RightText ?? string.Empty)).PadRight(halfWidth);
            if (rightText.Length > halfWidth) rightText = rightText[..halfWidth];
            Driver!.AddStr(rightText);
        }

        // Status bar
        Move(0, viewport.Height - 1);
        Driver!.SetAttribute(_headerAttr);
        var status = $" {_diff.TotalChanges} changes | Change {_diff.CurrentChange + 1}/{_diff.TotalChanges} | n=Next p=Prev F2=Save q=Quit";
        if (status.Length > viewport.Width) status = status[..viewport.Width];
        Driver!.AddStr(status.PadRight(viewport.Width));
        return false;
    }

    protected override bool OnKeyDown(Key keyEvent)
    {
        switch (keyEvent.KeyCode)
        {
            case KeyCode.Q:
            case KeyCode.F10:
            case KeyCode.Esc:
                RequestClose?.Invoke(this, EventArgs.Empty); return true;

            case KeyCode.F2:
                SaveDiffToFile(); return true;

            case KeyCode.N:
            case KeyCode.F7:
                _diff.NextChange(); return true;

            case KeyCode.P:
            case KeyCode.F8:
                _diff.PrevChange(); return true;

            case KeyCode.CursorDown:
                _diff.ScrollDown(); return true;

            case KeyCode.CursorUp:
                _diff.ScrollUp(); return true;

            case KeyCode.PageDown:
                _diff.ScrollDown(Viewport.Height - 3); return true;

            case KeyCode.PageUp:
                _diff.ScrollUp(Viewport.Height - 3); return true;

            default:
                return base.OnKeyDown(keyEvent);
        }
    }

    /// <summary>
    /// F2: prompt for output path and save the diff in unified-diff format.
    /// Equivalent to the mcdiff "save" action in the original C codebase.
    /// </summary>
    private void SaveDiffToFile()
    {
        var defaultName = _diff.LeftPath != null
            ? Path.GetFileName(_diff.LeftPath) + ".diff"
            : "diff.patch";

        string? outputPath = null;
        var d = new Dialog { Title = "Save diff", Width = 60, Height = 8 };
        d.Add(new Label { X = 1, Y = 1, Text = "Save unified diff to file:" });
        var tf = new TextField { X = 1, Y = 3, Width = Dim.Fill(1), Text = defaultName };
        d.Add(tf);
        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { outputPath = tf.Text?.ToString(); Application.RequestStop(d); };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok);
        d.AddButton(cancel);
        Application.Run(d);
        d.Dispose();

        if (string.IsNullOrWhiteSpace(outputPath)) return;
        try
        {
            _diff.SaveDiff(outputPath);
            MessageBox.Query("Saved", $"Diff saved to:\n{outputPath}", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Save failed", ex.Message, "OK");
        }
    }
}
