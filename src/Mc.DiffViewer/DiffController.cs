namespace Mc.DiffViewer;

/// <summary>
/// Controller for the diff viewer.
/// Equivalent to src/diffviewer/ydiff.c in the original C codebase.
/// </summary>
public sealed class DiffController
{
    private IReadOnlyList<DiffLine> _lines = [];
    private string? _leftPath;
    private string? _rightPath;
    private int _scrollLine;
    private int _currentChange;
    private List<int> _changeLines = [];

    public IReadOnlyList<DiffLine> Lines => _lines;
    public int ScrollLine => _scrollLine;
    public string? LeftPath => _leftPath;
    public string? RightPath => _rightPath;
    public int TotalChanges => _changeLines.Count;
    public int CurrentChange => _currentChange;

    public event EventHandler? Changed;

    public void LoadFiles(string leftPath, string rightPath)
    {
        _leftPath = leftPath;
        _rightPath = rightPath;

        var leftText = File.Exists(leftPath) ? File.ReadAllText(leftPath) : string.Empty;
        var rightText = File.Exists(rightPath) ? File.ReadAllText(rightPath) : string.Empty;

        _lines = DiffEngine.ComputeDiff(leftText, rightText);
        _scrollLine = 0;
        _currentChange = -1;
        IndexChanges();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void LoadTexts(string leftText, string rightText, string? leftName = null, string? rightName = null)
    {
        _leftPath = leftName;
        _rightPath = rightName;
        _lines = DiffEngine.ComputeDiff(leftText, rightText);
        _scrollLine = 0;
        _currentChange = -1;
        IndexChanges();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollDown(int lines = 1)
    {
        _scrollLine = Math.Min(_scrollLine + lines, Math.Max(0, _lines.Count - 1));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollUp(int lines = 1)
    {
        _scrollLine = Math.Max(0, _scrollLine - lines);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void NextChange()
    {
        if (_changeLines.Count == 0) return;
        _currentChange = (_currentChange + 1) % _changeLines.Count;
        _scrollLine = _changeLines[_currentChange];
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void PrevChange()
    {
        if (_changeLines.Count == 0) return;
        _currentChange = (_currentChange - 1 + _changeLines.Count) % _changeLines.Count;
        _scrollLine = _changeLines[_currentChange];
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<DiffLine> GetVisibleLines(int startLine, int count)
        => _lines.Skip(startLine).Take(count).ToList();

    /// <summary>
    /// Write a unified-diff representation of the loaded diff to <paramref name="outputPath"/>.
    /// Equivalent to the F2 "save diff" action in the original mcdiff.
    /// Format: standard `diff -u` unified diff with --- / +++ headers and @@ hunks.
    /// </summary>
    public void SaveDiff(string outputPath)
    {
        const int contextLines = 3;
        var sb = new System.Text.StringBuilder();
        var leftName  = _leftPath  ?? "left";
        var rightName = _rightPath ?? "right";
        sb.AppendLine($"--- {leftName}");
        sb.AppendLine($"+++ {rightName}");

        // Split lines into hunks separated by runs of context > 2*contextLines
        var hunks = BuildHunks(contextLines);
        foreach (var (hunkLines, leftStart, rightStart) in hunks)
        {
            int leftCount  = hunkLines.Count(l => l.Type != DiffLineType.Added);
            int rightCount = hunkLines.Count(l => l.Type != DiffLineType.Removed);
            sb.AppendLine($"@@ -{leftStart},{leftCount} +{rightStart},{rightCount} @@");
            foreach (var line in hunkLines)
            {
                var prefix = line.Type switch
                {
                    DiffLineType.Added   => "+",
                    DiffLineType.Removed => "-",
                    DiffLineType.Changed => "~",   // non-standard but informative
                    _                   => " ",
                };
                var text = line.Type == DiffLineType.Added
                    ? (line.RightText ?? string.Empty)
                    : (line.LeftText  ?? string.Empty);
                sb.AppendLine($"{prefix}{text}");
            }
        }
        File.WriteAllText(outputPath, sb.ToString());
    }

    private List<(List<DiffLine> Lines, int LeftStart, int RightStart)> BuildHunks(int ctx)
    {
        var result = new List<(List<DiffLine>, int, int)>();
        var inHunk = false;
        List<DiffLine>? current = null;
        int leftStart = 1, rightStart = 1;
        int contextTail = 0;

        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            bool isChange = line.Type != DiffLineType.Context;

            if (!inHunk)
            {
                if (!isChange) continue;
                // Start a new hunk: include up to ctx lines of preceding context
                int ctxStart = Math.Max(0, i - ctx);
                current = new List<DiffLine>();
                leftStart  = _lines[ctxStart].LeftLineNo;
                rightStart = _lines[ctxStart].RightLineNo;
                for (int k = ctxStart; k < i; k++) current.Add(_lines[k]);
                current.Add(line);
                inHunk = true;
                contextTail = 0;
            }
            else
            {
                current!.Add(line);
                if (!isChange) { contextTail++; if (contextTail >= ctx * 2) { result.Add((current, leftStart, rightStart)); inHunk = false; current = null; } }
                else contextTail = 0;
            }
        }
        if (inHunk && current != null)
        {
            // Trim trailing context to ctx lines
            while (current.Count > ctx && current[^1].Type == DiffLineType.Context)
                current.RemoveAt(current.Count - 1);
            result.Add((current, leftStart, rightStart));
        }
        return result;
    }

    private void IndexChanges()
    {
        _changeLines = [];
        for (int i = 0; i < _lines.Count; i++)
            if (_lines[i].Type != DiffLineType.Context)
                _changeLines.Add(i);
    }
}
