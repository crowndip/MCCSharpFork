using System.Collections.ObjectModel;
using Mc.Core.Config;
using Mc.Core.Models;
using Mc.Core.Utilities;
using Mc.Core.Vfs;
using Mc.DiffViewer;
using Mc.Editor;
using Mc.FileManager;
using Mc.Ui.Dialogs;
using Mc.Ui.Helpers;
using Mc.Ui.Widgets;
using Mc.Viewer;
using Terminal.Gui;

namespace Mc.Ui;

/// <summary>
/// Main application window.
/// Equivalent to src/main.c + src/filemanager/layout.c in the original C codebase.
/// Orchestrates two panels, menus, key bindings, and all operations.
/// </summary>
public sealed class McApplication : Toplevel
{
    private readonly FileManagerController _controller;
    private readonly McSettings _settings;

    private FilePanelView _leftPanelView = null!;
    private FilePanelView _rightPanelView = null!;
    private ButtonBarView _buttonBar = null!;
    private CommandLineView _commandLine = null!;
    private MenuBar _menuBar = null!;

    // File history: most-recently-used first
    private readonly List<string> _viewedFiles = [];
    private readonly List<string> _editedFiles = [];

    // Ctrl+X prefix key state (like the Ctrl+X submap in original MC)
    private bool _ctrlXPrefix = false;

    // Background file-operation jobs (equivalent to background.c in original MC)
    private sealed class BackgroundJob
    {
        public string Name    { get; init; } = string.Empty;
        public string Status  { get; set;  } = "Running…";
        public bool   Running { get; set;  } = true;
        public CancellationTokenSource Cts  { get; } = new();
        public Task?  Task    { get; set;  }
    }
    private readonly List<BackgroundJob> _backgroundJobs = [];

    // Panel overlay modes (Quick View / Info) — mirrors list_quick_view / info_panel in MC
    private enum PanelDisplayMode { Normal, QuickView, Info, Tree }
    private PanelDisplayMode _leftMode  = PanelDisplayMode.Normal;
    private PanelDisplayMode _rightMode = PanelDisplayMode.Normal;
    private View _leftOverlay  = null!;
    private View _rightOverlay = null!;

    public McApplication(FileManagerController controller, McSettings settings)
    {
        _controller = controller;
        _settings = settings;

        controller.StatusMessage += (_, msg) => ShowStatus(msg);
        controller.OperationError += (_, ex) => MessageDialog.Error(ex.Message);

        BuildLayout();
        Application.AddIdle(() =>
        {
            // Update panel titles on idle after startup
            _leftPanelView.SetNeedsDraw();
            _rightPanelView.SetNeedsDraw();
            return false;
        });
    }

    private void BuildLayout()
    {
        ColorScheme = McTheme.Panel;

        // Menu bar — mirrors original MC: Left | File | Command | [Tools] | Options | Right
        // Panel menu items are identical for Left and Right
        // Panel menu matches original MC Left/Right menus — no duplicate items. (#24)
        MenuItem[] PanelMenuItems(bool left) =>
        [
            new MenuItem("_Listing format...", string.Empty, () => ShowListingFormatDialog(left)),
            new MenuItem("_Quick view",        "Ctrl+X Q",   () => ToggleOverlayMode(PanelDisplayMode.QuickView)),
            new MenuItem("_Info",              "Ctrl+X I",   () => ToggleOverlayMode(PanelDisplayMode.Info)),
            new MenuItem("_Tree",              "Ctrl+X T",   () => ToggleOverlayMode(PanelDisplayMode.Tree)),
            new MenuItem("_Panelize",          string.Empty, ExternalPanelize),
            null!,
            new MenuItem("_Sort order...",     string.Empty, ShowSortDialog),
            new MenuItem("_Filter...",         string.Empty, () => ShowFilterDialog(left)),
            new MenuItem("_Encoding...",       string.Empty, () => ShowEncodingDialog(left)),
            null!,
            new MenuItem("_FTP link...",       string.Empty, () => ConnectVfsLink("ftp")),
            new MenuItem("_Shell link...",     string.Empty, () => NotImplemented("Shell link (FISH protocol)")),
            new MenuItem("S_FTP link...",      string.Empty, () => ConnectVfsLink("sftp")),
            null!,
            new MenuItem("_Rescan",            "Ctrl+R",
                () => (left ? _controller.LeftPanel : _controller.RightPanel).Reload()),
        ];

        _menuBar = new MenuBar
        {
            Menus = new[]
            {
                // ── Left ──────────────────────────────────────────────────
                new MenuBarItem("_Left",  PanelMenuItems(left: true)),

                // ── File ──────────────────────────────────────────────────
                new MenuBarItem("_File", new MenuItem[]
                {
                    new("_View",                 "F3",         () => ViewCurrent()),
                    new("View _file...",         string.Empty, () => ViewFilePrompt()),
                    new("F_iltered view",        string.Empty, () => ViewFiltered()),
                    new("_Edit",                 "F4",         () => EditCurrent()),
                    new("_Copy",                 "F5",         () => CopyFiles()),
                    new("C_hmod",                "Ctrl+X C",   () => Chmod()),
                    new("_Link",                 "Ctrl+X L",   () => CreateLink()),
                    new("_Symlink",              "Ctrl+X S",   () => CreateSymlink()),
                    new("Relati_ve symlink",     "Ctrl+X V",   () => CreateRelativeSymlink()),
                    new("Edit s_ymlink",         string.Empty, () => EditSymlink()),
                    new("Ch_own",                "Ctrl+X O",   () => Chown()),
                    new("_Advanced chown",       string.Empty, () => AdvancedChown()),
                    new("Ch_attr",               "Ctrl+X A",   () => Chattr()),
                    new("_Rename/Move",          "F6",         () => MoveFiles()),
                    new("_Mkdir",                "F7",         () => MakeDir()),
                    new("_Delete",               "F8",         () => DeleteFiles()),
                    new("_Quick cd",             string.Empty, () => QuickCd()),
                    null!,
                    new("_Select group",         "+",          () => SelectGroup()),
                    new("_Unselect group",       "\\",         () => UnselectGroup()),
                    new("_Invert selection",     "*",          () => InvertSelection()),
                    null!,
                    new("E_xit",                 "F10",        () => ConfirmQuit()),
                }),

                // ── Command ───────────────────────────────────────────────
                new MenuBarItem("_Command", new MenuItem[]
                {
                    new("_User menu",                   string.Empty, ShowUserMenu),
                    new("_Directory tree",              string.Empty, () => ShowTreeDialog(_controller.ActivePanel == _controller.LeftPanel)),
                    new("_Find file",                   string.Empty, ShowFindDialog),
                    new("_Swap panels",                 "Ctrl+U",     () => { _controller.SwapPanels(); RefreshPanels(); }),
                    new("Switch _panels on/off",        "Ctrl+O",     LaunchShell),
                    new("_Compare directories",         string.Empty, CompareDirs),
                    new("Compare _files",               string.Empty, ComparePanels),
                    new("E_xternal panelize",           string.Empty, ExternalPanelize),
                    new("Show directory si_zes",        string.Empty, ShowDirSize),
                    null!,
                    new("Command _history",             string.Empty, ShowCommandHistory),
                    new("Viewed/_edited files history", string.Empty, ShowViewedEditedHistory),
                    new("Directory _hotlist",           "Ctrl+\\",    ShowHotlist),
                    new("Active _VFS list",             string.Empty, ShowActiveVfsList),
                    new("_Background jobs",             string.Empty, ShowBackgroundJobs),
                    new("Screen _list",                 string.Empty, () => NotImplemented("Screen list")),
                    null!,
                    new("Edit e_xtension file",         string.Empty, () => EditConfigFile(ConfigPaths.ExtFile)),
                    new("Edit _menu file",              string.Empty, () => EditConfigFile(ConfigPaths.MenuFile)),
                    new("Edit hi_ghlighting group file",string.Empty, () => EditConfigFile(ConfigPaths.FileHighlightFile)),
                }),

                // ── Tools (custom addition for this .NET port) ────────────
                new MenuBarItem("_Tools", new MenuItem[]
                {
                    new("Copy _path to clipboard",  string.Empty, CopyPathToClipboard),
                    new("Copy file _name",          string.Empty, CopyNameToClipboard),
                    new("Copy _directory path",     string.Empty, CopyDirToClipboard),
                    null!,
                    new("_Checksum...",             string.Empty, ShowChecksum),
                    new("Directory _size...",       string.Empty, ShowDirSize),
                    new("_Touch (timestamps)...",   string.Empty, ShowTouch),
                    null!,
                    new("_Batch rename...",         string.Empty, ShowBatchRename),
                    null!,
                    new("Open _terminal here",      "Ctrl+T",     OpenTerminalHere),
                    new("_Compare with diff tool",  string.Empty, CompareWithDiffTool),
                }),

                // ── Options ───────────────────────────────────────────────
                new MenuBarItem("_Options", new MenuItem[]
                {
                    new("_Configuration...", string.Empty, ShowConfigurationDialog),
                    new("_Layout...",        string.Empty, ShowLayoutDialog),
                    new("_Panel options...", string.Empty, ShowPanelOptionsDialog),
                    new("C_onfirmation...",  string.Empty, ShowConfirmationDialog),
                    new("_Appearance...",    string.Empty, ShowAppearanceDialog),
                    new("_Learn keys...",    string.Empty, ShowLearnKeysDialog),
                    new("_Virtual FS...",    string.Empty, ShowVfsSettingsDialog),
                    null!,
                    new("_Save setup",       string.Empty, () => _settings.Save()),
                    null!,
                    new("_About...",         string.Empty, () => MessageDialog.Show("About",
                        "Midnight Commander for .NET\n" +
                        ".NET 8 rewrite of GNU Midnight Commander\n" +
                        "Built with Terminal.Gui")),
                }),

                // ── Right ─────────────────────────────────────────────────
                new MenuBarItem("_Right", PanelMenuItems(left: false)),
            },
        };
        Add(_menuBar);

        // Split view
        _leftPanelView = new FilePanelView(_controller.LeftPanel)
        {
            X = 0, Y = 1,
            Width = Dim.Percent(50),
            Height = Dim.Fill(2),
        };
        _leftPanelView.EntryActivated += OnPanelEntryActivated;
        _leftPanelView.BecameActive += (_, _) => SetActivePanel(_leftPanelView);
        _leftPanelView.IsActive = true;
        ApplyPanelSettings(_leftPanelView);
        // ApplyFilterSettings() is called after both panels are constructed (see below)

        // Right panel overlaps the left panel's right border by 1 column so that
        // the shared divider is a single │ rather than a double ││. (#5)
        _rightPanelView = new FilePanelView(_controller.RightPanel)
        {
            X = Pos.Right(_leftPanelView) - 1, Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
        };
        _rightPanelView.EntryActivated += OnPanelEntryActivated;
        _rightPanelView.BecameActive += (_, _) => SetActivePanel(_rightPanelView);
        ApplyPanelSettings(_rightPanelView);

        // Apply filter settings now that both panels are constructed (#4)
        _controller.LeftPanel.Filter.ShowHidden  = _settings.ShowHiddenFiles;
        _controller.LeftPanel.Filter.ShowBackups = _settings.ShowBackupFiles;
        _controller.RightPanel.Filter.ShowHidden  = _settings.ShowHiddenFiles;
        _controller.RightPanel.Filter.ShowBackups = _settings.ShowBackupFiles;

        // Cursor-change hooks: update overlay when active-panel cursor moves
        _leftPanelView.CursorChanged  += (_, _) => { if (_rightMode != PanelDisplayMode.Normal) UpdateOverlay(false); };
        _rightPanelView.CursorChanged += (_, _) => { if (_leftMode  != PanelDisplayMode.Normal) UpdateOverlay(true); };

        // Overlay views (initially hidden; replace the inactive panel in Quick View / Info modes)
        _leftOverlay = new View
        {
            X = 0, Y = 1,
            Width  = Dim.Percent(50),
            Height = Dim.Fill(2),
            Visible = false,
            ColorScheme = McTheme.Panel,
        };
        _rightOverlay = new View
        {
            X      = Pos.Right(_leftPanelView) - 1, Y = 1,
            Width  = Dim.Fill(),
            Height = Dim.Fill(2),
            Visible = false,
            ColorScheme = McTheme.Panel,
        };
        Add(_leftPanelView, _rightPanelView, _leftOverlay, _rightOverlay);

        // Command line
        _commandLine = new CommandLineView
        {
            X = 0, Y = Pos.Bottom(_leftPanelView),
            Width = Dim.Fill(),
            Height = 1,
        };
        _commandLine.CommandEntered += OnCommandEntered;
        Add(_commandLine);

        // Button bar
        _buttonBar = ButtonBarView.CreateDefault(
            onHelp:     HelpDialog.Show,
            onUserMenu: ShowUserMenu,
            onView:     ViewCurrent,
            onEdit:     EditCurrent,
            onCopy:     CopyFiles,
            onMove:     MoveFiles,
            onMkdir:    MakeDir,
            onDelete:   DeleteFiles,
            onMenu:     () => _menuBar.OpenMenu(),
            onQuit:     () => ConfirmQuit()
        );
        _buttonBar.X = 0;
        _buttonBar.Y = Pos.Bottom(_commandLine);
        Add(_buttonBar);
    }

