using Terminal.Gui;

namespace Mc.Editor;

/// <summary>
/// Top-level editor window combining MenuBar, EditorView, and the F-key button bar.
/// Equivalent to the mcedit toplevel dialog in src/editor/editwidget.c.
///
/// Layout (from top to bottom):
///   Row 0          : MenuBar  (File | Edit | Search | Command | Format | Window | Options)
///   Rows 1..N-2    : EditorView (text area + status bar at its own bottom row)
///   Row N-1        : F-key button bar (1Help 2Save 3Mark 4Replac … 0Quit)
/// </summary>
public sealed class EditorScreen : Toplevel
{
    private readonly List<EditorView> _editors = [];
    private int _currentTab;
    private readonly View _editorContainer;
    private readonly MenuBar _menuBar;
    private readonly EditorButtonBar _buttonBar;
    private View? _splitContainer;
    private EditorView? _splitView1;
    private EditorView? _splitView2;
    private bool _isSplitMode;
    private bool _isVerticalSplit;

    // File history (most-recently-used first) for File > History
    private readonly List<string> _fileHistory = [];
    private const int MaxHistory = 20;

    private EditorSettings _settings = new();

    private EditorView ActiveEditor => _isSplitMode && _splitView1 != null ? _splitView1 : 
                                       _editors.Count > 0 ? _editors[_currentTab] : _editors[0];

    public EditorScreen(string? filePath = null, bool readOnly = false)
    {
        _editorContainer = new View
        {
            X = 0, Y = 0,  // Start at top since no MenuBar initially
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            CanFocus = false,
        };

        var view = CreateEditorView(filePath, readOnly);
        _editors.Add(view);
        _currentTab = 0;
        _editorContainer.Add(view);

        if (filePath != null && File.Exists(filePath))
            AddToHistory(filePath);

        _menuBar = BuildMenuBar();
        _buttonBar = BuildButtonBar();

        // Load and apply settings
        _settings = EditorSettings.Load();
        view.ApplySettings(_settings);

        // DON'T add MenuBar - it steals keyboard input in Terminal.Gui v2
        // Only add container and button bar
        Add(_editorContainer, _buttonBar);
        
        // Set focus to editor
        view.SetFocus();
    }

    private EditorView CreateEditorView(string? filePath, bool readOnly = false)
    {
        var view = new EditorView(filePath)
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        if (readOnly) view.IsReadOnly = true;
        view.RequestClose += (_, _) => CloseCurrentTab();
        view.EditorTitleChanged += (_, _) => SetNeedsDraw();
        return view;
    }

    protected override bool OnKeyDown(Key keyEvent)
    {
        // F9 opens menu
        if (keyEvent.KeyCode == KeyCode.F9)
        {
            if (!Subviews.Contains(_menuBar))
            {
                Add(_menuBar);
                _editorContainer.Y = 1;
                _editorContainer.Height = Dim.Fill(1);
            }
            _menuBar.OpenMenu();
            return true;
        }
        
        // Escape closes menu if open
        if (keyEvent.KeyCode == KeyCode.Esc && Subviews.Contains(_menuBar))
        {
            Remove(_menuBar);
            _editorContainer.Y = 0;
            _editorContainer.Height = Dim.Fill(1);
            ActiveEditor?.SetFocus();
            return true;
        }
        
        // All other keys go to the editor
        return false;
    }

    private void CloseCurrentTab()
    {
        if (_isSplitMode)
        {
            ExitSplitMode();
            return;
        }
        
        if (_editors.Count == 1)
        {
            Application.RequestStop(this);
            return;
        }

        var editor = _editors[_currentTab];
        _editorContainer.Remove(editor);
        _editors.RemoveAt(_currentTab);
        
        if (_editors.Count > 0)
        {
            _currentTab = Math.Min(_currentTab, _editors.Count - 1);
            _editorContainer.Add(_editors[_currentTab]);
        }
    }

    // ── Menu Bar ─────────────────────────────────────────────────────────────

