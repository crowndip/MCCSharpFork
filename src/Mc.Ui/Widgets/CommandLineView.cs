using System.Collections.ObjectModel;
using Terminal.Gui;

namespace Mc.Ui.Widgets;

/// <summary>
/// Command input line at the bottom of the file manager.
/// Equivalent to WInput for the command line in the original C codebase.
/// Includes inline history dropdown (Ctrl+H or Up on empty input).
/// Equivalent to command.c history popup in the original MC.
/// </summary>
public sealed class CommandLineView : View
{
    private readonly TextField _input;
    private readonly Label _prompt;
    private readonly List<string> _history = [];
    private int _historyIndex = -1;

    // Inline history popup
    private View?     _popupContainer;
    private ListView? _popupList;

    // Current directory for Tab completion
    private string _currentDirectory = string.Empty;

    // Ctrl+Q quote-next state (#26)
    private bool _quoteNext;

    // Kill ring for Ctrl+K / Ctrl+Y (#6)
    private string _killRing = string.Empty;

    public event EventHandler<string>? CommandEntered;

    public CommandLineView()
    {
        Height = 1;
        Width = Dim.Fill();
        ColorScheme = McTheme.Panel;

        _prompt = new Label
        {
            X = 0, Y = 0,
            Width = Dim.Auto(),
            Height = 1,
            Text = "$ ",
            ColorScheme = McTheme.StatusBar,
        };

        _input = new TextField
        {
            X = Pos.Right(_prompt), Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            ColorScheme = McTheme.Panel,
        };

        Add(_prompt, _input);

        _input.KeyDown += OnInputKeyDown;
    }

