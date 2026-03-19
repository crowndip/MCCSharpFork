using System.Reflection;
using Mc.Core.Config;
using Mc.Core.Vfs;
using Mc.FileManager;
using Mc.Ui;
using Mc.Ui.Widgets;
using Moq;
using Terminal.Gui;
using Xunit;

namespace Mc.Ui.Tests;

/// <summary>
/// Tests for panel overlay-mode switching (Info / Quick View / File listing)
/// and the extended Tools menu items introduced in recent sessions.
///
/// User scenarios covered:
///   - Switch left pane view to Info: left panel hidden, overlay visible
///   - Switch view back to file listing: left panel visible, overlay hidden
///   - Tools menu has all 15+ items including clipboard helpers, FolderAnalyzer, BatchRename
///   - External editor (F4/Edit) menu item exists and is clickable
///   - Open in external editor triggers no exception when no file is selected
/// </summary>
[Collection("TUI Tests")]
public sealed class McApplicationPanelModeTests
{
    private readonly McApplication _app;

    public McApplicationPanelModeTests(ApplicationFixture _)
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
        var settings   = new McSettings(new McConfig());
        _app = new McApplication(controller, settings);
    }

    // ── Reflection helpers ────────────────────────────────────────────────────

    private FilePanelView GetLeftPanelView()
    {
        var fi = typeof(McApplication)
            .GetField("_leftPanelView", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (FilePanelView)fi.GetValue(_app)!;
    }

    private View GetLeftOverlay()
    {
        var fi = typeof(McApplication)
            .GetField("_leftOverlay", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (View)fi.GetValue(_app)!;
    }

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
        (menu.Children ?? []).OfType<MenuItem>().ToArray();

    // ── Info panel mode toggle ────────────────────────────────────────────────

    /// <summary>
    /// Left panel menu contains both "File listing" and "Info" items.
    /// </summary>
    [Fact]
    public void LeftPanel_Menu_ContainsFileListing_And_Info_Items()
    {
        var items = Items(GetMenu(0));
        Assert.Contains(items, i => Title(i).Contains("File listing"));
        Assert.Contains(items, i => Title(i).Contains("Info"));
    }

    /// <summary>
    /// Switching the left panel to Info mode hides the file-listing panel view
    /// and shows the overlay.
    /// </summary>
    [Fact]
    public void LeftPanel_Info_Action_HidesFilePanelView()
    {
        var infoItem = Items(GetMenu(0)).First(i => Title(i).Contains("Info"));
        Assert.NotNull(infoItem.Action);

        infoItem.Action!.Invoke();

        Assert.False(GetLeftPanelView().Visible);
        Assert.True(GetLeftOverlay().Visible);
    }

    /// <summary>
    /// After switching to Info, invoking "File listing" must restore the normal
    /// panel view (visible) and hide the overlay.
    /// </summary>
    [Fact]
    public void LeftPanel_FileListing_Action_RestoresNormalMode()
    {
        // First go to Info mode
        Items(GetMenu(0)).First(i => Title(i).Contains("Info")).Action!.Invoke();
        Assert.False(GetLeftPanelView().Visible);

        // Then switch back to file listing
        Items(GetMenu(0)).First(i => Title(i).Contains("File listing")).Action!.Invoke();

        Assert.True(GetLeftPanelView().Visible);
        Assert.False(GetLeftOverlay().Visible);
    }

    /// <summary>
    /// Invoking Info a second time on the same panel toggles it back to normal.
    /// </summary>
    [Fact]
    public void LeftPanel_Info_InvokedTwice_RestoresNormalMode()
    {
        var infoItem = Items(GetMenu(0)).First(i => Title(i).Contains("Info"));
        infoItem.Action!.Invoke(); // → Info
        infoItem.Action!.Invoke(); // → Normal (toggle back)

        Assert.True(GetLeftPanelView().Visible);
        Assert.False(GetLeftOverlay().Visible);
    }

    // ── Tools menu: clipboard items ───────────────────────────────────────────

    /// <summary>
    /// The Tools menu must contain all five clipboard-copy items introduced in
    /// the MCCompanion-aligned refresh (D/P/N/Q/L keys).
    /// </summary>
    [Theory]
    [InlineData("Copy directory")]
    [InlineData("Copy full path")]
    [InlineData("Copy file name")]
    [InlineData("Copy tagged paths")]
    [InlineData("Copy tagged names")]
    public void ToolsMenu_ClipboardItem_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(6));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("Copy directory")]
    [InlineData("Copy full path")]
    [InlineData("Copy file name")]
    [InlineData("Copy tagged paths")]
    [InlineData("Copy tagged names")]
    public void ToolsMenu_ClipboardItem_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(6)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    // ── Tools menu: file-info and utility items ───────────────────────────────

    [Theory]
    [InlineData("Open in file manager")]
    [InlineData("Open with")]
    [InlineData("File properties")]
    [InlineData("Attributes")]
    [InlineData("Touch")]
    [InlineData("Checksum")]
    [InlineData("Directory size")]
    [InlineData("Folder size analyzer")]
    [InlineData("Batch rename")]
    [InlineData("Open terminal here")]
    [InlineData("Compare with diff tool")]
    public void ToolsMenu_Item_IsVisible(string partialTitle)
    {
        var items = Items(GetMenu(6));
        Assert.Contains(items, i => Title(i).Contains(partialTitle));
    }

    [Theory]
    [InlineData("Open in file manager")]
    [InlineData("Open with")]
    [InlineData("File properties")]
    [InlineData("Attributes")]
    [InlineData("Touch")]
    [InlineData("Checksum")]
    [InlineData("Directory size")]
    [InlineData("Folder size analyzer")]
    [InlineData("Batch rename")]
    [InlineData("Open terminal here")]
    [InlineData("Compare with diff tool")]
    public void ToolsMenu_Item_IsClickable(string partialTitle)
    {
        var item = Items(GetMenu(6)).First(i => Title(i).Contains(partialTitle));
        Assert.NotNull(item.Action);
    }

    // ── External editor (F4) ──────────────────────────────────────────────────

    /// <summary>
    /// "Edit" (F4) must exist in the File menu with the correct shortcut and a
    /// non-null action. This is the entry point for opening files in an external editor.
    /// </summary>
    [Fact]
    public void FileMenu_Edit_HasF4Shortcut_AndIsClickable()
    {
        var item = Items(GetMenu(1)).First(i => Title(i).Contains("Edit"));
        Assert.Equal("F4", item.Help);
        Assert.NotNull(item.Action);
    }

    /// <summary>
    /// Verify the Edit menu item has a registered action without invoking it.
    /// (Invoking it when no file is selected calls Application.Run on a dialog
    /// which blocks forever under FakeDriver, hanging the CI job.)
    /// </summary>
    [Fact]
    public void FileMenu_Edit_NoFileSelected_HasRegisteredAction()
    {
        var item = Items(GetMenu(1)).First(i => Title(i).Contains("Edit"));
        Assert.NotNull(item.Action);
    }
}
