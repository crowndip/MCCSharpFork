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
        else if (key.KeyCode == (KeyCode.H | KeyCode.CtrlMask) ||
                 (key == Key.CursorUp && string.IsNullOrEmpty(_input.Text?.ToString())))
        {
            ShowHistoryPopup();
            key.Handled = true;
        }
        else if (key == Key.CursorUp)
        {
            NavigateHistory(1);
            key.Handled = true;
        }
        else if (key == Key.CursorDown)
        {
            NavigateHistory(-1);
            key.Handled = true;
        }
        else if (key == Key.Esc)
        {
            if (_popupContainer != null) { HideHistoryPopup(); key.Handled = true; }
        }
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

    public string Text => _input.Text?.ToString() ?? string.Empty;

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