    private MenuBar BuildMenuBar()
    {
        var menuBar = new MenuBar
        {
          Menus = new[]
          {
            new MenuBarItem("_File", new MenuItem[]
            {
                new MenuItem("_Open file…",       "Ctrl+O",       () => ActiveEditor.ExecuteOpenFile()),
                new MenuItem("_New",              "Ctrl+N",       () => OpenNewTab()),
                new MenuItem("_Close",            string.Empty,   () => ActiveEditor.ExecuteClose()),
                new MenuItem("_History…",         "Alt+Shift+E",  () => ShowFileHistory()),
                null!,
                new MenuItem("_Save",             "F2",           () => ActiveEditor.ExecuteSave()),
                new MenuItem("Save _as…",         "Shift+F2",     () => ActiveEditor.ExecuteSaveAs()),
                new MenuItem("_Insert file…",     "Shift+F5",     () => ActiveEditor.ExecuteInsertFile()),
                new MenuItem("Cop_y to file…",    "Ctrl+F",       () => ActiveEditor.ExecuteSaveBlock()),
                null!,
                new MenuItem("_User menu…",       "F11",          () => ActiveEditor.ExecuteUserMenu()),
                null!,
                new MenuItem("_Quit",             "F10",          () => ActiveEditor.ExecuteClose()),
            }),
            new MenuBarItem("_Edit", new MenuItem[]
            {
                new MenuItem("_Undo",             "Ctrl+Z",       () => ActiveEditor.ExecuteUndo()),
                new MenuItem("_Redo",             "Ctrl+Shift+Z", () => ActiveEditor.ExecuteRedo()),
                null!,
                new MenuItem("_Toggle ins/overw", "Ins",          () => ActiveEditor.ExecuteToggleInsert()),
                null!,
                new MenuItem("Toggle _mark",      "F3",           () => ActiveEditor.ExecuteToggleMark()),
                new MenuItem("Mark colu_mns",     "Shift+F3",     () => ActiveEditor.ExecuteMarkColumn()),
                new MenuItem("Mark _all",         "Ctrl+A",       () => ActiveEditor.ExecuteMarkAll()),
                new MenuItem("Un_mark",           string.Empty,   () => ActiveEditor.ExecuteUnmark()),
                null!,
                new MenuItem("_Copy",             "F5",           () => ActiveEditor.ExecuteCopyBlock()),
                new MenuItem("Mo_ve",             "F6",           () => ActiveEditor.ExecuteMoveBlock()),
                new MenuItem("_Delete",           "F8",           () => ActiveEditor.ExecuteDeleteBlock()),
                null!,
                new MenuItem("Copy to clip_file", "Ctrl+Ins",     () => ActiveEditor.ExecuteCopyToClipfile()),
                new MenuItem("Cut _to clipfile",  "Shift+Del",    () => ActiveEditor.ExecuteCutToClipfile()),
                new MenuItem("Paste from clip_file","Shift+Ins",  () => ActiveEditor.ExecutePasteFromClipfile()),
                null!,
                new MenuItem("_Copy to desktop",  "Ctrl+C",       () => ActiveEditor.ExecuteCopyToSystemClipboard()),
                new MenuItem("C_ut to desktop",   "Ctrl+X",       () => ActiveEditor.ExecuteCutToSystemClipboard()),
                new MenuItem("_Paste from desktop","Ctrl+V",      () => ActiveEditor.ExecutePasteFromSystemClipboard()),
                null!,
                new MenuItem("_Beginning",        "Ctrl+Home",    () => ActiveEditor.ExecuteGotoTop()),
                new MenuItem("_End",              "Ctrl+End",     () => ActiveEditor.ExecuteGotoBottom()),
            }),
            new MenuBarItem("_Search", new MenuItem[]
            {
                new MenuItem("_Search…",          "F7",           () => ActiveEditor.ExecuteSearch()),
                new MenuItem("Search _again",     "Shift+F7",     () => ActiveEditor.ExecuteSearchContinue()),
                new MenuItem("_Replace…",         "F4",           () => ActiveEditor.ExecuteReplace()),
                null!,
                new MenuItem("_Toggle bookmark",  "Alt+K",        () => ActiveEditor.ExecuteToggleBookmark()),
                new MenuItem("_Next bookmark",    "Alt+J",        () => ActiveEditor.ExecuteNextBookmark()),
                new MenuItem("_Prev bookmark",    "Alt+I",        () => ActiveEditor.ExecutePrevBookmark()),
                new MenuItem("_Flush bookmarks",  "Alt+O",        () => ActiveEditor.ExecuteFlushBookmarks()),
            }),
            new MenuBarItem("_Command", new MenuItem[]
            {
                new MenuItem("_Go to line…",          "Ctrl+G",   () => ActiveEditor.ExecuteGotoLine()),
                new MenuItem("_Toggle line numbers",  "Alt+N",    () => ActiveEditor.ExecuteToggleLineNumbers()),
                new MenuItem("Match _bracket",        "Alt+[",    () => ActiveEditor.ExecuteMatchBracket()),
                new MenuItem("Toggle s_yntax",        "Ctrl+S",   () => ActiveEditor.ExecuteToggleSyntax()),
                new MenuItem("Toggle _hex view",      "Ctrl+H",   () => ActiveEditor.ExecuteToggleHexMode()),
                new MenuItem("Toggle right _margin",  string.Empty, () => ActiveEditor.ExecuteToggleRightMargin()),
                null!,
                new MenuItem("_Encoding…",               "Alt+E",      () => ActiveEditor.ExecuteEncodingSelect()),
                null!,
                new MenuItem("_Delete macro…",           string.Empty, () => ActiveEditor.ExecuteDeleteMacro()),
                null!,
                new MenuItem("_Check word",              string.Empty, () => ActiveEditor.ExecuteCheckWord()),
                new MenuItem("Change spelling _language…", string.Empty, () => ActiveEditor.ExecuteChangeSpellingLanguage()),


                new MenuItem("_Refresh screen",       "Ctrl+L",   () => ActiveEditor.ExecuteRefresh()),
                null!,
                new MenuItem("_Load full file for editing…", string.Empty, () => ActiveEditor.ExecuteLoadFullFile()),
                null!,
                new MenuItem("Start/Stop macro _record", "Ctrl+R",() => ActiveEditor.ExecuteStartStopMacro()),
                null!,
                new MenuItem("_Spell check word",    "Ctrl+F5",   () => ActiveEditor.ExecuteSpellCheck()),
            }),
            new MenuBarItem("For_mat", new MenuItem[]
            {
                new MenuItem("Insert _literal…",  "Ctrl+Q",       () => ActiveEditor.ExecuteInsertLiteral()),
                new MenuItem("Insert _date/time", "Ctrl+D",       () => ActiveEditor.ExecuteInsertDateTime()),
                new MenuItem("_Format paragraph", "Alt+P",        () => ActiveEditor.ExecuteFormatParagraph()),
                new MenuItem("_Sort…",            "Alt+T",        () => ActiveEditor.ExecuteSort()),
                new MenuItem("_Paste output of…", "Alt+U",        () => ActiveEditor.ExecuteExternalCommand()),
                new MenuItem("_External formatter…", string.Empty, () => ActiveEditor.ExecuteExternalFormatter()),
                null!,
                new MenuItem("_Pretty Print (JSON/XML)", string.Empty, () => ActiveEditor.ExecutePrettyPrint()),
                null!,
                new MenuItem("Validate _XML",                string.Empty, () => ActiveEditor.ExecuteValidateXml()),
                new MenuItem("Validate XSD _Schema",         string.Empty, () => ActiveEditor.ExecuteValidateXsd()),
                new MenuItem("Validate XML against _XSD…",   string.Empty, () => ActiveEditor.ExecuteValidateXmlAgainstXsd()),
            }),
            new MenuBarItem("_Git", new MenuItem[]
            {
                new MenuItem("_Blame…",            "Ctrl+G B",    () => ShowGitBlame()),
                new MenuItem("_Diff…",             "Ctrl+G D",    () => ShowGitDiff()),
                new MenuItem("_Stage file",        "Ctrl+G S",    () => GitStageFile()),
                new MenuItem("_Unstage file",      "Ctrl+G U",    () => GitUnstageFile()),
                new MenuItem("_Status",            "Ctrl+G T",    () => ShowGitStatus()),
            }),
            new MenuBarItem("_Window", new MenuItem[]
            {
                new MenuItem("_New tab",           "Ctrl+T",      () => OpenNewTab()),
                new MenuItem("_Close tab",         "Ctrl+W",      () => CloseCurrentTab()),
                new MenuItem("_Next tab",          "Ctrl+Tab",    () => NextTab()),
                new MenuItem("_Previous tab",      "Ctrl+Shift+Tab", () => PrevTab()),
                null!,
                new MenuItem("Split _horizontal",  "Ctrl+_",      () => SplitHorizontal()),
                new MenuItem("Split _vertical",    "Ctrl+|",      () => SplitVertical()),
                new MenuItem("_Unsplit",           "Ctrl+U",      () => ExitSplitMode()),
                null!,
                new MenuItem("_Compare files…",    "Ctrl+D",      () => ShowCompareDialog()),
                new MenuItem("_Binary compare…",   "Ctrl+B",      () => ShowBinaryCompareDialog()),
                null!,
                new MenuItem("_List…",             string.Empty,  () => ExecuteWindowList()),
                new MenuItem("_Open another file…", string.Empty, () => ShowOpenAnotherFile()),
            }),
            new MenuBarItem("_Options", new MenuItem[]
            {
                new MenuItem("_General…",         string.Empty,   () => ActiveEditor.ExecuteOptions()),
                new MenuItem("Save _mode…",       string.Empty,   () => ActiveEditor.ExecuteSaveMode()),
                null!,
                new MenuItem("S_yntax highlighting…", string.Empty, () => ActiveEditor.ExecuteSyntaxChoose()),
                null!,
                new MenuItem("Toggle _visible tabs","Alt+_",       () => ActiveEditor.ExecuteToggleShowTabs()),
                null!,
                new MenuItem("_Learn keys…",             string.Empty, () => ActiveEditor.ExecuteLearnKeys()),
                new MenuItem("_Syntax file",             string.Empty, () => ActiveEditor.ExecuteEditSyntaxFile()),
                new MenuItem("_Menu file",               string.Empty, () => ActiveEditor.ExecuteEditMenuFile()),
                null!,
                new MenuItem("_Save setup",       string.Empty,   () => ExecuteSaveSetup()),
            }),
            new MenuBarItem("_About", new MenuItem[]
            {
                new MenuItem("_License",        string.Empty, () => ActiveEditor.ExecuteAboutLicense()),
                new MenuItem("_Github",         string.Empty, () => ActiveEditor.ExecuteAboutGitHub()),
                new MenuItem("_Fork from",      string.Empty, () => ActiveEditor.ExecuteAboutForkFrom()),
                new MenuItem("_Why forked",     string.Empty, () => ActiveEditor.ExecuteAboutWhyForked()),
                new MenuItem("_New functions",  string.Empty, () => ActiveEditor.ExecuteAboutNewFunctions()),
                new MenuItem("_System info",    string.Empty, () => ActiveEditor.ExecuteAboutSystemInfo()),
            }),
          }
        };
        
        // Prevent MenuBar from stealing keyboard input
        menuBar.CanFocus = false;
        
        return menuBar;
    }