    private void OnInputKeyDown(object? sender, Key key)
    {
        // Ctrl+Q: quote next character literally (#26)
        if (key.KeyCode == (KeyCode.Q | KeyCode.CtrlMask))
        {
            _quoteNext = true;
            key.Handled = true;
            return;
        }
        if (_quoteNext)
        {
            _quoteNext = false;
            var rune = key.AsRune;
            if (rune.Value > 0)
            {
                InsertAtCursor(rune.Value.ToString());
                key.Handled = true;
            }
            return;
        }

        if (key == Key.Enter)
        {
            HideHistoryPopup();
            var text = _input.Text?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (_history.Count == 0 || _history[^1] != text)
                    _history.Add(text);
                _historyIndex = -1;
                _input.Text = string.Empty;
                CommandEntered?.Invoke(this, text);
            }
        }
        else if (key == Key.Tab || key.KeyCode == (KeyCode.Tab | KeyCode.AltMask))
        {
            TabComplete();
            key.Handled = true;
        }
        else if (key.KeyCode == (KeyCode.H | KeyCode.CtrlMask) ||
                 key.KeyCode == (KeyCode.H | KeyCode.AltMask)  ||  // Alt+H = command history (#11)
                 (key == Key.CursorUp && string.IsNullOrEmpty(_input.Text?.ToString())))
        {
            ShowHistoryPopup();
            key.Handled = true;
        }
        else if (key == Key.CursorUp ||
                 key.KeyCode == (KeyCode.P | KeyCode.AltMask))  // Alt+P = previous (#7)
        {
            NavigateHistory(1);
            key.Handled = true;
        }
        else if (key == Key.CursorDown ||
                 key.KeyCode == (KeyCode.N | KeyCode.AltMask))  // Alt+N = next (#7)
        {
            NavigateHistory(-1);
            key.Handled = true;
        }
        else if (key == Key.Esc)
        {
            if (_popupContainer != null) { HideHistoryPopup(); key.Handled = true; }
        }
        // ── Emacs-style editing (#6) ──────────────────────────────────────────
        else if (key.KeyCode == (KeyCode.A | KeyCode.CtrlMask))  // Ctrl+A = start of line
        {
            _input.CursorPosition = 0;
            key.Handled = true;
        }
        else if (key.KeyCode == (KeyCode.E | KeyCode.CtrlMask))  // Ctrl+E = end of line
        {
            _input.CursorPosition = _input.Text?.Length ?? 0;
            key.Handled = true;
        }
        else if (key.KeyCode == (KeyCode.K | KeyCode.CtrlMask))  // Ctrl+K = kill to end of line
        {
            var text = _input.Text?.ToString() ?? string.Empty;
            var pos  = _input.CursorPosition;
            _killRing = text[pos..];
            _input.Text = text[..pos];
            key.Handled = true;
        }
        else if (key.KeyCode == (KeyCode.W | KeyCode.CtrlMask))  // Ctrl+W = kill word before cursor
        {
            var text = _input.Text?.ToString() ?? string.Empty;
            var pos  = _input.CursorPosition;
            var start = pos;
            while (start > 0 && text[start - 1] == ' ') start--;
            while (start > 0 && text[start - 1] != ' ') start--;
            _killRing = text[start..pos];
            _input.Text = text[..start] + text[pos..];
            _input.CursorPosition = start;
            key.Handled = true;
        }
        else if (key.KeyCode == (KeyCode.Y | KeyCode.CtrlMask))  // Ctrl+Y = yank (paste kill ring)
        {
            if (!string.IsNullOrEmpty(_killRing))
            {
                InsertAtCursor(_killRing);
                key.Handled = true;
            }
        }
        else if (key.KeyCode == (KeyCode.B | KeyCode.AltMask))  // Alt+B = move word left
        {
            var text = _input.Text?.ToString() ?? string.Empty;
            var pos  = _input.CursorPosition;
            while (pos > 0 && text[pos - 1] == ' ') pos--;
            while (pos > 0 && text[pos - 1] != ' ') pos--;
            _input.CursorPosition = pos;
            key.Handled = true;
        }
        else if (key.KeyCode == (KeyCode.F | KeyCode.AltMask))  // Alt+F = move word right
        {
            var text = _input.Text?.ToString() ?? string.Empty;
            var pos  = _input.CursorPosition;
            while (pos < text.Length && text[pos] == ' ') pos++;
            while (pos < text.Length && text[pos] != ' ') pos++;
            _input.CursorPosition = pos;
            key.Handled = true;
        }
    }

    private void InsertAtCursor(string s)
    {
        var text = _input.Text?.ToString() ?? string.Empty;
        var pos  = _input.CursorPosition;
        _input.Text = text[..pos] + s + text[pos..];
        _input.CursorPosition = pos + s.Length;
    }

    private void TabComplete()
    {
        var text  = _input.Text?.ToString() ?? string.Empty;
        var pos   = _input.CursorPosition;
        var word  = GetCurrentWord(text, pos, out var wordStart);
        if (string.IsNullOrEmpty(word)) return;

        // Determine the directory to search in
        string searchDir, prefix;
        if (word.Contains(System.IO.Path.DirectorySeparatorChar))
        {
            searchDir = System.IO.Path.GetDirectoryName(word) ?? _currentDirectory;
            prefix    = System.IO.Path.GetFileName(word);
        }
        else
        {
            searchDir = _currentDirectory;
            prefix    = word;
        }

        if (!System.IO.Directory.Exists(searchDir)) return;

        var matches = System.IO.Directory
            .GetFileSystemEntries(searchDir, prefix + "*")
            .Select(System.IO.Path.GetFileName)
            .Where(n => n != null)
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();

        if (matches.Count == 0) return;

        if (matches.Count == 1)
        {
            // Single match: complete it
            var completed = System.IO.Path.Combine(searchDir == _currentDirectory ? string.Empty : searchDir, matches[0]);
            if (System.IO.Directory.Exists(System.IO.Path.Combine(searchDir, matches[0])))
                completed += System.IO.Path.DirectorySeparatorChar;
            _input.Text = text[..wordStart] + completed + text[pos..];
            _input.CursorPosition = wordStart + completed.Length;
        }
        else
        {
            // Multiple matches: complete to longest common prefix
            var lcp = LongestCommonPrefix(matches);
            if (lcp.Length > prefix.Length)
            {
                var completed = System.IO.Path.Combine(searchDir == _currentDirectory ? string.Empty : searchDir, lcp);
                _input.Text = text[..wordStart] + completed + text[pos..];
                _input.CursorPosition = wordStart + completed.Length;
            }
            // Show matches as a popup
            ShowCompletionPopup(matches);
        }
    }

    private static string GetCurrentWord(string text, int pos, out int wordStart)
    {
        var start = pos;
        while (start > 0 && text[start - 1] != ' ') start--;
        wordStart = start;
        return text[start..pos];
    }

    private static string LongestCommonPrefix(IReadOnlyList<string> strs)
    {
        if (strs.Count == 0) return string.Empty;
        var prefix = strs[0];
        for (int i = 1; i < strs.Count; i++)
        {
            int j = 0;
            while (j < prefix.Length && j < strs[i].Length &&
                   string.Compare(prefix[j].ToString(), strs[i][j].ToString(),
                       StringComparison.OrdinalIgnoreCase) == 0)
                j++;
            prefix = prefix[..j];
        }
        return prefix;
    }

    private void ShowCompletionPopup(IReadOnlyList<string> matches)
    {
        var parent = SuperView;
        if (parent == null) return;
        // Remove any previous completion popup
        HideHistoryPopup();

        var popupHeight = Math.Min(matches.Count + 2, 10);
        var popup = new Window
        {
            X      = Pos.Left(this),
            Y      = Pos.Top(this) - popupHeight,
            Width  = Width,
            Height = popupHeight,
            ColorScheme = McTheme.Dialog,
            Title = "Tab completion (Enter=select, Esc=close)",
        };
        var lv = new ListView
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(),
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(new ObservableCollection<string>(matches));
        lv.SelectedItem = 0;
        popup.Add(lv);
        lv.KeyDown += (_, k) =>
        {
            if (k.KeyCode == KeyCode.Enter)
            {
                var idx = lv.SelectedItem;
                if (idx >= 0 && idx < matches.Count)
                {
                    var text  = _input.Text?.ToString() ?? string.Empty;
                    var pos   = _input.CursorPosition;
                    GetCurrentWord(text, pos, out var wordStart);
                    var completed = matches[idx];
                    _input.Text = text[..wordStart] + completed + text[pos..];
                    _input.CursorPosition = wordStart + completed.Length;
                }
                HideHistoryPopup();
                k.Handled = true;
            }
            else if (k.KeyCode == KeyCode.Esc)
            {
                HideHistoryPopup(); k.Handled = true;
            }
        };
        _popupContainer = popup;
        parent.Add(popup);
        lv.SetFocus();
    }

    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0) return;
        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count - 1);
        _input.Text = _history[_history.Count - 1 - _historyIndex];
        _input.CursorPosition = _input.Text.Length;
    }

    // ── Inline history popup ─────────────────────────────────────────────────

    private void ShowHistoryPopup()
    {
        if (_history.Count == 0) return;
        var parent = SuperView;
        if (parent == null) return;

        HideHistoryPopup();

        var items = _history.AsEnumerable().Reverse().ToList();
        var popupHeight = Math.Min(items.Count + 2, 12);

        var popup = new Window
        {
            // Position just above this command-line view
            X      = Pos.Left(this),
            Y      = Pos.Top(this) - popupHeight,
            Width  = Width,
            Height = popupHeight,
            ColorScheme = McTheme.Dialog,
            Title = "Command history (Enter=select, Esc=close)",
        };

        _popupList = new ListView
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = McTheme.Panel,
        };
        _popupList.SetSource(new ObservableCollection<string>(items));
        _popupList.SelectedItem = 0;
        popup.Add(_popupList);

        _popupList.KeyDown += (_, k) =>
        {
            if (k.KeyCode == KeyCode.Enter)
            {
                var idx = _popupList.SelectedItem;
                if (idx >= 0 && idx < items.Count)
                    SetText(items[idx]);
                HideHistoryPopup();
                k.Handled = true;
            }
            else if (k.KeyCode == KeyCode.Esc)
            {
                HideHistoryPopup();
                k.Handled = true;
            }
        };

        _popupList.MouseClick += (_, me) =>
        {
            if (me.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
            {
                var idx = _popupList.SelectedItem;
                if (idx >= 0 && idx < items.Count)
                    SetText(items[idx]);
                HideHistoryPopup();
            }
        };

        _popupContainer = popup;
        parent.Add(popup);
        _popupList.SetFocus();
    }

    private void HideHistoryPopup()
    {
        if (_popupContainer != null)
        {
            SuperView?.Remove(_popupContainer);
            _popupContainer.Dispose();
            _popupContainer = null;
            _popupList = null;
        }
        _input.SetFocus();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetDirectory(string dir)
    {
        _currentDirectory = dir;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var display = dir.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + dir[home.Length..]
            : dir;
        var sep   = System.IO.Path.DirectorySeparatorChar;
        var parts = display.TrimEnd('/', '\\').Split(sep);
        display = parts.Length > 2
            ? "…" + sep + string.Join(sep.ToString(), parts[^2..])
            : display;
        _prompt.Text = display + "> ";
    }

    public IReadOnlyList<string> History => _history;

    public new string Text => _input.Text?.ToString() ?? string.Empty;

    public void SetText(string text)
    {
        _input.Text = text;
        _input.CursorPosition = _input.Text?.Length ?? 0;
        _input.SetFocus();
    }

    /// <summary>Appends text to the command line (e.g. paste filename via Ctrl+Enter). (#8)</summary>
    public void AppendText(string text)
    {
        var current = _input.Text?.ToString() ?? string.Empty;
        SetText(current + text);
    }

    public void Focus() => _input.SetFocus();
}
