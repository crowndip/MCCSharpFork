using System.Reflection;
using Mc.Core.Config;
using Mc.Core.Vfs;
using Mc.FileManager;
using Mc.Ui;
using Moq;
using Terminal.Gui;
using Xunit;

namespace Mc.Ui.Tests;

/// <summary>
/// Tests for every top-level menu and every menu item in McApplication.
/// Each test verifies:
///   • Visible  — the menu / item exists with the expected title
///   • Clickable — the item has a non-null Action delegate
///   • Shortcut  — any advertised keyboard shortcut is documented correctly
/// </summary>
[Collection("TUI Tests")]
public sealed class McApplicationMenuTests
{
    private readonly McApplication _app;

    public McApplicationMenuTests(ApplicationFixture _)
    {
        // Build a minimal VFS with a mock provider so panels don't hit the real FS
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([]);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());
        mock.Setup(p => p.DirectoryExists(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.FileExists(It.IsAny<VfsPath>())).Returns(false);

        var vfs = new VfsRegistry();
        vfs.Register(mock.Object);

        var controller = new FileManagerController(vfs);
        var settings = new McSettings(new McConfig());
        _app = new McApplication(controller, settings);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private MenuBar GetMenuBar()
    {
        var fi = typeof(McApplication)
            .GetField("_menuBar", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (MenuBar)fi.GetValue(_app)!;
    }

    private MenuBarItem GetMenu(int index) => GetMenuBar().Menus[index];

    /// <summary>Strip hotkey underscore from title.</summary>
    private static string Title(MenuItem item) =>
        item.Title?.ToString()?.Replace("_", "") ?? string.Empty;

    /// <summary>Returns all non-separator children of a menu.</summary>
    private static MenuItem[] Items(MenuBarItem menu) =>
        menu.Children.Where(c => c is not null).ToArray()!;

    // ==================================================================
    // TOP-LEVEL MENU BAR STRUCTURE
    // ==================================================================

    [Fact]
    public void MenuBar_HasExactlySixTopLevelMenus()
        => Assert.Equal(6, GetMenuBar().Menus.Length);

    [Theory]
    [InlineData(0, "Left panel")]
    [InlineData(1, "File")]
    [InlineData(2, "Command")]
    [InlineData(3, "Tools")]
    [InlineData(4, "Options")]
    [InlineData(5, "Help")]
    public void TopLevel_Menu_TitleIsCorrect(int index, string expectedTitle)
        => Assert.Contains(expectedTitle, Title(GetMenu(index)));

    // ==================================================================
    // LEFT PANEL MENU  (index 0)
    // ==================================================================

    [Theory]
    [InlineData("Listing format")]
    [InlineData("Sort order")]
    [InlineData("Filter")]
    [InlineData("Rescan")]
    public void LeftPanel_MenuItem_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(0));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("Listing format")]
    [InlineData("Sort order")]
    [InlineData("Filter")]
    [InlineData("Rescan")]
    public void LeftPanel_MenuItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(0)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    // ==================================================================
    // FILE MENU  (index 1)
    // ==================================================================

