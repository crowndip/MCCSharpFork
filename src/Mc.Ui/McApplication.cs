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
    private readonly VfsRegistry _vfsRegistry;
    private readonly HotlistManager _hotlist;

    private FilePanelView _leftPanelView = null!;
    private FilePanelView _rightPanelView = null!;
    private ButtonBarView _buttonBar = null!;
    private CommandLineView _commandLine = null!;
    private MenuBar _menuBar = null!;
    private MenuBarItem _favoritesMenu = null!;
    private HintsBarView _hintsBar = null!;

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

    public McApplication(FileManagerController controller, McSettings settings, VfsRegistry vfsRegistry)
    {
        _controller = controller;
        _settings = settings;
        _vfsRegistry = vfsRegistry;
        _hotlist = controller.Hotlist;

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
            new MenuItem("_File listing",      string.Empty, () => ToggleOverlayModeForPanel(left, PanelDisplayMode.Normal)),
            new MenuItem("_Listing format...", string.Empty, () => ShowListingFormatDialog(left)),
            new MenuItem("_Column config...",  string.Empty, () => ShowColumnConfigDialog(left)),
            new MenuItem("_Quick view",        "Ctrl+X Q",   () => ToggleOverlayModeForPanel(left, PanelDisplayMode.QuickView)),
            new MenuItem("_Info",              "Ctrl+X I",   () => ToggleOverlayModeForPanel(left, PanelDisplayMode.Info)),
            new MenuItem("_Tree",              "Ctrl+X T",   () => ToggleOverlayModeForPanel(left, PanelDisplayMode.Tree)),
            new MenuItem("_Panelize",          string.Empty, ExternalPanelize),
            new MenuItem("F_lat/Recursive view", string.Empty,
                () => (left ? _leftPanelView : _rightPanelView).ToggleRecursiveView()),
            null!,
            new MenuItem("_Sort order...",     string.Empty, ShowSortDialog),
            new MenuItem("_Filter...",         string.Empty, () => ShowFilterDialog(left)),
            new MenuItem("_Encoding...",       string.Empty, () => ShowEncodingDialog(left)),
            null!,
            new MenuItem("_FTP link...",       string.Empty, () => ConnectVfsLink("ftp")),

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
                    new("_Sync panels",                 "Alt+I",      SyncInactivePanelToActive),
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
                    new("Screen _list",                 string.Empty, ShowScreenList),
                    null!,
                    new("Edit e_xtension file",         string.Empty, () => EditConfigFile(ConfigPaths.ExtFile)),
                    new("Edit _menu file",              string.Empty, () => EditConfigFile(ConfigPaths.MenuFile)),
                    new("Edit hi_ghlighting group file",string.Empty, () => EditConfigFile(ConfigPaths.FileHighlightFile)),
                    new("Edit _favorites file",         string.Empty, () => EditConfigFile(ConfigPaths.FavoritesFile)),
                }),

                // ── Favorites ─────────────────────────────────────────────
                _favoritesMenu = BuildFavoritesMenu(),

                // ── Drives ────────────────────────────────────────────────
                BuildDrivesMenu(),

                // ── Compression ───────────────────────────────────────────
                new MenuBarItem("C_ompression", new MenuItem[]
                {
                    new("_Unpack here",     string.Empty, UnpackHere),
                    new("_Selection to ZIP", string.Empty, SelectionToZip),
                    new("Selection to _7z",  string.Empty, SelectionTo7z),
                    null!,
                    new("_Test archive",     string.Empty, TestArchive),
                }),

                // ── Tools ─────────────────────────────────────────────────
                new MenuBarItem("_Tools", new MenuItem[]
                {
                    new("Copy _directory to clipboard",  string.Empty, CopyDirToClipboard),
                    new("Copy full _path to clipboard",  string.Empty, CopyPathToClipboard),
                    new("Copy file _name to clipboard",  string.Empty, CopyNameToClipboard),
                    new("Copy tagged _paths",            string.Empty, CopyTaggedPathsToClipboard),
                    new("Copy tagged name_s",            string.Empty, CopyTaggedNamesToClipboard),
                    null!,
                    new("Open in file _manager",         string.Empty, ShowContextMenu),
                    new("Open with _default app",        string.Empty, OpenWithDefaultApp),
                    new("_Open with...",                 string.Empty, ShowOpenWith),
                    new("F_ile properties",              string.Empty, ShowProperties),
                    new("_Attributes (chmod)...",        string.Empty, Chmod),
                    null!,
                    new("_Touch (timestamps)...",        string.Empty, ShowTouch),
                    new("C_hecksum...",                  string.Empty, ShowChecksum),
                    new("Directory _size...",            string.Empty, ShowDirSize),
                    new("Folder size anal_yzer...",     string.Empty, ShowFolderAnalyzer),
                    new("_Batch rename...",              string.Empty, ShowBatchRename),
                    new("Find _duplicates...",           string.Empty, ShowDuplicateFinder),
                    null!,
                    new("Op_en terminal here",           "Ctrl+T",     OpenTerminalHere),
                    new("_Compare with diff tool",       string.Empty, CompareWithDiffTool),
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
                }),

                // ── Right ─────────────────────────────────────────────────
                new MenuBarItem("_Right", PanelMenuItems(left: false)),

                // ── About ─────────────────────────────────────────────────
                new MenuBarItem("_About", new MenuItem[]
                {
                    new("_License",        string.Empty, AboutDialog.ShowLicense),
                    new("_Github",         string.Empty, AboutDialog.ShowGitHub),
                    new("_Fork from",      string.Empty, AboutDialog.ShowForkFrom),
                    new("_Why forked",     string.Empty, AboutDialog.ShowWhyForked),
                    new("_New functions",  string.Empty, AboutDialog.ShowNewFunctions),
                    new("_System info",    string.Empty, AboutDialog.ShowSystemInfo),
                }),
            },
        };
        Add(_menuBar);

        // Split view
        // Hints bar height (1 row when shown, 0 when hidden)
        int hintsRows = _settings.ShowHints ? 1 : 0;
        int panelFill = 2 + hintsRows; // cmdline + buttonbar + optional hints

        _leftPanelView = new FilePanelView(_controller.LeftPanel)
        {
            X = 0, Y = 1,
            Width = Dim.Percent(50),
            Height = Dim.Fill(panelFill),
        };
        _leftPanelView.EntryActivated += OnPanelEntryActivated;
        _leftPanelView.BecameActive += (_, _) => SetActivePanel(_leftPanelView);
        _leftPanelView.ScrollOtherRequested += (_, delta) => _rightPanelView.ScrollBy(delta);
        _leftPanelView.IsActive = true;
        ApplyPanelSettings(_leftPanelView, left: true);
        // ApplyFilterSettings() is called after both panels are constructed (see below)
        _controller.LeftPanel.Changed += OnPanelDirectoryChanged;

        // Right panel overlaps the left panel's right border by 1 column so that
        // the shared divider is a single │ rather than a double ││. (#5)
        _rightPanelView = new FilePanelView(_controller.RightPanel)
        {
            X = Pos.Right(_leftPanelView) - 1, Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(panelFill),
        };
        _rightPanelView.EntryActivated += OnPanelEntryActivated;
        _rightPanelView.BecameActive += (_, _) => SetActivePanel(_rightPanelView);
        _rightPanelView.ScrollOtherRequested += (_, delta) => _leftPanelView.ScrollBy(delta);
        ApplyPanelSettings(_rightPanelView, left: false);
        _controller.RightPanel.Changed += OnPanelDirectoryChanged;

        // Apply filter settings now that both panels are constructed (#4)
        ApplyFilterSettings();

        // Cursor-change hooks: update overlay when active-panel cursor moves; also advance hints tip (#14)
        _leftPanelView.CursorChanged  += (_, _) =>
        {
            if (_rightMode != PanelDisplayMode.Normal) UpdateOverlay(false);
            _hintsBar?.NextTip();
        };
        _rightPanelView.CursorChanged += (_, _) =>
        {
            if (_leftMode  != PanelDisplayMode.Normal) UpdateOverlay(true);
            _hintsBar?.NextTip();
        };

        // Overlay views (initially hidden; replace the inactive panel in Quick View / Info modes)
        _leftOverlay = new View
        {
            X = 0, Y = 1,
            Width  = Dim.Percent(50),
            Height = Dim.Fill(panelFill),
            Visible = false,
            ColorScheme = McTheme.Panel,
        };
        _rightOverlay = new View
        {
            X      = Pos.Right(_leftPanelView) - 1, Y = 1,
            Width  = Dim.Fill(),
            Height = Dim.Fill(panelFill),
            Visible = false,
            ColorScheme = McTheme.Panel,
        };
        Add(_leftPanelView, _rightPanelView, _leftOverlay, _rightOverlay);

        // Hints bar (between panels and command line) (#14)
        _hintsBar = new HintsBarView
        {
            X = 0, Y = Pos.Bottom(_leftPanelView),
            Width = Dim.Fill(),
            Height = 1,
            Visible = _settings.ShowHints,
        };
        Add(_hintsBar);

        // Command line
        _commandLine = new CommandLineView
        {
            X = 0, Y = _settings.ShowHints ? Pos.Bottom(_hintsBar) : Pos.Bottom(_leftPanelView),
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

            // Ctrl+Shift+Enter → paste full path to command line (#37) — must be before Ctrl+Enter
            case KeyCode.Enter when keyEvent.IsCtrl && keyEvent.IsShift:
                PastePathToCommandLine();
                return true;

            // Ctrl+Enter → paste filename to command line (#8)
            case KeyCode.Enter when keyEvent.IsCtrl:
                PasteFilenameToCommandLine();
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
                // Alt+1..9 → jump to favorite/recent directory
                if (keyEvent.IsAlt && keyEvent.AsRune.Value >= '1' && keyEvent.AsRune.Value <= '9')
                {
                    int index = keyEvent.AsRune.Value - '1';
                    JumpToFavorite(index);
                    return true;
                }
                return base.OnKeyDown(keyEvent);
        }
    }

    private void JumpToFavorite(int index)
    {
        var favorites = _hotlist.Entries.Take(9).ToList();
        var allPaths = new List<string>();
        
        // Add favorites first
        allPaths.AddRange(favorites.Select(f => f.Path));
        
        // Add recent directories (skip duplicates)
        foreach (var path in _hotlist.RecentDirectories)
        {
            if (!allPaths.Contains(path))
                allPaths.Add(path);
            if (allPaths.Count >= 9) break;
        }
        
        if (index < allPaths.Count)
        {
            NavigateToPath(allPaths[index]);
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
                        entry.Size = size;
                        GetActivePanel().SetNeedsDraw();
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
    private void ApplyPanelSettings(FilePanelView panel, bool left = false)  // #5 #6 #7
    {
        panel.ShowFreeSpace             = _settings.ShowFreeSpace;
        panel.LynxLikeMotion           = _settings.LynxLikeMotion;
        panel.MarkMovesCursor          = _settings.MarkMovesCursor;
        panel.QuickSearchCaseSensitive = _settings.QuickSearchCaseSensitive;
        panel.ShowScrollbar            = _settings.ShowScrollbar;        // #9
        panel.ShowMiniStatus           = _settings.ShowMiniStatus;       // #24
        panel.ShowExecutableSuffix     = _settings.ShowExecutableSuffix; // #36
        panel.FollowSymlinks           = _settings.PanelFollowSymlinks;  // #37
        panel.SplitNameExtension       = left ? _settings.LeftSplitNameExtension
                                               : _settings.RightSplitNameExtension;
        panel.ColumnConfig             = left ? _settings.LeftPanelColumns
                                               : _settings.RightPanelColumns;
    }

    private void OnPanelEntryActivated(object? sender, FileEntry? entry)
    {
        if (entry == null) return;
        if (entry.IsDirectory || entry.IsParentDir)
        {
            var targetPath = entry.FullPath;
            // CdFollowsLinks: resolve symlinks to their physical path (#46)
            if (_settings.CdFollowsLinks && entry.IsSymlink && entry.IsSymlinkToDirectory)
            {
                try
                {
                    var resolved = Path.GetFullPath(entry.SymlinkTarget ?? entry.FullPath.Path);
                    targetPath = VfsPath.FromLocal(resolved);
                }
                catch { /* keep original path on error */ }
            }
            _controller.NavigateTo(targetPath);
            return;
        }
        // Symlink to directory: navigate into it (#37)
        if (entry.IsSymlink && entry.IsSymlinkToDirectory)
        {
            var targetPath = entry.FullPath;
            if (_settings.CdFollowsLinks && entry.SymlinkTarget != null)
            {
                try
                {
                    var resolved = Path.IsPathRooted(entry.SymlinkTarget)
                        ? entry.SymlinkTarget
                        : Path.GetFullPath(entry.SymlinkTarget,
                            Path.GetDirectoryName(entry.FullPath.Path) ?? "/");
                    targetPath = VfsPath.FromLocal(resolved);
                }
                catch { }
            }
            _controller.NavigateTo(targetPath);
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
        ".7z"                  => new VfsPath("7z",   null, null, null, null, filePath + "|"),
        ".rar"                 => new VfsPath("rar",  null, null, null, null, filePath + "|"),
        ".cab"                 => new VfsPath("cab",  null, null, null, null, filePath + "|"),
        ".iso"                 => new VfsPath("iso",  null, null, null, null, filePath + "|"),
        ".arj"                 => new VfsPath("arj",  null, null, null, null, filePath + "|"),
        ".lha" or ".lzh"       => new VfsPath("lha",  null, null, null, null, filePath + "|"),
        ".jar"                 => new VfsPath("jar",  null, null, null, null, filePath + "|"),
        ".tar"                 => new VfsPath("tar",  null, null, null, null, filePath + "|"),
        ".tgz" or ".tar.gz"    => new VfsPath("tar",  null, null, null, null, filePath + "|"),
        ".tar.bz2" or ".tbz2"  => new VfsPath("tar",  null, null, null, null, filePath + "|"),
        ".tar.xz"  or ".txz"   => new VfsPath("tar",  null, null, null, null, filePath + "|"),
        _                      => null,
    };

    // --- File operations ---

    /// <summary>
    /// If <paramref name="vfsPath"/> refers to a file inside an archive (contains '|'),
    /// extracts it to a temporary directory and returns the local temp path.
    /// Otherwise returns <paramref name="vfsPath"/> unchanged and sets <paramref name="tempDir"/> to null.
    /// The caller is responsible for deleting <paramref name="tempDir"/> when done.
    /// </summary>
    private string? ResolveArchiveEntryToLocalPath(string vfsPath, out string? tempDir)
    {
        tempDir = null;
        var pipeIdx = vfsPath.IndexOf('|');
        if (pipeIdx < 0) return vfsPath;  // not an archive path

        var archivePath = vfsPath[..pipeIdx];
        var innerPath   = vfsPath[(pipeIdx + 1)..].TrimStart('/');
        if (string.IsNullOrEmpty(innerPath)) return null;

        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        tempDir = Path.Combine(Path.GetTempPath(), "mc_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            if (ext == ".zip")
            {
                // Use built-in ZIP support
                using var zip = System.IO.Compression.ZipFile.OpenRead(archivePath);
                var zipEntry  = zip.Entries.FirstOrDefault(e =>
                    string.Equals(e.FullName.Replace('\\', '/'), innerPath, StringComparison.OrdinalIgnoreCase));
                if (zipEntry == null)
                {
                    Directory.Delete(tempDir, recursive: true);
                    tempDir = null;
                    MessageDialog.Error($"File not found inside archive:\n{innerPath}");
                    return null;
                }
                var destFile = Path.Combine(tempDir, Path.GetFileName(innerPath));
                using (var src = zipEntry.Open())
                using (var dst = File.Create(destFile))
                    src.CopyTo(dst);
                return destFile;
            }
            else
            {
                // Use 7z CLI for other archive types (7z, tar, tgz, etc.)
                var sevenZipExe = ResolveSevenZip();
                if (sevenZipExe == null)
                {
                    Directory.Delete(tempDir, recursive: true);
                    tempDir = null;
                    MessageDialog.Error("7-Zip executable not found.\nSet the path in Options → Configuration → 7-Zip provider.");
                    return null;
                }
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = sevenZipExe,
                    WorkingDirectory       = tempDir,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };
                psi.ArgumentList.Add("e");
                psi.ArgumentList.Add(archivePath);
                psi.ArgumentList.Add(innerPath);
                psi.ArgumentList.Add($"-o{tempDir}");
                psi.ArgumentList.Add("-y");
                System.Diagnostics.Process? proc;
                try { proc = System.Diagnostics.Process.Start(psi); }
                catch (System.ComponentModel.Win32Exception)
                {
                    Directory.Delete(tempDir, recursive: true);
                    tempDir = null;
                    MessageDialog.Error($"Failed to launch 7-Zip ({sevenZipExe}).\nCheck the path in Options → Configuration → 7-Zip provider.");
                    return null;
                }
                proc?.WaitForExit();
                var destFile = Path.Combine(tempDir, Path.GetFileName(innerPath));
                if (!File.Exists(destFile))
                {
                    Directory.Delete(tempDir, recursive: true);
                    tempDir = null;
                    MessageDialog.Error($"Could not extract file from archive:\n{innerPath}");
                    return null;
                }
                return destFile;
            }
        }
        catch (Exception ex)
        {
            try { if (tempDir != null) Directory.Delete(tempDir, recursive: true); } catch { }
            tempDir = null;
            MessageDialog.Error($"Error extracting from archive:\n{ex.Message}");
            return null;
        }
    }

    private FileEntry? GetCurrentEntry() => GetActivePanel().CurrentEntry;

    private void ViewCurrent()
    {
        var marked = _controller.ActivePanel.GetMarkedEntries();
        
        // If multiple files are marked, open them all
        if (marked.Count > 1)
        {
            foreach (var markedEntry in marked.Where(e => !e.IsDirectory && !e.IsParentDir))
            {
                var markedPath = ResolveArchiveEntryToLocalPath(markedEntry.FullPath.Path, out var markedTempDir);
                if (markedPath == null) continue;
                try
                {
                    if (_settings.UseInternalViewer)
                        ViewFile(markedPath);
                    else
                        LaunchExternalProgram(_settings.ExternalViewer, markedPath);
                }
                finally
                {
                    if (markedTempDir != null) try { Directory.Delete(markedTempDir, recursive: true); } catch { }
                }
            }
            return;
        }

        var entry = GetCurrentEntry();
        if (entry == null) return;
        // Directories are viewed by navigating into them (matches original MC do_view_cmd behaviour)
        if (entry.IsDirectory || entry.IsParentDir)
        {
            _controller.NavigateTo(entry.FullPath);
            RefreshPanels();
            return;
        }
        var path = ResolveArchiveEntryToLocalPath(entry.FullPath.Path, out var tempDir);
        if (path == null) return;
        try
        {
            if (_settings.UseInternalViewer)
                ViewFile(path);
            else
                LaunchExternalProgram(_settings.ExternalViewer, path);
        }
        finally
        {
            if (tempDir != null) try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
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

        var screen = new EditorScreen(path, readOnly: true);
        Application.Run(screen);
        screen.Dispose();
    }

    /// <summary>Launch an external viewer or editor with the given file path.</summary>
    private static void LaunchExternalProgram(string program, string filePath)
    {
        if (string.IsNullOrWhiteSpace(program)) return;

        // Parse the program field which may be:
        //   "C:\path\prog.exe"          – quoted path (may contain spaces)
        //   C:\TOOLS\prog.exe           – unquoted absolute path
        //   code --wait                 – command + flags
        program = program.Trim();

        string exe;
        string[] preArgs;

        if (program.StartsWith('"'))
        {
            int close = program.IndexOf('"', 1);
            exe     = close > 0 ? program[1..close] : program[1..];
            var rem = close > 0 ? program[(close + 1)..].Trim() : string.Empty;
            preArgs = rem.Length > 0 ? rem.Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
        }
        else
        {
            // If it looks like an absolute path (drive letter or /), treat the whole
            // string as the executable so paths with spaces don't need quoting.
            bool isAbsPath = program.Length >= 2 &&
                             (program[1] == ':' || program[0] == '/' || program.StartsWith(@"\\"));
            if (isAbsPath)
            {
                exe     = program;
                preArgs = [];
            }
            else
            {
                int sp  = program.IndexOf(' ');
                exe     = sp < 0 ? program : program[..sp];
                var rem = sp < 0 ? string.Empty : program[(sp + 1)..].Trim();
                preArgs = rem.Length > 0 ? rem.Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
            }
        }

        var allArgs = new string[preArgs.Length + 1];
        preArgs.CopyTo(allArgs, 0);
        allArgs[^1] = filePath;

        if (!ProcessHelper.TryLaunchArgs(exe, allArgs))
            MessageDialog.Error($"Could not launch external program:\n{exe}");
    }

    private void EditCurrent()
    {
        var marked = _controller.ActivePanel.GetMarkedEntries();
        
        // If multiple files are marked, open them all
        if (marked.Count > 1)
        {
            foreach (var markedEntry in marked.Where(e => !e.IsDirectory && !e.IsParentDir))
            {
                var markedPath = ResolveArchiveEntryToLocalPath(markedEntry.FullPath.Path, out var markedTempDir);
                if (markedPath == null) continue;
                _editedFiles.Remove(markedPath);
                _editedFiles.Insert(0, markedPath);
                
                try
                {
                    if (!_settings.UseInternalEditor)
                    {
                        LaunchExternalProgram(_settings.ExternalEditor, markedPath);
                    }
                    else
                    {
                        var screen = new EditorScreen(markedPath, readOnly: markedTempDir != null);
                        Application.Run(screen);
                        screen.Dispose();
                    }
                }
                finally
                {
                    if (markedTempDir != null) try { Directory.Delete(markedTempDir, recursive: true); } catch { }
                }
            }
            RefreshPanels();
            return;
        }

        var entry = GetCurrentEntry();
        string? path;
        string? tempDir = null;

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
            path = ResolveArchiveEntryToLocalPath(entry.FullPath.Path, out tempDir);
            if (path == null) return;
            _editedFiles.Remove(path); _editedFiles.Insert(0, path);
        }

        try
        {
            if (!_settings.UseInternalEditor)
            {
                LaunchExternalProgram(_settings.ExternalEditor, path);
                return;
            }

            var screen = new EditorScreen(path, readOnly: tempDir != null);
            Application.Run(screen);
            screen.Dispose();
            RefreshPanels();
        }
        finally
        {
            if (tempDir != null) try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private void CopyFiles()
    {
        var entry  = GetCurrentEntry();
        var marked = _controller.ActivePanel.GetMarkedEntries();
        // For single-file copy, pre-fill dest with dir+filename so user can rename inline (matches original MC)
        var inactiveDir = _controller.InactivePanel.CurrentPath.Path;
        var dest = marked.Count > 0 || entry == null
            ? inactiveDir
            : Path.Combine(inactiveDir, entry.Name);
        var sourceName    = marked.Count > 0 ? $"{marked.Count} files" : (entry?.Name ?? "marked files");
        var defaultSource = marked.Count > 0 ? "*" : (entry?.Name ?? "*");
        var opts = CopyMoveDialog.Show(false, sourceName, dest, defaultSource);
        if (opts?.Confirmed != true) return;

        // Overwrite confirmation: pre-check which files would be overwritten (#1)
        if (_settings.ConfirmOverwrite)
        {
            var sources = _controller.ActivePanel.GetMarkedEntries();
            if (sources.Count == 0 && entry != null) sources = new List<FileEntry> { entry };
            var resolvedDest = ResolveDestinationPath(opts.DestinationPath).Path;
            var destIsDir    = Directory.Exists(resolvedDest);
            var conflicts = sources
                .Where(e => !e.IsDirectory && (destIsDir
                    ? File.Exists(Path.Combine(resolvedDest, e.Name))
                    : sources.Count == 1 && File.Exists(resolvedDest)))
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

        var preserveAttrs      = opts.PreserveAttributes;
        var sourceMask         = string.IsNullOrWhiteSpace(opts.SourceMask) ? null : opts.SourceMask;
        var followSymlinks     = opts.FollowSymlinks;
        var diveIntoSubdir     = opts.DiveIntoSubdir;
        var stableSymlinks     = opts.StableSymlinks;
        var preserveExt2Attrs  = opts.PreserveExt2Attributes;  // #35
        var destPath = ResolveDestinationPath(opts.DestinationPath);
        RunFileOperation("Copy", opts.RunInBackground, (progress, ct) =>
            _controller.CopyMarkedAsync(entry, destPath, progress, ct,
                preserveAttributes: preserveAttrs,
                sourceMask: sourceMask,
                followSymlinks: followSymlinks,
                diveIntoSubdir: diveIntoSubdir,
                stableSymlinks: stableSymlinks,
                preserveExt2Attributes: preserveExt2Attrs));
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
            // Pre-fill with the full destination path (other panel dir + filename),
            // matching original MC: the user can change the filename to rename,
            // or change the directory part to move elsewhere.
            dest = Path.Combine(_controller.InactivePanel.CurrentPath.Path, entry.Name);
            sourceName = entry.Name;
        }
        else return;
        var moveSource = marked.Count > 0 ? "*" : (entry?.Name ?? "*");
        var opts = CopyMoveDialog.Show(true, sourceName, dest, moveSource);
        if (opts?.Confirmed != true) return;

        var moveMask = string.IsNullOrWhiteSpace(opts.SourceMask) ? null : opts.SourceMask;
        var destPath = ResolveDestinationPath(opts.DestinationPath);
        RunFileOperation("Move", opts.RunInBackground, (progress, ct) =>
            _controller.MoveMarkedAsync(entry, destPath, progress, ct, sourceMask: moveMask));
    }

    private VfsPath ResolveDestinationPath(string path)
    {
        if (Path.IsPathRooted(path))
            return VfsPath.FromLocal(path);
        // Relative path: resolve against the inactive panel's directory
        return VfsPath.FromLocal(
            Path.Combine(_controller.InactivePanel.CurrentPath.Path, path));
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
            // Start the operation on a thread-pool thread BEFORE showing the dialog.
            // When the operation finishes it calls progress.Close() which posts
            // Application.RequestStop to the main thread, unblocking Show() below.
            _ = Task.Run(async () =>
            {
                try { await operation(progress, progress.CancellationToken); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Application.Invoke(() => MessageDialog.Error(ex.Message)); }
                finally { progress.Close(); Application.Invoke(RefreshPanels); }
            });
            // Block the main thread in a nested modal event loop until the task finishes.
            progress.Show();
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
        _ = Task.Run(async () =>
        {
            try { await _controller.DeleteMarkedAsync(progress, progress.CancellationToken); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Application.Invoke(() => MessageDialog.Error(ex.Message)); }
            finally { progress.Close(); Application.Invoke(RefreshPanels); }
        });
        progress.Show();
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

    /// <summary>Toggles overlay mode for a specific panel (used by Left/Right menu items).</summary>
    private void ToggleOverlayModeForPanel(bool isLeft, PanelDisplayMode mode)
    {
        ref var storedMode = ref isLeft ? ref _leftMode : ref _rightMode;
        storedMode = (mode == PanelDisplayMode.Normal || storedMode == mode)
            ? PanelDisplayMode.Normal
            : mode;
        ApplyOverlay(isLeft, storedMode);
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
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
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
            Width = Dim.Fill(4),
            Height = Dim.Fill(4),
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

        // Replace button — only useful when a content pattern was searched
        bool hasContentPattern = !string.IsNullOrEmpty(opts.ContentPattern);
        var replaceBtn = new Button { Text = "Replace...", Enabled = false };
        replaceBtn.Accepting += (_, _) =>
        {
            var snapshot = resultPaths.ToList();
            afterClose = () => Mc.Ui.Dialogs.FindReplaceDialog.Show(snapshot, opts);
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
        if (hasContentPattern) d.AddButton(replaceBtn);
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

                // Precompute date/size filter boundaries (#7, #8)
                var now = DateTime.Now;
                DateTime? newerThanDate = opts.NewerThanDays > 0 ? now.AddDays(-opts.NewerThanDays) : null;
                DateTime? olderThanDate = opts.OlderThanDays > 0 ? now.AddDays(-opts.OlderThanDays) : null;
                long minBytes = opts.MinSizeKB * 1024;
                long maxBytes = opts.MaxSizeKB * 1024;

                foreach (var file in EnumerateFiles(startDir))
                {
                    if (token.IsCancellationRequested) break;

                    // Update progress label with current directory
                    var scanDir = Path.GetDirectoryName(file) ?? startDir;
                    Application.Invoke(() => statusLabel.Text = $" Scanning: {scanDir}");

                    // Date filter (#7)
                    if (newerThanDate.HasValue || olderThanDate.HasValue)
                    {
                        DateTime mtime;
                        try { mtime = File.GetLastWriteTime(file); } catch { continue; }
                        if (newerThanDate.HasValue && mtime < newerThanDate.Value) continue;
                        if (olderThanDate.HasValue && mtime > olderThanDate.Value) continue;
                    }

                    // Size filter (#8)
                    if (opts.MinSizeKB > 0 || opts.MaxSizeKB > 0)
                    {
                        long size;
                        try { size = new FileInfo(file).Length; } catch { continue; }
                        if (opts.MinSizeKB > 0 && size < minBytes) continue;
                        if (opts.MaxSizeKB > 0 && size > maxBytes) continue;
                    }

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

                // Search in archives if requested
                if (opts.SearchInArchives)
                {
                    SearchInArchives(startDir, opts, token, resultPaths, displayItems, statusLabel);
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
                if (hasContentPattern) replaceBtn.Enabled = n > 0;
            });
        });

        Application.Run(d);
        cts.Cancel();  // stop search if dialog closed via Esc
        cts.Dispose();
        d.Dispose();
        afterClose?.Invoke();

        // "Again": show the options dialog again so user can tweak and re-run
        if (restartSearch)
            ShowFindDialog();
    }

    /// <summary>Search inside archive files for matching files.</summary>
    private void SearchInArchives(string startDir, FindOptions opts, CancellationToken token,
        List<string> resultPaths, ObservableCollection<string> displayItems, Label statusLabel)
    {
        var archiveExtensions = new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".cab", ".iso", ".arj", ".lha", ".lzh", ".jar" };
        var archives = Directory.EnumerateFiles(startDir, "*.*", opts.SearchInSubdirs ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => archiveExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        foreach (var archivePath in archives)
        {
            if (token.IsCancellationRequested) break;
            
            Application.Invoke(() => statusLabel.Text = $" Scanning archive: {Path.GetFileName(archivePath)}");

            try
            {
                var vfsPath = new VfsPath(GetArchiveScheme(archivePath), null, null, null, null, archivePath + "|");
                var provider = _vfsRegistry.Resolve(vfsPath);
                var entries = provider.ListDirectory(vfsPath);

                foreach (var entry in entries.Where(e => !e.IsDirectory && !e.IsParentDir))
                {
                    if (token.IsCancellationRequested) break;

                    // Match file pattern
                    if (!string.IsNullOrEmpty(opts.FilePattern) && opts.FilePattern != "*")
                    {
                        var pattern = opts.FilePattern.Replace("*", ".*").Replace("?", ".");
                        if (!System.Text.RegularExpressions.Regex.IsMatch(entry.Name, pattern, 
                            opts.CaseSensitive ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            continue;
                    }

                    var displayPath = $"{Path.GetFileName(archivePath)}|{entry.Name}";
                    var fullPath = $"{archivePath}|{entry.Name}";
                    
                    Application.Invoke(() =>
                    {
                        resultPaths.Add(fullPath);
                        displayItems.Add(displayPath);
                        statusLabel.Text = $" Found {displayItems.Count} file(s)...";
                    });
                }
            }
            catch { /* Skip archives that can't be read */ }
        }
    }

    private static string GetArchiveScheme(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".zip" => "zip",
            ".7z" => "7z",
            ".rar" => "rar",
            ".tar" or ".gz" or ".bz2" or ".xz" => "tar",
            ".cab" => "cab",
            ".iso" => "iso",
            ".arj" => "arj",
            ".lha" or ".lzh" => "lha",
            ".jar" => "jar",
            _ => "7z"
        };
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
        win.Dispose();
    }

    private void LaunchShell()
    {
        // Capture any text already typed in the command line before suspending MC (#40)
        var pendingCmd = _commandLine.Text.Trim();

        Application.Driver?.End();
        Console.Clear();
        var shell = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe")
            : (Environment.GetEnvironmentVariable("SHELL") ?? "/bin/sh");

        var activePath = _controller.ActivePanel.CurrentPath;
        var workingDir = activePath.IsLocal && Directory.Exists(activePath.Path)
            ? activePath.Path
            : Environment.CurrentDirectory;

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = shell,
            UseShellExecute = false,
            WorkingDirectory = workingDir,
        };

        // For bash: pre-fill readline buffer with any typed command (#40)
        // We write a temp init file that sources .bashrc then sets READLINE_LINE.
        // For other shells, fall back to setting MC_PENDING_CMD env var as a hint.
        string? tempInit = null;
        if (!OperatingSystem.IsWindows() && !string.IsNullOrEmpty(pendingCmd))
        {
            var shellName = Path.GetFileName(shell);
            if (shellName is "bash")
            {
                tempInit = Path.GetTempFileName();
                var escaped = pendingCmd.Replace("'", "'\\''");
                File.WriteAllText(tempInit,
                    "[ -f ~/.bashrc ] && . ~/.bashrc\n" +
                    $"READLINE_LINE='{escaped}'\n" +
                    $"READLINE_POINT={pendingCmd.Length}\n");
                psi.ArgumentList.Add("--init-file");
                psi.ArgumentList.Add(tempInit);
            }
            else if (shellName is "zsh")
            {
                // zsh: pre-populate history so Up-arrow gives the command
                var escaped = pendingCmd.Replace("'", "'\\''");
                psi.EnvironmentVariables["MC_PENDING_CMD"] = pendingCmd;
                // ZDOTDIR trick: put command into a pre-executed zshrc snippet
                tempInit = Path.GetTempFileName();
                File.WriteAllText(tempInit,
                    "[ -f ~/.zshrc ] && source ~/.zshrc\n" +
                    $"print -z '{escaped}'\n");
                psi.EnvironmentVariables["ZDOTDIR"] = Path.GetDirectoryName(tempInit)!;
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(tempInit)!, ".zshrc"), File.ReadAllText(tempInit));
            }
            else
            {
                psi.EnvironmentVariables["MC_PENDING_CMD"] = pendingCmd;
            }
        }

        using var proc = System.Diagnostics.Process.Start(psi);
        proc?.WaitForExit();

        if (tempInit != null) try { File.Delete(tempInit); } catch { }

        Application.Driver?.Init();
        RefreshPanels();
        Application.LayoutAndDraw(true);
    }

    // ── Compression ───────────────────────────────────────────────────────

    private static readonly string[] SevenZipCandidates = ["7z", "7za", "7zz"];

    /// <summary>
    /// Resolves the 7z executable: tries the user-configured path first, then
    /// well-known names on PATH, then the Windows default install location.
    /// Returns null when 7z cannot be found.
    /// </summary>
    private string? ResolveSevenZip()
    {
        bool Probe(string exe)
        {
            try
            {
                using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "i")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                });
                p?.WaitForExit(5_000);
                return true;
            }
            catch { return false; }
        }

        var configured = _settings.SevenZipPath;
        if (!string.IsNullOrWhiteSpace(configured) && Probe(configured)) return configured;

        foreach (var c in SevenZipCandidates)
            if (Probe(c)) return c;

        const string winDefault = @"C:\Program Files\7-Zip\7z.exe";
        if (File.Exists(winDefault) && Probe(winDefault)) return winDefault;

        return null;
    }

    private void UnpackHere()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsDirectory)
        {
            MessageDialog.Error("Select an archive file first.");
            return;
        }

        var archivePath = entry.FullPath.Path;
        var destDir     = _controller.ActivePanel.CurrentPath.Path;

        var exe = ResolveSevenZip();
        if (exe == null)
        {
            MessageDialog.Error(
                "7-Zip executable not found.\n" +
                "Set the path in Options → Configuration → 7-Zip provider,\n" +
                "or install 7-Zip and ensure 7z is on PATH.");
            return;
        }

        var msg = $"Unpack \"{entry.Name}\"\ninto: {destDir}";
        if (Directory.EnumerateFileSystemEntries(destDir).Any())
            msg += "\n\nDestination has files — existing will be overwritten.";

        var runMode = AskRunMode("Unpack here", msg, "Unpack");
        if (runMode == null) return;

        if (runMode == false) // background
        {
            StartSevenZipBackground($"Unpack {entry.Name}", exe, psi =>
            {
                psi.ArgumentList.Add("x");
                psi.ArgumentList.Add(archivePath);
                psi.ArgumentList.Add($"-o{destDir}");
                psi.ArgumentList.Add("-y");
                psi.ArgumentList.Add("-bb1");
            });
            return;
        }

        // Foreground: count entries then show progress dialog.
        int totalEntries = CountArchiveEntries(exe, archivePath);
        int    exitCode  = 0;
        string errorText = string.Empty;

        using var progress = new ProgressDialog("Unpacking");
        _ = Task.Run(() =>
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };
                // x = extract with full paths; -bb1 = one line per file; -y = yes to all
                psi.ArgumentList.Add("x");
                psi.ArgumentList.Add(archivePath);
                psi.ArgumentList.Add($"-o{destDir}");
                psi.ArgumentList.Add("-y");
                psi.ArgumentList.Add("-bb1");

                using var proc = System.Diagnostics.Process.Start(psi)
                    ?? throw new InvalidOperationException("Failed to start 7z.");

                int done = 0;
                string? line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    // -bb1 outputs "- path/to/file" for each extracted entry
                    if (line.StartsWith("- ", StringComparison.Ordinal))
                    {
                        done++;
                        var name = line[2..];
                        progress.Report(new OperationProgress
                        {
                            CurrentFile = name,
                            FilesDone   = done,
                            TotalFiles  = totalEntries,
                            BytesDone   = done,
                            TotalBytes  = totalEntries > 0 ? totalEntries : done + 1,
                        });
                    }
                }

                errorText = proc.StandardError.ReadToEnd();
                proc.WaitForExit(300_000);
                exitCode = proc.ExitCode;
            }
            catch (Exception ex)
            {
                exitCode  = -1;
                errorText = ex.Message;
            }
            finally
            {
                progress.Close();
            }
        });

        progress.Show(); // blocks until Close() is called from the task

        if (exitCode != 0)
        {
            MessageDialog.Error("Not a valid archive.");
            return;
        }

        _controller.ActivePanel.Reload();
        _controller.InactivePanel.Reload();
    }

    private void TestArchive()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsDirectory)
        {
            MessageDialog.Error("Select an archive file first.");
            return;
        }

        var exe = ResolveSevenZip();
        if (exe == null)
        {
            MessageDialog.Error(
                "7-Zip executable not found.\n" +
                "Set the path in Options → Configuration → 7-Zip provider,\n" +
                "or install 7-Zip and ensure 7z is on PATH.");
            return;
        }

        var archivePath = entry.FullPath.Path;

        int dialogW = Math.Max(76, (Application.Driver?.Cols ?? 80) - 4);
        int dialogH = Math.Clamp((Application.Driver?.Rows ?? 40) * 2 / 3, 18, (Application.Driver?.Rows ?? 40) - 4);

        var d = new Dialog
        {
            Title       = $"Test archive: {entry.Name}",
            Width       = dialogW,
            Height      = dialogH,
            ColorScheme = McTheme.Dialog,
        };

        var tv = new TextView
        {
            X           = 1,
            Y           = 1,
            Width       = Dim.Fill(1),
            Height      = Dim.Fill(4),
            Text        = "Running 7z t …\n",
            ReadOnly    = true,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(tv);

        var btnClose = new Button
        {
            X         = Pos.Center(),
            Y         = Pos.AnchorEnd(2),
            Text      = "Close",
            IsDefault = true,
        };
        btnClose.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(btnClose);

        _ = Task.Run(() =>
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };
                psi.ArgumentList.Add("t");
                psi.ArgumentList.Add(archivePath);

                using var proc = System.Diagnostics.Process.Start(psi)
                    ?? throw new InvalidOperationException("Failed to start 7z.");

                string? line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    var captured = line;
                    Application.Invoke(() =>
                    {
                        tv.Text += captured + "\n";
                        tv.MoveEnd();
                        d.SetNeedsDraw();
                    });
                }

                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(300_000);

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Application.Invoke(() =>
                    {
                        tv.Text += "\n--- stderr ---\n" + stderr;
                        tv.MoveEnd();
                        d.SetNeedsDraw();
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Invoke(() =>
                {
                    tv.Text += $"\nError: {ex.Message}";
                    tv.MoveEnd();
                    d.SetNeedsDraw();
                });
            }
        });

        Application.Run(d);
        d.Dispose();
    }

    private void SelectionToZip() => SelectionToArchive("zip", "-tzip");
    private void SelectionTo7z()  => SelectionToArchive("7z",  "-t7z");

    private void SelectionToArchive(string extension, string formatSwitch)
    {
        var marked  = _controller.ActivePanel.GetMarkedEntries();
        List<FileEntry> sources;
        if (marked.Count > 0)
        {
            sources = [..marked];
        }
        else
        {
            var cur = GetCurrentEntry();
            if (cur == null)
            {
                MessageDialog.Error("No file or folder selected.");
                return;
            }
            sources = [cur];
        }

        var exe = ResolveSevenZip();
        if (exe == null)
        {
            MessageDialog.Error(
                "7-Zip executable not found.\n" +
                "Set the path in Options → Configuration → 7-Zip provider,\n" +
                "or install 7-Zip and ensure 7z is on PATH.");
            return;
        }

        var destDir     = _controller.ActivePanel.CurrentPath.Path;
        var timestamp   = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var archivePath = Path.Combine(destDir, $"Archive_{timestamp}.{extension}");

        var itemLabel = sources.Count == 1 ? $"\"{sources[0].Name}\"" : $"{sources.Count} items";
        var (runMode, chosenName) = AskPackMode(
            $"Pack to {extension.ToUpperInvariant()}",
            destDir,
            Path.GetFileName(archivePath),
            itemLabel);
        if (runMode == null) return;

        // Rebuild archivePath from user-edited filename.
        archivePath = Path.Combine(destDir, chosenName);

        if (runMode == false) // background
        {
            StartSevenZipBackground($"Pack {chosenName}", exe, psi =>
            {
                psi.WorkingDirectory = destDir;
                psi.ArgumentList.Add("a");
                psi.ArgumentList.Add(formatSwitch);
                psi.ArgumentList.Add("-mx=7");
                psi.ArgumentList.Add("-bb1");
                psi.ArgumentList.Add("-y");
                psi.ArgumentList.Add(archivePath);
                foreach (var e in sources)
                    psi.ArgumentList.Add(e.FullPath.Path);
            });
            return;
        }

        // Foreground: show progress dialog.
        int    exitCode  = 0;
        string errorText = string.Empty;

        using var progress = new ProgressDialog($"Creating {extension.ToUpperInvariant()}");
        _ = Task.Run(() =>
        {
            System.Diagnostics.Process? proc = null;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    WorkingDirectory       = destDir,
                };
                // a = add; formatSwitch = archive type; -mx=7 = Maximum compression; -bb1 = one line per file; -y = yes to all
                psi.ArgumentList.Add("a");
                psi.ArgumentList.Add(formatSwitch);
                psi.ArgumentList.Add("-mx=7");
                psi.ArgumentList.Add("-bb1");
                psi.ArgumentList.Add("-y");
                psi.ArgumentList.Add(archivePath);
                foreach (var e in sources)
                    psi.ArgumentList.Add(e.FullPath.Path);

                proc = System.Diagnostics.Process.Start(psi)
                    ?? throw new InvalidOperationException("Failed to start 7z.");

                // Kill the process when the user clicks Cancel in the progress dialog.
                progress.CancellationToken.Register(
                    () => { try { proc.Kill(entireProcessTree: true); } catch { } });

                int done = 0;
                string? line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    // -bb1 outputs "+ path/to/file" for each added entry
                    if (line.StartsWith("+ ", StringComparison.Ordinal) ||
                        line.StartsWith("U ", StringComparison.Ordinal))
                    {
                        done++;
                        var name = line[2..];
                        progress.Report(new OperationProgress
                        {
                            CurrentFile = name,
                            FilesDone   = done,
                            TotalFiles  = sources.Count,
                            BytesDone   = done,
                            TotalBytes  = sources.Count > 0 ? sources.Count : done + 1,
                        });
                    }
                }

                errorText = proc.StandardError.ReadToEnd();
                proc.WaitForExit(300_000);
                exitCode = proc.ExitCode;
            }
            catch (Exception ex)
            {
                exitCode  = -1;
                errorText = ex.Message;
            }
            finally
            {
                proc?.Dispose();
                progress.Close();
            }
        });

        progress.Show(); // blocks until Close() is called from the task

        if (progress.CancellationToken.IsCancellationRequested || exitCode != 0)
        {
            // Delete any partial archive left behind.
            try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch { }
            if (!progress.CancellationToken.IsCancellationRequested)
                MessageDialog.Error($"7-Zip failed (exit code {exitCode}).\n{errorText}");
            return;
        }

        _controller.ActivePanel.Reload();
        _controller.InactivePanel.Reload();
    }

    /// <summary>
    /// Shows a 3-button dialog: action / Background / Cancel.
    /// Returns true = run foreground, false = run background, null = cancelled.
    /// Used for operations that have no editable output name (e.g. Unpack here).
    /// </summary>
    private static bool? AskRunMode(string title, string message, string actionLabel)
    {
        bool? result = null;
        var lines    = message.Split('\n');
        int  width   = Math.Min(72, Math.Max(44, lines.Max(l => l.Length) + 6));
        int  height  = lines.Length + 6;

        var d = new Dialog { Title = title, Width = width, Height = height, ColorScheme = McTheme.Dialog };

        int y = 1;
        foreach (var line in lines)
            d.Add(new Label { X = 1, Y = y++, Text = line });

        var btnAction = new Button { Text = actionLabel, IsDefault = true };
        btnAction.Accepting += (_, _) => { result = true;  Application.RequestStop(d); };

        var btnBg = new Button { Text = "Background" };
        btnBg.Accepting += (_, _) => { result = false; Application.RequestStop(d); };

        var btnCancel = new Button { Text = "Cancel" };
        btnCancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(btnAction);
        d.AddButton(btnBg);
        d.AddButton(btnCancel);
        Application.Run(d);
        d.Dispose();
        return result;
    }

    /// <summary>
    /// Shows the pack confirmation dialog. The destination filename is pre-filled with
    /// <paramref name="defaultName"/> but is fully editable.
    /// Returns (true=foreground, false=background, null=cancelled, chosenFilename).
    /// </summary>
    private static (bool? mode, string archiveName) AskPackMode(
        string title, string destDir, string defaultName, string itemLabel)
    {
        bool?  mode        = null;
        string archiveName = defaultName;

        int width = Math.Min(72, Math.Max(52, Math.Max(destDir.Length, defaultName.Length) + 6));

        var d = new Dialog { Title = title, Width = width, Height = 10, ColorScheme = McTheme.Dialog };

        // Truncate destDir display if wider than the dialog interior.
        var dirDisplay = destDir.Length > width - 4
            ? "…" + destDir[^(width - 5)..]
            : destDir;

        d.Add(new Label { X = 1, Y = 1, Text = $"Pack {itemLabel} to:" });
        d.Add(new Label { X = 1, Y = 2, Text = dirDisplay });

        var nameField = new TextField
        {
            X = 1, Y = 4, Width = Dim.Fill(2), Height = 1,
            Text = defaultName,
            ColorScheme = McTheme.Dialog,
        };
        nameField.CursorPosition = defaultName.LastIndexOf('.') > 0
            ? defaultName.LastIndexOf('.')
            : defaultName.Length;
        d.Add(nameField);

        var btnPack = new Button { Text = "Pack" };
        btnPack.Accepting += (_, _) =>
        {
            archiveName = nameField.Text?.ToString() ?? defaultName;
            mode = true;
            Application.RequestStop(d);
        };

        var btnBg = new Button { Text = "Background" };
        btnBg.Accepting += (_, _) =>
        {
            archiveName = nameField.Text?.ToString() ?? defaultName;
            mode = false;
            Application.RequestStop(d);
        };

        var btnCancel = new Button { Text = "Cancel" };
        btnCancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(btnPack);
        d.AddButton(btnBg);
        d.AddButton(btnCancel);
        nameField.SetFocus();
        Application.Run(d);
        d.Dispose();
        return (mode, archiveName);
    }

    /// <summary>
    /// Starts a 7-Zip process as a true background OS thread tracked in
    /// <see cref="_backgroundJobs"/>. Uses async output reading to avoid stdout/stderr
    /// deadlocks. The process is killed when the job's <see cref="BackgroundJob.Cts"/> is
    /// cancelled (via the Kill button in the Background Jobs dialog).
    /// </summary>
    private void StartSevenZipBackground(string jobName, string exe,
        Action<System.Diagnostics.ProcessStartInfo> configureArgs)
    {
        var job = new BackgroundJob { Name = jobName };
        _backgroundJobs.Add(job);

        // Use a dedicated OS thread so the Terminal.Gui synchronization context
        // cannot schedule this work back onto the UI thread.
        var thread = new System.Threading.Thread(() =>
        {
            System.Diagnostics.Process? proc = null;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };
                configureArgs(psi);
                proc = System.Diagnostics.Process.Start(psi)
                    ?? throw new InvalidOperationException("Failed to start 7z.");

                // Kill the process if the user hits "Kill" in the Background Jobs dialog.
                job.Cts.Token.Register(() => { try { proc.Kill(entireProcessTree: true); } catch { } });

                // Drain stdout and stderr on dedicated threads to prevent pipe-buffer
                // deadlocks on Windows (BeginOutputReadLine has known WaitForExit issues).
                // Both extraction ("- file") and packing ("+ file") use "<prefix> <space> <name>".
                var drainOut = Task.Run(() =>
                {
                    string? line;
                    while ((line = proc.StandardOutput.ReadLine()) != null)
                        if (line.Length > 2 && line[1] == ' ')
                            job.Status = line[2..];
                });
                var drainErr = Task.Run(() => proc.StandardError.ReadToEnd());

                proc.WaitForExit(300_000);
                Task.WaitAll(drainOut, drainErr);
                job.Status = job.Cts.IsCancellationRequested
                    ? "Cancelled"
                    : proc.ExitCode == 0 ? "Done" : $"Error (exit {proc.ExitCode})";
            }
            catch (Exception ex)
            {
                job.Status = $"Error: {ex.Message}";
            }
            finally
            {
                proc?.Dispose();
                job.Running = false;
                Application.Invoke(RefreshPanels);
            }
        }) { IsBackground = true, Name = jobName };

        thread.Start();
    }

    /// <summary>
    /// Runs "7z l" on the archive and returns the total number of entries
    /// (files + folders) by parsing the summary line at the bottom.
    /// Returns 0 on any failure (progress bar will run without a known total).
    /// </summary>
    private static int CountArchiveEntries(string exe, string archivePath)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            psi.ArgumentList.Add("l");
            psi.ArgumentList.Add(archivePath);

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return 0;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(30_000);

            // Last data line: "2024-01-01  D....  …   N files, M folders"
            // or just "N files" with no folders line.
            var match = System.Text.RegularExpressions.Regex.Match(
                output,
                @"(\d+)\s+files?(?:,\s*(\d+)\s+folders?)?",
                System.Text.RegularExpressions.RegexOptions.RightToLeft);
            if (!match.Success) return 0;

            int files   = int.Parse(match.Groups[1].Value);
            int folders = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            return files + folders;
        }
        catch { return 0; }
    }

    private void OnCommandEntered(object? sender, string command)
    {
        var shell = OperatingSystem.IsWindows() ? "cmd" : "/bin/sh";
        var flag  = OperatingSystem.IsWindows() ? "/c" : "-c";
        var psi = new System.Diagnostics.ProcessStartInfo(shell) { UseShellExecute = false };
        psi.ArgumentList.Add(flag);
        psi.ArgumentList.Add(command);
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

    private void CopyTaggedPathsToClipboard()
    {
        var dir     = _controller.ActivePanel.CurrentPath.Path;
        var marked  = _controller.ActivePanel.GetMarkedEntries();
        string text;
        if (marked.Count > 0)
            text = string.Join("\n", marked.Select(e => e.FullPath.Path));
        else
        {
            var entry = GetCurrentEntry();
            if (entry == null) return;
            text = entry.FullPath.Path;
        }
        if (!ClipboardHelper.TrySet(text))
            MessageDialog.Error("Clipboard not available.");
    }

    private void CopyTaggedNamesToClipboard()
    {
        var marked = _controller.ActivePanel.GetMarkedEntries();
        string text;
        if (marked.Count > 0)
            text = string.Join("\n", marked.Select(e => e.Name));
        else
        {
            var entry = GetCurrentEntry();
            if (entry == null) return;
            text = entry.Name;
        }
        if (!ClipboardHelper.TrySet(text))
            MessageDialog.Error("Clipboard not available.");
    }

    // --- Tools: File manager / Open with / Properties / Attributes ---

    private void ShowContextMenu()
    {
        var entry = GetCurrentEntry();
        string path = entry != null ? entry.FullPath.Path : _controller.ActivePanel.CurrentPath.Path;
        string parent = Path.GetDirectoryName(path) ?? path;
        if (!ProcessHelper.OpenFileManager(path))
            MessageDialog.Error($"Could not open file manager.\nTried: nautilus, dolphin, nemo, xdg-open.\nPath: {parent}");
    }

    private void ShowOpenWith()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsDirectory)
        {
            MessageDialog.Show("Open with", "Select a file first.");
            return;
        }
        var app = InputDialog.Show("Open with", "Application:", string.Empty);
        if (string.IsNullOrWhiteSpace(app)) return;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName        = OperatingSystem.IsLinux() ? "xdg-open" : app,
                UseShellExecute = false,
            };
            if (OperatingSystem.IsLinux())
            {
                // xdg-open can't take an app; launch app directly with path arg
                psi.FileName = app;
            }
            psi.ArgumentList.Add(entry.FullPath.Path);
            using var _ = System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageDialog.Error($"Could not launch '{app}':\n{ex.Message}");
        }
    }

    private void OpenWithDefaultApp()
    {
        var entry = GetCurrentEntry();
        if (entry == null || entry.IsDirectory || entry.IsParentDir)
        {
            MessageDialog.Show("Open with default app", "Select a file first.");
            return;
        }

        // For archive entries, extract to temp first
        string? tempDir = null;
        var localPath = ResolveArchiveEntryToLocalPath(entry.FullPath.Path, out tempDir);
        if (localPath == null) return;
        var path = localPath;
        bool ok = false;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                    { UseShellExecute = true });
                ok = true;
            }
            else if (OperatingSystem.IsMacOS())
            {
                ok = ProcessHelper.TryLaunchArgs("open", path);
            }
            else
            {
                ok = ProcessHelper.TryLaunchArgs("xdg-open", path);
            }
        }
        catch (Exception ex)
        {
            MessageDialog.Error($"Could not open file with default app:\n{ex.Message}");
        }

        if (!ok && tempDir != null)
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
            return;
        }

        // Delay cleanup so the external app has time to open the file before the temp dir is removed
        if (tempDir != null)
        {
            var capturedDir = tempDir;
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                try { Directory.Delete(capturedDir, recursive: true); } catch { }
            });
        }
    }

    private void ShowProperties()
    {
        var entry = GetCurrentEntry();
        if (entry == null)
        {
            MessageDialog.Show("Properties", "No file selected.");
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            // Show the native Windows Explorer shell context menu (right-click menu).
            WindowsShellContextMenu.Show(entry.FullPath.Path);
        }
        else
        {
            InfoDialog.Show(entry);
        }
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

    private void ShowFolderAnalyzer()
    {
        // Use all marked directories; fall back to current directory entry or active panel path.
        var marked = _controller.ActivePanel.GetMarkedEntries()
            .Where(e => e.IsDirectory && !e.IsParentDir)
            .Select(e => e.FullPath.Path)
            .ToList();

        if (marked.Count == 0)
        {
            var entry = GetCurrentEntry();
            marked.Add(entry is { IsDirectory: true, IsParentDir: false }
                ? entry.FullPath.Path
                : _controller.ActivePanel.CurrentPath.Path);
        }

        FolderAnalyzerDialog.Show([.. marked]);
        RefreshPanels();
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

    // --- Tools: Duplicate finder ---

    private void ShowDuplicateFinder()
    {
        var startDir = _controller.ActivePanel.CurrentPath.Path;
        var duplicates = FindDuplicates(startDir);
        
        if (duplicates.Count == 0)
        {
            MessageDialog.Show("Find Duplicates", "No duplicate files found.");
            return;
        }

        // Show results in a dialog
        var displayItems = new ObservableCollection<string>();
        var resultPaths = new List<string>();
        
        foreach (var group in duplicates)
        {
            displayItems.Add($"--- {group.Count} duplicates, {FileSizeFormatter.Format(group.First().Size)} each ---");
            resultPaths.Add(string.Empty); // Placeholder for header
            foreach (var file in group)
            {
                displayItems.Add($"  {file.Path}");
                resultPaths.Add(file.Path);
            }
        }

        var d = new Dialog
        {
            Title = $"Duplicate Files ({duplicates.Count} groups)",
            Width = Dim.Fill(4),
            Height = Dim.Fill(4),
            ColorScheme = McTheme.Dialog,
        };

        var lv = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = Dim.Fill(3),
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(displayItems);
        d.Add(lv);

        var goBtn = new Button { Text = "Go to", IsDefault = true };
        goBtn.Accepting += (_, _) =>
        {
            var idx = lv.SelectedItem;
            if (idx >= 0 && idx < resultPaths.Count && !string.IsNullOrEmpty(resultPaths[idx]))
            {
                var path = resultPaths[idx];
                var dir = Path.GetDirectoryName(path) ?? startDir;
                _controller.NavigateTo(VfsPath.FromLocal(dir));
                RefreshPanels();
            }
            Application.RequestStop(d);
        };

        var closeBtn = new Button { Text = "Close" };
        closeBtn.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(goBtn);
        d.AddButton(closeBtn);
        Application.Run(d);
        d.Dispose();
    }

    private List<List<(string Path, long Size)>> FindDuplicates(string startDir)
    {
        var filesBySize = new Dictionary<long, List<string>>();
        
        // Group files by size
        foreach (var file in Directory.EnumerateFiles(startDir, "*", SearchOption.AllDirectories))
        {
            try
            {
                var size = new FileInfo(file).Length;
                if (!filesBySize.ContainsKey(size))
                    filesBySize[size] = new List<string>();
                filesBySize[size].Add(file);
            }
            catch { }
        }

        // For files with same size, compare content
        var duplicates = new List<List<(string Path, long Size)>>();
        foreach (var group in filesBySize.Where(g => g.Value.Count > 1))
        {
            var byHash = new Dictionary<string, List<string>>();
            foreach (var file in group.Value)
            {
                try
                {
                    var hash = ComputeFileHash(file);
                    if (!byHash.ContainsKey(hash))
                        byHash[hash] = new List<string>();
                    byHash[hash].Add(file);
                }
                catch { }
            }

            foreach (var hashGroup in byHash.Where(g => g.Value.Count > 1))
            {
                duplicates.Add(hashGroup.Value.Select(p => (p, group.Key)).ToList());
            }
        }

        return duplicates;
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
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
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
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
            Title       = "Listing Format",
            Width       = 52,
            Height      = 17,
            ColorScheme = McTheme.Dialog,
        };

        int sel = panel.ListingMode switch
        {
            PanelListingMode.Brief => 1,
            PanelListingMode.Long  => 2,
            PanelListingMode.User  => 3,
            _                      => 0,
        };
        var rg = new RadioGroup
        {
            X            = 2,
            Y            = 1,
            RadioLabels  = ["Full file list", "Brief file list", "Long (ls -l style)", "User defined"],
            SelectedItem = sel,
            ColorScheme  = McTheme.Dialog,
        };
        d.Add(rg);

        // Format-string input, enabled only when "User defined" is selected (#28)
        d.Add(new Label { X = 2, Y = 6, Text = "Format (field[:w],…):", ColorScheme = McTheme.Dialog });
        var fmtField = new TextField
        {
            X           = 2,
            Y           = 7,
            Width       = 46,
            Text        = panel.UserFormatString,
            ColorScheme = McTheme.Dialog,
            Enabled     = sel == 3,
        };
        d.Add(fmtField);

        // Split name/extension — only meaningful in Full mode
        var splitCb = new CheckBox
        {
            X           = 2,
            Y           = 9,
            Text        = "Split name / extension into separate columns",
            CheckedState = panel.SplitNameExtension ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
            Enabled     = sel == 0,
        };
        d.Add(splitCb);

        rg.SelectedItemChanged += (_, args) =>
        {
            fmtField.Enabled = args.SelectedItem == 3;
            splitCb.Enabled  = args.SelectedItem == 0;   // Full mode only
        };

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            panel.ListingMode = rg.SelectedItem switch
            {
                1 => PanelListingMode.Brief,
                2 => PanelListingMode.Long,
                3 => PanelListingMode.User,
                _ => PanelListingMode.Full,
            };
            if (rg.SelectedItem == 3 && !string.IsNullOrWhiteSpace(fmtField.Text?.ToString()))
                panel.UserFormatString = fmtField.Text!.ToString()!;

            panel.SplitNameExtension = splitCb.CheckedState == CheckState.Checked;
            if (left) _settings.LeftSplitNameExtension = panel.SplitNameExtension;
            else      _settings.RightSplitNameExtension = panel.SplitNameExtension;
            _settings.Save();
            Application.RequestStop(d);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    private void ShowColumnConfigDialog(bool left)
    {
        var panel = left ? _leftPanelView : _rightPanelView;
        var config = panel.ColumnConfig ?? (left ? _settings.LeftPanelColumns : _settings.RightPanelColumns);

        var dialog = new Dialogs.ColumnConfigDialog(config);
        Application.Run(dialog);
        
        if (dialog.Result != null && dialog.WasAccepted)
        {
            panel.ColumnConfig = dialog.Result;
            if (left) _settings.LeftPanelColumns = dialog.Result;
            else      _settings.RightPanelColumns = dialog.Result;
            _settings.Save();
            
            // Refresh panel to show new columns
            panel.SetNeedsDraw();
            _controller.ActivePanel.Reload();
        }
        
        dialog.Dispose();
    }

    private void ShowFilterDialog(bool left)
    {
        var listing = left ? _controller.LeftPanel : _controller.RightPanel;
        var pattern = InputDialog.Show("Filter", "Filter pattern (* = all):", listing.Filter.Pattern ?? string.Empty);
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
            Width       = Dim.Fill(4),
            Height      = Dim.Fill(4),
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

        var screen = new EditorScreen(path);
        Application.Run(screen);
        screen.Dispose();
    }

    private static void ShowStatus(string message)
    {
        // Status messages are displayed in the panel status area on next draw
    }

    // --- Favorites menu ---

    private void OnPanelDirectoryChanged(object? sender, EventArgs e)
    {
        var panel = sender as Mc.FileManager.DirectoryListing;
        var path = panel?.CurrentPath.Path;
        if (string.IsNullOrEmpty(path))
            return;
        
        // Track in recent directories
        _hotlist.AddRecent(path);
        RebuildFavoritesMenu();
    }

    private MenuBarItem BuildFavoritesMenu()
    {
        var items = new List<MenuItem>
        {
            new("_Add current folder", "Ctrl+X H", AddCurrentDirToHotlist),
            new("_Clear recent", string.Empty, () => { _hotlist.ClearRecent(); RebuildFavoritesMenu(); }),
            null!,
        };

        var currentPath = _leftPanelView?.Listing?.CurrentPath?.Path ?? "";
        int shortcutNum = 1;

        // Pinned favorites (from hotlist)
        var favorites = _hotlist.Entries.Take(9).ToList();
        foreach (var entry in favorites)
        {
            var captured = entry.Path;
            var isCurrent = captured == currentPath;
            var label = (isCurrent ? "★ " : "  ") + entry.Label;
            var shortcut = shortcutNum <= 9 ? $"Alt+{shortcutNum}" : "";
            items.Add(new MenuItem(label, shortcut, () => NavigateToPath(captured)));
            shortcutNum++;
        }

        // Separator if we have recent directories
        if (_hotlist.RecentDirectories.Count > 0)
        {
            items.Add(null!);
            items.Add(new MenuItem("Recent:", string.Empty, null) { CanExecute = () => false });
        }

        // Recent directories (non-pinned)
        var maxRecent = Math.Min(9 - shortcutNum + 1, _hotlist.RecentDirectories.Count);
        for (int i = 0; i < maxRecent; i++)
        {
            var path = _hotlist.RecentDirectories[i];
            // Skip if already in favorites
            if (favorites.Any(f => f.Path == path)) continue;
            
            var isCurrent = path == currentPath;
            var label = (isCurrent ? "★ " : "  ") + Path.GetFileName(path) + " (" + Path.GetDirectoryName(path) + ")";
            var shortcut = shortcutNum <= 9 ? $"Alt+{shortcutNum}" : "";
            items.Add(new MenuItem(label, shortcut, () => NavigateToPath(path)));
            shortcutNum++;
        }

        if (favorites.Count == 0 && _hotlist.RecentDirectories.Count == 0)
        {
            items.Add(new MenuItem("(no favorites or recent)", string.Empty, null) { CanExecute = () => false });
        }

        return new MenuBarItem("F_avorites", items.ToArray());
    }

    private void NavigateToPath(string path)
    {
        try
        {
            _controller.NavigateTo(Mc.Core.Vfs.VfsPath.FromLocal(path));
        }
        catch (Exception ex)
        {
            MessageDialog.Error($"Cannot navigate to {path}: {ex.Message}");
        }
    }

    private void RebuildFavoritesMenu()
    {
        _favoritesMenu = BuildFavoritesMenu();
        // Replace the Favorites slot (index 3) in the menu bar
        var menus = _menuBar.Menus.ToList();
        // Find Favorites by title
        int idx = menus.FindIndex(m => m?.Title?.ToString()?.Contains("avorites") == true);
        if (idx >= 0)
            menus[idx] = _favoritesMenu;
        else
            menus.Insert(3, _favoritesMenu);
        _menuBar.Menus = menus.ToArray();
        _menuBar.SetNeedsDraw();
    }

    private void AddCurrentFolderToFavorites()
    {
        var path = _controller.ActivePanel.CurrentPath.Path;
        if (string.IsNullOrEmpty(path))
        {
            MessageDialog.Error("Cannot add empty path to favorites.");
            return;
        }
        bool added = FavoritesManager.Add(path);
        if (added)
            RebuildFavoritesMenu();
        else
            MessageDialog.Show("Favorites", $"'{path}' is already in favorites.");
    }

    private void NavigateToFavorite(string path)
    {
        _controller.NavigateTo(VfsPath.FromLocal(path));
    }

    // --- Drives menu ---

    private MenuBarItem BuildDrivesMenu()
    {
        var items = new List<MenuItem>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                string displayName;
                string rootPath = drive.Name;
                bool ready = drive.IsReady;

                if (ready)
                {
                    string label = string.Empty;
                    try { label = drive.VolumeLabel; } catch { }

                    string typeTag = drive.DriveType switch
                    {
                        DriveType.CDRom     => "CD",
                        DriveType.Network   => "Net",
                        DriveType.Removable => "Removable",
                        DriveType.Ram       => "RAM",
                        _                   => string.Empty,
                    };

                    string spaceTag = string.Empty;
                    try
                    {
                        long used  = drive.TotalSize - drive.AvailableFreeSpace;
                        long total = drive.TotalSize;
                        spaceTag = $"({FormatDriveSize(used)} / {FormatDriveSize(total)})";
                    }
                    catch { }

                    string baseName = rootPath.TrimEnd('/', '\\');
                    string meta = string.Join("  ", new[] { label, typeTag.Length > 0 ? $"[{typeTag}]" : string.Empty, spaceTag }
                                                    .Where(s => !string.IsNullOrEmpty(s)));
                    displayName = meta.Length > 0 ? $"{baseName}  {meta}" : baseName;
                }
                else
                {
                    displayName = $"{rootPath.TrimEnd('/', '\\')}  [not ready]";
                }

                var captured = rootPath;
                items.Add(new MenuItem(displayName, string.Empty,
                    () => _controller.NavigateTo(VfsPath.FromLocal(captured)),
                    canExecute: () => ready));
            }
        }
        catch { /* drive enumeration failed */ }

        if (items.Count > 0) items.Add(null!);
        items.Add(new MenuItem("_Open location...", string.Empty, OpenLocation));

        return new MenuBarItem("_Drives", items.ToArray());
    }

    private static string FormatDriveSize(long bytes)
    {
        const long GB = 1_073_741_824L;
        const long MB = 1_048_576L;
        if (bytes >= GB)  return $"{bytes / (double)GB:0.#} GB";
        if (bytes >= MB)  return $"{bytes / (double)MB:0.#} MB";
        return $"{bytes / 1024.0:0.#} KB";
    }

    private void OpenLocation()
    {
        var path = InputDialog.Show("Open Location",
            "Enter local or network path:", string.Empty);
        if (string.IsNullOrWhiteSpace(path)) return;
        _controller.NavigateTo(VfsPath.Parse(path.Trim()));
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
        int dlgH   = Math.Min(entries.Count + 7, (Application.Driver?.Rows ?? 40) - 2);

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

        // Macro expansion — matches original MC expand_format() (#18 #30)
        var fileEntry      = GetCurrentEntry();
        var otherPanelView = _controller.ActivePanel == _controller.LeftPanel ? _rightPanelView : _leftPanelView;
        var otherFileEntry = otherPanelView.CurrentEntry;
        var markedFiles    = _controller.ActivePanel.GetMarkedEntries();

        // %{Prompt} — interactive prompt replacement
        var cmd = System.Text.RegularExpressions.Regex.Replace(chosenCommand,
            @"%\{([^}]*)\}",
            m =>
            {
                var promptText = m.Groups[1].Value;
                return InputDialog.Show("User menu prompt", promptText, string.Empty) ?? string.Empty;
            });

        // %s / %t — space-separated list of tagged/marked files (full paths) (or current file if none marked)
        var taggedList = markedFiles.Count > 0
            ? string.Join(" ", markedFiles.Select(e => $"\"{e.FullPath.Path}\""))
            : (fileEntry != null ? $"\"{fileEntry.FullPath.Path}\"" : string.Empty);

        // %m — marked file names only (no directory) (#30)
        var markedNames = markedFiles.Count > 0
            ? string.Join(" ", markedFiles.Select(e => $"\"{e.Name}\""))
            : (fileEntry != null ? $"\"{fileEntry.Name}\"" : string.Empty);

        // %a — archive name: VFS path root if inside a VFS archive, else empty (#30)
        var activePathStr = _controller.ActivePanel.CurrentPath.Path;
        var archiveName   = activePathStr.Contains('#') ? activePathStr[..(activePathStr.IndexOf('#') + 1)] : string.Empty;

        // %n — filename stripped of leading dot (e.g. ".bashrc" → "bashrc"); for non-hidden files same as name
        var fileName = fileEntry?.Name ?? string.Empty;
        var nameNoLeadingDot = fileName.TrimStart('.');

        // %l — symlink target path of the current file (empty if not a symlink)
        var symlinkTarget = (fileEntry?.IsSymlink == true) ? (fileEntry.SymlinkTarget ?? string.Empty) : string.Empty;

        // %x — extension including the dot (e.g. ".c", ".txt"); empty if no extension
        var extWithDot = Path.GetExtension(fileName);

        cmd = cmd
            .Replace("%f",  fileEntry?.FullPath.Path ?? string.Empty)
            .Replace("%p",  fileName)                                                                      // basename of current file in active panel
            .Replace("%P",  otherFileEntry?.Name ?? string.Empty)                                          // basename of current file in other panel
            .Replace("%b",  Path.GetFileNameWithoutExtension(fileName))                                    // basename without extension
            .Replace("%n",  nameNoLeadingDot)                                                              // filename stripped of leading dot
            .Replace("%e",  Path.GetExtension(fileName).TrimStart('.'))                                    // extension without dot
            .Replace("%x",  extWithDot)                                                                    // extension with dot (e.g. ".c")
            .Replace("%l",  symlinkTarget)                                                                 // symlink target path
            .Replace("%d",  _controller.ActivePanel.CurrentPath.Path)
            .Replace("%D",  _controller.InactivePanel.CurrentPath.Path)
            .Replace("%s",  taggedList)   // #18
            .Replace("%t",  taggedList)   // #18
            .Replace("%m",  markedNames)  // #30 marked file names only
            .Replace("%a",  archiveName); // #30 archive name

        ExecuteUserMenuCommand(cmd);
    }

    /// <summary>Opens a file in the internal editor without refreshing history.</summary>
    private void EditFileDirectly(string path)
    {
        var screen = new EditorScreen(path);
        Application.Run(screen);
        screen.Dispose();
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
        catch { return false; }
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
            var safePath = _controller.ActivePanel.CurrentPath.Path.Replace("'", "'\\''");
            File.WriteAllText(tmpScript, $"#!/bin/sh\ncd '{safePath}'\n{command}\n");
            var psi = new System.Diagnostics.ProcessStartInfo("/bin/sh") { UseShellExecute = false };
            psi.ArgumentList.Add(tmpScript);
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
            Width       = Dim.Fill(4),
            Height      = Dim.Fill(4),
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
            Width       = Dim.Fill(4),
            Height      = Dim.Fill(4),
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
            var screen = new Mc.Editor.EditorScreen(chosen.Value.Path);
            Application.Run(screen); screen.Dispose();
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

    // --- Command menu: Screen list (#15) ---

    /// <summary>
    /// Shows the screen list dialog — a numbered list of the file manager and all files
    /// that have been opened in the editor or viewer during this session.
    /// Selecting a screen re-opens it (or returns to the file manager for screen 0).
    /// Equivalent to do_screen_list() in src/filemanager/screen.c.
    /// </summary>
    private void ShowScreenList()
    {
        // Build screen entries: 0 = MC (file manager), then editors, then viewers
        var screens = new List<(string Label, string Path, bool IsEditor)>();
        screens.Add(("  0  MC  Midnight Commander", string.Empty, false));  // index 0 = main MC

        int idx = 1;
        foreach (var p in _editedFiles)
        {
            screens.Add(($"  {idx,-3} Edit  {p}", p, true));
            idx++;
        }
        foreach (var p in _viewedFiles.Where(v => !_editedFiles.Contains(v)))
        {
            screens.Add(($"  {idx,-3} View  {p}", p, false));
            idx++;
        }

        int chosenIdx = -1;
        var d = new Dialog
        {
            Title       = "Screen list",
            Width       = Math.Min(78, Application.Screen.Width - 4),
            Height      = Math.Min(screens.Count + 6, Application.Screen.Height - 4),
            ColorScheme = McTheme.Dialog,
        };

        var lv = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };
        var displayItems = screens.Select(s => s.Label).ToList();
        lv.SetSource(new ObservableCollection<string>(displayItems));
        lv.SelectedItem = 0;
        d.Add(lv);

        lv.OpenSelectedItem += (_, _) =>
        {
            chosenIdx = lv.SelectedItem;
            Application.RequestStop(d);
        };

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => { chosenIdx = lv.SelectedItem; Application.RequestStop(d); };
        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);

        lv.SetFocus();
        Application.Run(d); d.Dispose();

        if (chosenIdx < 0) return;
        if (chosenIdx == 0) return;  // screen 0 = MC — already there

        var chosen = screens[chosenIdx];
        if (chosen.IsEditor)
            EditFileDirectly(chosen.Path);
        else
            ViewFile(chosen.Path);
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
        if (_backgroundJobs.Count == 0)
        {
            MessageDialog.Show("Background jobs", "No background jobs.");
            return;
        }

        var d = new Dialog
        {
            Title  = "Background Jobs",
            Width  = 72,
            Height = Math.Clamp(_backgroundJobs.Count + 6, 8, 22),
            ColorScheme = McTheme.Dialog,
        };

        var listView = new ListView
        {
            X = 1, Y = 1, Width = Dim.Fill(1),
            Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };

        void RefreshList()
        {
            var items = _backgroundJobs
                .Select(j => $"{(j.Running ? "Running " : "Done    ")}  {j.Name,-30}  {j.Status}")
                .ToList();
            listView.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(items));
        }

        RefreshList();
        d.Add(listView);

        var kill = new Button { Text = "Kill" };
        kill.Accepting += (_, _) =>
        {
            var idx = listView.SelectedItem;
            if (idx >= 0 && idx < _backgroundJobs.Count)
                _backgroundJobs[idx].Cts.Cancel();
        };

        var removeFinished = new Button { Text = "Remove finished" };
        removeFinished.Accepting += (_, _) =>
        {
            _backgroundJobs.RemoveAll(j => !j.Running);
            if (_backgroundJobs.Count == 0)
            {
                Application.RequestStop(d);
                return;
            }
            RefreshList();
        };

        var close = new Button { Text = "Close", IsDefault = true };
        close.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(kill);
        d.AddButton(removeFinished);
        d.AddButton(close);
        Application.Run(d);
        d.Dispose();
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
            Width = 68,
            Height = 24,
            ColorScheme = McTheme.Dialog,
        };

        CheckBox CB(int y, string label, bool val) => new CheckBox
        {
            X = 2, Y = y, Text = label,
            CheckedState = val ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };

        McTextField TF(int y, string val) => new McTextField
        {
            X = 22, Y = y, Width = 42,
            Text = val,
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
        var extEditor = TF(11, _settings.ExternalEditor);

        d.Add(new Label { X = 2, Y = 13, Text = "External viewer:", ColorScheme = McTheme.Dialog });
        var extViewer = TF(13, _settings.ExternalViewer);

        d.Add(new Label { X = 2, Y = 15, Text = "7-Zip provider:", ColorScheme = McTheme.Dialog });
        var sevenZip  = TF(15, _settings.SevenZipPath);
        d.Add(new Label { X = 2, Y = 16,
            Text = "  e.g. C:\\Program Files\\7-Zip\\7z.exe",
            ColorScheme = McTheme.Dialog });

        d.Add(verbose, totals, autoSave, showOutput, useSubshell, askRun,
              useIntViewer, useIntEditor, extEditor, extViewer, sevenZip);

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
            _settings.SevenZipPath         = sevenZip.Text?.ToString()  ?? string.Empty;
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
        var execSuffix   = CB(11, "Show * suffix for executables",  _settings.ShowExecutableSuffix);  // #36
        var followLinks  = CB(12, "Symlinks to dirs in dir color",  _settings.PanelFollowSymlinks);   // #37
        d.Add(showHidden, showBackup, markMoves, miniStatus, lynxMotion,
              scrollbar, highlight, mixFiles, caseSensitive, freeSpace,
              execSuffix, followLinks);

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
            _settings.ShowExecutableSuffix     = execSuffix.CheckedState    == CheckState.Checked;  // #36
            _settings.PanelFollowSymlinks      = followLinks.CheckedState   == CheckState.Checked;  // #37
            _settings.Save();
            // Apply live settings to panels (#5 #6 #7 #9 #24)
            ApplyPanelSettings(_leftPanelView,  left: true);
            ApplyPanelSettings(_rightPanelView, left: false);
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
            ("Alt+I",       "Sync panels (copy active path to other)"),
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

        // Interactive key tester section (#44)
        // The dialog is split: top half = binding list, bottom = interactive key tester.
        const int listHeight = 16;
        const int dialogHeight = listHeight + 8;

        var d = new Dialog
        {
            Title = "Learn keys",
            Width = 60,
            Height = dialogHeight,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = "Key binding          Action", ColorScheme = McTheme.Dialog });

        var lv = new ListView
        {
            X = 1, Y = 2,
            Width = Dim.Fill(1),
            Height = listHeight,
            ColorScheme = McTheme.Panel,
        };
        var items = bindings.Select(b => $"{b.Item1,-20} {b.Item2}").ToList();
        lv.SetSource(new ObservableCollection<string>(items));
        d.Add(lv);

        // Separator and key tester (#44)
        int sep = listHeight + 2;
        d.Add(new Label { X = 1, Y = sep,     Text = new string('─', 56), ColorScheme = McTheme.Dialog });
        d.Add(new Label { X = 1, Y = sep + 1, Text = "Key tester — press any key to identify it:", ColorScheme = McTheme.Dialog });
        var keyLabel = new Label
        {
            X = 1, Y = sep + 2,
            Width = Dim.Fill(1),
            Text = "(waiting for keypress...)",
            ColorScheme = McTheme.Dialog,
        };
        d.Add(keyLabel);

        // The ListView intercepts all keys for navigation; we use its KeyDown event
        // to capture keys in the tester area and also let the list handle them.
        d.KeyDown += (_, k) =>
        {
            // Build a readable key description (#44)
            var parts = new System.Text.StringBuilder();
            if (k.IsCtrl)  parts.Append("Ctrl+");
            if (k.IsAlt)   parts.Append("Alt+");
            if (k.IsShift && k.KeyCode != (k.KeyCode & ~KeyCode.ShiftMask)) parts.Append("Shift+");

            var code = k.KeyCode & ~KeyCode.CtrlMask & ~KeyCode.AltMask & ~KeyCode.ShiftMask;
            var rune = k.AsRune;
            if (rune.Value >= 32)
                parts.Append((char)rune.Value);
            else
                parts.Append(code.ToString());

            // Find matching action in binding table
            string desc = string.Empty;
            var keyStr = parts.ToString();
            var match = bindings.FirstOrDefault(b => b.Item1.Equals(keyStr, StringComparison.OrdinalIgnoreCase));
            if (match != default)
                desc = $"  →  {match.Item2}";

            keyLabel.Text = $"Pressed: {keyStr}{desc}";
        };

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
        // Built-in themes first, then "default" (MC blue), then file-based skins
        var skinNames = new List<string> { "WhiteOnBlack", "ColorOnWhite", "default" };
        skinNames.AddRange(skins.Select(f => Path.GetFileNameWithoutExtension(f)));

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
            var idx    = lv.SelectedItem;
            var chosen = idx >= 0 && idx < skinNames.Count ? skinNames[idx] : "WhiteOnBlack";
            _settings.ActiveSkin = chosen;
            _settings.Save();
            McTheme.Apply(chosen);
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
            _leftPanelView.Height = Dim.Percent(pct)! - 1;

            _rightPanelView.X      = 0;
            _rightPanelView.Y      = Pos.Bottom(_leftPanelView) - 1; // share bottom/top border
            _rightPanelView.Width  = Dim.Fill();
            _rightPanelView.Height = Dim.Fill(2);

            _leftOverlay.X      = 0;
            _leftOverlay.Y      = 1;
            _leftOverlay.Width  = Dim.Fill();
            _leftOverlay.Height = Dim.Percent(pct)! - 1;

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
