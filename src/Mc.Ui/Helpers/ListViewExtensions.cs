using Terminal.Gui;

namespace Mc.Ui.Helpers;

/// <summary>
/// Extension methods for Terminal.Gui <see cref="ListView"/>.
/// </summary>
public static class ListViewExtensions
{
    /// <summary>
    /// Subscribes to both the <c>MouseWheel</c> and <c>MouseClick</c> events of
    /// <paramref name="lv"/> and moves the selection up or down on mouse wheel.
    /// <para>
    /// Terminal.Gui v2's built-in ListView wheel handler checks <c>me.Flags ==
    /// MouseFlags.WheeledDown</c> (exact equality), which can fail on Linux/ANSI
    /// drivers where extra flag bits are set. Using <c>HasFlag</c> here is more
    /// robust and keeps behaviour consistent with the file panels (3 lines/tick).
    /// </para>
    /// </summary>
    /// <param name="lv">The ListView to enable wheel scroll on.</param>
    /// <param name="linesPerTick">Lines to move per wheel detent (default 3).</param>
    public static void EnableWheelScroll(this ListView lv, int linesPerTick = 3)
    {
        void Handle(MouseEventArgs e)
        {
            if (e.Flags.HasFlag(MouseFlags.WheeledUp))
            {
                for (int i = 0; i < linesPerTick; i++) lv.MoveUp();
                e.Handled = true;
            }
            else if (e.Flags.HasFlag(MouseFlags.WheeledDown))
            {
                for (int i = 0; i < linesPerTick; i++) lv.MoveDown();
                e.Handled = true;
            }
        }

        // Linux/ANSI: wheel arrives via MouseClick.
        // Windows: wheel arrives via MouseWheel.
        // Subscribe to both so scrolling works on all platforms.
        lv.MouseClick += (_, e) => Handle(e);
        lv.MouseWheel += (_, e) => Handle(e);
    }
}
