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
    private readonly EditorView _view;
    private readonly MenuBar _menuBar;
    private readonly EditorButtonBar _buttonBar;

    // File history (most-recently-used first) for File > History
    private readonly List<string> _fileHistory = [];
    private const int MaxHistory = 20;

    private EditorSettings _settings = new();

    public EditorScreen(string? filePath = null, bool readOnly = false)
    {
        if (filePath != null && File.Exists(filePath))
            AddToHistory(filePath);

        _view = new EditorView(filePath)
        {
            X = 0, Y = 1,
            Width  = Dim.Fill(),
            Height = Dim.Fill(1),   // leave 1 row at bottom for button bar
        };
        if (readOnly) _view.IsReadOnly = true;
        _view.RequestClose       += (_, _) => Application.RequestStop(this);
        _view.EditorTitleChanged += (_, _) => { /* title lives in status bar */ };

        _menuBar   = BuildMenuBar();
        _buttonBar = BuildButtonBar();

        // Load and apply settings
        _settings = EditorSettings.Load();
        _view.ApplySettings(_settings);

        // Menu bar sits at the very top (Terminal.Gui puts MenuBar at Y=0 automatically when added to Toplevel)
        Add(_menuBar, _view, _buttonBar);
    }

    // ── Menu Bar ─────────────────────────────────────────────────────────────

    private MenuBar BuildMenuBar()
    {
        return new MenuBar
        {
          Menus = new[]
          {
            new MenuBarItem("_File", new MenuItem[]
            {
                new MenuItem("_Open file…",       "Ctrl+O",       () => _view.ExecuteOpenFile()),
                new MenuItem("_New",              "Ctrl+N",       () => _view.ExecuteNewFile()),
                new MenuItem("_Close",            string.Empty,   () => _view.ExecuteClose()),
                new MenuItem("_History…",         "Alt+Shift+E",  () => ShowFileHistory()),
                null!,
                new MenuItem("_Save",             "F2",           () => _view.ExecuteSave()),
                new MenuItem("Save _as…",         "Shift+F2",     () => _view.ExecuteSaveAs()),
                new MenuItem("_Insert file…",     "Shift+F5",     () => _view.ExecuteInsertFile()),
                new MenuItem("Cop_y to file…",    "Ctrl+F",       () => _view.ExecuteSaveBlock()),
                null!,
                new MenuItem("_User menu…",       "F11",          () => _view.ExecuteUserMenu()),
                null!,
                new MenuItem("_Quit",             "F10",          () => _view.ExecuteClose()),
            }),
            new MenuBarItem("_Edit", new MenuItem[]
            {
                new MenuItem("_Undo",             "Ctrl+Z",       () => _view.ExecuteUndo()),
                new MenuItem("_Redo",             "Ctrl+Shift+Z", () => _view.ExecuteRedo()),
                null!,
                new MenuItem("_Toggle ins/overw", "Ins",          () => _view.ExecuteToggleInsert()),
                null!,
                new MenuItem("Toggle _mark",      "F3",           () => _view.ExecuteToggleMark()),
                new MenuItem("Mark colu_mns",     "Shift+F3",     () => _view.ExecuteMarkColumn()),
                new MenuItem("Mark _all",         "Ctrl+A",       () => _view.ExecuteMarkAll()),
                new MenuItem("Un_mark",           string.Empty,   () => _view.ExecuteUnmark()),
                null!,
                new MenuItem("_Copy",             "F5",           () => _view.ExecuteCopyBlock()),
                new MenuItem("Mo_ve",             "F6",           () => _view.ExecuteMoveBlock()),
                new MenuItem("_Delete",           "F8",           () => _view.ExecuteDeleteBlock()),
                null!,
                new MenuItem("Copy to clip_file", "Ctrl+Ins",     () => _view.ExecuteCopyToClipfile()),
                new MenuItem("Cut _to clipfile",  "Shift+Del",    () => _view.ExecuteCutToClipfile()),
                new MenuItem("Paste from clip_file","Shift+Ins",  () => _view.ExecutePasteFromClipfile()),
                null!,
                new MenuItem("_Copy to desktop",  "Ctrl+C",       () => _view.ExecuteCopyToSystemClipboard()),
                new MenuItem("C_ut to desktop",   "Ctrl+X",       () => _view.ExecuteCutToSystemClipboard()),
                new MenuItem("_Paste from desktop","Ctrl+V",      () => _view.ExecutePasteFromSystemClipboard()),
                null!,
                new MenuItem("_Beginning",        "Ctrl+Home",    () => _view.ExecuteGotoTop()),
                new MenuItem("_End",              "Ctrl+End",     () => _view.ExecuteGotoBottom()),
            }),
            new MenuBarItem("_Search", new MenuItem[]
            {
                new MenuItem("_Search…",          "F7",           () => _view.ExecuteSearch()),
                new MenuItem("Search _again",     "Shift+F7",     () => _view.ExecuteSearchContinue()),
                new MenuItem("_Replace…",         "F4",           () => _view.ExecuteReplace()),
                null!,
                new MenuItem("_Toggle bookmark",  "Alt+K",        () => _view.ExecuteToggleBookmark()),
                new MenuItem("_Next bookmark",    "Alt+J",        () => _view.ExecuteNextBookmark()),
                new MenuItem("_Prev bookmark",    "Alt+I",        () => _view.ExecutePrevBookmark()),
                new MenuItem("_Flush bookmarks",  "Alt+O",        () => _view.ExecuteFlushBookmarks()),
            }),
            new MenuBarItem("_Command", new MenuItem[]
            {
                new MenuItem("_Go to line…",          "Ctrl+G",   () => _view.ExecuteGotoLine()),
                new MenuItem("_Toggle line numbers",  "Alt+N",    () => _view.ExecuteToggleLineNumbers()),
                new MenuItem("Match _bracket",        "Alt+[",    () => _view.ExecuteMatchBracket()),
                new MenuItem("Toggle s_yntax",        "Ctrl+S",   () => _view.ExecuteToggleSyntax()),
                new MenuItem("Toggle _hex view",      "Ctrl+H",   () => _view.ExecuteToggleHexMode()),
                new MenuItem("Toggle right _margin",  string.Empty, () => _view.ExecuteToggleRightMargin()),
                null!,
                new MenuItem("_Encoding…",               "Alt+E",      () => _view.ExecuteEncodingSelect()),
                null!,
                new MenuItem("_Delete macro…",           string.Empty, () => _view.ExecuteDeleteMacro()),
                null!,
                new MenuItem("_Check word",              string.Empty, () => _view.ExecuteCheckWord()),
                new MenuItem("Change spelling _language…", string.Empty, () => _view.ExecuteChangeSpellingLanguage()),


                new MenuItem("_Refresh screen",       "Ctrl+L",   () => _view.ExecuteRefresh()),
                null!,
                new MenuItem("_Load full file for editing…", string.Empty, () => _view.ExecuteLoadFullFile()),
                null!,
                new MenuItem("Start/Stop macro _record", "Ctrl+R",() => _view.ExecuteStartStopMacro()),
                null!,
                new MenuItem("_Spell check word",    "Ctrl+F5",   () => _view.ExecuteSpellCheck()),
            }),
            new MenuBarItem("For_mat", new MenuItem[]
            {
                new MenuItem("Insert _literal…",  "Ctrl+Q",       () => _view.ExecuteInsertLiteral()),
                new MenuItem("Insert _date/time", "Ctrl+D",       () => _view.ExecuteInsertDateTime()),
                new MenuItem("_Format paragraph", "Alt+P",        () => _view.ExecuteFormatParagraph()),
                new MenuItem("_Sort…",            "Alt+T",        () => _view.ExecuteSort()),
                new MenuItem("_Paste output of…", "Alt+U",        () => _view.ExecuteExternalCommand()),
                new MenuItem("_External formatter…", string.Empty, () => _view.ExecuteExternalFormatter()),
                null!,
                new MenuItem("_Pretty Print (JSON/XML)", string.Empty, () => _view.ExecutePrettyPrint()),
                null!,
                new MenuItem("Validate _XML",                string.Empty, () => _view.ExecuteValidateXml()),
                new MenuItem("Validate XSD _Schema",         string.Empty, () => _view.ExecuteValidateXsd()),
                new MenuItem("Validate XML against _XSD…",   string.Empty, () => _view.ExecuteValidateXmlAgainstXsd()),
            }),
            new MenuBarItem("_Window", new MenuItem[]
            {


                new MenuItem("_List…",             string.Empty,  () => ExecuteWindowList()),
                new MenuItem("_Open another file…", string.Empty, () => ShowOpenAnotherFile()),
            }),
            new MenuBarItem("_Options", new MenuItem[]
            {
                new MenuItem("_General…",         string.Empty,   () => _view.ExecuteOptions()),
                new MenuItem("Save _mode…",       string.Empty,   () => _view.ExecuteSaveMode()),
                null!,
                new MenuItem("S_yntax highlighting…", string.Empty, () => _view.ExecuteSyntaxChoose()),
                null!,
                new MenuItem("Toggle _visible tabs","Alt+_",       () => _view.ExecuteToggleShowTabs()),
                null!,
                new MenuItem("_Learn keys…",             string.Empty, () => _view.ExecuteLearnKeys()),
                new MenuItem("_Syntax file",             string.Empty, () => _view.ExecuteEditSyntaxFile()),
                new MenuItem("_Menu file",               string.Empty, () => _view.ExecuteEditMenuFile()),
                null!,
                new MenuItem("_Save setup",       string.Empty,   () => ExecuteSaveSetup()),
            }),
            new MenuBarItem("_About", new MenuItem[]
            {
                new MenuItem("_License",        string.Empty, () => _view.ExecuteAboutLicense()),
                new MenuItem("_Github",         string.Empty, () => _view.ExecuteAboutGitHub()),
                new MenuItem("_Fork from",      string.Empty, () => _view.ExecuteAboutForkFrom()),
                new MenuItem("_Why forked",     string.Empty, () => _view.ExecuteAboutWhyForked()),
                new MenuItem("_New functions",  string.Empty, () => _view.ExecuteAboutNewFunctions()),
                new MenuItem("_System info",    string.Empty, () => _view.ExecuteAboutSystemInfo()),
            }),
          }
        };
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
            ("Save",    () => _view.ExecuteSave()),
            ("Mark",    () => _view.ExecuteToggleMark()),
            ("Replac",  () => _view.ExecuteReplace()),
            ("Copy",    () => _view.ExecuteCopyBlock()),
            ("Move",    () => _view.ExecuteMoveBlock()),
            ("Search",  () => _view.ExecuteSearch()),
            ("Delete",  () => _view.ExecuteDeleteBlock()),
            ("PullDn",  () => _menuBar.SetFocus()),
            ("Quit",    () => _view.ExecuteClose())
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
                _view.ExecuteOpenFile();
            }
            catch { }
        }
    }

    private void ShowOpenAnotherFile()
    {
        _view.ExecuteOpenFile();
    }

    private void ExecuteWindowList()
    {
        // Since only one file at a time is supported, just show the current filename
        var view = _view;
        var title = view.Title;
        MessageBox.Query("Window List", title, "OK");
    }

    private void ExecuteSaveSetup()
    {
        _settings = _view.CaptureSettings();
        _settings.Save();
        MessageBox.Query("Save Setup", "Settings saved to ~/.config/mc/ini", "OK");
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
