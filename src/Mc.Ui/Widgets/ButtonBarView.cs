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
        MouseClick += OnMouseClick;
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (!e.Flags.HasFlag(MouseFlags.Button1Clicked) &&
            !e.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
            return;

        int totalWidth = Viewport.Width;
        int count = _buttons.Length;
        if (count == 0 || totalWidth == 0) return;
        int baseWidth = totalWidth / count;
        if (baseWidth == 0) return;

        // All buttons are baseWidth wide; last button absorbs any remainder.
        // Math.Min clamps clicks on the last button's extra columns to index count-1.
        int idx = Math.Min(e.Position.X / baseWidth, count - 1);
        _buttons[idx].Callback?.Invoke();
        e.Handled = true;
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

        int totalWidth = viewport.Width;
        int count      = _buttons.Length;
        int baseWidth  = totalWidth / count;
        int remainder  = totalWidth % count; // extra pixels distributed to last button (#35)
        int x = 0;

        for (int i = 0; i < count; i++)
        {
            var (label, _, _) = _buttons[i];
            // Last button absorbs any remainder so bar fills exactly to screen edge
            int btnWidth = i == count - 1 ? baseWidth + remainder : baseWidth;

            // Digit part (F-key number) in white on black
            Driver.SetAttribute(McTheme.ButtonBar.HotNormal);
            int numLen = 0;
            while (numLen < label.Length && char.IsDigit(label[numLen])) numLen++;

            Move(x, 0);
            Driver.AddStr(label[..numLen]);

            // Label part in black on cyan
            Driver.SetAttribute(McTheme.ButtonBar.Normal);
            var text = label[numLen..];
            int labelRoom = btnWidth - numLen;
            if (text.Length > labelRoom) text = text[..labelRoom];
            Driver.AddStr(text.PadRight(labelRoom));

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
