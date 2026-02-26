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

    private void IndexChanges()
    {
        _changeLines = [];
        for (int i = 0; i < _lines.Count; i++)
            if (_lines[i].Type != DiffLineType.Context)
                _changeLines.Add(i);
    }
}