    // ── Button Bar ───────────────────────────────────────────────────────────

    private EditorButtonBar BuildButtonBar()
    {
        return new EditorButtonBar(
            ("Help",    () => MessageBox.Query("Help", "mcedit keyboard help:\n" +
                                               "F2=Save  F3=Mark  F4=Replace  F5=Copy\n" +
                                               "F6=Move  F7=Search  F8=Delete\n" +
                                               "F9=Menu  F10=Quit\n" +
                                               "Ctrl+Z=Undo  Alt+K=Bookmark\n" +
                                               "Ctrl+G=GoToLine  Alt+[=MatchBracket", "OK")),
            ("Save",    () => ActiveEditor.ExecuteSave()),
            ("Mark",    () => ActiveEditor.ExecuteToggleMark()),
            ("Replac",  () => ActiveEditor.ExecuteReplace()),
            ("Copy",    () => ActiveEditor.ExecuteCopyBlock()),
            ("Move",    () => ActiveEditor.ExecuteMoveBlock()),
            ("Search",  () => ActiveEditor.ExecuteSearch()),
            ("Delete",  () => ActiveEditor.ExecuteDeleteBlock()),
            ("PullDn",  () => _menuBar.SetFocus()),
            ("Quit",    () => ActiveEditor.ExecuteClose())
        );
    }

