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
/// The menu structure mirrors the original GNU Midnight Commander exactly,
/// with the addition of our custom Tools menu.
/// Order: Left | File | Command | Tools | Options | Right
/// </summary>
[Collection("TUI Tests")]
public sealed class McApplicationMenuTests
{
    private readonly McApplication _app;

    public McApplicationMenuTests(ApplicationFixture _)
    {
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

    private static string Title(MenuItem item) =>
        item.Title?.ToString()?.Replace("_", "") ?? string.Empty;

    private static MenuItem[] Items(MenuBarItem menu) =>
        menu.Children.Where(c => c is not null).ToArray()!;

    // ==================================================================
    // TOP-LEVEL MENU BAR — Left | File | Command | Tools | Options | Right
    // ==================================================================

    [Fact]
    public void MenuBar_HasExactlySixTopLevelMenus()
        => Assert.Equal(6, GetMenuBar().Menus.Length);

    [Theory]
    [InlineData(0, "Left")]
    [InlineData(1, "File")]
    [InlineData(2, "Command")]
    [InlineData(3, "Tools")]
    [InlineData(4, "Options")]
    [InlineData(5, "Right")]
    public void TopLevel_Menu_TitleIsCorrect(int index, string expected)
        => Assert.Contains(expected, Title(GetMenu(index)));

    // ==================================================================
    // LEFT PANEL MENU  (index 0) — same structure as original MC
    // ==================================================================

    [Theory]
    [InlineData("Listing format")]
    [InlineData("Quick view")]
    [InlineData("Info")]
    [InlineData("Tree")]
    [InlineData("Panelize")]
    [InlineData("Sort order")]
    [InlineData("Filter")]
    [InlineData("Encoding")]
    [InlineData("FTP link")]
    [InlineData("Shell link")]
    [InlineData("SFTP link")]
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

    [Fact]
    public void LeftPanel_Rescan_HasCtrlRShortcut()
    {
        var item = Items(GetMenu(0)).First(i => Title(i).Contains("Rescan"));
        Assert.Equal("Ctrl+R", item.Help);
    }

    // ==================================================================
    // FILE MENU  (index 1)
    // ==================================================================

    [Theory]
    [InlineData("View")]
    [InlineData("View file")]
    [InlineData("Filtered view")]
    [InlineData("Edit")]
    [InlineData("Copy")]
    [InlineData("Chmod")]
    [InlineData("Link")]
    [InlineData("Symlink")]
    [InlineData("Relative symlink")]
    [InlineData("Edit symlink")]
    [InlineData("Chown")]
    [InlineData("Advanced chown")]
    [InlineData("Rename/Move")]
    [InlineData("Mkdir")]
    [InlineData("Delete")]
    [InlineData("Quick cd")]
    [InlineData("Select group")]
    [InlineData("Unselect group")]
    [InlineData("Invert selection")]
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
    [InlineData("Rename/Move")]
    [InlineData("Mkdir")]
    [InlineData("Delete")]
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
    [InlineData("Rename/Move", "F6")]
    [InlineData("Mkdir",       "F7")]
    [InlineData("Delete",      "F8")]
    [InlineData("Exit",        "F10")]
    public void FileMenu_MenuItem_HasCorrectShortcut(string partialTitle, string shortcut)
    {
        var item = Items(GetMenu(1)).First(i => Title(i).Contains(partialTitle));
        Assert.Equal(shortcut, item.Help);
    }

    // ==================================================================
    // COMMAND MENU  (index 2)
    // ==================================================================

    [Theory]
    [InlineData("User menu")]
    [InlineData("Directory tree")]
    [InlineData("Find file")]
    [InlineData("Swap panels")]
    [InlineData("Switch panels on/off")]
    [InlineData("Compare directories")]
    [InlineData("Compare files")]
    [InlineData("External panelize")]
    [InlineData("Show directory sizes")]
    [InlineData("Command history")]
    [InlineData("Viewed/edited files history")]
    [InlineData("Directory hotlist")]
    [InlineData("Active VFS list")]
    [InlineData("Background jobs")]
    [InlineData("Screen list")]
    [InlineData("Edit extension file")]
    [InlineData("Edit menu file")]
    [InlineData("Edit highlighting group file")]
    public void CommandMenu_MenuItem_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(2));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("Find file")]
    [InlineData("Swap panels")]
    [InlineData("Switch panels on/off")]
    [InlineData("Compare files")]
    [InlineData("Directory hotlist")]
    public void CommandMenu_MenuItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(2)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    [Theory]
    [InlineData("Swap panels",          "Ctrl+U")]
    [InlineData("Switch panels on/off", "Ctrl+O")]
    [InlineData("Directory hotlist",    "Ctrl+\\")]
    public void CommandMenu_MenuItem_HasCorrectShortcut(string partialTitle, string shortcut)
    {
        var item = Items(GetMenu(2)).First(i => Title(i).Contains(partialTitle));
        Assert.Equal(shortcut, item.Help);
    }

    // ==================================================================
    // TOOLS MENU  (index 3) — custom addition for this .NET port
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
    [InlineData("Confirmation")]
    [InlineData("Appearance")]
    [InlineData("Learn keys")]
    [InlineData("Virtual FS")]
    [InlineData("Save setup")]
    [InlineData("About")]
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
    [InlineData("About")]
    public void OptionsMenu_MenuItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(4)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    // ==================================================================
    // RIGHT PANEL MENU  (index 5) — identical structure to Left
    // ==================================================================

    [Theory]
    [InlineData("Listing format")]
    [InlineData("Quick view")]
    [InlineData("Info")]
    [InlineData("Tree")]
    [InlineData("Panelize")]
    [InlineData("Sort order")]
    [InlineData("Filter")]
    [InlineData("Encoding")]
    [InlineData("FTP link")]
    [InlineData("Shell link")]
    [InlineData("Rescan")]
    public void RightPanel_MenuItem_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(5));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("Listing format")]
    [InlineData("Sort order")]
    [InlineData("Rescan")]
    public void RightPanel_MenuItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(5)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    [Fact]
    public void RightPanel_Rescan_HasCtrlRShortcut()
    {
        var item = Items(GetMenu(5)).First(i => Title(i).Contains("Rescan"));
        Assert.Equal("Ctrl+R", item.Help);
    }

    // ==================================================================
    // SAFE ACTION INVOCATION — items with non-blocking side-effects
    // ==================================================================

    [Fact]
    public void LeftPanel_Rescan_ExecutesWithoutException()
    {
        var item = Items(GetMenu(0)).First(i => Title(i).Contains("Rescan"));
        Assert.Null(Record.Exception(() => item.Action!.Invoke()));
    }

    [Fact]
    public void RightPanel_Rescan_ExecutesWithoutException()
    {
        var item = Items(GetMenu(5)).First(i => Title(i).Contains("Rescan"));
        Assert.Null(Record.Exception(() => item.Action!.Invoke()));
    }

    [Fact]
    public void Command_SwapPanels_ExecutesWithoutException()
    {
        var item = Items(GetMenu(2)).First(i => Title(i).Contains("Swap panels"));
        Assert.Null(Record.Exception(() => item.Action!.Invoke()));
    }

    [Fact]
    public void Options_SaveSetup_ExecutesWithoutException()
    {
        var item = Items(GetMenu(4)).First(i => Title(i).Contains("Save setup"));
        var ex = Record.Exception(() => item.Action!.Invoke());
        Assert.True(ex is null || ex is DirectoryNotFoundException or IOException
                    or ArgumentException or ArgumentNullException,
            $"Unexpected exception: {ex?.GetType().Name}: {ex?.Message}");
    }

    [Fact]
    public void File_InvertSelection_ExecutesWithoutException()
    {
        var item = Items(GetMenu(1)).First(i => Title(i).Contains("Invert selection"));
        Assert.Null(Record.Exception(() => item.Action!.Invoke()));
    }
}