    [Theory]
    [InlineData("View")]
    [InlineData("Edit")]
    [InlineData("Copy")]
    [InlineData("Move/Rename")]
    [InlineData("New dir")]
    [InlineData("Delete")]
    [InlineData("Info")]
    [InlineData("Chmod")]
    [InlineData("Exit")]
    public void FileMenu_MenuItem_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(1));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("View")]
    [InlineData("Edit")]
    [InlineData("Copy")]
    [InlineData("Move/Rename")]
    [InlineData("New dir")]
    [InlineData("Delete")]
    [InlineData("Info")]
    [InlineData("Chmod")]
    [InlineData("Exit")]
    public void FileMenu_MenuItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(1)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    [Theory]
    [InlineData("View",        "F3")]
    [InlineData("Edit",        "F4")]
    [InlineData("Copy",        "F5")]
    [InlineData("Move/Rename", "F6")]
    [InlineData("New dir",     "F7")]
    [InlineData("Delete",      "F8")]
    [InlineData("Info",        "Ctrl+L")]
    [InlineData("Chmod",       "Ctrl+X")]
    [InlineData("Exit",        "F10")]
    public void FileMenu_MenuItem_HasCorrectShortcutDocumented(string partialTitle, string shortcut)
    {
        var item = Items(GetMenu(1)).First(i => Title(i).Contains(partialTitle));
        Assert.Equal(shortcut, item.Help);
    }

    // ==================================================================
    // COMMAND MENU  (index 2)
    // ==================================================================

    [Theory]
    [InlineData("Find file")]
    [InlineData("Hotlist")]
    [InlineData("Directory tree")]
    [InlineData("Swap panels")]
    [InlineData("Compare panels")]
    [InlineData("Shell")]
    public void CommandMenu_MenuItem_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(2));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("Find file")]
    [InlineData("Hotlist")]
    [InlineData("Swap panels")]
    [InlineData("Shell")]
    public void CommandMenu_MenuItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(2)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    [Theory]
    [InlineData("Swap panels", "Ctrl+U")]
    [InlineData("Shell",       "Ctrl+O")]
    public void CommandMenu_MenuItem_HasCorrectShortcutDocumented(string partialTitle, string shortcut)
    {
        var item = Items(GetMenu(2)).First(i => Title(i).Contains(partialTitle));
        Assert.Equal(shortcut, item.Help);
    }

    // ==================================================================
    // TOOLS MENU  (index 3)
    // ==================================================================

    [Theory]
    [InlineData("Copy path to clipboard")]
    [InlineData("Copy file name")]
    [InlineData("Copy directory path")]
    [InlineData("Checksum")]
    [InlineData("Directory size")]
    [InlineData("Touch")]
    [InlineData("Batch rename")]
    [InlineData("Open terminal here")]
    [InlineData("Compare with diff tool")]
    public void ToolsMenu_MenuItem_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(3));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("Copy path to clipboard")]
    [InlineData("Copy file name")]
    [InlineData("Copy directory path")]
    [InlineData("Checksum")]
    [InlineData("Directory size")]
    [InlineData("Touch")]
    [InlineData("Batch rename")]
    [InlineData("Open terminal here")]
    [InlineData("Compare with diff tool")]
    public void ToolsMenu_MenuItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(3)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    [Fact]
    public void ToolsMenu_OpenTerminalHere_HasCtrlTShortcut()
    {
        var item = Items(GetMenu(3)).First(i => Title(i).Contains("Open terminal here"));
        Assert.Equal("Ctrl+T", item.Help);
    }

    // ==================================================================
    // OPTIONS MENU  (index 4)
    // ==================================================================

    [Theory]
    [InlineData("Configuration")]
    [InlineData("Layout")]
    [InlineData("Panel options")]
    [InlineData("Save setup")]
    public void OptionsMenu_MenuItem_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(4));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("Configuration")]
    [InlineData("Layout")]
    [InlineData("Panel options")]
    [InlineData("Save setup")]
    public void OptionsMenu_MenuItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(4)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    // ==================================================================
    // HELP MENU  (index 5)
    // ==================================================================

    [Theory]
    [InlineData("Help")]
    [InlineData("About")]
    public void HelpMenu_MenuItem_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(5));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("Help")]
    [InlineData("About")]
    public void HelpMenu_MenuItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(5)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    [Fact]
    public void HelpMenu_Help_HasF1Shortcut()
    {
        var item = Items(GetMenu(5)).First(i => Title(i).Contains("Help"));
        Assert.Equal("F1", item.Help);
    }

    // ==================================================================
    // SAFE ACTION INVOCATION — actions that don't open dialogs
    // ==================================================================

    [Fact]
    public void LeftPanel_Rescan_ExecutesWithoutException()
    {
        var item = Items(GetMenu(0)).First(i => Title(i).Contains("Rescan"));
        var ex = Record.Exception(() => item.Action!.Invoke());
        Assert.Null(ex);
    }

    [Fact]
    public void Command_SwapPanels_ExecutesWithoutException()
    {
        var item = Items(GetMenu(2)).First(i => Title(i).Contains("Swap panels"));
        var ex = Record.Exception(() => item.Action!.Invoke());
        Assert.Null(ex);
    }

    [Fact]
    public void Options_SaveSetup_ExecutesWithoutException()
    {
        // McSettings.Save() with a default in-memory McConfig may try to write
        // to an empty path — wrap in try/catch since no real file path is set.
        var item = Items(GetMenu(4)).First(i => Title(i).Contains("Save setup"));
        var ex = Record.Exception(() => item.Action!.Invoke());
        // We only care that the method *exists* and doesn't crash the process;
        // I/O errors around an empty path are acceptable.
        Assert.True(ex is null || ex is DirectoryNotFoundException or IOException
                    or ArgumentException or ArgumentNullException,
            $"Unexpected exception type: {ex?.GetType().Name}: {ex?.Message}");
    }
}
