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

    public void Save() => _config.Save();
}
