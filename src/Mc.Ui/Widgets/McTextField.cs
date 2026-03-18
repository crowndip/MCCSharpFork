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
        if (newHasFocus)
            EscSeqUtils.CSI_SetCursorStyle(EscSeqUtils.DECSCUSR_Style.BlinkingUnderline);
        else if (newFocused is not McTextField && newFocused is not Mc.Editor.EditorView)
            EscSeqUtils.CSI_SetCursorStyle(EscSeqUtils.DECSCUSR_Style.UserShape);
    }
}
