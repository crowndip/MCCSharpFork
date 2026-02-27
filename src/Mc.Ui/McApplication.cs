using System.Collections.ObjectModel;
using Mc.Core.Config;
using Mc.Core.Models;
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
        MenuItem[] PanelMenuItems(bool left) =>
        [
            new MenuItem("_File listing",      string.Empty, () => ShowListingFormatDialog(left)),
            new MenuItem("_Quick view",        string.Empty, QuickViewCurrent),
            new MenuItem("_Info",              string.Empty, ShowInfo),
            new MenuItem("_Tree",              string.Empty, () => ShowTreeDialog(left)),
            new MenuItem("_Panelize",          string.Empty, ExternalPanelize),
            null!,
            new MenuItem("_Listing format...", string.Empty, () => ShowListingFormatDialog(left)),
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
                    new("C_hmod",                string.Empty, () => Chmod()),
                    new("_Link",                 string.Empty, () => CreateLink()),
                    new("_Symlink",              string.Empty, () => CreateSymlink()),
                    new("Relati_ve symlink",      string.Empty, () => CreateRelativeSymlink()),
                    new("Edit s_ymlink",         string.Empty, () => EditSymlink()),
                    new("Ch_own",                string.Empty, () => Chown()),
                    new("_Advanced chown",       string.Empty, () => AdvancedChown()),
                    new("_Rename/Move",          "F6",         () => MoveFiles()),
                    new("_Mkdir",                "F7",         () => MakeDir()),
                    new("_Delete",               "F8",         () => DeleteFiles()),
                    new("_Quick cd",             string.Empty, () => QuickCd()),
                    null!,
                    new("_Select group",         "+",          () => SelectGroup()),
                    new("_Unselect group",       "-",          () => UnselectGroup()),
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
                    new("_Appearance...",    string.Empty, () => NotImplemented("Appearance / skins")),
                    new("_Learn keys...",    string.Empty, () => NotImplemented("Learn keys")),
                    new("_Virtual FS...",    string.Empty, () => NotImplemented("Virtual FS settings")),
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

        _rightPanelView = new FilePanelView(_controller.RightPanel)
        {
            X = Pos.Right(_leftPanelView), Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
        };
        _rightPanelView.EntryActivated += OnPanelEntryActivated;
        _rightPanelView.BecameActive += (_, _) => SetActivePanel(_rightPanelView);

        Add(_leftPanelView, _rightPanelView);

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
                case KeyCode.C: Chmod(); return true;           // Ctrl+X C → chmod
                case KeyCode.O: Chown(); return true;           // Ctrl+X O → chown
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

            case KeyCode.L when keyEvent.IsCtrl: ShowInfo(); return true;
            case KeyCode.X when keyEvent.IsCtrl: _ctrlXPrefix = true; return true; // start Ctrl+X prefix

            case KeyCode.T when keyEvent.IsCtrl: OpenTerminalHere(); return true;
            case KeyCode.Insert: GetActivePanel().ToggleMark(); return true;

            default:
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

    private void OnPanelEntryActivated(object? sender, FileEntry? entry)
    {
        if (entry == null) return;
        if (entry.IsDirectory || entry.IsParentDir)
        {
            _controller.NavigateTo(entry.FullPath);
        }
        else if (_settings.UseInternalViewer)
        {
            ViewFile(entry.FullPath.Path);
        }
        else
        {
            _controller.OpenEntry(entry);
        }
    }

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
        string? path = entry?.FullPath.Path;
        if (path != null) { _editedFiles.Remove(path); _editedFiles.Insert(0, path); }

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
        var entry = GetCurrentEntry();
        var sourceName = entry?.Name ?? "marked files";
        var opts = CopyMoveDialog.Show(false, sourceName, dest);
        if (opts?.Confirmed != true) return;

        var progress = new ProgressDialog("Copy");
        progress.Show();
        _ = Task.Run(async () =>
        {
            try { await _controller.CopyMarkedAsync(progress, progress.CancellationToken); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Application.Invoke(() => MessageDialog.Error(ex.Message)); }
            finally { progress.Close(); Application.Invoke(RefreshPanels); }
        });
    }

    private void MoveFiles()
    {
        var entry = GetCurrentEntry();
        var marked = _controller.ActivePanel.GetMarkedEntries();
        // Single-file rename: pre-fill destination with the filename so user can rename in-place.
        // With multiple marked files the destination is the inactive panel directory (move).
        string dest;
        string sourceName;
        if (marked.Count > 0)
        {
            dest = _controller.InactivePanel.CurrentPath.Path;
            sourceName = $"{marked.Count} files";
        }
        else if (entry != null)
        {
            dest = entry.Name;   // pre-fill filename only → allows inline rename
            sourceName = entry.Name;
        }
        else return;
        var opts = CopyMoveDialog.Show(true, sourceName, dest);
        if (opts?.Confirmed != true) return;

        var progress = new ProgressDialog("Move");
        progress.Show();
        _ = Task.Run(async () =>
        {
            try { await _controller.MoveMarkedAsync(progress, progress.CancellationToken); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Application.Invoke(() => MessageDialog.Error(ex.Message)); }
            finally { progress.Close(); Application.Invoke(RefreshPanels); }
        });
    }

    private void MakeDir()
    {
        var name = MkdirDialog.Show();
        if (name == null) return;
        _controller.CreateDirectory(name);
    }

    private void DeleteFiles()
    {
        var marked = _controller.ActivePanel.GetMarkedEntries();
        var names = marked.Count > 0
            ? marked.Select(e => e.Name).ToList()
            : (GetCurrentEntry() is { } e ? new List<string> { e.Name } : new List<string>());

        if (names.Count == 0) return;
        if (!DeleteDialog.Confirm(names)) return;

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

    private void Chmod()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;
        var newMode = ChmodDialog.Show(entry.Name, entry.Permissions);
        if (newMode == null) return;
        try
        {
            File.SetUnixFileMode(entry.FullPath.Path, newMode.Value);
            RefreshPanels();
        }
        catch (Exception ex) { MessageDialog.Error(ex.Message); }
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

    private void ShowFindDialog()
    {
        var opts = FindDialog.Show(_controller.ActivePanel.CurrentPath.Path);
        if (opts?.Confirmed != true) return;

        var startDir = _controller.ActivePanel.CurrentPath.Path;
        var searchOpt = opts.SearchInSubdirs
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        List<string> results;
        try
        {
            results = Directory.EnumerateFiles(startDir, opts.FilePattern, searchOpt)
                .Take(500)
                .ToList();

            if (!string.IsNullOrEmpty(opts.ContentPattern))
            {
                var cmp = opts.CaseSensitive
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;
                if (opts.ContentRegex)
                {
                    var rx = new System.Text.RegularExpressions.Regex(
                        opts.ContentPattern,
                        opts.CaseSensitive ? System.Text.RegularExpressions.RegexOptions.None
                                           : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    results = results.Where(f =>
                    {
                        try { return rx.IsMatch(File.ReadAllText(f)); } catch { return false; }
                    }).ToList();
                }
                else
                {
                    results = results.Where(f =>
                    {
                        try { return File.ReadAllText(f).Contains(opts.ContentPattern, cmp); } catch { return false; }
                    }).ToList();
                }
            }
        }
        catch (Exception ex) { MessageDialog.Error(ex.Message); return; }

        if (results.Count == 0) { MessageDialog.Show("Find", "No files found."); return; }

        string? selectedPath = null;
        var d = new Dialog
        {
            Title = $"Find: {opts.FilePattern} ({results.Count} found)",
            Width = Dim.Fill() - 4,
            Height = Dim.Fill() - 4,
            ColorScheme = McTheme.Dialog,
        };
        var lv = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = Dim.Fill(4),
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(new ObservableCollection<string>(
            results.Select(r => r[startDir.TrimEnd('/').Length..].TrimStart('/'))));
        d.Add(lv);

        var go = new Button { X = Pos.Center() - 8, Y = Pos.Bottom(lv), Text = "Go to", IsDefault = true };
        go.Accepting += (_, _) =>
        {
            if (lv.SelectedItem >= 0) selectedPath = results[lv.SelectedItem];
            Application.RequestStop(d);
        };
        var close = new Button { X = Pos.Center() + 3, Y = Pos.Bottom(lv), Text = "Close" };
        close.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(go); d.AddButton(close);
        Application.Run(d); d.Dispose();

        if (selectedPath != null)
        {
            var dir = Path.GetDirectoryName(selectedPath) ?? startDir;
            _controller.NavigateTo(VfsPath.FromLocal(dir));
            RefreshPanels();
        }
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

        if (leftEntry != null && !leftEntry.IsDirectory && rightEntry != null && !rightEntry.IsDirectory)
        {
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
        var entry = GetCurrentEntry();
        if (entry == null) return;

        var result = ChownDialog.Show(entry.Name, entry.OwnerName ?? string.Empty, entry.GroupName ?? string.Empty);
        if (result == null) return;

        ApplyChown(entry.FullPath.Path, result.Owner, result.Group);
        RefreshPanels();
    }

    private void AdvancedChown()
    {
        var entry = GetCurrentEntry();
        if (entry == null) return;

        // MC combines chown + chmod in one dialog; we call them in sequence
        var chownResult = ChownDialog.Show(entry.Name, entry.OwnerName ?? string.Empty, entry.GroupName ?? string.Empty);
        if (chownResult == null) return;

        var newMode = ChmodDialog.Show(entry.Name, entry.Permissions);
        if (newMode == null) return;

        try
        {
            ApplyChown(entry.FullPath.Path, chownResult.Owner, chownResult.Group);
            File.SetUnixFileMode(entry.FullPath.Path, newMode.Value);
            RefreshPanels();
        }
        catch (Exception ex) { MessageDialog.Error(ex.Message); }
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

    private void SelectGroup()
    {
        var pattern = InputDialog.Show("Select Group", "Pattern (+):", "*");
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            _controller.ActivePanel.MarkByPattern(pattern);
            RefreshPanels();
        }
    }

    private void UnselectGroup()
    {
        var pattern = InputDialog.Show("Unselect Group", "Pattern (-):", "*");
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            _controller.ActivePanel.MarkByPattern(pattern, mark: false);
            RefreshPanels();
        }
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
    private void CompareDirs()
    {
        var leftLookup  = _controller.LeftPanel.Entries
            .Where(e => !e.IsParentDir)
            .ToDictionary(e => e.Name);
        var rightLookup = _controller.RightPanel.Entries
            .Where(e => !e.IsParentDir)
            .ToDictionary(e => e.Name);

        foreach (var e in _controller.LeftPanel.Entries.Where(x => !x.IsParentDir))
            e.IsMarked = !rightLookup.TryGetValue(e.Name, out var match) || e.Size != match.Size;

        foreach (var e in _controller.RightPanel.Entries.Where(x => !x.IsParentDir))
            e.IsMarked = !leftLookup.TryGetValue(e.Name, out var match) || e.Size != match.Size;

        _controller.LeftPanel.RefreshMarking();
        _controller.RightPanel.RefreshMarking();
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

            // Show what would be panelized (full panelize requires VFS support)
            MessageDialog.Show("External Panelize",
                $"{files.Count} file(s) returned.\n(Full panel injection not yet implemented)");
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
            RadioLabels  = ["Full file list", "Brief file list"],
            SelectedItem = panel.ListingMode == PanelListingMode.Brief ? 1 : 0,
            ColorScheme  = McTheme.Dialog,
        };
        d.Add(rg);

        var ok = new Button { X = Pos.Center() - 8, Y = 6, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            panel.ListingMode = rg.SelectedItem == 1 ? PanelListingMode.Brief : PanelListingMode.Full;
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

        // Common encodings matching MC's default list
        string[] encodings =
        [
            "UTF-8", "ISO-8859-1", "ISO-8859-2", "ISO-8859-5", "ISO-8859-15",
            "KOI8-R", "KOI8-U", "CP1250", "CP1251", "CP1252",
            "CP866", "GB2312", "GBK", "BIG5", "SHIFT_JIS", "EUC-JP",
        ];

        var current = listing.CurrentPath.Encoding ?? "UTF-8";
        var selected = current;

        var d = new Dialog
        {
            Title       = "Select encoding",
            Width       = 40,
            Height      = encodings.Length + 6,
            ColorScheme = McTheme.Dialog,
        };

        var lv = new ListView
        {
            X           = 1, Y = 1,
            Width       = Dim.Fill(1),
            Height      = encodings.Length,
            ColorScheme = McTheme.Panel,
        };
        lv.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(encodings));
        lv.SelectedItem = Math.Max(0, Array.IndexOf(encodings, current));
        d.Add(lv);

        var ok = new Button { X = Pos.Center() - 8, Y = encodings.Length + 2, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            if (lv.SelectedItem >= 0) selected = encodings[lv.SelectedItem];
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 2, Y = encodings.Length + 2, Text = "Cancel" };
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

        List<UserMenuEntry> entries;
        try   { entries = ParseUserMenuFile(menuFile); }
        catch (Exception ex) { MessageDialog.Error($"Cannot read user menu:\n{ex.Message}"); return; }

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

        // Macro expansion (%f %d %D %b %e) — matching original MC macros
        var fileEntry = GetCurrentEntry();
        var cmd = chosenCommand
            .Replace("%f",  fileEntry?.FullPath.Path ?? string.Empty)
            .Replace("%b",  Path.GetFileNameWithoutExtension(fileEntry?.Name ?? string.Empty))
            .Replace("%e",  Path.GetExtension(fileEntry?.Name ?? string.Empty).TrimStart('.'))
            .Replace("%d",  _controller.ActivePanel.CurrentPath.Path)
            .Replace("%D",  _controller.InactivePanel.CurrentPath.Path);

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
    private static List<UserMenuEntry> ParseUserMenuFile(string path)
    {
        var entries  = new List<UserMenuEntry>();
        string? label = null;
        char hotKey  = '\0';
        var cmdLines = new List<string>();

        foreach (var rawLine in File.ReadAllLines(path))
        {
            if (string.IsNullOrEmpty(rawLine)) continue;
            var trimmed = rawLine.TrimStart();
            if (trimmed.StartsWith('#')) continue;
            if (trimmed.StartsWith('+') || trimmed.StartsWith('=')) continue; // condition lines

            bool isCommand = rawLine[0] is ' ' or '\t';

            if (isCommand)
            {
                if (label != null) cmdLines.Add(trimmed);
            }
            else
            {
                // Flush previous entry
                if (label != null && cmdLines.Count > 0)
                    entries.Add(new UserMenuEntry(label, string.Join("\n", cmdLines), hotKey));

                // Parse header: single-char key (letter/digit) + whitespace → extract hotkey
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
            }
        }

        if (label != null && cmdLines.Count > 0)
            entries.Add(new UserMenuEntry(label, string.Join("\n", cmdLines), hotKey));

        return entries;
    }

    private sealed record UserMenuEntry(string Label, string Command, char HotKey);

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

        var display = entries.Select(e => e.Label).ToList();
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
    private static void ShowBackgroundJobs() =>
        MessageDialog.Show("Background jobs",
            "No background jobs.\n\n" +
            "File operations (copy/move/delete) in this port\n" +
            "run as async tasks with a progress dialog.");

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
            Width = 60,
            Height = 15,
            ColorScheme = McTheme.Dialog,
        };

        var useIntViewer = new CheckBox
        {
            X = 2, Y = 1,
            Text = "Use internal view",
            CheckedState = _settings.UseInternalViewer ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        var useIntEditor = new CheckBox
        {
            X = 2, Y = 2,
            Text = "Use internal edit",
            CheckedState = _settings.UseInternalEditor ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 2, Y = 4, Text = "External editor:", ColorScheme = McTheme.Dialog });
        var extEditor = new TextField
        {
            X = 20, Y = 4, Width = 34,
            Text = _settings.ExternalEditor,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(new Label { X = 2, Y = 6, Text = "External viewer:", ColorScheme = McTheme.Dialog });
        var extViewer = new TextField
        {
            X = 20, Y = 6, Width = 34,
            Text = _settings.ExternalViewer,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(useIntViewer, useIntEditor, extEditor, extViewer);

        var ok = new Button { X = Pos.Center() - 8, Y = 10, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            _settings.UseInternalViewer = useIntViewer.CheckedState == CheckState.Checked;
            _settings.UseInternalEditor = useIntEditor.CheckedState == CheckState.Checked;
            _settings.ExternalEditor    = extEditor.Text?.ToString() ?? "vi";
            _settings.ExternalViewer    = extViewer.Text?.ToString() ?? "less";
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 2, Y = 10, Text = "Cancel" };
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
            Width = 50,
            Height = 10,
            ColorScheme = McTheme.Dialog,
        };

        var showHidden = new CheckBox
        {
            X = 2, Y = 1,
            Text = "Show hidden files",
            CheckedState = _settings.ShowHiddenFiles ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        var showBackup = new CheckBox
        {
            X = 2, Y = 2,
            Text = "Show backup files",
            CheckedState = _settings.ShowBackupFiles ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        var markMoves = new CheckBox
        {
            X = 2, Y = 3,
            Text = "Mark moves cursor down",
            CheckedState = _settings.MarkMovesCursor ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(showHidden, showBackup, markMoves);

        var ok = new Button { X = Pos.Center() - 8, Y = 6, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            _settings.ShowHiddenFiles = showHidden.CheckedState == CheckState.Checked;
            _settings.ShowBackupFiles = showBackup.CheckedState == CheckState.Checked;
            _settings.MarkMovesCursor = markMoves.CheckedState == CheckState.Checked;
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 2, Y = 6, Text = "Cancel" };
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
            Height = 10,
            ColorScheme = McTheme.Dialog,
        };

        var confirmDelete = new CheckBox
        {
            X = 2, Y = 1,
            Text = "Confirm delete",
            CheckedState = _settings.ConfirmDelete ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        var confirmOverwrite = new CheckBox
        {
            X = 2, Y = 2,
            Text = "Confirm overwrite",
            CheckedState = _settings.ConfirmOverwrite ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        var confirmExit = new CheckBox
        {
            X = 2, Y = 3,
            Text = "Confirm exit",
            CheckedState = _settings.ConfirmExit ? CheckState.Checked : CheckState.UnChecked,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(confirmDelete, confirmOverwrite, confirmExit);

        var ok = new Button { X = Pos.Center() - 8, Y = 6, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            _settings.ConfirmDelete    = confirmDelete.CheckedState == CheckState.Checked;
            _settings.ConfirmOverwrite = confirmOverwrite.CheckedState == CheckState.Checked;
            _settings.ConfirmExit      = confirmExit.CheckedState == CheckState.Checked;
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 2, Y = 6, Text = "Cancel" };
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
            Width = 50,
            Height = 10,
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

        var ok = new Button { X = Pos.Center() - 8, Y = 6, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            _settings.HorizontalSplit = rg.SelectedItem == 1;
            Application.RequestStop(d);
        };
        var cancel = new Button { X = Pos.Center() + 2, Y = 6, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }
}