    protected override bool OnKeyDown(Key keyEvent)
    {
        // Handle Ctrl+X prefix sub-commands (matches original MC Ctrl+X keymap)
        if (_ctrlXPrefix)
        {
            _ctrlXPrefix = false;
            switch (keyEvent.KeyCode)
            {
                case KeyCode.C: Chmod(); return true;                                        // Ctrl+X C → chmod (#46)
                case KeyCode.O: Chown(); return true;                                        // Ctrl+X O → chown (#46)
                case KeyCode.Q: ToggleOverlayMode(PanelDisplayMode.QuickView); return true; // Ctrl+X Q → quick view
                case KeyCode.I: ToggleOverlayMode(PanelDisplayMode.Info);      return true; // Ctrl+X I → info panel
                case KeyCode.A: Chattr(); return true;                                       // Ctrl+X A → chattr
                case KeyCode.P when !keyEvent.IsCtrl:
                    PastePathToCommandLine(); return true;                                   // Ctrl+X P → active panel path (#50)
                case KeyCode.P when keyEvent.IsCtrl:
                    PasteOtherPanelPathToCommandLine(); return true;                         // Ctrl+X Ctrl+P → other panel path (#48)
                case KeyCode.T when !keyEvent.IsCtrl:
                    PasteTaggedFilesToCommandLine(); return true;                            // Ctrl+X T → tagged files → cmdline (#47)
                case KeyCode.T when keyEvent.IsCtrl:
                    PasteTaggedFilesFromOtherPanelToCommandLine(); return true;              // Ctrl+X Ctrl+T → tagged from other panel
                case KeyCode.H: AddCurrentDirToHotlist(); return true;                      // Ctrl+X H → add to hotlist (#12)
                case KeyCode.D: CompareDirs(); return true;                                  // Ctrl+X D → compare dirs (#13)
                case KeyCode.L: CreateLink(); return true;                                   // Ctrl+X L → hard link (#14)
                case KeyCode.S: CreateSymlink(); return true;                                // Ctrl+X S → absolute symlink (#14)
                case KeyCode.V: CreateRelativeSymlink(); return true;                        // Ctrl+X V → relative symlink (#14)
            }
            return true; // consume unknown Ctrl+X subkey
        }

        switch (keyEvent.KeyCode)
        {
            case KeyCode.F1:  HelpDialog.Show(); return true;
            case KeyCode.F3:  ViewCurrent(); return true;
            case KeyCode.F4:  EditCurrent(); return true;
            case KeyCode.F5:  CopyFiles(); return true;
            case KeyCode.F6:  MoveFiles(); return true;
            case KeyCode.F7:  MakeDir(); return true;
            case KeyCode.F8:  DeleteFiles(); return true;
            case KeyCode.F9:  _menuBar.OpenMenu(); return true;
            case KeyCode.F10: ConfirmQuit(); return true;

            case KeyCode.Tab:
            case KeyCode.Tab | KeyCode.ShiftMask:
                SwitchPanel(); return true;

            case KeyCode.U when keyEvent.IsCtrl: _controller.SwapPanels(); RefreshPanels(); return true;
            case KeyCode.R when keyEvent.IsCtrl: _controller.Refresh(); return true;

            case KeyCode.L when keyEvent.IsCtrl: Application.LayoutAndDraw(true); return true; // Refresh/redraw screen (matches original MC Ctrl+L)
            case KeyCode.I when keyEvent.IsCtrl: ShowInfo(); return true;           // Ctrl+I → file info (original MC Ctrl+I / *)
            case KeyCode.X when keyEvent.IsCtrl: _ctrlXPrefix = true; return true; // start Ctrl+X prefix

            case KeyCode.T when keyEvent.IsCtrl: OpenTerminalHere(); return true;
            case KeyCode.Insert: GetActivePanel().ToggleMark(); return true;

            // Ctrl+Enter → paste filename to command line (#8)
            case KeyCode.Enter when keyEvent.IsCtrl:
                PasteFilenameToCommandLine();
                return true;

            // Ctrl+Shift+Enter → paste full path to command line (#37)
            case KeyCode.Enter when keyEvent.IsCtrl && keyEvent.IsShift:
                PastePathToCommandLine();
                return true;

            // Alt+. handled in default clause below (#11)

            // Alt+I → sync inactive panel to active path (#12)
            case KeyCode.I | KeyCode.AltMask:
                SyncInactivePanelToActive();
                return true;

            // Alt+O → open other panel at current file's directory (#13)
            case KeyCode.O | KeyCode.AltMask:
                OpenOtherPanelAtCurrentDir();
                return true;

            // Alt+Y → go back in panel directory history (#9)
            case KeyCode.Y | KeyCode.AltMask:
                NavigatePanelBack();
                return true;

            // Alt+U → go forward in panel directory history (#9)
            case KeyCode.U | KeyCode.AltMask:
                NavigatePanelForward();
                return true;

            // Alt+Enter → show file info / properties (#45)
            case KeyCode.Enter | KeyCode.AltMask:
                ShowInfo();
                return true;

            // Alt+C → Quick CD (#5)
            case KeyCode.C | KeyCode.AltMask:
                QuickCd();
                return true;

            // Alt+I is already handled above, but Alt+O is here too
            // Alt+T → cycle panel listing mode (#25)
            case KeyCode.T | KeyCode.AltMask:
                CycleListingMode();
                return true;

            // Alt+G → jump to first, Alt+R → middle, Alt+J → last in panel (#27)
            case KeyCode.G | KeyCode.AltMask:
                GetActivePanel().JumpToFirst(); return true;
            case KeyCode.R | KeyCode.AltMask:
                GetActivePanel().JumpToMiddle(); return true;
            case KeyCode.J | KeyCode.AltMask:
                GetActivePanel().JumpToLast(); return true;

            // Ctrl+Space → calculate/show directory size (#10)
            case KeyCode.Space when keyEvent.IsCtrl:
                ShowDirSizeForCurrentEntry();
                return true;

            // Ctrl+O → switch panels on/off / subshell (#original MC)
            case KeyCode.O when keyEvent.IsCtrl:
                LaunchShell();
                return true;

            default:
                // Alt+. → toggle hidden files (#11) — KeyCode.Period doesn't exist in TG2; match by rune value
                if (keyEvent.IsAlt && keyEvent.AsRune.Value == '.')
                {
                    ToggleHiddenFiles();
                    return true;
                }
                // Alt+? → Find file (#6)
                if (keyEvent.IsAlt && keyEvent.AsRune.Value == '?')
                {
                    ShowFindDialog();
                    return true;
                }
                // Alt+, → toggle split direction (#26)
                if (keyEvent.IsAlt && keyEvent.AsRune.Value == ',')
                {
                    ToggleSplitDirection();
                    return true;
                }
                return base.OnKeyDown(keyEvent);
        }
    }

    // --- Panel management ---

    private FilePanelView GetActivePanel()
        => _controller.ActivePanel == _controller.LeftPanel ? _leftPanelView : _rightPanelView;

    private void SwitchPanel()
    {
        _controller.SwitchPanel();
        var active = GetActivePanel();
        SetActivePanel(active);
        active.SetFocus();
    }

    private void SetActivePanel(FilePanelView panel)
    {
        _leftPanelView.IsActive  = panel == _leftPanelView;
        _rightPanelView.IsActive = panel == _rightPanelView;
        if (panel == _leftPanelView && _controller.ActivePanel != _controller.LeftPanel)
            _controller.SwitchPanel();
        if (panel == _rightPanelView && _controller.ActivePanel != _controller.RightPanel)
            _controller.SwitchPanel();
    }

    private void RefreshPanels()
    {
        _leftPanelView.Refresh();
        _rightPanelView.Refresh();
    }

