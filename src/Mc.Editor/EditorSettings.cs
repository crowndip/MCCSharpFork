namespace Mc.Editor;

/// <summary>
/// Persists editor options to ~/.config/mc/ini under [Editor] section.
/// Equivalent to the editor options stored in mc config.
/// </summary>
public sealed class EditorSettings
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "mc", "ini");

    // All settings with defaults
    public int TabWidth { get; set; } = 8;
    public bool ExpandTabs { get; set; } = false;
    public bool AutoIndent { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = false;
    public bool SyntaxHighlighting { get; set; } = true;
    public bool ShowRightMargin { get; set; } = false;
    public int RightMarginColumn { get; set; } = 72;
    public bool ShowTabTws { get; set; } = false;
    public int SaveMode { get; set; } = 0;  // 0=quick, 1=safe, 2=backup
    public string BackupExtension { get; set; } = "~";
    public bool ConfirmSave { get; set; } = false;
    public bool SavePosition { get; set; } = true;
    public bool TypewriterWrap { get; set; } = false;
    public int WrapLineLength { get; set; } = 72;
    public bool BackspaceThruTabs { get; set; } = false;

    public static EditorSettings Load()
    {
        var s = new EditorSettings();
        try
        {
            if (!File.Exists(ConfigPath)) return s;
            var lines = File.ReadAllLines(ConfigPath);
            bool inSection = false;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed == "[Editor]") { inSection = true; continue; }
                if (trimmed.StartsWith('[')) { inSection = false; continue; }
                if (!inSection || !trimmed.Contains('=')) continue;
                var sep = trimmed.IndexOf('=');
                var key = trimmed[..sep].Trim();
                var val = trimmed[(sep + 1)..].Trim();
                switch (key)
                {
                    case "tab_spacing":       if (int.TryParse(val, out int tw)) s.TabWidth = tw; break;
                    case "fill_tabs":         s.ExpandTabs = val == "true"; break;
                    case "auto_indent":       s.AutoIndent = val != "false"; break;
                    case "line_state":        s.ShowLineNumbers = val == "true"; break;
                    case "syntax_hl":         s.SyntaxHighlighting = val != "false"; break;
                    case "show_right_margin": s.ShowRightMargin = val == "true"; break;
                    case "right_margin_col":  if (int.TryParse(val, out int rm)) s.RightMarginColumn = rm; break;
                    case "visible_tabs":      s.ShowTabTws = val == "true"; break;
                    case "save_mode":         if (int.TryParse(val, out int sm)) s.SaveMode = sm; break;
                    case "backup_ext":        s.BackupExtension = val; break;
                    case "confirm_save":      s.ConfirmSave = val == "true"; break;
                    case "save_position":     s.SavePosition = val != "false"; break;
                    case "typewriter_wrap":   s.TypewriterWrap = val == "true"; break;
                    case "wrap_line_length":      if (int.TryParse(val, out int wl)) s.WrapLineLength = wl; break;
                    case "backspace_thru_tabs":  s.BackspaceThruTabs = val == "true"; break;
                }
            }
        }
        catch { /* ignore errors, use defaults */ }
        return s;
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);

            // Read existing file, replace [Editor] section
            var existingLines = File.Exists(ConfigPath)
                ? File.ReadAllLines(ConfigPath).ToList()
                : new List<string>();
            var newSection = new[]
            {
                "[Editor]",
                $"tab_spacing = {TabWidth}",
                $"fill_tabs = {ExpandTabs.ToString().ToLower()}",
                $"auto_indent = {AutoIndent.ToString().ToLower()}",
                $"line_state = {ShowLineNumbers.ToString().ToLower()}",
                $"syntax_hl = {SyntaxHighlighting.ToString().ToLower()}",
                $"show_right_margin = {ShowRightMargin.ToString().ToLower()}",
                $"right_margin_col = {RightMarginColumn}",
                $"visible_tabs = {ShowTabTws.ToString().ToLower()}",
                $"save_mode = {SaveMode}",
                $"backup_ext = {BackupExtension}",
                $"confirm_save = {ConfirmSave.ToString().ToLower()}",
                $"save_position = {SavePosition.ToString().ToLower()}",
                $"typewriter_wrap = {TypewriterWrap.ToString().ToLower()}",
                $"wrap_line_length = {WrapLineLength}",
                $"backspace_thru_tabs = {BackspaceThruTabs.ToString().ToLower()}",
            };

            // Remove existing [Editor] section
            int sectionStart = existingLines.FindIndex(l => l.Trim() == "[Editor]");
            if (sectionStart >= 0)
            {
                int sectionEnd = sectionStart + 1;
                while (sectionEnd < existingLines.Count && !existingLines[sectionEnd].Trim().StartsWith('['))
                    sectionEnd++;
                existingLines.RemoveRange(sectionStart, sectionEnd - sectionStart);
                existingLines.InsertRange(sectionStart, newSection);
            }
            else
            {
                existingLines.AddRange(newSection);
            }
            File.WriteAllLines(ConfigPath, existingLines);
        }
        catch { /* ignore save errors */ }
    }
}