    // ── File history dialog ──────────────────────────────────────────────────

    private void AddToHistory(string path)
    {
        _fileHistory.Remove(path);
        _fileHistory.Insert(0, path);
        if (_fileHistory.Count > MaxHistory)
            _fileHistory.RemoveAt(_fileHistory.Count - 1);
    }

    private void ShowFileHistory()
    {
        if (_fileHistory.Count == 0)
        {
            MessageBox.Query("History", "No file history yet.", "OK");
            return;
        }
        string? chosen = null;
        var d = new Dialog
        {
            Title  = "File History",
            Width  = Math.Min(70, Application.Screen.Width - 4),
            Height = Math.Min(_fileHistory.Count + 6, 20),
        };
        var lv = new ListView { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(4) };
        lv.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(_fileHistory));
        lv.SelectedItem = 0;
        d.Add(lv);
        lv.OpenSelectedItem += (_, _) =>
        {
            if (lv.SelectedItem >= 0) chosen = _fileHistory[lv.SelectedItem];
            Application.RequestStop(d);
        };
        var ok     = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting     += (_, _) => { if (lv.SelectedItem >= 0) chosen = _fileHistory[lv.SelectedItem]; Application.RequestStop(d); };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        lv.SetFocus();
        Application.Run(d); d.Dispose();
        if (chosen != null)
        {
            // Open chosen file in the same screen (load into current view)
            try
            {
                ActiveEditor.ExecuteOpenFile();
            }
            catch { }
        }
    }

    private void ShowOpenAnotherFile()
    {
        ActiveEditor.ExecuteOpenFile();
    }

    private void ExecuteWindowList()
    {
        if (_editors.Count == 0) return;
        var items = _editors.Select((e, i) => 
            $"{(i == _currentTab ? "*" : " ")} {i + 1}. {Path.GetFileName(e.FilePath ?? "untitled")}").ToArray();
        int choice = MessageBox.Query("Window List", $"{_editors.Count} file(s) open", items);
        if (choice >= 0 && choice < _editors.Count)
            SwitchToTab(choice);
    }

    private void ExecuteSaveSetup()
    {
        _settings = ActiveEditor.CaptureSettings();
        _settings.Save();
        MessageBox.Query("Save Setup", "Settings saved to ~/.config/mc/ini", "OK");
    }

    // ── Multi-tab operations ─────────────────────────────────────────────────

    private void OpenNewTab()
    {
        if (_isSplitMode) ExitSplitMode();
        
        var view = CreateEditorView(null);
        _editors.Add(view);
        view.ApplySettings(_settings);
        
        SwitchToTab(_editors.Count - 1);
    }

    private void SwitchToTab(int index)
    {
        if (index < 0 || index >= _editors.Count || index == _currentTab) return;
        
        _editorContainer.Remove(_editors[_currentTab]);
        _currentTab = index;
        _editorContainer.Add(_editors[_currentTab]);
        SetNeedsDraw();
    }

    private void NextTab()
    {
        if (_isSplitMode || _editors.Count <= 1) return;
        SwitchToTab((_currentTab + 1) % _editors.Count);
    }

    private void PrevTab()
    {
        if (_isSplitMode || _editors.Count <= 1) return;
        SwitchToTab((_currentTab - 1 + _editors.Count) % _editors.Count);
    }

    // ── Split view operations ────────────────────────────────────────────────

    private void SplitHorizontal()
    {
        if (_isSplitMode) return;
        EnterSplitMode(false);
    }

    private void SplitVertical()
    {
        if (_isSplitMode) return;
        EnterSplitMode(true);
    }

    private void EnterSplitMode(bool vertical)
    {
        _isSplitMode = true;
        _isVerticalSplit = vertical;

        Remove(_editorContainer);

        _splitView1 = ActiveEditor;
        _splitView2 = CreateEditorView(_splitView1.FilePath);
        _splitView2.ApplySettings(_settings);

        if (vertical)
        {
            _splitView1.X = 0; _splitView1.Y = 0;
            _splitView1.Width = Dim.Percent(50);
            _splitView1.Height = Dim.Fill();

            _splitView2.X = Pos.Right(_splitView1) + 1;
            _splitView2.Y = 0;
            _splitView2.Width = Dim.Fill();
            _splitView2.Height = Dim.Fill();
        }
        else
        {
            _splitView1.X = 0; _splitView1.Y = 0;
            _splitView1.Width = Dim.Fill();
            _splitView1.Height = Dim.Percent(50);

            _splitView2.X = 0;
            _splitView2.Y = Pos.Bottom(_splitView1) + 1;
            _splitView2.Width = Dim.Fill();
            _splitView2.Height = Dim.Fill();
        }

        _splitContainer = new View
        {
            X = 0, Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };
        _splitContainer.Add(_splitView1, _splitView2);
        Add(_splitContainer);
    }

    private void ExitSplitMode()
    {
        if (!_isSplitMode) return;

        if (_splitContainer != null)
            Remove(_splitContainer);

        _splitView1 = null;
        _splitView2 = null;
        _splitContainer = null;
        _isSplitMode = false;

        Add(_editorContainer);
    }

    // ── Compare operations ───────────────────────────────────────────────────

    private void ShowCompareDialog()
    {
        var d = new Dialog { Title = "Compare Files", Width = 70, Height = 12 };
        d.Add(new Label { X = 1, Y = 1, Text = "Left file:" });
        var tf1 = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Text = ActiveEditor.FilePath ?? "" };
        d.Add(tf1);
        d.Add(new Label { X = 1, Y = 4, Text = "Right file:" });
        var tf2 = new TextField { X = 1, Y = 5, Width = Dim.Fill(1) };
        d.Add(tf2);

        var ok = new Button { Text = "Compare", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting += (_, _) =>
        {
            var left = tf1.Text?.ToString();
            var right = tf2.Text?.ToString();
            if (!string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right))
            {
                Application.RequestStop(d);
                ShowTextCompare(left, right);
            }
        };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    private void ShowTextCompare(string leftPath, string rightPath)
    {
        var compareView = new TextCompareView(leftPath, rightPath);
        var compareWindow = new Window
        {
            Title = $"Compare: {Path.GetFileName(leftPath)} <> {Path.GetFileName(rightPath)}",
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        compareWindow.Add(compareView);
        compareView.RequestClose += (_, _) => Application.RequestStop(compareWindow);
        Application.Run(compareWindow);
        compareWindow.Dispose();
    }

    private void ShowBinaryCompareDialog()
    {
        var d = new Dialog { Title = "Binary Compare", Width = 70, Height = 12 };
        d.Add(new Label { X = 1, Y = 1, Text = "Left file:" });
        var tf1 = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Text = ActiveEditor.FilePath ?? "" };
        d.Add(tf1);
        d.Add(new Label { X = 1, Y = 4, Text = "Right file:" });
        var tf2 = new TextField { X = 1, Y = 5, Width = Dim.Fill(1) };
        d.Add(tf2);

        var ok = new Button { Text = "Compare", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting += (_, _) =>
        {
            var left = tf1.Text?.ToString();
            var right = tf2.Text?.ToString();
            if (!string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right))
            {
                Application.RequestStop(d);
                ShowBinaryCompare(left, right);
            }
        };
        cancel.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok); d.AddButton(cancel);
        Application.Run(d); d.Dispose();
    }

    private void ShowBinaryCompare(string leftPath, string rightPath)
    {
        var compareView = new BinaryCompareView(leftPath, rightPath);
        var compareWindow = new Window
        {
            Title = $"Binary Compare: {Path.GetFileName(leftPath)} <> {Path.GetFileName(rightPath)}",
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        compareWindow.Add(compareView);
        compareView.RequestClose += (_, _) => Application.RequestStop(compareWindow);
        Application.Run(compareWindow);
        compareWindow.Dispose();
    }

    // ── Git operations ───────────────────────────────────────────────────────

    private void ShowGitBlame()
    {
        var filePath = ActiveEditor.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            MessageBox.ErrorQuery("Git Blame", "No file open", "OK");
            return;
        }

        if (!GitHelper.IsGitRepository(filePath))
        {
            MessageBox.ErrorQuery("Git Blame", "Not a git repository", "OK");
            return;
        }

        var blameView = new GitBlameView(filePath);
        var blameWindow = new Window
        {
            Title = $"Git Blame: {Path.GetFileName(filePath)}",
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        blameWindow.Add(blameView);
        blameView.RequestClose += (_, _) => Application.RequestStop(blameWindow);
        Application.Run(blameWindow);
        blameWindow.Dispose();
    }

    private void ShowGitDiff()
    {
        var filePath = ActiveEditor.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            MessageBox.ErrorQuery("Git Diff", "No file open", "OK");
            return;
        }

        if (!GitHelper.IsGitRepository(filePath))
        {
            MessageBox.ErrorQuery("Git Diff", "Not a git repository", "OK");
            return;
        }

        var diffView = new GitDiffView(filePath);
        var diffWindow = new Window
        {
            Title = $"Git Diff: {Path.GetFileName(filePath)}",
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        diffWindow.Add(diffView);
        diffView.RequestClose += (_, _) => Application.RequestStop(diffWindow);
        Application.Run(diffWindow);
        diffWindow.Dispose();
    }

    private void GitStageFile()
    {
        var filePath = ActiveEditor.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            MessageBox.ErrorQuery("Git Stage", "No file open", "OK");
            return;
        }

        if (!GitHelper.IsGitRepository(filePath))
        {
            MessageBox.ErrorQuery("Git Stage", "Not a git repository", "OK");
            return;
        }

        if (GitHelper.StageFile(filePath))
            MessageBox.Query("Git Stage", $"Staged: {Path.GetFileName(filePath)}", "OK");
        else
            MessageBox.ErrorQuery("Git Stage", "Failed to stage file", "OK");
    }

    private void GitUnstageFile()
    {
        var filePath = ActiveEditor.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            MessageBox.ErrorQuery("Git Unstage", "No file open", "OK");
            return;
        }

        if (!GitHelper.IsGitRepository(filePath))
        {
            MessageBox.ErrorQuery("Git Unstage", "Not a git repository", "OK");
            return;
        }

        if (GitHelper.UnstageFile(filePath))
            MessageBox.Query("Git Unstage", $"Unstaged: {Path.GetFileName(filePath)}", "OK");
        else
            MessageBox.ErrorQuery("Git Unstage", "Failed to unstage file", "OK");
    }

    private void ShowGitStatus()
    {
        var filePath = ActiveEditor.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            MessageBox.ErrorQuery("Git Status", "No file open", "OK");
            return;
        }

        if (!GitHelper.IsGitRepository(filePath))
        {
            MessageBox.ErrorQuery("Git Status", "Not a git repository", "OK");
            return;
        }

        var status = GitHelper.GetStatus(filePath);
        MessageBox.Query("Git Status", $"{Path.GetFileName(filePath)}\n\nStatus: {status}", "OK");
    }
}