    /// <summary>Pastes the current entry's filename into the command line (Ctrl+Enter). (#8)</summary>
    private void PasteFilenameToCommandLine()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;
        var name = entry.IsParentDir ? ".." : entry.Name;
        _commandLine.AppendText(name);
    }

    /// <summary>Pastes the current entry's full path into the command line (Ctrl+X P / Ctrl+Shift+Enter). (#37 #50)</summary>
    private void PastePathToCommandLine()
    {
        var entry = GetCurrentEntry();
        var path = entry != null
            ? entry.FullPath.Path
            : _controller.ActivePanel.CurrentPath.Path;
        _commandLine.AppendText(path);
    }

    /// <summary>
    /// Pushes ShowHiddenFiles and ShowBackupFiles from settings into both panel filter objects,
    /// then reloads both panels so the change takes effect immediately. (#4)
    /// </summary>
    private void ApplyFilterSettings()
    {
        _controller.LeftPanel.Filter.ShowHidden  = _settings.ShowHiddenFiles;
        _controller.LeftPanel.Filter.ShowBackups = _settings.ShowBackupFiles;
        _controller.RightPanel.Filter.ShowHidden  = _settings.ShowHiddenFiles;
        _controller.RightPanel.Filter.ShowBackups = _settings.ShowBackupFiles;
        _controller.LeftPanel.Reload();
        _controller.RightPanel.Reload();
    }

    /// <summary>Toggles display of hidden (dot) files via Alt+. (#11)</summary>
    private void ToggleHiddenFiles()
    {
        _settings.ShowHiddenFiles = !_settings.ShowHiddenFiles;
        _settings.Save();
        ApplyFilterSettings(); // #4: propagate to filter objects
    }

    /// <summary>Synchronises the inactive panel to the active panel's current path (Alt+I). (#12)</summary>
    private void SyncInactivePanelToActive()
    {
        var activePath = _controller.ActivePanel.CurrentPath;
        _controller.InactivePanel.Load(activePath);
    }

    /// <summary>Navigates the other panel to the directory of the current file (Alt+O). (#13)</summary>
    private void OpenOtherPanelAtCurrentDir()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;
        var dir = entry.IsDirectory || entry.IsParentDir
            ? entry.FullPath
            : VfsPath.FromLocal(Path.GetDirectoryName(entry.FullPath.Path) ?? entry.FullPath.Path);
        _controller.InactivePanel.Load(dir);
    }

    /// <summary>Adds the active panel's current directory to the hotlist (Ctrl+X H). (#12)</summary>
    private void AddCurrentDirToHotlist()
    {
        var path = _controller.ActivePanel.CurrentPath.Path;
        var name = Path.GetFileName(path.TrimEnd('/')) ?? path;
        _controller.Hotlist.Add(name, path);
        ShowStatus($"Added to hotlist: {path}");
    }

    /// <summary>Pastes tagged files from the active panel to the command line (Ctrl+X T). (#47)</summary>
    private void PasteTaggedFilesToCommandLine()
    {
        var marked = _controller.ActivePanel.GetMarkedEntries();
        if (marked.Count == 0)
        {
            var cur = GetCurrentEntry();
            if (cur != null) _commandLine.AppendText(cur.Name + " ");
            return;
        }
        foreach (var e in marked)
            _commandLine.AppendText(e.Name + " ");
    }

    /// <summary>Pastes tagged files from the INACTIVE panel to the command line (Ctrl+X Ctrl+T).</summary>
    private void PasteTaggedFilesFromOtherPanelToCommandLine()
    {
        var marked = _controller.InactivePanel.GetMarkedEntries();
        foreach (var e in marked)
            _commandLine.AppendText(e.Name + " ");
    }

    /// <summary>Pastes the INACTIVE panel's current path to the command line (Ctrl+X Ctrl+P). (#48)</summary>
    private void PasteOtherPanelPathToCommandLine()
    {
        _commandLine.AppendText(_controller.InactivePanel.CurrentPath.Path);
    }

    /// <summary>Cycles the active panel through Full → Brief → Long listing modes (Alt+T). (#25)</summary>
    private void CycleListingMode()
    {
        var panel = GetActivePanel();
        panel.CycleListingMode();
    }

    /// <summary>Toggles between vertical and horizontal panel split (Alt+,). (#26)</summary>
    private void ToggleSplitDirection()
    {
        _settings.HorizontalSplit = !_settings.HorizontalSplit;
        _settings.Save();
        ApplyLayoutSettings();
    }

    /// <summary>Shows directory size of the current entry in-panel (Ctrl+Space). (#10)</summary>
    private void ShowDirSizeForCurrentEntry()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsParentDir) return;
        if (entry.IsDirectory)
        {
            ShowStatus($"Calculating size of {entry.Name}…");
            _ = Task.Run(() =>
            {
                try
                {
                    long size = CalculateDirectorySize(entry.FullPath.Path);
                    Application.Invoke(() =>
                    {
                        ShowStatus($"{entry.Name}: {FileSizeFormatter.Format(size)}");
                    });
                }
                catch { Application.Invoke(() => ShowStatus($"{entry.Name}: (error)")); }
            });
        }
        else
        {
            ShowStatus($"{entry.Name}: {FileSizeFormatter.Format(entry.Size)}");
        }
    }

    private static long CalculateDirectorySize(string path)
    {
        long total = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                try { total += new FileInfo(f).Length; } catch { }
        }
        catch { }
        return total;
    }

    /// <summary>Goes back in the active panel's directory history (Alt+Y). (#9)</summary>
    private void NavigatePanelBack()
    {
        if (_controller.ActivePanel.CanGoBack)
            _controller.ActivePanel.GoBack();
    }

    /// <summary>Goes forward in the active panel's directory history (Alt+U). (#9)</summary>
    private void NavigatePanelForward()
    {
        if (_controller.ActivePanel.CanGoForward)
            _controller.ActivePanel.GoForward();
    }

    /// <summary>Push McSettings values into both panel views. Called after settings change.</summary>
    private void ApplyPanelSettings(FilePanelView panel)  // #5 #6 #7
    {
        panel.ShowFreeSpace             = _settings.ShowFreeSpace;
        panel.LynxLikeMotion           = _settings.LynxLikeMotion;
        panel.MarkMovesCursor          = _settings.MarkMovesCursor;
        panel.QuickSearchCaseSensitive = _settings.QuickSearchCaseSensitive;
        panel.ShowScrollbar            = _settings.ShowScrollbar;   // #9
        panel.ShowMiniStatus           = _settings.ShowMiniStatus;  // #24
    }

    private void OnPanelEntryActivated(object? sender, FileEntry? entry)
    {
        if (entry == null) return;
        if (entry.IsDirectory || entry.IsParentDir)
        {
            _controller.NavigateTo(entry.FullPath);
            return;
        }

        // Archive VFS: enter ZIP/TAR archives as virtual directories (#20)
        var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
        var archivePath = TryGetArchiveVfsPath(entry.FullPath.Path, ext);
        if (archivePath != null)
        {
            _controller.NavigateTo(archivePath);
            return;
        }

        // Extension associations take priority over the internal viewer (#47)
        var openCmd = _controller.Extensions.GetOpenCommand(entry.Name);
        if (openCmd != null)
        {
            var expanded = _controller.Extensions.ExpandCommand(openCmd, entry.FullPath.Path);
            ProcessHelper.RunDetached(expanded);
            return;
        }

        if (_settings.UseInternalViewer)
        {
            ViewFile(entry.FullPath.Path);
        }
        else
        {
            _controller.OpenEntry(entry);
        }
    }

    /// <summary>Returns a VFS path for browsing inside an archive file, or null if not an archive. (#20)</summary>
    private static VfsPath? TryGetArchiveVfsPath(string filePath, string ext) => ext switch
    {
        ".zip"                 => new VfsPath("zip",  null, null, null, null, filePath + "|"),
        ".tar"                 => new VfsPath("tar",  null, null, null, null, filePath + "|"),
        ".tgz" or ".tar.gz"   => new VfsPath("tar",  null, null, null, null, filePath + "|"),
        ".tar.bz2" or ".tbz2" => new VfsPath("tar",  null, null, null, null, filePath + "|"),
        ".tar.xz"  or ".txz"  => new VfsPath("tar",  null, null, null, null, filePath + "|"),
        _                      => null,
    };

    // --- File operations ---

    private FileEntry? GetCurrentEntry() => GetActivePanel().CurrentEntry;

    private void ViewCurrent()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;
        // Directories are viewed by navigating into them (matches original MC do_view_cmd behaviour)
        if (entry.IsDirectory || entry.IsParentDir)
        {
            _controller.NavigateTo(entry.FullPath);
            RefreshPanels();
            return;
        }
        ViewFile(entry.FullPath.Path);
    }

    /// <summary>
    /// Quick view: shows the selected file in the internal viewer.
    /// In the original MC this switches the active panel to quick-view mode;
    /// here we open the full-screen viewer as the closest equivalent.
    /// Equivalent to quick_view_cmd() in src/filemanager/panel.c.
    /// </summary>
    private void QuickViewCurrent()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsDirectory || entry.IsParentDir) return;
        ViewFile(entry.FullPath.Path);
    }

    private void ViewFile(string path)
    {
        _viewedFiles.Remove(path);
        _viewedFiles.Insert(0, path);

        var viewerWin = new Window
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = McTheme.Dialog,
        };
        var viewer = new ViewerView(path);
        viewer.X = 0; viewer.Y = 0;
        viewer.Width = Dim.Fill(); viewer.Height = Dim.Fill();
        viewer.RequestClose += (_, _) => Application.RequestStop(viewerWin);
        viewerWin.Title = viewer.Title;
        viewerWin.Add(viewer);
        Application.Run(viewerWin);
    }

    private void EditCurrent()
    {
        var entry = GetCurrentEntry();
        string? path;

        if (entry == null || entry.IsDirectory || entry.IsParentDir)
        {
            // F4 with no file selected: prompt for a new filename (edit_cmd_new() in original MC)
            var newName = InputDialog.Show("Edit", "Enter file name:", string.Empty);
            if (newName == null) return;
            path = Path.IsPathRooted(newName)
                ? newName
                : Path.Combine(_controller.ActivePanel.CurrentPath.Path, newName);
        }
        else
        {
            path = entry.FullPath.Path;
            _editedFiles.Remove(path); _editedFiles.Insert(0, path);
        }

        var editorWin = new Window
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = McTheme.Dialog,
        };
        var editor = new EditorView(path);
        editor.X = 0; editor.Y = 0;
        editor.Width = Dim.Fill(); editor.Height = Dim.Fill();
        editor.RequestClose += (_, _) => Application.RequestStop(editorWin);
        editorWin.Title = editor.Title;
        editorWin.Add(editor);
        Application.Run(editorWin);
        RefreshPanels();
    }

    private void CopyFiles()
    {
        var dest = _controller.InactivePanel.CurrentPath.Path;
        var entry  = GetCurrentEntry();
        var marked = _controller.ActivePanel.GetMarkedEntries();
        var sourceName    = marked.Count > 0 ? $"{marked.Count} files" : (entry?.Name ?? "marked files");
        var defaultSource = marked.Count > 0 ? "*" : (entry?.Name ?? "*");
        var opts = CopyMoveDialog.Show(false, sourceName, dest, defaultSource);
        if (opts?.Confirmed != true) return;

        // Overwrite confirmation: pre-check which files would be overwritten (#1)
        if (_settings.ConfirmOverwrite)
        {
            var sources = _controller.ActivePanel.GetMarkedEntries();
            if (sources.Count == 0 && entry != null) sources = new List<FileEntry> { entry };
            var conflicts = sources
                .Where(e => !e.IsDirectory && File.Exists(Path.Combine(opts.DestinationPath, e.Name)))
                .Select(e => e.Name)
                .ToList();
            if (conflicts.Count > 0)
            {
                var msg = conflicts.Count == 1
                    ? $"Overwrite \"{conflicts[0]}\"?"
                    : $"Overwrite {conflicts.Count} existing files?\n  " + string.Join("\n  ", conflicts.Take(5))
                      + (conflicts.Count > 5 ? $"\n  …and {conflicts.Count - 5} more" : string.Empty);
                if (!MessageDialog.Confirm("Overwrite?", msg, "Overwrite", "Cancel"))
                    return;
            }
        }

        var preserveAttrs = opts.PreserveAttributes;
        RunFileOperation("Copy", opts.RunInBackground, (progress, ct) =>
            _controller.CopyMarkedAsync(progress, ct, preserveAttrs));
    }

    private void MoveFiles()
    {
        var entry = GetCurrentEntry();
        var marked = _controller.ActivePanel.GetMarkedEntries();
        string dest, sourceName;
        if (marked.Count > 0)
        {
            dest = _controller.InactivePanel.CurrentPath.Path;
            sourceName = $"{marked.Count} files";
        }
        else if (entry != null)
        {
            dest = entry.Name;
            sourceName = entry.Name;
        }
        else return;
        var moveSource = marked.Count > 0 ? "*" : (entry?.Name ?? "*");
        var opts = CopyMoveDialog.Show(true, sourceName, dest, moveSource);
        if (opts?.Confirmed != true) return;

        RunFileOperation("Move", opts.RunInBackground, _controller.MoveMarkedAsync);
    }

    private void RunFileOperation(string name, bool background,
        Func<IProgress<OperationProgress>?, CancellationToken, Task> operation)
    {
        if (background)
        {
            var job = new BackgroundJob { Name = name };
            var reporter = new Progress<OperationProgress>(p =>
                job.Status = p.CurrentFile ?? job.Status);
            job.Task = Task.Run(async () =>
            {
                try { await operation(reporter, job.Cts.Token); job.Status = "Done"; }
                catch (OperationCanceledException) { job.Status = "Cancelled"; }
                catch (Exception ex) { job.Status = $"Error: {ex.Message}"; }
                finally { job.Running = false; Application.Invoke(RefreshPanels); }
            });
            _backgroundJobs.Add(job);
        }
        else
        {
            var progress = new ProgressDialog(name);
            progress.Show();
            _ = Task.Run(async () =>
            {
                try { await operation(progress, progress.CancellationToken); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Application.Invoke(() => MessageDialog.Error(ex.Message)); }
                finally { progress.Close(); Application.Invoke(RefreshPanels); }
            });
        }
    }

    private void MakeDir()
    {
        var name = MkdirDialog.Show();
        if (name == null) return;
        _controller.CreateDirectory(name);
    }

    private void DeleteFiles()
    {
        var marked  = _controller.ActivePanel.GetMarkedEntries();
        var targets = marked.Count > 0
            ? marked.ToList()
            : (GetCurrentEntry() is { } e ? [e] : []);

        if (targets.Count == 0) return;

        // Primary confirmation
        if (!DeleteDialog.Confirm(targets.Select(e => e.Name).ToList())) return;

        // Secondary confirmation per directory — equivalent to original MC's erase_dir() prompt.
        // Ask "Delete directory '<name>' recursively?" for each directory in the target list.
        var toDelete = new List<FileEntry>();
        foreach (var entry in targets)
        {
            if (entry.IsDirectory)
            {
                if (MessageDialog.Confirm("Delete",
                    $"Delete directory \"{entry.Name}\" recursively?", "Yes", "No"))
                    toDelete.Add(entry);
                // else user declined this directory — skip it
            }
            else
            {
                toDelete.Add(entry);
            }
        }

        if (toDelete.Count == 0) return;

        // Mark exactly the confirmed entries so DeleteMarkedAsync processes the right set
        _controller.ActivePanel.MarkAll(false);
        foreach (var del in toDelete) del.IsMarked = true;
        _controller.ActivePanel.RefreshMarking();

        var progress = new ProgressDialog("Delete");
        progress.Show();
        _ = Task.Run(async () =>
        {
            try { await _controller.DeleteMarkedAsync(progress, progress.CancellationToken); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Application.Invoke(() => MessageDialog.Error(ex.Message)); }
            finally { progress.Close(); Application.Invoke(RefreshPanels); }
        });
    }

    private void ShowInfo()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;
        InfoDialog.Show(entry);
    }

    // ── Quick View / Info panel overlay (persistent panel mode) ───────────────
    // Matches list_quick_view / list_info modes in original MC (src/filemanager/panel.c).
    // Ctrl+X Q toggles Quick View; Ctrl+X I toggles Info on the INACTIVE panel.

    private void ToggleOverlayMode(PanelDisplayMode mode)
    {
        bool inactiveIsLeft = _controller.ActivePanel == _controller.RightPanel;
        if (inactiveIsLeft)
        {
            _leftMode = _leftMode == mode ? PanelDisplayMode.Normal : mode;
            ApplyOverlay(true, _leftMode);
        }
        else
        {
            _rightMode = _rightMode == mode ? PanelDisplayMode.Normal : mode;
            ApplyOverlay(false, _rightMode);
        }
    }

    private void ApplyOverlay(bool isLeft, PanelDisplayMode mode)
    {
        var panelView = isLeft ? _leftPanelView : _rightPanelView;
        var overlay   = isLeft ? _leftOverlay   : _rightOverlay;

        panelView.Visible = mode == PanelDisplayMode.Normal;
        overlay.Visible   = mode != PanelDisplayMode.Normal;

        if (mode != PanelDisplayMode.Normal)
            UpdateOverlay(isLeft);

        Application.LayoutAndDraw(true);
    }

    private void UpdateOverlay(bool isLeft)
    {
        var overlay = isLeft ? _leftOverlay : _rightOverlay;
        var mode    = isLeft ? _leftMode    : _rightMode;
        var entry   = GetCurrentEntry(); // cursor in the ACTIVE panel

        overlay.RemoveAll();

        // Border title
        var title = mode switch
        {
            PanelDisplayMode.QuickView => "Quick View",
            PanelDisplayMode.Info      => "Info",
            PanelDisplayMode.Tree      => "Directory Tree",
            _                          => string.Empty,
        };
        overlay.Add(new Label { X = 0, Y = 0, Text = title, ColorScheme = McTheme.Panel });

        if (mode == PanelDisplayMode.Info)
            PopulateInfoOverlay(overlay, entry);
        else if (mode == PanelDisplayMode.Tree)
            PopulateTreeOverlay(overlay, isLeft);
        else
            PopulateQuickViewOverlay(overlay, entry);

        overlay.SetNeedsDraw();
    }

    private static void PopulateInfoOverlay(View overlay, FileEntry? entry)
    {
        if (entry == null || entry.IsParentDir)
        {
            overlay.Add(new Label { X = 1, Y = 2, Text = "No file selected." });
            return;
        }

        var lines = new[]
        {
            $"Name:   {entry.Name}",
            $"Type:   {(entry.IsDirectory ? "Directory" : entry.IsSymlink ? "Symlink" : "File")}",
            $"Size:   {FileSizeFormatter.FormatExact(entry.Size)} B",
            $"        ({FileSizeFormatter.Format(entry.Size)})",
            $"Mode:   {PermissionsFormatter.Format(entry.Permissions, entry.IsDirectory, entry.IsSymlink)}",
            $"Owner:  {entry.OwnerName ?? entry.DirEntry.OwnerUid.ToString()}",
            $"Group:  {entry.GroupName ?? entry.DirEntry.OwnerGid.ToString()}",
            $"Mtime:  {entry.ModificationTime:yyyy-MM-dd HH:mm}",
            $"Atime:  {entry.DirEntry.AccessTime:yyyy-MM-dd HH:mm}",
        };

        if (entry.IsSymlink && entry.DirEntry.SymlinkTarget != null)
        {
            var all = lines.ToList();
            all.Add($"→       {entry.DirEntry.SymlinkTarget}");
            for (int i = 0; i < all.Count; i++)
                overlay.Add(new Label { X = 1, Y = 2 + i, Text = all[i] });
        }
        else
        {
            for (int i = 0; i < lines.Length; i++)
                overlay.Add(new Label { X = 1, Y = 2 + i, Text = lines[i] });
        }
    }

    private static void PopulateQuickViewOverlay(View overlay, FileEntry? entry)
    {
        if (entry == null || entry.IsDirectory || entry.IsParentDir || entry.IsSymlink)
        {
            var msg = entry?.IsDirectory == true ? "(directory)" : "(no preview)";
            overlay.Add(new Label { X = 1, Y = 2, Text = msg });
            return;
        }

        try
        {
            // Read up to 500 lines for in-panel quick view
            var lines = File.ReadLines(entry.FullPath.Path).Take(500).ToList();
            for (int i = 0; i < lines.Count; i++)
                overlay.Add(new Label { X = 1, Y = 2 + i, Text = lines[i] });
        }
        catch
        {
            overlay.Add(new Label { X = 1, Y = 2, Text = "(cannot read file)" });
        }
    }

    // ── Tree panel overlay ────────────────────────────────────────────────────
    // Equivalent to the tree panel mode in src/filemanager/panel.c (list_tree).
    // Selecting a directory navigates the ACTIVE panel there.

    private void PopulateTreeOverlay(View overlay, bool isLeft)
    {
        var activeListing  = _controller.ActivePanel;
        var currentPath    = activeListing.CurrentPath.Path;
        var rootPath       = OperatingSystem.IsWindows()
            ? (Path.GetPathRoot(currentPath) ?? "C:\\") : "/";

        // Lazily track expanded set per overlay (store in overlay's Data field)
        var expanded = overlay.Data as HashSet<string>
                       ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootPath };
        // Expand ancestors of current path
        var anc = currentPath;
        while (!string.IsNullOrEmpty(anc) && anc != Path.GetPathRoot(anc))
        { expanded.Add(anc); anc = Path.GetDirectoryName(anc) ?? rootPath; }
        overlay.Data = expanded;

        var nodes   = new List<(string FullPath, int Depth, bool HasChildren)>();
        int selIdx  = 0;

        void Visit(string path, int depth)
        {
            bool hasCh;
            try { hasCh = Directory.EnumerateDirectories(path).Any(); } catch { hasCh = false; }
            nodes.Add((path, depth, hasCh));
            if (hasCh && expanded.Contains(path))
            {
                try
                {
                    foreach (var sub in Directory.GetDirectories(path)
                                                 .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                        Visit(sub, depth + 1);
                }
                catch { }
            }
        }
        Visit(rootPath, 0);

        var display = new List<string>();
        for (int i = 0; i < nodes.Count; i++)
        {
            var (path, depth, hasCh) = nodes[i];
            var name   = depth == 0 ? path
                : (Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) ?? path);
            var indent = new string(' ', depth * 2);
            var marker = !hasCh ? "  " : expanded.Contains(path) ? "▼ " : "▶ ";
            display.Add(indent + marker + name);
            if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) selIdx = i;
        }

        var lv = new ListView
        {
            X = 0, Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(new ObservableCollection<string>(display));
        lv.SelectedItem = Math.Max(0, Math.Min(selIdx, nodes.Count - 1));
        overlay.Add(lv);

        lv.OpenSelectedItem += (_, _) =>
        {
            var idx = lv.SelectedItem;
            if (idx < 0 || idx >= nodes.Count) return;
            var (path, _, hasCh) = nodes[idx];
            if (hasCh)
            {
                if (expanded.Contains(path)) expanded.Remove(path);
                else expanded.Add(path);
            }
            // Navigate active panel
            activeListing.Load(VfsPath.FromLocal(path));
            UpdateOverlay(isLeft);
            RefreshPanels();
        };
    }

    private void Chmod()
    {
        var marked  = _controller.ActivePanel.GetMarkedEntries();
        var targets = marked.Count > 0 ? marked : (GetCurrentEntry() is { } e ? [e] : []);
        if (targets.Count == 0) return;

        var first  = targets[0];
        var result = ChmodDialog.Show(first.Name, first.Permissions, targets.Count);
        if (result == null) return;

        var entries = result.ApplyToAll ? targets : [first];
        foreach (var target in entries)
        {
            try { File.SetUnixFileMode(target.FullPath.Path, result.Mode); }
            catch (Exception ex) { MessageDialog.Error($"{target.Name}: {ex.Message}"); }
        }
        RefreshPanels();
    }

    private void ShowSortDialog()
    {
        var panel = _controller.ActivePanel;
        var opts = SortDialog.Show(panel.Sort);
        if (opts == null) return;
        panel.Sort.Field = opts.Field;
        panel.Sort.Descending = opts.Descending;
        panel.Sort.DirectoriesFirst = opts.DirectoriesFirst;
        panel.Sort.CaseSensitive = opts.CaseSensitive;
        panel.Reload();
    }

    /// <summary>
    /// Two-phase Find File: first show the search-options dialog, then run the search
    /// with real-time progress and Go / Panelize / View / Edit buttons.
    /// Equivalent to find.c + find_cmd() in the original MC.
    /// </summary>
    private void ShowFindDialog()
    {
        var startDir = _controller.ActivePanel.CurrentPath.Path;
        var opts = FindDialog.Show(startDir);
        if (opts?.Confirmed != true) return;
        // Use the (possibly edited) start directory from the dialog
        ShowFindResults(opts, opts.StartDirectory.Length > 0 ? opts.StartDirectory : startDir);
    }

    private void ShowFindResults(FindOptions opts, string startDir)
    {
        var displayItems = new ObservableCollection<string>();
        var resultPaths  = new List<string>();
        var cts = new System.Threading.CancellationTokenSource();

        var d = new Dialog
        {
            Title = $"Find: {opts.FilePattern}",
            Width = Dim.Fill() - 4,
            Height = Dim.Fill() - 4,
            ColorScheme = McTheme.Dialog,
        };

        var statusLabel = new Label
        {
            X = 1, Y = 0, Width = Dim.Fill(1),
            Text = " Searching...",
            ColorScheme = McTheme.Dialog,
        };
        d.Add(statusLabel);

        var lv = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = Dim.Fill(5),
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(displayItems);
        d.Add(lv);

        Action? afterClose = null;

        var stopBtn = new Button { Text = "Stop" };
        stopBtn.Accepting += (_, _) => cts.Cancel();

        var goBtn = new Button { Text = "Go to", IsDefault = true, Enabled = false };
        goBtn.Accepting += (_, _) =>
        {
            var idx = lv.SelectedItem;
            if (idx >= 0 && idx < resultPaths.Count)
            {
                var path = resultPaths[idx];
                afterClose = () =>
                {
                    var dir = Path.GetDirectoryName(path) ?? startDir;
                    _controller.NavigateTo(VfsPath.FromLocal(dir));
                    RefreshPanels();
                };
            }
            cts.Cancel();
            Application.RequestStop(d);
        };

        var panelizeBtn = new Button { Text = "Panelize", Enabled = false };
        panelizeBtn.Accepting += (_, _) =>
        {
            var snapshot = resultPaths.ToList();
            afterClose = () => PanelizeFoundFiles(snapshot, startDir);
            cts.Cancel();
            Application.RequestStop(d);
        };

        var viewBtn = new Button { Text = "View", Enabled = false };
        viewBtn.Accepting += (_, _) =>
        {
            var idx = lv.SelectedItem;
            if (idx >= 0 && idx < resultPaths.Count)
            {
                var path = resultPaths[idx];
                afterClose = () => ViewFile(path);
            }
            cts.Cancel();
            Application.RequestStop(d);
        };

        var editBtn = new Button { Text = "Edit", Enabled = false };
        editBtn.Accepting += (_, _) =>
        {
            var idx = lv.SelectedItem;
            if (idx >= 0 && idx < resultPaths.Count)
            {
                var path = resultPaths[idx];
                afterClose = () => EditFileDirectly(path);
            }
            cts.Cancel();
            Application.RequestStop(d);
        };

        bool restartSearch = false;
        var againBtn = new Button { Text = "Again" };
        againBtn.Accepting += (_, _) => { restartSearch = true; cts.Cancel(); Application.RequestStop(d); };

        var closeBtn = new Button { Text = "Close" };
        closeBtn.Accepting += (_, _) => { cts.Cancel(); Application.RequestStop(d); };

        d.AddButton(stopBtn);
        d.AddButton(goBtn);
        d.AddButton(panelizeBtn);
        d.AddButton(viewBtn);
        d.AddButton(editBtn);
        d.AddButton(againBtn);
        d.AddButton(closeBtn);

        // ── Background search task ─────────────────────────────────────
        var token = cts.Token;
        Task.Run(() =>
        {
            try
            {
                var cmp = opts.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                System.Text.RegularExpressions.Regex? rx = null;
                if (opts.ContentRegex && !string.IsNullOrEmpty(opts.ContentPattern))
                    rx = new System.Text.RegularExpressions.Regex(
                        opts.ContentPattern,
                        opts.CaseSensitive ? System.Text.RegularExpressions.RegexOptions.None
                                           : System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // Build set of ignored directory names (#33)
                var ignoredDirNames = new HashSet<string>(
                    (opts.IgnoreDirs ?? string.Empty)
                        .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);

                // Build enumerable, optionally skipping hidden dirs and following symlinks
                IEnumerable<string> EnumerateFiles(string root)
                {
                    IEnumerable<string> files;
                    try { files = Directory.EnumerateFiles(root, opts.FilePattern); }
                    catch { yield break; }
                    foreach (var f in files)
                    {
                        if (token.IsCancellationRequested) yield break;
                        // Skip symlinks unless FollowSymlinks is on
                        if (!opts.FollowSymlinks && File.GetAttributes(f).HasFlag(FileAttributes.ReparsePoint))
                            continue;
                        yield return f;
                    }
                    if (!opts.SearchInSubdirs) yield break;
                    IEnumerable<string> dirs;
                    try { dirs = Directory.EnumerateDirectories(root); }
                    catch { yield break; }
                    foreach (var dir in dirs)
                    {
                        if (token.IsCancellationRequested) yield break;
                        var dirName = Path.GetFileName(dir);
                        // Skip hidden directories (names starting with '.') when requested
                        if (opts.SkipHiddenDirs && dirName.StartsWith('.')) continue;
                        // Skip ignored directories (#33)
                        if (ignoredDirNames.Count > 0 && ignoredDirNames.Contains(dirName)) continue;
                        // Skip symlinked directories unless FollowSymlinks
                        if (!opts.FollowSymlinks && new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint))
                            continue;
                        foreach (var f in EnumerateFiles(dir)) yield return f;
                    }
                }

                foreach (var file in EnumerateFiles(startDir))
                {
                    if (token.IsCancellationRequested) break;

                    // Update progress label with current directory
                    var scanDir = Path.GetDirectoryName(file) ?? startDir;
                    Application.Invoke(() => statusLabel.Text = $" Scanning: {scanDir}");

                    // Content filter
                    if (!string.IsNullOrEmpty(opts.ContentPattern))
                    {
                        bool match;
                        try
                        {
                            if (rx != null) match = rx.IsMatch(File.ReadAllText(file));
                            else            match = File.ReadAllText(file).Contains(opts.ContentPattern, cmp);
                        }
                        catch { match = false; }
                        if (!match) continue;
                    }

                    var prefix = startDir.TrimEnd('/');
                    var rel    = file.Length > prefix.Length ? file[(prefix.Length + 1)..] : file;
                    Application.Invoke(() =>
                    {
                        resultPaths.Add(file);
                        displayItems.Add(rel);
                        statusLabel.Text = $" Found {displayItems.Count} file(s)...";
                    });
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Application.Invoke(() => statusLabel.Text = $" Error: {ex.Message}");
            }

            Application.Invoke(() =>
            {
                var n = displayItems.Count;
                statusLabel.Text = n > 0 ? $" Found {n} file(s)." : " No files found.";
                stopBtn.Enabled     = false;
                goBtn.Enabled       = n > 0;
                panelizeBtn.Enabled = n > 0;
                viewBtn.Enabled     = n > 0;
                editBtn.Enabled     = n > 0;
            });
        });

        Application.Run(d);
        cts.Cancel();  // stop search if dialog closed via Esc
        d.Dispose();
        afterClose?.Invoke();

        // "Again": show the options dialog again so user can tweak and re-run
        if (restartSearch)
            ShowFindDialog();
    }

    /// <summary>
    /// Navigates the active panel to <paramref name="startDir"/> and marks the
    /// files returned by a Find search — equivalent to find_cmd()'s Panelize action.
    /// </summary>
    private void PanelizeFoundFiles(List<string> paths, string startDir)
    {
        _controller.NavigateTo(VfsPath.FromLocal(startDir));
        RefreshPanels();
        var panel = _controller.ActivePanel;
        var names = new HashSet<string>(paths.Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase);
        panel.MarkAll(false);
        foreach (var e in panel.Entries.Where(e => !e.IsParentDir && names.Contains(e.Name)))
            e.IsMarked = true;
        panel.RefreshMarking();
    }

    private void ShowHotlist()
    {
        var path = HotlistDialog.Show(_controller.Hotlist, _controller.ActivePanel.CurrentPath.Path);
        if (path == null) return;
        _controller.NavigateTo(Mc.Core.Vfs.VfsPath.FromLocal(path));
    }

    private void ComparePanels()
    {
        var leftPath = _controller.LeftPanel.CurrentPath.Path;
        var rightPath = _controller.RightPanel.CurrentPath.Path;

        // Compare current files or directories
        var leftEntry = _leftPanelView.CurrentEntry;
        var rightEntry = _rightPanelView.CurrentEntry;

        // Equivalent to original MC's diff_view_cmd() — shows an error rather than silently doing nothing
        if (leftEntry == null || leftEntry.IsDirectory || leftEntry.IsParentDir ||
            rightEntry == null || rightEntry.IsDirectory || rightEntry.IsParentDir)
        {
            MessageDialog.Show("Compare files",
                "Please select a regular file (not a directory) in each panel.");
            return;
        }

        var win = new Window
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(),
            ColorScheme = McTheme.Dialog,
        };
        var diff = new DiffView(leftEntry.FullPath.Path, rightEntry.FullPath.Path);
        diff.X = 0; diff.Y = 0;
        diff.Width = Dim.Fill(); diff.Height = Dim.Fill();
        diff.RequestClose += (_, _) => Application.RequestStop(win);
        win.Title = diff.Title;
        win.Add(diff);
        Application.Run(win);
    }

    private void LaunchShell()
    {
        Application.Driver?.End();
        Console.Clear();
        var shell = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe")
            : (Environment.GetEnvironmentVariable("SHELL") ?? "/bin/sh");
        using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = shell,
            UseShellExecute = false,
        });
        proc?.WaitForExit();
        Application.Driver?.Init();
        RefreshPanels();
        Application.LayoutAndDraw(true);
    }

    private void OnCommandEntered(object? sender, string command)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"",
            UseShellExecute = false,
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        proc?.WaitForExit();
        RefreshPanels();
    }

    private void ConfirmQuit()
    {
        if (_settings.ConfirmExit && !MessageDialog.Confirm("Quit", "Are you sure you want to quit?", "Quit", "Cancel"))
            return;

        // Save settings
        _settings.LeftPanelPath  = _controller.LeftPanel.CurrentPath.Path;
        _settings.RightPanelPath = _controller.RightPanel.CurrentPath.Path;
        _settings.Save();

        Application.RequestStop();
    }

    // --- Tools: Clipboard ---

    private void CopyPathToClipboard()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;
        if (!ClipboardHelper.TrySet(entry.FullPath.Path))
            MessageDialog.Error("Clipboard not available.");
    }

    private void CopyNameToClipboard()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;
        if (!ClipboardHelper.TrySet(entry.Name))
            MessageDialog.Error("Clipboard not available.");
    }

    private void CopyDirToClipboard()
    {
        var path = _controller.ActivePanel.CurrentPath.Path;
        if (!ClipboardHelper.TrySet(path))
            MessageDialog.Error("Clipboard not available.");
    }

    // --- Tools: File info ---

    private void ShowChecksum()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsDirectory)
        {
            MessageDialog.Show("Checksum", "Select a file first.");
            return;
        }
        ChecksumDialog.Show(entry.FullPath.Path);
    }

    private void ShowDirSize()
    {
        var entry = GetCurrentEntry();
        string targetPath;
        if (entry != null && (entry.IsDirectory || entry.IsParentDir))
            targetPath = entry.FullPath.Path;
        else
            targetPath = _controller.ActivePanel.CurrentPath.Path;
        DirSizeDialog.Show(targetPath);
    }

    private void ShowTouch()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsDirectory)
        {
            MessageDialog.Show("Touch", "Select a file first.");
            return;
        }
        TouchDialog.Show(entry.FullPath.Path);
        RefreshPanels();
    }

    // --- Tools: Batch rename ---

    private void ShowBatchRename()
    {
        var marked = _controller.ActivePanel.GetMarkedEntries()
            .Where(e => !e.IsDirectory && !e.IsParentDir)
            .Select(e => e.FullPath.Path)
            .ToList();

        if (marked.Count == 0)
        {
            var entry = GetCurrentEntry();
            if (entry != null && !entry.IsDirectory)
                marked.Add(entry.FullPath.Path);
        }

        if (marked.Count == 0)
        {
            MessageDialog.Show("Batch Rename", "Mark files to rename first.");
            return;
        }

        var count = BatchRenameDialog.Show(marked);
        if (count > 0)
        {
            RefreshPanels();
            MessageDialog.Show("Batch Rename", $"{count} file(s) renamed successfully.");
        }
    }

    // --- Tools: External apps ---

    private void OpenTerminalHere()
    {
        var dir = _controller.ActivePanel.CurrentPath.Path;
        if (!ProcessHelper.OpenTerminal(dir))
            MessageDialog.Error("Could not find a terminal emulator.\nTried: gnome-terminal, konsole, xfce4-terminal, xterm.");
    }

    private void CompareWithDiffTool()
    {
        var leftEntry  = _leftPanelView.CurrentEntry;
        var rightEntry = _rightPanelView.CurrentEntry;

        if (leftEntry == null || leftEntry.IsDirectory || rightEntry == null || rightEntry.IsDirectory)
        {
            MessageDialog.Show("Compare", "Position the cursor on a file in each panel.");
            return;
        }

        if (!ProcessHelper.OpenDiff(leftEntry.FullPath.Path, rightEntry.FullPath.Path))
            MessageDialog.Error("Could not find a diff tool.\nTried: meld, kdiff3, code, bcompare, vimdiff.");
    }

    // --- File menu: View file / Filtered view ---

    private void ViewFilePrompt()
    {
        var path = InputDialog.Show("View file", "Filename:", string.Empty);
        if (string.IsNullOrWhiteSpace(path)) return;
        if (File.Exists(path))
            ViewFile(path);
        else
            MessageDialog.Error($"File not found:\n{path}");
    }

    private void ViewFiltered()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsDirectory) return;

        var cmd = InputDialog.Show("Filtered view", "Filter command (%f = filename):", "cat %f");
        if (string.IsNullOrWhiteSpace(cmd)) return;

        var filePath = entry.FullPath.Path;
        var command  = cmd.Replace("%f", filePath);
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName  = "/bin/sh",
                Arguments = $"-c \"{command} > '{tempFile}'\"",
                UseShellExecute        = false,
                RedirectStandardError  = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit();
            if (File.Exists(tempFile))
                ViewFile(tempFile);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    // --- File menu: Link / Symlink ---

    private void CreateLink()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsDirectory) return;

        var dest = InputDialog.Show("Create hard link", "Link name:", entry.Name);
        if (string.IsNullOrWhiteSpace(dest)) return;
        if (!Path.IsPathRooted(dest))
            dest = Path.Combine(_controller.ActivePanel.CurrentPath.Path, dest);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("ln")
            {
                Arguments              = $"\"{entry.FullPath.Path}\" \"{dest}\"",
                UseShellExecute        = false,
                RedirectStandardError  = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit();
            if (proc?.ExitCode != 0)
                MessageDialog.Error(proc?.StandardError.ReadToEnd() ?? "Failed to create link.");
            RefreshPanels();
        }
        catch (Exception ex) { MessageDialog.Error(ex.Message); }
    }

    private void CreateSymlink()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;

        var linkName = InputDialog.Show("Create symlink", "Symlink name:", entry.Name);
        if (string.IsNullOrWhiteSpace(linkName)) return;
        if (!Path.IsPathRooted(linkName))
            linkName = Path.Combine(_controller.ActivePanel.CurrentPath.Path, linkName);

        try
        {
            File.CreateSymbolicLink(linkName, entry.FullPath.Path);
            RefreshPanels();
        }
        catch (Exception ex) { MessageDialog.Error(ex.Message); }
    }

    private void CreateRelativeSymlink()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;

        var linkName = InputDialog.Show("Create relative symlink", "Symlink name:", entry.Name);
        if (string.IsNullOrWhiteSpace(linkName)) return;
        if (!Path.IsPathRooted(linkName))
            linkName = Path.Combine(_controller.ActivePanel.CurrentPath.Path, linkName);

        var linkDir   = Path.GetDirectoryName(linkName) ?? _controller.ActivePanel.CurrentPath.Path;
        var relTarget = Path.GetRelativePath(linkDir, entry.FullPath.Path);
        try
        {
            File.CreateSymbolicLink(linkName, relTarget);
            RefreshPanels();
        }
        catch (Exception ex) { MessageDialog.Error(ex.Message); }
    }

    private void EditSymlink()
    {
        var entry = GetCurrentEntry();
        if (entry == null || !entry.IsSymlink) return;

        string currentTarget;
        try   { currentTarget = new FileInfo(entry.FullPath.Path).ResolveLinkTarget(false)?.FullName ?? string.Empty; }
        catch { currentTarget = string.Empty; }

        var newTarget = InputDialog.Show("Edit symlink", "Symlink target:", currentTarget);
        if (newTarget == null || newTarget == currentTarget) return;

        // Confirm before modifying (matches original MC's edit_symlink_cmd() behaviour)
        if (!MessageDialog.Confirm("Edit symlink",
            $"Do you want to update the symlink \"{entry.Name}\"?", "Yes", "No"))
            return;

        try
        {
            File.Delete(entry.FullPath.Path);
            File.CreateSymbolicLink(entry.FullPath.Path, newTarget);
            RefreshPanels();
        }
        catch (Exception ex) { MessageDialog.Error(ex.Message); }
    }

    // --- File menu: Chown / Advanced chown ---

    private void Chown()
    {
        var marked  = _controller.ActivePanel.GetMarkedEntries();
        var targets = marked.Count > 0 ? marked : (GetCurrentEntry() is { } e ? [e] : []);
        if (targets.Count == 0) return;

        var first  = targets[0];
        var result = ChownDialog.Show(first.Name, first.OwnerName ?? string.Empty, first.GroupName ?? string.Empty, targets.Count);
        if (result == null) return;

        var entries = result.ApplyToAll ? targets : [first];
        foreach (var target in entries)
        {
            try { ApplyChown(target.FullPath.Path, result.Owner, result.Group); }
            catch (Exception ex) { MessageDialog.Error($"{target.Name}: {ex.Message}"); }
        }
        RefreshPanels();
    }

    private void AdvancedChown()
    {
        var marked  = _controller.ActivePanel.GetMarkedEntries();
        var targets = marked.Count > 0 ? marked.ToList() : (GetCurrentEntry() is { } e ? new List<FileEntry> { e } : new List<FileEntry>());
        if (targets.Count == 0) return;

        var first = targets[0];
        var result = AdvancedChownDialog.Show(
            first.Name,
            first.OwnerName ?? string.Empty,
            first.GroupName ?? string.Empty,
            first.Permissions,
            targets.Count);
        if (result == null) return;

        var toProcess = result.ApplyToAll ? targets : new List<FileEntry> { first };
        foreach (var entry in toProcess)
        {
            try
            {
                ApplyChown(entry.FullPath.Path, result.Owner, result.Group);
                File.SetUnixFileMode(entry.FullPath.Path, result.Mode);
            }
            catch (Exception ex) { MessageDialog.Error(ex.Message); break; }
        }
        RefreshPanels();
    }

    private static void ApplyChown(string path, string owner, string group)
    {
        var arg = string.IsNullOrWhiteSpace(group) ? owner : $"{owner}:{group}";
        var psi = new System.Diagnostics.ProcessStartInfo("chown")
        {
            Arguments             = $"{arg} \"{path}\"",
            UseShellExecute       = false,
            RedirectStandardError = true,
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        proc?.WaitForExit();
        if (proc?.ExitCode != 0)
        {
            var err = proc?.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(err)) throw new Exception(err.Trim());
        }
    }

    // --- Chattr (ext2 file attributes) ---
    // Equivalent to chattr_cmd() in src/filemanager/chattr.c.

    private void Chattr()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsParentDir) return;

        // Query current attributes via lsattr
        string currentAttrs = string.Empty;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("lsattr")
            {
                Arguments = $"-d \"{entry.FullPath.Path}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? string.Empty;
            proc?.WaitForExit();
            // lsattr output: "----i--------e-- filename"
            if (output.Length > 0)
                currentAttrs = output.Split(' ')[0].Trim();
        }
        catch { /* lsattr not available */ }

        // Common ext2 attributes
        var attrDefs = new[]
        {
            ('a', "Append only"),
            ('c', "Compressed"),
            ('d', "No dump"),
            ('e', "Extents format"),
            ('i', "Immutable"),
            ('j', "Journal data"),
            ('s', "Secure delete"),
            ('S', "Synchronous updates"),
            ('t', "No tail merging"),
            ('T', "Top of dir hierarchy"),
            ('u', "Undeletable"),
        };

        var d = new Dialog
        {
            Title = $"File attributes: {entry.Name}",
            Width = 50,
            Height = attrDefs.Length + 7,
            ColorScheme = McTheme.Dialog,
        };

        var checkboxes = attrDefs.Select((def, i) => new CheckBox
        {
            X = 2, Y = 1 + i,
            Text = $"[{def.Item1}] {def.Item2}",
            CheckedState = currentAttrs.Contains(def.Item1) ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        }).ToArray();
        foreach (var cb in checkboxes) d.Add(cb);

        var ok = new Button { Text = "Set", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            var attrs = new string(attrDefs
                .Where((def, i) => checkboxes[i].CheckedState == CheckState.Checked)
                .Select(def => def.Item1).ToArray());
            var setStr  = string.IsNullOrEmpty(attrs) ? string.Empty : "+" + attrs;
            var clearStr= new string(attrDefs
                .Where((def, i) => checkboxes[i].CheckedState != CheckState.Checked)
                .Select(def => def.Item1).ToArray());
            if (!string.IsNullOrEmpty(clearStr)) clearStr = "-" + clearStr;

            try
            {
                var arg = setStr + clearStr;
                if (string.IsNullOrEmpty(arg)) { Application.RequestStop(d); return; }
                var psi = new System.Diagnostics.ProcessStartInfo("chattr")
                {
                    Arguments = $"{arg} \"{entry.FullPath.Path}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit();
                if (proc?.ExitCode != 0)
                {
                    var err = proc?.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(err))
                        MessageDialog.Error(err.Trim());
                }
            }
            catch (Exception ex) { MessageDialog.Error(ex.Message); }
            Application.RequestStop(d);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d);
    }

    // --- File menu: Quick cd / Selection ---

    private void QuickCd()
    {
        var current = _controller.ActivePanel.CurrentPath.Path;
        var path    = InputDialog.Show("Quick CD", "Directory:", current);
        if (!string.IsNullOrWhiteSpace(path))
            _controller.NavigateTo(VfsPath.FromLocal(path));
    }

    /// <summary>
    /// Connect to a remote VFS link (FTP or SFTP).
    /// Equivalent to ftplink_cmd() / sftplink_cmd() in src/filemanager/panel.c.
    /// Prompts for a URL and navigates the active panel to it.
    /// </summary>
    private void ConnectVfsLink(string scheme)
    {
        var title   = scheme.ToUpperInvariant() + " link";
        var prompt  = $"Enter {scheme.ToUpperInvariant()} URL ({scheme}://[user@]host[:port][/path]):";
        var def     = $"{scheme}://";
        var url     = InputDialog.Show(title, prompt, def);
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            var vfsPath = VfsPath.Parse(url);
            if (vfsPath.Scheme != scheme)
            {
                MessageDialog.Error($"Invalid {scheme.ToUpperInvariant()} URL.\nExpected format: {scheme}://[user@]host/path");
                return;
            }
            _controller.NavigateTo(vfsPath);
            RefreshPanels();
        }
        catch (Exception ex) { MessageDialog.Error(ex.Message); }
    }

    private void SelectGroup()   => SelectUnselect(mark: true);
    private void UnselectGroup() => SelectUnselect(mark: false);

    /// <summary>
    /// Shows the Select/Unselect group dialog matching original MC's panel_select_files() dialog:
    /// pattern input + "Files only", "Case sensitive", "Using shell patterns" checkboxes.
    /// </summary>
    private void SelectUnselect(bool mark)
    {
        var title = mark ? "Select" : "Unselect";

        var d = new Dialog
        {
            Title = title,
            Width = 50,
            Height = 11,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(new Label { X = 1, Y = 1, Text = "Pattern:" });
        var patInput = new TextField
        {
            X = 1, Y = 2, Width = 46, Height = 1,
            Text = "*", ColorScheme = McTheme.Dialog,
        };
        d.Add(patInput);

        var filesOnlyCb = new CheckBox
        {
            X = 1, Y = 4, Text = "Files only",
            CheckedState = CheckState.Checked, ColorScheme = McTheme.Dialog,
        };
        var caseCb = new CheckBox
        {
            X = 1, Y = 5, Text = "Case sensitive",
            CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog,
        };
        var shellCb = new CheckBox
        {
            X = 1, Y = 6, Text = "Using shell patterns",
            CheckedState = CheckState.Checked, ColorScheme = McTheme.Dialog,
        };
        d.Add(filesOnlyCb, caseCb, shellCb);

        var ok = new Button { Text = title, IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            var pat = patInput.Text?.ToString() ?? "*";
            if (string.IsNullOrWhiteSpace(pat)) pat = "*";
            // Shell patterns use glob syntax; regex mode passes the pattern as-is.
            // FilterOptions in DirectoryListing already uses glob matching.
            _controller.ActivePanel.MarkByPattern(
                pat,
                caseSensitive: caseCb.CheckedState == CheckState.Checked,
                mark: mark,
                filesOnly: filesOnlyCb.CheckedState == CheckState.Checked);
            RefreshPanels();
            Application.RequestStop(d);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        patInput.SetFocus();
        Application.Run(d);
        d.Dispose();
    }

    private void InvertSelection()
    {
        _controller.ActivePanel.InvertMarking();
        RefreshPanels();
    }

    // --- Command menu: Compare directories ---

    /// <summary>
    /// Marks files that differ (name or size) between left and right panels.
    /// Equivalent to compare_dirs_cmd() / compare_dir_select() in the original C codebase.
    /// </summary>
    /// <summary>
    /// Compare directories using a method the user selects — matches original MC's
    /// compare_dirs_cmd() which offers Quick / Size only / Thorough via query_dialog().
    /// </summary>
    private void CompareDirs()
    {
        // Method selector dialog (Quick / Size only / Thorough / Cancel)
        int method = -1;
        var dlg = new Dialog
        {
            Title = "Compare directories",
            Width = 38,
            Height = 9,
            ColorScheme = McTheme.Dialog,
        };
        dlg.Add(new Label { X = 1, Y = 1, Text = "Select comparison method:" });
        var rg = new RadioGroup
        {
            X = 2, Y = 2,
            RadioLabels = ["Quick (timestamps)", "Size only", "Thorough (byte-by-byte)"],
            SelectedItem = 0,
            ColorScheme = McTheme.Dialog,
        };
        dlg.Add(rg);
        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { method = rg.SelectedItem; Application.RequestStop(dlg); };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(dlg);
        dlg.AddButton(ok); dlg.AddButton(cancel);
        Application.Run(dlg);
        dlg.Dispose();
        if (method < 0) return;

        var leftLookup  = _controller.LeftPanel.Entries
            .Where(e => !e.IsParentDir)
            .ToDictionary(e => e.Name);
        var rightLookup = _controller.RightPanel.Entries
            .Where(e => !e.IsParentDir)
            .ToDictionary(e => e.Name);

        bool IsDifferent(Mc.Core.Models.FileEntry left, Mc.Core.Models.FileEntry right) => method switch
        {
            0 => left.ModificationTime != right.ModificationTime || left.Size != right.Size, // Quick
            1 => left.Size != right.Size,                                             // Size only
            _ => !FilesAreIdentical(left.FullPath.Path, right.FullPath.Path),        // Thorough
        };

        foreach (var e in _controller.LeftPanel.Entries.Where(x => !x.IsParentDir))
        {
            e.IsMarked = !rightLookup.TryGetValue(e.Name, out var match)
                || IsDifferent(e, match);
        }
        foreach (var e in _controller.RightPanel.Entries.Where(x => !x.IsParentDir))
        {
            e.IsMarked = !leftLookup.TryGetValue(e.Name, out var match)
                || IsDifferent(e, match);
        }

        _controller.LeftPanel.RefreshMarking();
        _controller.RightPanel.RefreshMarking();
    }

    private static bool FilesAreIdentical(string a, string b)
    {
        try
        {
            var infoA = new FileInfo(a);
            var infoB = new FileInfo(b);
            if (!infoA.Exists || !infoB.Exists || infoA.Length != infoB.Length) return false;
            const int BUF = 65536;
            using var fa = File.OpenRead(a);
            using var fb = File.OpenRead(b);
            var bufA = new byte[BUF]; var bufB = new byte[BUF];
            int r;
            while ((r = fa.Read(bufA, 0, BUF)) > 0)
            {
                fb.ReadExactly(bufB, 0, r);
                if (!bufA.AsSpan(0, r).SequenceEqual(bufB.AsSpan(0, r))) return false;
            }
            return true;
        }
        catch { return false; }
    }

    // --- Command menu: External panelize ---

    private void ExternalPanelize()
    {
        var cmd = InputDialog.Show("External Panelize", "Command:", string.Empty);
        if (string.IsNullOrWhiteSpace(cmd)) return;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = "/bin/sh",
                Arguments              = $"-c \"{cmd}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                WorkingDirectory       = _controller.ActivePanel.CurrentPath.Path,
            };
            using var proc   = System.Diagnostics.Process.Start(psi);
            var output       = proc?.StandardOutput.ReadToEnd() ?? string.Empty;
            proc?.WaitForExit();

            var files = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.TrimEnd('\r'))
                .Where(f => f.Length > 0)
                .ToList();

            if (files.Count == 0)
            { MessageDialog.Show("External Panelize", "No files returned by command."); return; }

            // Inject into panel: navigate to current dir, mark all returned filenames.
            // Equivalent to external_panelize() in original MC — replaces panel content
            // with the command's output so F5/F6/F8 can operate on the results.
            var panelDir = _controller.ActivePanel.CurrentPath.Path.TrimEnd('/');
            var inPanel  = new HashSet<string>(
                files.Select(f => Path.IsPathRooted(f) ? f : Path.Combine(panelDir, f))
                     .Where(f => Path.GetDirectoryName(f) == panelDir)
                     .Select(Path.GetFileName)!,
                StringComparer.Ordinal);

            RefreshPanels();  // reload so entries reflect current disk state
            var panel = _controller.ActivePanel;
            panel.MarkAll(false);
            foreach (var e in panel.Entries.Where(e => !e.IsParentDir && inPanel.Contains(e.Name)))
                e.IsMarked = true;
            panel.RefreshMarking();

            int outsidePanel = files.Count - inPanel.Count;
            string msg = $"Marked {inPanel.Count} file(s) in current panel.";
            if (outsidePanel > 0)
                msg += $"\n{outsidePanel} file(s) were outside the current directory and were skipped.";
            MessageDialog.Show("External Panelize", msg);
        }
        catch (Exception ex) { MessageDialog.Error(ex.Message); }
    }

    // --- Panel menu: Listing format / Filter ---

    private void ShowListingFormatDialog(bool left)
    {
        var panel = left ? _leftPanelView : _rightPanelView;

        var d = new Dialog
        {
            Title        = "Listing Format",
            Width        = 40,
            Height       = 10,
            ColorScheme  = McTheme.Dialog,
        };

        var rg = new RadioGroup
        {
            X            = 2,
            Y            = 1,
            RadioLabels  = ["Full file list", "Brief file list", "Long (ls -l style)"],
            SelectedItem = panel.ListingMode == PanelListingMode.Brief ? 1
                         : panel.ListingMode == PanelListingMode.Long  ? 2 : 0,
            ColorScheme  = McTheme.Dialog,
        };
        d.Add(rg);

        var ok = new Button { X = Pos.Center() - 8, Y = 7, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            panel.ListingMode = rg.SelectedItem switch
            {
                1 => PanelListingMode.Brief,
                2 => PanelListingMode.Long,
                _ => PanelListingMode.Full,
            };
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 2, Y = 6, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    private void ShowFilterDialog(bool left)
    {
        var listing = left ? _controller.LeftPanel : _controller.RightPanel;
        var pattern = InputDialog.Show("Filter", "Filter pattern (* = all):", listing.Filter.Pattern);
        if (pattern == null) return;
        listing.Filter.Pattern = pattern;
        listing.Reload();
    }

    /// <summary>
    /// Encoding selector: lets the user choose the character encoding for the panel.
    /// The path is reloaded with an #enc: suffix, matching encoding_cmd() in the original C codebase.
    /// </summary>
    private void ShowEncodingDialog(bool left)
    {
        var listing = left ? _controller.LeftPanel : _controller.RightPanel;

        // All encodings registered with .NET (equivalent to iconv -l in the original MC)
        // Put common ones first, then append the rest alphabetically.
        var preferredFirst = new[] { "UTF-8", "ISO-8859-1", "ISO-8859-2", "ISO-8859-5",
            "ISO-8859-15", "KOI8-R", "KOI8-U", "CP1250", "CP1251", "CP1252",
            "CP866", "GB2312", "GBK", "BIG5", "SHIFT_JIS", "EUC-JP" };
        var allEncodings = System.Text.Encoding.GetEncodings()
            .Select(ei => ei.Name.ToUpperInvariant())
            .OrderBy(n => n)
            .Distinct()
            .ToList();
        // preferred first, then everything else
        var encodings = preferredFirst
            .Where(allEncodings.Contains)
            .Concat(allEncodings.Where(n => !preferredFirst.Contains(n)))
            .ToArray();

        var current  = listing.CurrentPath.Encoding ?? "UTF-8";
        var selected = current;

        var d = new Dialog
        {
            Title       = "Select encoding",
            Width       = 42,
            Height      = 20,
            ColorScheme = McTheme.Dialog,
        };

        // Filter field for quick search in encoding list
        d.Add(new Label { X = 1, Y = 1, Text = "Filter:", ColorScheme = McTheme.Dialog });
        var filterField = new TextField { X = 9, Y = 1, Width = 30, ColorScheme = McTheme.Dialog };

        var lv = new ListView
        {
            X = 1, Y = 2,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };
        d.Add(filterField, lv);

        string[] filtered = encodings;
        void ApplyFilter()
        {
            var q = filterField.Text?.ToString() ?? string.Empty;
            filtered = string.IsNullOrEmpty(q)
                ? encodings
                : encodings.Where(e => e.Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray();
            lv.SetSource(new ObservableCollection<string>(filtered));
            lv.SelectedItem = Math.Max(0, Array.IndexOf(filtered, selected));
        }
        filterField.TextChanged += (_, _) => ApplyFilter();
        ApplyFilter();
        lv.SelectedItem = Math.Max(0, Array.IndexOf(filtered, current));

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            if (lv.SelectedItem >= 0 && lv.SelectedItem < filtered.Length)
                selected = filtered[lv.SelectedItem];
            Application.RequestStop(d);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => { selected = current; Application.RequestStop(d); };
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (selected == current) return;
        var oldPath = listing.CurrentPath;
        var newPath = new VfsPath(oldPath.Scheme, oldPath.Host, oldPath.Port,
                                  oldPath.User, oldPath.Password, oldPath.Path,
                                  selected == "UTF-8" ? null : selected);
        listing.Load(newPath);
    }

    /// <summary>
    /// Directory tree browser.
    /// Shows a tree of directories the user can expand/collapse and navigate to.
    /// Equivalent to tree_cmd() / tree panel mode in the original C codebase.
    /// Enter on a directory with children expands/collapses it.
    /// Enter on a leaf directory, or the "Go" button, navigates the panel there.
    /// </summary>
    private void ShowTreeDialog(bool left)
    {
        var listing = left ? _controller.LeftPanel : _controller.RightPanel;
        if (!listing.CurrentPath.IsLocal)
        {
            NotImplemented("Directory tree (remote paths)");
            return;
        }

        var currentPath = listing.CurrentPath.Path;
        var rootPath    = OperatingSystem.IsWindows()
            ? (Path.GetPathRoot(currentPath) ?? "C:\\")
            : "/";

        // Pre-expand every ancestor of the current directory so it is visible on open.
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootPath };
        var ancestor = currentPath;
        while (!string.IsNullOrEmpty(ancestor) && ancestor != Path.GetPathRoot(ancestor))
        {
            expanded.Add(ancestor);
            ancestor = Path.GetDirectoryName(ancestor) ?? rootPath;
        }

        var nodes        = new List<(string FullPath, int Depth, bool HasChildren)>();
        var selectedIdx  = 0;
        string? navigateTo = null;

        ListView lv = null!;

        void Rebuild(string? selectPath)
        {
            nodes.Clear();

            void Visit(string path, int depth)
            {
                bool hasChildren;
                try   { hasChildren = Directory.EnumerateDirectories(path).Any(); }
                catch { hasChildren = false; }

                nodes.Add((path, depth, hasChildren));

                if (hasChildren && expanded.Contains(path))
                {
                    try
                    {
                        foreach (var sub in Directory.GetDirectories(path)
                                                     .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                            Visit(sub, depth + 1);
                    }
                    catch { /* access denied — skip */ }
                }
            }

            Visit(rootPath, 0);

            var display = new List<string>();
            selectedIdx = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                var (path, depth, hasCh) = nodes[i];
                var name   = depth == 0
                    ? path
                    : (Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) ?? path);
                var indent = new string(' ', depth * 2);
                var marker = !hasCh          ? "  "
                           : expanded.Contains(path) ? "▼ "
                           : "▶ ";
                display.Add(indent + marker + name);
                if (selectPath != null &&
                    string.Equals(path, selectPath, StringComparison.OrdinalIgnoreCase))
                    selectedIdx = i;
            }

            if (lv != null)
            {
                lv.SetSource(new ObservableCollection<string>(display));
                lv.SelectedItem = Math.Max(0, Math.Min(selectedIdx, nodes.Count - 1));
                lv.SetNeedsDraw();
            }
        }

        var d = new Dialog
        {
            Title       = "Directory tree",
            Width       = Dim.Fill() - 4,
            Height      = Dim.Fill() - 4,
            ColorScheme = McTheme.Dialog,
        };

        lv = new ListView
        {
            X           = 1, Y = 1,
            Width       = Dim.Fill(1),
            Height      = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };

        Rebuild(currentPath);   // lv is now assigned
        d.Add(lv);

        // Enter on a dir with children: expand/collapse.
        // Enter on a leaf dir: navigate immediately.
        lv.OpenSelectedItem += (_, _) =>
        {
            var idx = lv.SelectedItem;
            if (idx < 0 || idx >= nodes.Count) return;
            var (path, _, hasCh) = nodes[idx];
            if (hasCh)
            {
                if (expanded.Contains(path)) expanded.Remove(path);
                else                         expanded.Add(path);
                Rebuild(path);
            }
            else
            {
                navigateTo = path;
                Application.RequestStop(d);
            }
        };

        // F2 = rescan selected subtree, F8 = delete selected directory (original MC tree.c)
        lv.KeyDown += (_, k) =>
        {
            var idx = lv.SelectedItem;
            if (idx < 0 || idx >= nodes.Count) return;
            var (selPath, _, _) = nodes[idx];

            if (k.KeyCode == KeyCode.F2)
            {
                // Rescan: force re-expand the selected directory
                expanded.Add(selPath);
                Rebuild(selPath);
                k.Handled = true;
            }
            else if (k.KeyCode == KeyCode.F8)
            {
                if (selPath == rootPath) return;
                if (!MessageDialog.Confirm("Delete", $"Delete directory \"{Path.GetFileName(selPath)}\"?", "Yes", "No"))
                    return;
                try
                {
                    Directory.Delete(selPath, recursive: false);
                    expanded.Remove(selPath);
                    Rebuild(Path.GetDirectoryName(selPath) ?? rootPath);
                }
                catch (Exception ex) { MessageDialog.Error(ex.Message); }
                k.Handled = true;
            }
        };

        var go = new Button { X = Pos.Center() - 8, Y = Pos.Bottom(lv), Text = "Go", IsDefault = true };
        go.Accepting += (_, _) =>
        {
            var idx = lv.SelectedItem;
            if (idx >= 0 && idx < nodes.Count)
                navigateTo = nodes[idx].FullPath;
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 2, Y = Pos.Bottom(lv), Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(go); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (navigateTo != null)
        {
            listing.Load(VfsPath.FromLocal(navigateTo));
            RefreshPanels();
        }
    }

    // --- Command menu: Edit config files ---

    private void EditConfigFile(string path)
    {
        try { if (!File.Exists(path)) File.WriteAllText(path, string.Empty); }
        catch (Exception ex) { MessageDialog.Error(ex.Message); return; }

        var editorWin = new Window
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = McTheme.Dialog,
        };
        var editor = new EditorView(path);
        editor.X = 0; editor.Y = 0;
        editor.Width = Dim.Fill(); editor.Height = Dim.Fill();
        editor.RequestClose += (_, _) => Application.RequestStop(editorWin);
        editorWin.Title = editor.Title;
        editorWin.Add(editor);
        Application.Run(editorWin);
    }

    private static void ShowStatus(string message)
    {
        // Status messages are displayed in the panel status area on next draw
    }

    // --- Command menu: User menu ---

    /// <summary>
    /// Reads ~/.config/mc/menu (user) or system mc.menu, shows a listbox with
    /// hotkey letters displayed, and executes the chosen command via /bin/sh.
    /// Equivalent to user_menu_cmd() in src/filemanager/usermenu.c.
    /// File format: "key&lt;TAB&gt;Label" header + indented command lines.
    /// Macros: %f=file, %d=active dir, %D=other panel dir, %b=basename without ext, %e=extension.
    /// </summary>
    private void ShowUserMenu()
    {
        // Resolve menu file: user menu first, fall back to system menu
        var menuFile = ConfigPaths.MenuFile;
        if (!File.Exists(menuFile) || new FileInfo(menuFile).Length == 0)
        {
            // Try system locations used by original MC
            string[] systemPaths =
            [
                "/usr/share/mc/mc.menu",
                "/etc/mc/mc.menu",
                "/usr/local/share/mc/mc.menu",
            ];
            string? sysMenu = Array.Find(systemPaths, File.Exists);

            if (sysMenu != null)
            {
                menuFile = sysMenu;
            }
            else
            {
                // Create a useful default user menu matching the original mc.menu style
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(menuFile)!);
                    File.WriteAllText(menuFile,
                        "# Midnight Commander - User Menu\n" +
                        "# Format:  key<TAB>Label\n" +
                        "#          <TAB>command line(s)\n" +
                        "# Macros: %f=file  %d=active dir  %D=other dir  %b=basename  %e=extension\n\n" +
                        "v\tView file\n" +
                        "\t${PAGER:-less} \"%f\"\n\n" +
                        "e\tEdit file\n" +
                        "\t${EDITOR:-vi} \"%f\"\n\n" +
                        "c\tCopy to other panel\n" +
                        "\tcp -i \"%f\" \"%D\"\n\n" +
                        "m\tMove/rename to other panel\n" +
                        "\tmv -i \"%f\" \"%D\"\n\n" +
                        "d\tDiff with other panel file\n" +
                        "\tdiff \"%f\" \"%D/%f\"\n\n" +
                        "a\tArchive to .tar.gz\n" +
                        "\ttar czf \"%b.tar.gz\" \"%f\"\n\n" +
                        "x\tExtract .tar.gz / .tgz\n" +
                        "\ttar xzf \"%f\"\n");
                }
                catch (Exception ex)
                {
                    MessageDialog.Error($"Cannot create user menu:\n{ex.Message}");
                    return;
                }
            }
        }

        List<UserMenuEntry> allEntries;
        try   { allEntries = ParseUserMenuFile(menuFile); }
        catch (Exception ex) { MessageDialog.Error($"Cannot read user menu:\n{ex.Message}"); return; }

        // Filter entries by condition (+/= lines) — equivalent to check_conditions() in usermenu.c
        var curFile = GetCurrentEntry()?.Name ?? string.Empty;
        var curDir  = _controller.ActivePanel.CurrentPath.Path;
        var entries = allEntries
            .Where(e => e.Condition == null || EvaluateUserMenuCondition(e.Condition, curFile, curDir))
            .ToList();

        if (entries.Count == 0)
        {
            MessageDialog.Show("User menu", "No entries in menu file.\nEdit it with Command → Edit menu file.");
            return;
        }

        // Build display lines: "l  Label text" — hotkey letter highlighted by MC convention
        var displayLines = entries.Select(e =>
            e.HotKey != '\0' ? $"{e.HotKey}  {e.Label}" : $"   {e.Label}").ToList();

        // Dynamic dialog width: fit widest label (clamped to 62, matching original MC's max ~60+2)
        int maxLen = displayLines.Max(l => l.Length);
        int dlgW   = Math.Clamp(maxLen + 6, 40, 64);
        int dlgH   = Math.Min(entries.Count + 7, Application.Driver.Rows - 2);

        string? chosenCommand = null;

        var d = new Dialog
        {
            Title       = "User menu",
            Width       = dlgW,
            Height      = dlgH,
            ColorScheme = McTheme.Dialog,
        };
        var lv = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(new ObservableCollection<string>(displayLines));
        d.Add(lv);

        // Enter on a list item executes immediately (same as original MC listbox_run)
        lv.OpenSelectedItem += (_, _) =>
        {
            if (lv.SelectedItem >= 0) chosenCommand = entries[lv.SelectedItem].Command;
            Application.RequestStop(d);
        };

        // Hotkey letter press: jump directly to the matching entry and execute it
        d.KeyDown += (_, key) =>
        {
            char ch = (char)(key.KeyCode & ~KeyCode.ShiftMask);
            if (char.IsLetterOrDigit(ch))
            {
                int idx = entries.FindIndex(e => char.ToLowerInvariant(e.HotKey) == char.ToLowerInvariant(ch));
                if (idx >= 0)
                {
                    chosenCommand = entries[idx].Command;
                    Application.RequestStop(d);
                    key.Handled = true;
                }
            }
        };

        var btnRun = new Button { Text = "Run", IsDefault = true };
        btnRun.Accepting += (_, _) =>
        {
            if (lv.SelectedItem >= 0) chosenCommand = entries[lv.SelectedItem].Command;
            Application.RequestStop(d);
        };
        var btnEdit = new Button { Text = "Edit menu" };
        btnEdit.Accepting += (_, _) =>
        {
            Application.RequestStop(d);
            // Open the user menu file in the internal editor
            var userMenu = ConfigPaths.MenuFile;
            EditFileDirectly(userMenu);
        };
        var btnCancel = new Button { Text = "Cancel" };
        btnCancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(btnRun); d.AddButton(btnEdit); d.AddButton(btnCancel);

        lv.SetFocus();
        Application.Run(d);
        d.Dispose();

        if (string.IsNullOrWhiteSpace(chosenCommand)) return;

        // Macro expansion — matches original MC expand_format() (#18)
        var fileEntry   = GetCurrentEntry();
        var markedFiles = _controller.ActivePanel.GetMarkedEntries();

        // %{Prompt} — interactive prompt replacement
        var cmd = System.Text.RegularExpressions.Regex.Replace(chosenCommand,
            @"%\{([^}]*)\}",
            m =>
            {
                var promptText = m.Groups[1].Value;
                return InputDialog.Show("User menu prompt", promptText, string.Empty) ?? string.Empty;
            });

        // %s / %t — space-separated list of tagged/marked files (or current file if none marked)
        var taggedList = markedFiles.Count > 0
            ? string.Join(" ", markedFiles.Select(e => $"\"{e.FullPath.Path}\""))
            : (fileEntry != null ? $"\"{fileEntry.FullPath.Path}\"" : string.Empty);

        cmd = cmd
            .Replace("%f",  fileEntry?.FullPath.Path ?? string.Empty)
            .Replace("%b",  Path.GetFileNameWithoutExtension(fileEntry?.Name ?? string.Empty))
            .Replace("%e",  Path.GetExtension(fileEntry?.Name ?? string.Empty).TrimStart('.'))
            .Replace("%d",  _controller.ActivePanel.CurrentPath.Path)
            .Replace("%D",  _controller.InactivePanel.CurrentPath.Path)
            .Replace("%s",  taggedList)   // #18
            .Replace("%t",  taggedList);  // #18

        ExecuteUserMenuCommand(cmd);
    }

    /// <summary>Opens a file in the internal editor without refreshing history.</summary>
    private void EditFileDirectly(string path)
    {
        var editorWin = new Window
        {
            X = 0, Y = 0,
            Width = Dim.Fill(), Height = Dim.Fill(),
            ColorScheme = McTheme.Dialog,
        };
        var editor = new EditorView(path);
        editor.X = 0; editor.Y = 0;
        editor.Width = Dim.Fill(); editor.Height = Dim.Fill();
        editor.RequestClose += (_, _) => Application.RequestStop(editorWin);
        editorWin.Title = editor.Title;
        editorWin.Add(editor);
        Application.Run(editorWin);
    }

    /// <summary>
    /// Parses the MC user menu file.
    /// Non-indented line: optional single-char key, then whitespace, then label.
    /// Indented line(s): shell command body.
    /// Comment lines (#), blank lines, and condition lines (+/=) are skipped.
    /// </summary>
    /// <summary>
    /// Parses a mc.menu file and returns menu entries, each with an optional condition string.
    /// Condition lines (+/= lines) that immediately precede a header line are attached to that entry.
    /// Equivalent to check_conditions() / parse_mc_menu() in src/usermenu.c.
    /// </summary>
    private static List<UserMenuEntry> ParseUserMenuFile(string path)
    {
        var entries         = new List<UserMenuEntry>();
        string? label       = null;
        char    hotKey      = '\0';
        string? condition   = null; // condition attached to the CURRENT entry being built
        string? nextCond    = null; // condition seen just before the next header
        var     cmdLines    = new List<string>();

        void Flush()
        {
            if (label != null && cmdLines.Count > 0)
                entries.Add(new UserMenuEntry(label, string.Join("\n", cmdLines), hotKey, condition));
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            if (string.IsNullOrEmpty(rawLine)) { nextCond = null; continue; }
            var trimmed = rawLine.TrimStart();
            if (trimmed.StartsWith('#')) continue;

            // Condition line (+/= expression) — attach to the immediately following header
            if (trimmed.StartsWith('+') || trimmed.StartsWith('='))
            {
                nextCond = trimmed;
                continue;
            }

            bool isCommand = rawLine[0] is ' ' or '\t';

            if (isCommand)
            {
                if (label != null) cmdLines.Add(trimmed);
                nextCond = null; // a command line resets pending condition
            }
            else
            {
                Flush();

                // Parse header: single-char hotkey + whitespace
                if (rawLine.Length >= 2 && char.IsLetterOrDigit(rawLine[0]) && char.IsWhiteSpace(rawLine[1]))
                {
                    hotKey = rawLine[0];
                    label  = rawLine[1..].TrimStart();
                }
                else
                {
                    hotKey = '\0';
                    label  = rawLine.Trim();
                }
                cmdLines.Clear();
                condition = nextCond;
                nextCond  = null;
            }
        }
        Flush();

        return entries;
    }

    private sealed record UserMenuEntry(string Label, string Command, char HotKey, string? Condition);

    /// <summary>
    /// Evaluates a condition line from an mc.menu file.
    /// Supports the subset used by the original MC check_conditions():
    ///   + f pattern   — show if current filename matches shell pattern
    ///   + d pattern   — show if current directory matches shell pattern
    ///   + ! ...       — negate the condition
    ///   = ...         — same semantics as +
    /// Multiple space-separated clauses are ANDed (basic subset).
    /// </summary>
    private bool EvaluateUserMenuCondition(string condition, string fileName, string dir)
    {
        // Strip the leading '+' or '=' and whitespace
        var expr = condition.TrimStart('+', '=').Trim();
        bool negate = false;
        if (expr.StartsWith('!')) { negate = true; expr = expr[1..].TrimStart(); }

        bool result = EvaluateSingleCondition(expr, fileName, dir);
        return negate ? !result : result;
    }

    private bool EvaluateSingleCondition(string expr, string fileName, string dir)
    {
        // Tokenise: first char is the type, rest is the pattern
        if (expr.Length < 3) return true; // malformed → show entry
        char   type    = expr[0];
        string pattern = expr[2..].Trim();

        return type switch
        {
            'f' => MatchShellPattern(fileName, pattern),
            'd' => MatchShellPattern(dir,      pattern),
            't' => _controller.ActivePanel.MarkedCount > 0, // #39 true only when files are tagged
            _   => true, // unknown type → show entry
        };
    }

    /// <summary>Matches a filename against a shell glob pattern (* and ? only).</summary>
    private static bool MatchShellPattern(string input, string pattern)
    {
        // Use Regex.IsMatch with the glob converted to regex
        try
        {
            var rx = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                               .Replace("\\*", ".*")
                               .Replace("\\?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                input, rx,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch { return true; }
    }

    /// <summary>
    /// Runs a shell command for the user menu, suspending the TUI so interactive
    /// commands (editors, pagers, etc.) can use the full terminal.
    /// Equivalent to execute_menu_command() in the original C codebase.
    /// </summary>
    private void ExecuteUserMenuCommand(string command)
    {
        Application.Driver?.End();
        Console.Clear();

        var tmpScript = Path.Combine(Path.GetTempPath(), $"mc-menu-{Guid.NewGuid():N}.sh");
        try
        {
            File.WriteAllText(tmpScript, $"#!/bin/sh\ncd \"{_controller.ActivePanel.CurrentPath.Path}\"\n{command}\n");
            var psi = new System.Diagnostics.ProcessStartInfo("/bin/sh")
            {
                Arguments       = tmpScript,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit();
        }
        finally
        {
            try { File.Delete(tmpScript); } catch { }
        }

        // Press Enter to continue — matches original MC execute_menu_command() (#19)
        Console.Write("\nPress Enter to continue...");
        Console.ReadLine();

        Application.Driver?.Init();
        Application.LayoutAndDraw(true);
        RefreshPanels();
    }

    // --- Command menu: Command history ---

    /// <summary>
    /// Shows command history from the current session and the shell history file.
    /// Selecting an entry pastes it to the command line (does not execute).
    /// Equivalent to CK_History in the original C codebase.
    /// </summary>
    private void ShowCommandHistory()
    {
        // Collect history: session first, then shell history file (deduplicated)
        var history = new List<string>(_commandLine.History.Reverse<string>());

        try
        {
            var shell    = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
            var histFile = shell.Contains("zsh")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zsh_history")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bash_history");

            if (File.Exists(histFile))
            {
                foreach (var raw in Enumerable.Reverse(File.ReadAllLines(histFile)))
                {
                    // Zsh format: ": timestamp:duration;command" — strip prefix
                    var line = raw.StartsWith(": ") && raw.Contains(';')
                        ? raw[(raw.IndexOf(';') + 1)..]
                        : raw;
                    if (!string.IsNullOrWhiteSpace(line) && !history.Contains(line))
                        history.Add(line);
                    if (history.Count >= 200) break;
                }
            }
        }
        catch { /* history file not readable — use session history only */ }

        if (history.Count == 0)
        {
            MessageDialog.Show("Command history", "No command history available.");
            return;
        }

        string? selected = null;
        var d = new Dialog
        {
            Title       = "Command history",
            Width       = Dim.Fill() - 4,
            Height      = Dim.Fill() - 4,
            ColorScheme = McTheme.Dialog,
        };
        var lv = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(new ObservableCollection<string>(history));
        d.Add(lv);

        lv.OpenSelectedItem += (_, _) =>
        {
            if (lv.SelectedItem >= 0) selected = history[lv.SelectedItem];
            Application.RequestStop(d);
        };

        var use    = new Button { X = Pos.Center() - 8, Y = Pos.Bottom(lv), Text = "Use", IsDefault = true };
        use.Accepting += (_, _) =>
        {
            if (lv.SelectedItem >= 0) selected = history[lv.SelectedItem];
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 2, Y = Pos.Bottom(lv), Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(use); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (!string.IsNullOrEmpty(selected))
        {
            _commandLine.SetText(selected);
            _commandLine.Focus();
        }
    }

    // --- Command menu: Viewed/edited history ---

    /// <summary>
    /// Shows a list of files viewed (F3) and edited (F4) in this session.
    /// Selecting a file navigates the active panel to its directory.
    /// Equivalent to show_editor_viewer_history() in the original C codebase.
    /// </summary>
    private void ShowViewedEditedHistory()
    {
        if (_viewedFiles.Count == 0 && _editedFiles.Count == 0)
        {
            MessageDialog.Show("Viewed/edited files history", "No files have been viewed or edited yet.");
            return;
        }

        // Build combined list: edited first (most likely to revisit), then viewed
        var allEntries = new List<(string Path, string Tag)>();
        foreach (var p in _editedFiles) allEntries.Add((p, "[edited]"));
        foreach (var p in _viewedFiles.Where(v => !_editedFiles.Contains(v))) allEntries.Add((p, "[viewed]"));

        var display = allEntries.Select(e => $"{e.Tag} {e.Path}").ToList();

        (string Path, string Tag)? chosen = null;
        var d = new Dialog
        {
            Title       = "Viewed/edited files history",
            Width       = Dim.Fill() - 4,
            Height      = Dim.Fill() - 4,
            ColorScheme = McTheme.Dialog,
        };
        var lv = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(new ObservableCollection<string>(display));
        d.Add(lv);

        lv.OpenSelectedItem += (_, _) =>
        {
            if (lv.SelectedItem >= 0) chosen = allEntries[lv.SelectedItem];
            Application.RequestStop(d);
        };

        var view = new Button { X = Pos.Center() - 16, Y = Pos.Bottom(lv), Text = "View" };
        view.Accepting += (_, _) =>
        {
            if (lv.SelectedItem >= 0) chosen = (allEntries[lv.SelectedItem].Path, "[view]");
            Application.RequestStop(d);
        };
        var edit = new Button { X = Pos.Center() - 8, Y = Pos.Bottom(lv), Text = "Edit" };
        edit.Accepting += (_, _) =>
        {
            if (lv.SelectedItem >= 0) chosen = (allEntries[lv.SelectedItem].Path, "[edit]");
            Application.RequestStop(d);
        };
        var panel = new Button { X = Pos.Center() + 2, Y = Pos.Bottom(lv), Text = "Panel", IsDefault = true };
        panel.Accepting += (_, _) =>
        {
            if (lv.SelectedItem >= 0) chosen = allEntries[lv.SelectedItem];
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 10, Y = Pos.Bottom(lv), Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(view); d.AddButton(edit); d.AddButton(panel); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (chosen == null) return;

        if (chosen.Value.Tag == "[view]")
        {
            ViewFile(chosen.Value.Path);
        }
        else if (chosen.Value.Tag == "[edit]")
        {
            // Navigate and open editor
            var dir = Path.GetDirectoryName(chosen.Value.Path);
            if (dir != null) _controller.NavigateTo(VfsPath.FromLocal(dir));
            RefreshPanels();
            // Open in editor
            var editorWin = new Window { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), ColorScheme = McTheme.Dialog };
            var editor = new Mc.Editor.EditorView(chosen.Value.Path);
            editor.X = 0; editor.Y = 0; editor.Width = Dim.Fill(); editor.Height = Dim.Fill();
            editor.RequestClose += (_, _) => Application.RequestStop(editorWin);
            editorWin.Title = editor.Title; editorWin.Add(editor);
            Application.Run(editorWin); editorWin.Dispose();
        }
        else
        {
            // Navigate panel to the file's directory and select the file
            var dir = Path.GetDirectoryName(chosen.Value.Path);
            if (dir != null)
            {
                _controller.NavigateTo(VfsPath.FromLocal(dir));
                RefreshPanels();
            }
        }
    }

    // --- Command menu: Active VFS list ---

    /// <summary>
    /// Shows currently active VFS connections (non-local panel paths).
    /// Equivalent to vfs_list() → hotlist_show(LIST_VFSLIST) in the original C codebase.
    /// "Free VFSs" navigates both panels back to local paths.
    /// </summary>
    private void ShowActiveVfsList()
    {
        var leftPath  = _controller.LeftPanel.CurrentPath;
        var rightPath = _controller.RightPanel.CurrentPath;

        var entries = new List<(VfsPath Path, string Label)>();
        if (leftPath.IsRemote)  entries.Add((leftPath,  $"[Left]  {leftPath}"));
        if (rightPath.IsRemote) entries.Add((rightPath, $"[Right] {rightPath}"));

        if (entries.Count == 0)
        {
            MessageDialog.Show("Active VFS directories", "No active VFS connections.\nBoth panels are on the local filesystem.");
            return;
        }

        // Build richer display: [side] scheme://[user@]host[:port]/path
        var display = entries.Select(e =>
        {
            var p = e.Path;
            var userPart  = p.User != null ? $"{p.User}@" : string.Empty;
            var portPart  = p.Port.HasValue ? $":{p.Port}" : string.Empty;
            var hostPart  = p.Host != null ? $"{userPart}{p.Host}{portPart}" : string.Empty;
            var label     = hostPart.Length > 0 ? $"{p.Scheme}://{hostPart}{p.Path}" : p.ToString();
            return e.Label[..8] + label;  // e.g. "[Left]  ftp://user@host/path"
        }).ToList();
        VfsPath? chosen = null;
        bool freeAll  = false;

        var d = new Dialog
        {
            Title       = "Active VFS directories",
            Width       = 60,
            Height      = entries.Count + 9,
            ColorScheme = McTheme.Dialog,
        };
        var lv = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = entries.Count + 1,
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(new ObservableCollection<string>(display));
        d.Add(lv);

        var browse = new Button { X = Pos.Center() - 16, Y = entries.Count + 3, Text = "Browse", IsDefault = true };
        browse.Accepting += (_, _) =>
        {
            if (lv.SelectedItem >= 0) chosen = entries[lv.SelectedItem].Path;
            Application.RequestStop(d);
        };
        var free   = new Button { X = Pos.Center() - 4,  Y = entries.Count + 3, Text = "Free VFSs" };
        free.Accepting += (_, _) => { freeAll = true; Application.RequestStop(d); };
        var cancel = new Button { X = Pos.Center() + 8,  Y = entries.Count + 3, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(browse); d.AddButton(free); d.AddButton(cancel);
        Application.Run(d); d.Dispose();

        if (freeAll)
        {
            // Navigate both panels back to the local home directory
            var home = VfsPath.FromLocal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            _controller.LeftPanel.Load(home);
            _controller.RightPanel.Load(home);
            RefreshPanels();
        }
        else if (chosen != null)
        {
            _controller.NavigateTo(chosen);
            RefreshPanels();
        }
    }

    // --- Command menu: Background jobs ---

    /// <summary>
    /// Shows background file operation jobs.
    /// In the original MC, file operations can run in background (ENABLE_BACKGROUND).
    /// In this .NET port all file operations run as awaited async tasks with a progress
    /// dialog, so there are no truly "background" jobs to list.
    /// Equivalent to jobs_box() in the original C codebase.
    /// </summary>
    private void ShowBackgroundJobs()
    {
        // Prune finished jobs
        _backgroundJobs.RemoveAll(j => !j.Running && (j.Task?.IsCompleted ?? true) &&
                                       j.Status is "Done" or "Cancelled");

        if (_backgroundJobs.Count == 0)
        {
            MessageDialog.Show("Background jobs", "No background jobs running.");
            return;
        }

        var d = new Dialog
        {
            Title  = "Background Jobs",
            Width  = 68,
            Height = Math.Min(5 + _backgroundJobs.Count * 2, 22),
            ColorScheme = McTheme.Dialog,
        };

        for (int i = 0; i < _backgroundJobs.Count; i++)
        {
            var job = _backgroundJobs[i];
            d.Add(new Label { X = 1, Y = 1 + i * 2, Text = $"{job.Name}: {job.Status}" });
        }

        var listView = new ListView
        {
            X = 1, Y = 1, Width = Dim.Fill(1),
            Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };
        var items = _backgroundJobs.Select(j => $"{j.Name,-10} {(j.Running ? "Running" : "Finished"),-10} {j.Status}").ToList();
        listView.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(items));
        d.Add(listView);

        var kill = new Button { Text = "Kill" };
        kill.Accepting += (_, _) =>
        {
            var idx = listView.SelectedItem;
            if (idx >= 0 && idx < _backgroundJobs.Count)
                _backgroundJobs[idx].Cts.Cancel();
        };

        var close = new Button { Text = "Close", IsDefault = true };
        close.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(kill);
        d.AddButton(close);
        Application.Run(d);
    }

    // --- Not-yet-implemented stub ---

    private static void NotImplemented(string feature) =>
        MessageDialog.Show("Not Implemented",
            $"'{feature}' is not yet implemented in this .NET port.");

    // --- Options menu dialogs ---

    /// <summary>
    /// Main configuration dialog.
    /// Equivalent to mc_options_dialog() in the original C codebase.
    /// </summary>
    private void ShowConfigurationDialog()
    {
        var d = new Dialog
        {
            Title = "Configuration",
            Width = 62,
            Height = 22,
            ColorScheme = McTheme.Dialog,
        };

        CheckBox CB(int y, string label, bool val) => new CheckBox
        {
            X = 2, Y = y, Text = label,
            CheckedState = val ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };

        var verbose      = CB(1,  "Verbose operation",           _settings.VerboseOperation);
        var totals       = CB(2,  "Compute totals",              _settings.ComputeTotals);
        var autoSave     = CB(3,  "Auto save setup",             _settings.AutoSaveSetup);
        var showOutput   = CB(4,  "Show output of commands",     _settings.ShowOutputOfCommands);
        var useSubshell  = CB(5,  "Use subshell",                _settings.UseSubshell);
        var askRun       = CB(6,  "Ask before running programs", _settings.AskBeforeRunning);
        var useIntViewer = CB(8,  "Use internal view",           _settings.UseInternalViewer);
        var useIntEditor = CB(9,  "Use internal edit",           _settings.UseInternalEditor);

        d.Add(new Label { X = 2, Y = 11, Text = "External editor:", ColorScheme = McTheme.Dialog });
        var extEditor = new TextField
        {
            X = 20, Y = 11, Width = 38,
            Text = _settings.ExternalEditor,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(new Label { X = 2, Y = 12, Text = "External viewer:", ColorScheme = McTheme.Dialog });
        var extViewer = new TextField
        {
            X = 20, Y = 12, Width = 38,
            Text = _settings.ExternalViewer,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(verbose, totals, autoSave, showOutput, useSubshell, askRun,
              useIntViewer, useIntEditor, extEditor, extViewer);

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            _settings.VerboseOperation     = verbose.CheckedState     == CheckState.Checked;
            _settings.ComputeTotals        = totals.CheckedState      == CheckState.Checked;
            _settings.AutoSaveSetup        = autoSave.CheckedState    == CheckState.Checked;
            _settings.ShowOutputOfCommands = showOutput.CheckedState  == CheckState.Checked;
            _settings.UseSubshell          = useSubshell.CheckedState == CheckState.Checked;
            _settings.AskBeforeRunning     = askRun.CheckedState      == CheckState.Checked;
            _settings.UseInternalViewer    = useIntViewer.CheckedState == CheckState.Checked;
            _settings.UseInternalEditor    = useIntEditor.CheckedState == CheckState.Checked;
            _settings.ExternalEditor       = extEditor.Text?.ToString() ?? "vi";
            _settings.ExternalViewer       = extViewer.Text?.ToString() ?? "less";
            _settings.Save();
            Application.RequestStop(d);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    /// <summary>
    /// Panel options dialog.
    /// Equivalent to panel_options_box() in the original C codebase.
    /// </summary>
    private void ShowPanelOptionsDialog()
    {
        var d = new Dialog
        {
            Title = "Panel options",
            Width = 58,
            Height = 18,
            ColorScheme = McTheme.Dialog,
        };

        CheckBox CB(int y, string label, bool val) => new CheckBox
        {
            X = 2, Y = y, Text = label,
            CheckedState = val ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };

        var showHidden   = CB(1,  "Show hidden files",              _settings.ShowHiddenFiles);
        var showBackup   = CB(2,  "Show backup files",              _settings.ShowBackupFiles);
        var markMoves    = CB(3,  "Mark moves cursor down",         _settings.MarkMovesCursor);
        var miniStatus   = CB(4,  "Show mini status",               _settings.ShowMiniStatus);
        var lynxMotion   = CB(5,  "Lynx-like motion (← = parent)", _settings.LynxLikeMotion);
        var scrollbar    = CB(6,  "Show scrollbar",                 _settings.ShowScrollbar);
        var highlight    = CB(7,  "Highlight files by attributes",  _settings.HighlightFiles);
        var mixFiles     = CB(8,  "Mix all files (dirs + files)",   _settings.MixAllFiles);
        var caseSensitive= CB(9,  "Case-sensitive quick search",    _settings.QuickSearchCaseSensitive);
        var freeSpace    = CB(10, "Show free space",                _settings.ShowFreeSpace);
        d.Add(showHidden, showBackup, markMoves, miniStatus, lynxMotion,
              scrollbar, highlight, mixFiles, caseSensitive, freeSpace);

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            _settings.ShowHiddenFiles          = showHidden.CheckedState    == CheckState.Checked;
            _settings.ShowBackupFiles          = showBackup.CheckedState    == CheckState.Checked;
            _settings.MarkMovesCursor          = markMoves.CheckedState     == CheckState.Checked;
            _settings.ShowMiniStatus           = miniStatus.CheckedState    == CheckState.Checked;
            _settings.LynxLikeMotion           = lynxMotion.CheckedState    == CheckState.Checked;
            _settings.ShowScrollbar            = scrollbar.CheckedState     == CheckState.Checked;
            _settings.HighlightFiles           = highlight.CheckedState     == CheckState.Checked;
            _settings.MixAllFiles              = mixFiles.CheckedState      == CheckState.Checked;
            _settings.QuickSearchCaseSensitive = caseSensitive.CheckedState == CheckState.Checked;
            _settings.ShowFreeSpace            = freeSpace.CheckedState     == CheckState.Checked;
            _settings.Save();
            // Apply live settings to panels (#5 #6 #7 #9 #24)
            ApplyPanelSettings(_leftPanelView);
            ApplyPanelSettings(_rightPanelView);
            // Apply filter settings (ShowHidden, ShowBackup) to both panels (#4)
            ApplyFilterSettings();
            Application.RequestStop(d);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    /// <summary>
    /// Learn keys dialog — shows current key bindings.
    /// Equivalent to learn_keys() in the original C codebase (src/learn.c).
    /// </summary>
    private static void ShowLearnKeysDialog()
    {
        // Static binding table matching OnKeyDown and Ctrl+X submap
        var bindings = new[]
        {
            ("F1",          "Help"),
            ("F3",          "View file"),
            ("F4",          "Edit file"),
            ("F5",          "Copy"),
            ("F6",          "Move / Rename"),
            ("F7",          "Make directory"),
            ("F8",          "Delete"),
            ("F9",          "Menu bar"),
            ("F10",         "Quit"),
            ("Tab",         "Switch panel"),
            ("Ctrl+L",      "Refresh / Redraw screen"),
            ("Ctrl+I",      "File info"),
            ("Ctrl+R",      "Reload panel"),
            ("Ctrl+U",      "Swap panels"),
            ("Ctrl+T",      "Open terminal here"),
            ("Ctrl+\\",     "Directory hotlist"),
            ("Ctrl+H",      "Command history popup"),
            ("Ctrl+X C",    "Chmod"),
            ("Ctrl+X O",    "Chown"),
            ("Ctrl+X Q",    "Quick view panel"),
            ("Ctrl+X I",    "Info panel"),
            ("Ctrl+X T",    "Tree panel"),
            ("Ctrl+X A",    "Chattr"),
            ("Insert",      "Mark / Unmark file"),
            ("+ (keypad)",  "Select group"),
            ("- (keypad)",  "Unselect group"),
        };

        var d = new Dialog
        {
            Title = "Learn keys",
            Width = 60,
            Height = Math.Min(bindings.Length + 6, 28),
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = "Key binding          Action", ColorScheme = McTheme.Dialog });

        var lv = new ListView
        {
            X = 1, Y = 2,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };
        var items = bindings.Select(b => $"{b.Item1,-20} {b.Item2}").ToList();
        lv.SetSource(new ObservableCollection<string>(items));
        d.Add(lv);

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok);
        Application.Run(d); d.Dispose();
    }

    /// <summary>
    /// VFS settings dialog (cache timeout, FTP settings).
    /// Equivalent to configure_vfs() in the original C codebase.
    /// </summary>
    private void ShowAppearanceDialog()
    {
        var skins = McTheme.FindSkinFiles();
        var skinNames = skins.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        skinNames.Insert(0, "default");

        var currentSkin = _settings.ActiveSkin;
        var selectedIdx = skinNames.IndexOf(currentSkin);
        if (selectedIdx < 0) selectedIdx = 0;

        var d = new Dialog
        {
            Title  = "Appearance",
            Width  = 50,
            Height = 16,
            ColorScheme = McTheme.Dialog,
        };

        var label = new Label
        {
            X = 1, Y = 0,
            Text = "Select skin (color theme):",
            ColorScheme = McTheme.Dialog,
        };

        var lv = new ListView
        {
            X = 1, Y = 2,
            Width  = Dim.Fill(1),
            Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(skinNames));
        lv.SelectedItem = Math.Max(0, selectedIdx);

        var okBtn = new Button { Text = "OK", IsDefault = true };
        okBtn.Accepting += (_, _) =>
        {
            var idx      = lv.SelectedItem;
            var chosen   = idx >= 0 && idx < skinNames.Count ? skinNames[idx] : "default";
            _settings.ActiveSkin = chosen;
            if (chosen == "default")
                McTheme.ApplyDefault();
            else
            {
                var path = idx > 0 ? skins[idx - 1] : string.Empty;
                if (!string.IsNullOrEmpty(path))
                    McTheme.ApplySkin(path);
            }
            Application.LayoutAndDraw(true);
            Application.RequestStop(d);
        };

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(okBtn);
        d.AddButton(cancelBtn);
        d.Add(label, lv);
        Application.Run(d);
        d.Dispose();
    }

    private void ShowVfsSettingsDialog()
    {
        var d = new Dialog
        {
            Title = "Virtual File System",
            Width = 60,
            Height = 14,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 2, Y = 1, Text = "VFS cache timeout (sec):", ColorScheme = McTheme.Dialog });
        var timeout = new TextField
        {
            X = 28, Y = 1, Width = 8,
            Text = _settings.VfsCacheTimeout.ToString(),
            ColorScheme = McTheme.Dialog,
        };
        d.Add(new Label { X = 2, Y = 3, Text = "FTP anonymous password:", ColorScheme = McTheme.Dialog });
        var anonPass = new TextField
        {
            X = 28, Y = 3, Width = 28,
            Text = _settings.FtpAnonymousPassword,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(new Label { X = 2, Y = 4, Text = "FTP proxy host:", ColorScheme = McTheme.Dialog });
        var proxy = new TextField
        {
            X = 28, Y = 4, Width = 28,
            Text = _settings.FtpProxy,
            ColorScheme = McTheme.Dialog,
        };
        var passiveCb = new CheckBox
        {
            X = 2, Y = 6, Text = "Use FTP passive mode",
            CheckedState = _settings.FtpPassiveMode ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(timeout, anonPass, proxy, passiveCb);

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            if (int.TryParse(timeout.Text?.ToString(), out var t)) _settings.VfsCacheTimeout = t;
            _settings.FtpAnonymousPassword = anonPass.Text?.ToString() ?? string.Empty;
            _settings.FtpProxy             = proxy.Text?.ToString() ?? string.Empty;
            _settings.FtpPassiveMode       = passiveCb.CheckedState == CheckState.Checked;
            _settings.Save();
            Application.RequestStop(d);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    /// <summary>
    /// Confirmation options dialog.
    /// Equivalent to confirm_box() in the original C codebase.
    /// </summary>
    private void ShowConfirmationDialog()
    {
        var d = new Dialog
        {
            Title = "Confirmation",
            Width = 50,
            Height = 12,
            ColorScheme = McTheme.Dialog,
        };

        CheckBox CB(int y, string label, bool val) => new CheckBox
        {
            X = 2, Y = y, Text = label,
            CheckedState = val ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        var confirmDelete    = CB(1, "Confirm delete",    _settings.ConfirmDelete);
        var confirmOverwrite = CB(2, "Confirm overwrite", _settings.ConfirmOverwrite);
        var confirmMove      = CB(3, "Confirm move",      _settings.ConfirmMove);    // #31
        var confirmExec      = CB(4, "Confirm execute",   _settings.ConfirmExecute); // #46
        var confirmExit      = CB(5, "Confirm exit",      _settings.ConfirmExit);
        d.Add(confirmDelete, confirmOverwrite, confirmMove, confirmExec, confirmExit);

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            _settings.ConfirmDelete    = confirmDelete.CheckedState    == CheckState.Checked;
            _settings.ConfirmOverwrite = confirmOverwrite.CheckedState == CheckState.Checked;
            _settings.ConfirmMove      = confirmMove.CheckedState      == CheckState.Checked;
            _settings.ConfirmExecute   = confirmExec.CheckedState      == CheckState.Checked;
            _settings.ConfirmExit      = confirmExit.CheckedState      == CheckState.Checked;
            _settings.Save();
            Application.RequestStop(d);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    /// <summary>
    /// Layout dialog.
    /// Equivalent to layout_box() in the original C codebase.
    /// </summary>
    private void ShowLayoutDialog()
    {
        var d = new Dialog
        {
            Title = "Layout",
            Width = 54,
            Height = 18,
            ColorScheme = McTheme.Dialog,
        };

        var rg = new RadioGroup
        {
            X = 2, Y = 1,
            RadioLabels = ["Vertical split (side by side)", "Horizontal split (top/bottom)"],
            SelectedItem = _settings.HorizontalSplit ? 1 : 0,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(rg);

        CheckBox CB(int y, string label, bool val) => new CheckBox
        {
            X = 2, Y = y, Text = label,
            CheckedState = val ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };

        var showMenubar  = CB(4,  "Show menubar",     _settings.ShowMenubar);
        var showCmdLine  = CB(5,  "Show command line", _settings.ShowCommandLine);
        var showKeyBar   = CB(6,  "Show key bar",      _settings.ShowKeyBar);
        d.Add(showMenubar, showCmdLine, showKeyBar);

        d.Add(new Label { X = 2, Y = 8, Text = "Panel split %:", ColorScheme = McTheme.Dialog });
        var splitField = new TextField
        {
            X = 18, Y = 8, Width = 5,
            Text = _settings.PanelSplitRatio.ToString(),
            ColorScheme = McTheme.Dialog,
        };
        d.Add(splitField);
        var equalBtn = new Button { X = 24, Y = 8, Text = "Equal (50/50)" };
        equalBtn.Accepting += (_, _) => splitField.Text = "50";
        d.Add(equalBtn);

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            _settings.HorizontalSplit = rg.SelectedItem == 1;
            _settings.ShowMenubar     = showMenubar.CheckedState == CheckState.Checked;
            _settings.ShowCommandLine = showCmdLine.CheckedState == CheckState.Checked;
            _settings.ShowKeyBar      = showKeyBar.CheckedState  == CheckState.Checked;
            if (int.TryParse(splitField.Text?.ToString(), out var ratio) && ratio is >= 10 and <= 90)
                _settings.PanelSplitRatio = ratio;
            _settings.Save();
            ApplyLayoutSettings();
            Application.RequestStop(d);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    /// <summary>Apply show/hide and split-direction layout settings to the live UI elements. (#10)</summary>
    private void ApplyLayoutSettings()
    {
        _menuBar.Visible     = _settings.ShowMenubar;
        _commandLine.Visible = _settings.ShowCommandLine;
        _buttonBar.Visible   = _settings.ShowKeyBar;

        var pct = _settings.PanelSplitRatio;

        if (_settings.HorizontalSplit)
        {
            // Top/bottom layout (#10)
            _leftPanelView.X      = 0;
            _leftPanelView.Y      = 1;
            _leftPanelView.Width  = Dim.Fill();
            _leftPanelView.Height = Dim.Percent(pct) - 1;

            _rightPanelView.X      = 0;
            _rightPanelView.Y      = Pos.Bottom(_leftPanelView) - 1; // share bottom/top border
            _rightPanelView.Width  = Dim.Fill();
            _rightPanelView.Height = Dim.Fill(2);

            _leftOverlay.X      = 0;
            _leftOverlay.Y      = 1;
            _leftOverlay.Width  = Dim.Fill();
            _leftOverlay.Height = Dim.Percent(pct) - 1;

            _rightOverlay.X      = 0;
            _rightOverlay.Y      = Pos.Bottom(_leftPanelView) - 1;
            _rightOverlay.Width  = Dim.Fill();
            _rightOverlay.Height = Dim.Fill(2);
        }
        else
        {
            // Side-by-side layout (original behaviour)
            _leftPanelView.X      = 0;
            _leftPanelView.Y      = 1;
            _leftPanelView.Width  = Dim.Percent(pct);
            _leftPanelView.Height = Dim.Fill(2);

            _rightPanelView.X      = Pos.Right(_leftPanelView) - 1;
            _rightPanelView.Y      = 1;
            _rightPanelView.Width  = Dim.Fill();
            _rightPanelView.Height = Dim.Fill(2);

            _leftOverlay.X      = 0;
            _leftOverlay.Y      = 1;
            _leftOverlay.Width  = Dim.Percent(pct);
            _leftOverlay.Height = Dim.Fill(2);

            _rightOverlay.X      = Pos.Right(_leftPanelView) - 1;
            _rightOverlay.Y      = 1;
            _rightOverlay.Width  = Dim.Fill();
            _rightOverlay.Height = Dim.Fill(2);
        }

        Application.LayoutAndDraw(true);
    }
}
