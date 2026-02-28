namespace Mc.Core.Config;

/// <summary>
/// Strongly-typed wrapper over McConfig.
/// Mirrors the mc_global_t and various settings structs from the C codebase.
/// </summary>
public sealed class McSettings
{
    private readonly McConfig _config;

    public McSettings(McConfig config) => _config = config;

    // --- Panels ---
    public bool ShowHiddenFiles
    {
        get => _config.GetBool("Panels", "show_hidden_files");
        set => _config.Set("Panels", "show_hidden_files", value);
    }

    public bool ShowBackupFiles
    {
        get => _config.GetBool("Panels", "show_backup_files", true);
        set => _config.Set("Panels", "show_backup_files", value);
    }

    public bool MarkMovesCursor
    {
        get => _config.GetBool("Panels", "mark_moves_down", true);
        set => _config.Set("Panels", "mark_moves_down", value);
    }

    public string LeftPanelPath
    {
        get => _config.GetString("Panels", "left_panel_last_dir", Environment.CurrentDirectory);
        set => _config.Set("Panels", "left_panel_last_dir", value);
    }

    public string RightPanelPath
    {
        get => _config.GetString("Panels", "right_panel_last_dir", Environment.CurrentDirectory);
        set => _config.Set("Panels", "right_panel_last_dir", value);
    }

    // --- Layout ---
    public bool HorizontalSplit
    {
        get => _config.GetBool("Layout", "horizontal_split");
        set => _config.Set("Layout", "horizontal_split", value);
    }

    public int PanelSplitRatio
    {
        get => _config.GetInt("Layout", "panel_split_ratio", 50);
        set => _config.Set("Layout", "panel_split_ratio", value);
    }

    // --- Behaviour ---
    public bool ConfirmDelete
    {
        get => _config.GetBool("Midnight-Commander", "confirm_delete", true);
        set => _config.Set("Midnight-Commander", "confirm_delete", value);
    }

    public bool ConfirmOverwrite
    {
        get => _config.GetBool("Midnight-Commander", "confirm_overwrite", true);
        set => _config.Set("Midnight-Commander", "confirm_overwrite", value);
    }

    public bool ConfirmExit
    {
        get => _config.GetBool("Midnight-Commander", "confirm_exit", true);
        set => _config.Set("Midnight-Commander", "confirm_exit", value);
    }

    public bool UseInternalEditor
    {
        get => _config.GetBool("Midnight-Commander", "use_internal_edit", true);
        set => _config.Set("Midnight-Commander", "use_internal_edit", value);
    }

    public bool UseInternalViewer
    {
        get => _config.GetBool("Midnight-Commander", "use_internal_view", true);
        set => _config.Set("Midnight-Commander", "use_internal_view", value);
    }

    public string ExternalEditor
    {
        get => _config.GetString("Midnight-Commander", "editor", "vi");
        set => _config.Set("Midnight-Commander", "editor", value);
    }

    public string ExternalViewer
    {
        get => _config.GetString("Midnight-Commander", "viewer", "less");
        set => _config.Set("Midnight-Commander", "viewer", value);
    }

    public string ActiveSkin
    {
        get => _config.GetString("Midnight-Commander", "skin", "default");
        set => _config.Set("Midnight-Commander", "skin", value);
    }

    // --- Editor ---
    public bool EditorSyntaxHighlighting
    {
        get => _config.GetBool("Editor", "syntax_highlighting", true);
        set => _config.Set("Editor", "syntax_highlighting", value);
    }

    public bool EditorLineNumbers
    {
        get => _config.GetBool("Editor", "line_numbers");
        set => _config.Set("Editor", "line_numbers", value);
    }

    public int EditorTabWidth
    {
        get => _config.GetInt("Editor", "tab_spacing", 4);
        set => _config.Set("Editor", "tab_spacing", value);
    }

    public bool EditorExpandTabs
    {
        get => _config.GetBool("Editor", "expand_tabs");
        set => _config.Set("Editor", "expand_tabs", value);
    }

