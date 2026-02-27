using Terminal.Gui;

namespace Mc.Ui.Widgets;

/// <summary>
/// The F1–F10 button bar at the bottom of the screen.
/// Equivalent to WButtonBar in lib/widget/buttonbar.c.
/// </summary>
public sealed class ButtonBarView : View
{
    private readonly (string Label, string Action, Action Callback)[] _buttons;

    public ButtonBarView(params (string Label, string Action, Action Callback)[] buttons)
    {
        _buttons = buttons;
        Height = 1;
        Width = Dim.Fill();
        ColorScheme = McTheme.ButtonBar;
        CanFocus = false;
    }

    public static ButtonBarView CreateDefault(
        Action onHelp,
        Action onUserMenu,
        Action onView,
        Action onEdit,
        Action onCopy,
        Action onMove,
        Action onMkdir,
        Action onDelete,
        Action onMenu,
        Action onQuit)
    {
        return new ButtonBarView(
            ("1Help",   "F1",  onHelp),
            ("2Menu",   "F2",  onUserMenu),
            ("3View",   "F3",  onView),
            ("4Edit",   "F4",  onEdit),
            ("5Copy",   "F5",  onCopy),
            ("6RenMov", "F6",  onMove),
            ("7Mkdir",  "F7",  onMkdir),
            ("8Delete", "F8",  onDelete),
            ("9PullDn", "F9",  onMenu),
            ("10Quit",  "F10", onQuit)
        );
    }

    public void UpdateButton(int index, string label, Action callback)
    {
        if (index < 0 || index >= _buttons.Length) return;
        _buttons[index] = (_buttons[index].Label[..1] + label, _buttons[index].Action, callback);
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        var viewport = Viewport;

        int x = 0;
        int totalWidth = viewport.Width;
        int btnWidth = totalWidth / _buttons.Length;

        for (int i = 0; i < _buttons.Length; i++)
        {
            var (label, _, _) = _buttons[i];
            // Number part in bold/white on black
            Driver.SetAttribute(McTheme.ButtonBar.HotNormal);
            int numLen = 0;
            while (numLen < label.Length && char.IsDigit(label[numLen])) numLen++;

            Move(x, 0);
            Driver.AddStr(label[..numLen]);

            // Label part in black on cyan
            Driver.SetAttribute(McTheme.ButtonBar.Normal);
            var text = label[numLen..];
            if (text.Length > btnWidth - numLen - 1)
                text = text[..(btnWidth - numLen - 1)];
            Driver.AddStr(text.PadRight(btnWidth - numLen));

            x += btnWidth;
        }
        return false;
    }

    private static readonly Key[] _fKeys =
        [Key.F1, Key.F2, Key.F3, Key.F4, Key.F5,
         Key.F6, Key.F7, Key.F8, Key.F9, Key.F10];

    public bool HandleKey(Key key)
    {
        for (int i = 0; i < Math.Min(_buttons.Length, _fKeys.Length); i++)
        {
            if (key == _fKeys[i])
            {
                _buttons[i].Callback?.Invoke();
                return true;
            }
        }
        return false;
    }
}
