using Terminal.Gui;

namespace Mc.Ui.Widgets;

/// <summary>
/// A <see cref="TextField"/> that shows a blinking block cursor when focused
/// and restores the terminal default when focus leaves.
/// Uses DECSCUSR escape sequence via <see cref="EscSeqUtils.CSI_SetCursorStyle"/>.
/// </summary>
internal class McTextField : TextField
{
    protected override void OnHasFocusChanged(bool newHasFocus, View previousFocused, View newFocused)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocused, newFocused);
        EscSeqUtils.CSI_SetCursorStyle(newHasFocus
            ? EscSeqUtils.DECSCUSR_Style.BlinkingUnderline
            : EscSeqUtils.DECSCUSR_Style.UserShape);
    }
}