/// <summary>
/// Simple F-key button bar for the editor screen.
/// Displays numbered labels (1Help 2Save … 0Quit) and responds to mouse clicks.
/// </summary>
internal sealed class EditorButtonBar : View
{
    private readonly (string Label, Action Callback)[] _buttons;

    public EditorButtonBar(params (string Label, Action Callback)[] buttons)
    {
        _buttons = buttons;
        Height   = 1;
        Width    = Dim.Fill();
        Y        = Pos.AnchorEnd(1);
        CanFocus = false;
        MouseClick += OnMouseClick;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        var viewport = Viewport;
        int totalWidth = viewport.Width;
        int count = _buttons.Length;
        if (count == 0 || totalWidth == 0) return false;
        int baseWidth = totalWidth / count;

        Move(0, 0);
        for (int i = 0; i < count; i++)
        {
            var (label, _) = _buttons[i];
            int fNum = (i + 1) % 10; // F1=1…F9=9, F10=0
            string cell = $"{fNum}{label}";
            if (cell.Length > baseWidth) cell = cell[..baseWidth];
            else if (cell.Length < baseWidth) cell = cell.PadRight(baseWidth);

            // Number in bold-white-on-blue, label in black-on-cyan
            Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.White, Color.Blue));
            Driver!.AddStr(fNum.ToString());
            Driver!.SetAttribute(new Terminal.Gui.Attribute(Color.Black, Color.Cyan));
            var labelPart = cell[1..];
            Driver!.AddStr(labelPart);
        }
        return false;
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (!e.Flags.HasFlag(MouseFlags.Button1Clicked) &&
            !e.Flags.HasFlag(MouseFlags.Button1DoubleClicked)) return;
        int totalWidth = Viewport.Width;
        int count = _buttons.Length;
        if (count == 0 || totalWidth == 0) return;
        int baseWidth = totalWidth / count;
        if (baseWidth == 0) return;
        int idx = Math.Min(e.Position.X / baseWidth, count - 1);
        _buttons[idx].Callback?.Invoke();
        e.Handled = true;
    }
}