    // --- Configuration (configure_box in setup.c) ---
    public bool VerboseOperation
    {
        get => _config.GetBool("Midnight-Commander", "verbose", true);
        set => _config.Set("Midnight-Commander", "verbose", value);
    }

    public bool ComputeTotals
    {
        get => _config.GetBool("Midnight-Commander", "compute_totals", true);
        set => _config.Set("Midnight-Commander", "compute_totals", value);
    }

    public bool AutoSaveSetup
    {
        get => _config.GetBool("Midnight-Commander", "auto_save_setup", true);
        set => _config.Set("Midnight-Commander", "auto_save_setup", value);
    }

    public bool ShowOutputOfCommands
    {
        get => _config.GetBool("Midnight-Commander", "show_output_of_cmds", true);
        set => _config.Set("Midnight-Commander", "show_output_of_cmds", value);
    }

    public bool UseSubshell
    {
        get => _config.GetBool("Midnight-Commander", "use_subshell", true);
        set => _config.Set("Midnight-Commander", "use_subshell", value);
    }

    public bool AskBeforeRunning
    {
        get => _config.GetBool("Midnight-Commander", "ask_before_run");
        set => _config.Set("Midnight-Commander", "ask_before_run", value);
    }

    // --- Panel options (panel_options_box) ---
    public bool ShowMiniStatus
    {
        get => _config.GetBool("Panels", "show_mini_info", true);
        set => _config.Set("Panels", "show_mini_info", value);
    }

    public bool LynxLikeMotion
    {
        get => _config.GetBool("Panels", "navigate_with_arrows", true);
        set => _config.Set("Panels", "navigate_with_arrows", value);
    }

    public bool ShowScrollbar
    {
        get => _config.GetBool("Panels", "show_scrollbar");
        set => _config.Set("Panels", "show_scrollbar", value);
    }

    public bool HighlightFiles
    {
        get => _config.GetBool("Panels", "highlight_files_by_attributes");
        set => _config.Set("Panels", "highlight_files_by_attributes", value);
    }

    public bool MixAllFiles
    {
        get => _config.GetBool("Panels", "mix_all_files");
        set => _config.Set("Panels", "mix_all_files", value);
    }

    public bool QuickSearchCaseSensitive
    {
        get => _config.GetBool("Panels", "qsearch_case_sensitive");
        set => _config.Set("Panels", "qsearch_case_sensitive", value);
    }

    public bool ShowFreeSpace
    {
        get => _config.GetBool("Panels", "show_free_space", true);
        set => _config.Set("Panels", "show_free_space", value);
    }

    // --- Layout (layout_box) ---
    public bool ShowMenubar
    {
        get => _config.GetBool("Layout", "menubar_visible", true);
        set => _config.Set("Layout", "menubar_visible", value);
    }

    public bool ShowCommandLine
    {
        get => _config.GetBool("Layout", "command_prompt", true);
        set => _config.Set("Layout", "command_prompt", value);
    }

    public bool ShowKeyBar
    {
        get => _config.GetBool("Layout", "keybar_visible", true);
        set => _config.Set("Layout", "keybar_visible", value);
    }

    // --- VFS settings (vfs_setup.c) ---
    public int VfsCacheTimeout
    {
        get => _config.GetInt("VFS", "vfs_timeout", 60);
        set => _config.Set("VFS", "vfs_timeout", value);
    }

    public string FtpAnonymousPassword
    {
        get => _config.GetString("VFS", "ftp_anonymous_passwd", "anonymous@");
        set => _config.Set("VFS", "ftp_anonymous_passwd", value);
    }

    public string FtpProxy
    {
        get => _config.GetString("VFS", "ftp_proxy", string.Empty);
        set => _config.Set("VFS", "ftp_proxy", value);
    }

    public bool FtpPassiveMode
    {
        get => _config.GetBool("VFS", "ftp_use_passive_connections", true);
        set => _config.Set("VFS", "ftp_use_passive_connections", value);
    }

    public void Save() => _config.Save();
}
