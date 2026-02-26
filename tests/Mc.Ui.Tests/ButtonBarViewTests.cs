using System.Reflection;
using Mc.Ui.Widgets;
using Terminal.Gui;
using Xunit;

namespace Mc.Ui.Tests;

/// <summary>
/// Tests for the F1–F10 button bar at the bottom of the file manager.
/// Verifies each button is present (visible), routes key presses to its
/// callback (clickable), and that the callback can be updated (configurable).
/// </summary>
[Collection("TUI Tests")]
public sealed class ButtonBarViewTests
{
    // Terminal.Gui is initialised once for the whole collection (TuiFixture.cs).
    public ButtonBarViewTests(ApplicationFixture _) { }

    // ------------------------------------------------------------------
    // Helper: build a ButtonBarView with individually tracked callbacks
    // ------------------------------------------------------------------

    private record ButtonCallbacks(
        Action Help, Action UserMenu, Action View, Action Edit, Action Copy,
        Action Move, Action Mkdir, Action Delete, Action Menu, Action Quit);

    private static (ButtonBarView bar, bool[] triggered) BuildBar()
    {
        var triggered = new bool[10];
        var bar = ButtonBarView.CreateDefault(
            onHelp:     () => triggered[0] = true,
            onUserMenu: () => triggered[1] = true,
            onView:     () => triggered[2] = true,
            onEdit:     () => triggered[3] = true,
            onCopy:     () => triggered[4] = true,
            onMove:     () => triggered[5] = true,
            onMkdir:    () => triggered[6] = true,
            onDelete:   () => triggered[7] = true,
            onMenu:     () => triggered[8] = true,
            onQuit:     () => triggered[9] = true
        );
        return (bar, triggered);
    }

    // ------------------------------------------------------------------
    // Retrieve internal _buttons array via reflection
    // ------------------------------------------------------------------

    private static (string Label, string Action, Action Callback)[] GetButtons(ButtonBarView bar)
    {
        var fi = typeof(ButtonBarView).GetField(
            "_buttons", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return ((string Label, string Action, Action Callback)[])fi.GetValue(bar)!;
    }

    // ==================================================================
    // VISIBILITY — each button must have the right label
    // ==================================================================

    [Fact]
    public void F1_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("1Help", GetButtons(bar)[0].Label);
    }

    [Fact]
    public void F2_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("2Menu", GetButtons(bar)[1].Label);
    }

    [Fact]
    public void F3_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("3View", GetButtons(bar)[2].Label);
    }

    [Fact]
    public void F4_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("4Edit", GetButtons(bar)[3].Label);
    }

    [Fact]
    public void F5_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("5Copy", GetButtons(bar)[4].Label);
    }

    [Fact]
    public void F6_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("6Move", GetButtons(bar)[5].Label);
    }

    [Fact]
    public void F7_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("7Mkdir", GetButtons(bar)[6].Label);
    }

    [Fact]
    public void F8_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("8Delete", GetButtons(bar)[7].Label);
    }

    [Fact]
    public void F9_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("9Menu", GetButtons(bar)[8].Label);
    }

    [Fact]
    public void F10_Button_HasCorrectLabel()
    {
        var (bar, _) = BuildBar();
        Assert.Equal("10Quit", GetButtons(bar)[9].Label);
    }

    [Fact]
    public void CreateDefault_Produces_ExactlyTenButtons()
    {
        var (bar, _) = BuildBar();
        Assert.Equal(10, GetButtons(bar).Length);
    }

    // ==================================================================
    // CLICKABLE + WORKS — HandleKey must invoke the correct callback
    // ==================================================================

    [Fact]
    public void HandleKey_F1_InvokesHelpCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F1);
        Assert.True(handled, "HandleKey should return true for F1");
        Assert.True(triggered[0], "F1 (Help) callback must be invoked");
    }

    [Fact]
    public void HandleKey_F2_InvokesUserMenuCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F2);
        Assert.True(handled);
        Assert.True(triggered[1], "F2 (Menu) callback must be invoked");
    }

    [Fact]
    public void HandleKey_F3_InvokesViewCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F3);
        Assert.True(handled);
        Assert.True(triggered[2], "F3 (View) callback must be invoked");
    }

    [Fact]
    public void HandleKey_F4_InvokesEditCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F4);
        Assert.True(handled);
        Assert.True(triggered[3], "F4 (Edit) callback must be invoked");
    }

    [Fact]
    public void HandleKey_F5_InvokesCopyCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F5);
        Assert.True(handled);
        Assert.True(triggered[4], "F5 (Copy) callback must be invoked");
    }

    [Fact]
    public void HandleKey_F6_InvokesMoveCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F6);
        Assert.True(handled);
        Assert.True(triggered[5], "F6 (Move) callback must be invoked");
    }

    [Fact]
    public void HandleKey_F7_InvokesMkdirCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F7);
        Assert.True(handled);
        Assert.True(triggered[6], "F7 (Mkdir) callback must be invoked");
    }

    [Fact]
    public void HandleKey_F8_InvokesDeleteCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F8);
        Assert.True(handled);
        Assert.True(triggered[7], "F8 (Delete) callback must be invoked");
    }

    [Fact]
    public void HandleKey_F9_InvokesMenuCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F9);
        Assert.True(handled);
        Assert.True(triggered[8], "F9 (Menu) callback must be invoked");
    }

    [Fact]
    public void HandleKey_F10_InvokesQuitCallback()
    {
        var (bar, triggered) = BuildBar();
        var handled = bar.HandleKey(Key.F10);
        Assert.True(handled);
        Assert.True(triggered[9], "F10 (Quit) callback must be invoked");
    }

    [Fact]
    public void HandleKey_OnlyTheMatchingCallback_IsInvoked()
    {
        var (bar, triggered) = BuildBar();
        bar.HandleKey(Key.F3);   // View
        Assert.False(triggered[0], "F1 must NOT fire when F3 is pressed");
        Assert.True(triggered[2],  "F3 must fire");
        Assert.False(triggered[9], "F10 must NOT fire when F3 is pressed");
    }

    [Fact]
    public void HandleKey_NonFunctionKey_ReturnsFalse()
    {
        var (bar, _) = BuildBar();
        Assert.False(bar.HandleKey(Key.A), "Letter keys must not be handled by the button bar");
    }

    // ==================================================================
    // UpdateButton — button can be relabelled and re-wired at runtime
    // ==================================================================

    [Fact]
    public void UpdateButton_ChangesCallback()
    {
        var (bar, triggered) = BuildBar();
        bool newCalled = false;
        bar.UpdateButton(0, "NewAction", () => newCalled = true);
        bar.HandleKey(Key.F1);
        Assert.True(newCalled,     "Updated callback must be called");
        Assert.False(triggered[0], "Old callback must NOT be called after update");
    }

    [Fact]
    public void UpdateButton_PreservesLabelNumberPrefix()
    {
        var (bar, _) = BuildBar();
        bar.UpdateButton(0, "Custom", () => { });
        // The number prefix (F-key digit) must be preserved
        Assert.StartsWith("1", GetButtons(bar)[0].Label);
        Assert.Contains("Custom", GetButtons(bar)[0].Label);
    }

    [Fact]
    public void UpdateButton_OutOfRange_DoesNotThrow()
    {
        var (bar, _) = BuildBar();
        // Should silently ignore out-of-range indices
        var ex = Record.Exception(() => bar.UpdateButton(99, "X", () => { }));
        Assert.Null(ex);
    }
}
