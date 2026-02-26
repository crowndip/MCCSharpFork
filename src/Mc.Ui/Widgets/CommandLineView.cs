using Terminal.Gui;

namespace Mc.Ui.Widgets;

/// <summary>
/// Command input line at the bottom of the file manager.
/// Equivalent to WInput for the command line in the original C codebase.
/// </summary>
public sealed class CommandLineView : View
{
    private readonly TextField _input;
    private readonly Label _prompt;
    private readonly List<string> _history = [];
    private int _historyIndex = -1;

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
            var text = _input.Text?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                _history.Add(text);
                _historyIndex = -1;
                _input.Text = string.Empty;
                CommandEntered?.Invoke(this, text);
            }
        }
        else if (key == Key.CursorUp)
        {
            NavigateHistory(1);
        }
        else if (key == Key.CursorDown)
        {
            NavigateHistory(-1);
        }
    }

    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0) return;
        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count - 1);
        _input.Text = _history[_history.Count - 1 - _historyIndex];
        _input.CursorPosition = _input.Text.Length;
    }

    public void SetDirectory(string dir)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var display = dir.StartsWith(home)
            ? "~" + dir[home.Length..]
            : dir;
        // Keep prompt short: show only last two path components
        var parts = display.TrimEnd('/').Split('/');
        display = parts.Length > 2
            ? "…/" + string.Join("/", parts[^2..])
            : display;
        _prompt.Text = display + "$ ";
    }

    public void Focus() => _input.SetFocus();
}
