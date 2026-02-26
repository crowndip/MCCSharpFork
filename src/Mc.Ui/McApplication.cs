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

        // Menu bar
        _menuBar = new MenuBar
        {
            Menus = new[]
            {
                new MenuBarItem("_Left panel", new[]
                {
                    new MenuItem("_Listing format...", string.Empty, ShowSortDialog),
                    new MenuItem("_Sort order...",     string.Empty, ShowSortDialog),
                    new MenuItem("_Filter...",         string.Empty, () => { }),
                    new MenuItem("_Rescan",            "Ctrl+R",     () => _controller.LeftPanel.Reload()),
                }),
                new MenuBarItem("_File", new[]
                {
                    new MenuItem("_View",        "F3",  () => ViewCurrent()),
                    new MenuItem("_Edit",        "F4",  () => EditCurrent()),
                    new MenuItem("_Copy",        "F5",  () => CopyFiles()),
                    new MenuItem("_Move/Rename", "F6",  () => MoveFiles()),
                    new MenuItem("_New dir",     "F7",  () => MakeDir()),
                    new MenuItem("_Delete",      "F8",  () => DeleteFiles()),
                    null!,
                    new MenuItem("_Info",   "Ctrl+L",  () => ShowInfo()),
                    new MenuItem("_Chmod",  "Ctrl+X",  () => Chmod()),
                    null!,
                    new MenuItem("E_xit",   "F10",     () => Application.RequestStop()),
                }),
                new MenuBarItem("_Command", new[]
                {
                    new MenuItem("_Find file",      "F7",       ShowFindDialog),
                    new MenuItem("_Hotlist",        "Ctrl+\\",  ShowHotlist),
                    new MenuItem("_Directory tree", string.Empty, () => { }),
                    new MenuItem("_Swap panels",    "Ctrl+U",   () => { _controller.SwapPanels(); RefreshPanels(); }),
                    new MenuItem("_Compare panels", string.Empty, ComparePanels),
                    null!,
                    new MenuItem("_Shell",   "Ctrl+O",  LaunchShell),
                }),
                new MenuBarItem("_Tools", new[]
                {
                    // --- Clipboard ---
                    new MenuItem("Copy _path to clipboard",   string.Empty, CopyPathToClipboard),
                    new MenuItem("Copy file _name",           string.Empty, CopyNameToClipboard),
                    new MenuItem("Copy _directory path",      string.Empty, CopyDirToClipboard),
                    null!,
                    // --- File info ---
                    new MenuItem("_Checksum...",              string.Empty, ShowChecksum),
                    new MenuItem("Directory _size...",        string.Empty, ShowDirSize),
                    new MenuItem("_Touch (timestamps)...",    string.Empty, ShowTouch),
                    null!,
                    // --- Operations ---
                    new MenuItem("_Batch rename...",          string.Empty, ShowBatchRename),
                    null!,
                    // --- External apps ---
                    new MenuItem("Open _terminal here",       "Ctrl+T",     OpenTerminalHere),
                    new MenuItem("_Compare with diff tool",   string.Empty, CompareWithDiffTool),
                }),
                new MenuBarItem("_Options", new[]
                {
                    new MenuItem("_Configuration...", string.Empty, () => { }),
                    new MenuItem("_Layout...",        string.Empty, () => { }),
                    new MenuItem("_Panel options...", string.Empty, () => { }),
                    null!,
                    new MenuItem("_Save setup", string.Empty, () => _settings.Save()),
                }),
                new MenuBarItem("_Help", new[]
                {
                    new MenuItem("_Help",   "F1", HelpDialog.Show),
                    new MenuItem("_About",  string.Empty, () =>
                        MessageDialog.Show("About", "Midnight Commander for .NET\n.NET 8 rewrite of GNU Midnight Commander\nBuilt with Terminal.Gui")),
                }),
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
            onUserMenu: () => { },
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
            case KeyCode.X when keyEvent.IsCtrl: Chmod(); return true;

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
        if (entry == null || entry.IsDirectory) return;
        ViewFile(entry.FullPath.Path);
    }

    private void ViewFile(string path)
    {
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
        var dest = _controller.InactivePanel.CurrentPath.Path;
        var entry = GetCurrentEntry();
        var opts = CopyMoveDialog.Show(true, entry?.Name ?? "marked files", dest);
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
            _controller.ActivePanel.Reload(); // Simplified
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
        var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/sh";
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

    private static void ShowStatus(string message)
    {
        // In a real implementation, show in a status area
        // For now just update the title bar
    }
}
