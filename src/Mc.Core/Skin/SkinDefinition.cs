namespace Mc.Core.Skin;

/// <summary>
/// A named color pair (foreground + background).
/// Equivalent to a single skin color entry in mc.
/// </summary>
public sealed record ColorPair(string Foreground, string Background);

/// <summary>
/// Complete skin (color theme) definition.
/// Equivalent to skin INI files in misc/skins/ of the original codebase.
/// </summary>
public sealed class SkinDefinition
{
    public string Name { get; set; } = "default";
    public string Description { get; set; } = string.Empty;

    // --- Panel colors ---
    public ColorPair PanelNormal { get; set; } = new("white", "blue");
    public ColorPair PanelSelected { get; set; } = new("black", "cyan");
    public ColorPair PanelMarked { get; set; } = new("yellow", "blue");
    public ColorPair PanelMarkedSelected { get; set; } = new("yellow", "cyan");
    public ColorPair PanelHeader { get; set; } = new("black", "cyan");
    public ColorPair PanelDirectory { get; set; } = new("brightwhite", "blue");
    public ColorPair PanelSymlink { get; set; } = new("brightcyan", "blue");
    public ColorPair PanelExecutable { get; set; } = new("brightgreen", "blue");

    // --- Dialog colors ---
    public ColorPair DialogNormal { get; set; } = new("black", "white");
    public ColorPair DialogSelected { get; set; } = new("black", "cyan");
    public ColorPair DialogTitle { get; set; } = new("brightwhite", "white");
    public ColorPair DialogButton { get; set; } = new("black", "cyan");
    public ColorPair DialogInput { get; set; } = new("black", "cyan");
    public ColorPair DialogHelp { get; set; } = new("black", "green");

    // --- Menu colors ---
    public ColorPair MenuNormal { get; set; } = new("white", "black");
    public ColorPair MenuSelected { get; set; } = new("black", "white");
    public ColorPair MenuHotKey { get; set; } = new("yellow", "black");

    // --- Status bar / button bar ---
    public ColorPair StatusBar { get; set; } = new("black", "cyan");
    public ColorPair ButtonBar { get; set; } = new("black", "cyan");
    public ColorPair ButtonBarLabel { get; set; } = new("white", "black");

    // --- Editor colors ---
    public ColorPair EditorNormal { get; set; } = new("white", "black");
    public ColorPair EditorSelected { get; set; } = new("black", "white");
    public ColorPair EditorLineNumber { get; set; } = new("yellow", "black");
    public ColorPair EditorSearchResult { get; set; } = new("black", "yellow");

    // --- Viewer colors ---
    public ColorPair ViewerNormal { get; set; } = new("white", "black");
    public ColorPair ViewerBold { get; set; } = new("brightwhite", "black");
    public ColorPair ViewerUnderline { get; set; } = new("cyan", "black");
    public ColorPair ViewerSearchResult { get; set; } = new("black", "yellow");
}
