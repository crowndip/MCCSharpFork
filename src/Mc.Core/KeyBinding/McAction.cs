namespace Mc.Core.KeyBinding;

/// <summary>
/// All named actions in the application.
/// Equivalent to the CK_* constants in the original C codebase.
/// </summary>
public enum McAction
{
    // Global
    Quit,
    Help,
    UserMenu,
    Menu,
    Refresh,
    Shell,
    SwapPanels,
    TogglePanels,
    QuietQuit,

    // Panel navigation
    Up,
    Down,
    Left,
    Right,
    PageUp,
    PageDown,
    Home,
    End,
    SwitchPanel,

    // File operations
    View,
    Edit,
    Copy,
    Move,
    MakeDir,
    Delete,
    Rename,
    Link,
    Symlink,
    ChangePermissions,
    ChangeOwner,
    Info,

    // Selection
    Mark,
    MarkAll,
    UnmarkAll,
    MarkPattern,
    InvertSelection,

    // Navigation
    ChangeDir,
    ParentDir,
    RootDir,
    HomeDir,
    DirHistory,
    DirUp,

    // Search
    Find,
    FindNext,
    FindPrev,

    // Sorting / filtering
    Sort,
    Filter,
    Rescan,

    // View modes
    ListFull,
    ListBrief,
    ListLong,
    ListUser,
    ToggleShowHidden,
    ToggleShowBackup,

    // Panel specific
    ToggleTree,
    Hotlist,

    // Editor
    Save,
    SaveAs,
    Open,
    New,
    Undo,
    Redo,
    Cut,
    CopyText,
    Paste,
    SelectAll,
    Replace,
    Goto,
    BookmarkToggle,
    BookmarkNext,
    BookmarkPrev,
    WordWrap,
    LineNumbers,
    SyntaxHighlight,
    Indent,
    Unindent,
    Insert,
    DeleteLine,
    MacroRecord,
    MacroPlay,

    // Viewer
    ViewHex,
    ViewWrap,
    ViewSearch,
    ViewNext,
    ViewPrev,

    // Diff
    DiffNextChange,
    DiffPrevChange,
    DiffEditLeft,
    DiffEditRight,
}
