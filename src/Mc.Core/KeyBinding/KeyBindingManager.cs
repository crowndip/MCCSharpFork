using Mc.Core.Config;

namespace Mc.Core.KeyBinding;

/// <summary>
/// Maps keyboard keys to named actions.
/// Equivalent to src/keymap.c and lib/keybind.c
/// </summary>
public enum BindingContext { Panel, Editor }

public sealed class KeyBindingManager
{
    private readonly Dictionary<string, McAction> _panelBindings  = new(StringComparer.Ordinal);
    private readonly Dictionary<string, McAction> _editorBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<McAction, List<string>> _actionToKeys = [];

    public KeyBindingManager()
    {
        LoadDefaults();
    }

    private void LoadDefaults()
    {
        // Panel / global
        Bind("F1",  McAction.Help,              BindingContext.Panel);
        Bind("F2",  McAction.UserMenu,           BindingContext.Panel);
        Bind("F9",  McAction.Menu,               BindingContext.Panel);
        Bind("F10", McAction.Quit,               BindingContext.Panel);
        Bind("Ctrl+C", McAction.Quit,            BindingContext.Panel);
        Bind("Ctrl+O", McAction.Shell,           BindingContext.Panel);
        Bind("Ctrl+U", McAction.SwapPanels,      BindingContext.Panel);
        Bind("Ctrl+R", McAction.Refresh,         BindingContext.Panel);
        Bind("Tab",        McAction.SwitchPanel, BindingContext.Panel);
        Bind("Shift+Tab",  McAction.SwitchPanel, BindingContext.Panel);

        // File operations (panel)
        Bind("F3", McAction.View,    BindingContext.Panel);
        Bind("F4", McAction.Edit,    BindingContext.Panel);
        Bind("F5", McAction.Copy,    BindingContext.Panel);
        Bind("F6", McAction.Move,    BindingContext.Panel);
        Bind("F7", McAction.MakeDir, BindingContext.Panel);
        Bind("F8", McAction.Delete,  BindingContext.Panel);
        Bind("Ctrl+X", McAction.Rename, BindingContext.Panel);
        Bind("Ctrl+L", McAction.Info,   BindingContext.Panel);

        // Selection (panel)
        Bind("Insert",    McAction.Mark,             BindingContext.Panel);
        Bind("Plus",      McAction.MarkPattern,      BindingContext.Panel);
        Bind("Backslash", McAction.UnmarkAll,        BindingContext.Panel);
        Bind("Asterisk",  McAction.InvertSelection,  BindingContext.Panel);

        // Navigation (panel)
        Bind("Ctrl+PageUp",    McAction.ParentDir,         BindingContext.Panel);
        Bind("Ctrl+Home",      McAction.RootDir,            BindingContext.Panel);
        Bind("Alt+H",          McAction.DirHistory,         BindingContext.Panel);
        Bind("Backspace",      McAction.ParentDir,          BindingContext.Panel);

        // Sort / filter (panel)
        Bind("Ctrl+S",          McAction.Sort,              BindingContext.Panel);
        Bind("Ctrl+X+F",        McAction.Filter,            BindingContext.Panel);
        Bind("Ctrl+Backslash",  McAction.ToggleShowHidden,  BindingContext.Panel);

        // Editor-specific
        Bind("F2",    McAction.Save,      BindingContext.Editor);
        Bind("Ctrl+Z", McAction.Undo,    BindingContext.Editor);
        Bind("Ctrl+Y", McAction.Redo,    BindingContext.Editor);
        Bind("Ctrl+A", McAction.SelectAll, BindingContext.Editor);
        Bind("Ctrl+H", McAction.Replace,  BindingContext.Editor);
        Bind("Ctrl+G", McAction.Goto,     BindingContext.Editor);
        Bind("F4",    McAction.MacroPlay, BindingContext.Editor);
    }

    public void Bind(string keySpec, McAction action,
        BindingContext context = BindingContext.Panel)
    {
        var map = context == BindingContext.Editor ? _editorBindings : _panelBindings;
        map[keySpec] = action;
        if (!_actionToKeys.TryGetValue(action, out var keys))
            _actionToKeys[action] = keys = [];
        if (!keys.Contains(keySpec)) keys.Add(keySpec);
    }

    public void Unbind(string keySpec)
    {
        _panelBindings.Remove(keySpec);
        _editorBindings.Remove(keySpec);
    }

    public bool TryGetAction(string keySpec, out McAction action,
        BindingContext context = BindingContext.Panel)
    {
        var map = context == BindingContext.Editor ? _editorBindings : _panelBindings;
        return map.TryGetValue(keySpec, out action);
    }

    // Legacy overload for backwards compatibility
    public bool TryGetAction(string keySpec, out McAction action)
        => _panelBindings.TryGetValue(keySpec, out action) ||
           _editorBindings.TryGetValue(keySpec, out action);

    public IReadOnlyList<string> GetKeys(McAction action)
        => _actionToKeys.TryGetValue(action, out var k) ? k.AsReadOnly() : [];

    public void LoadFromConfig(McConfig config)
    {
        foreach (var key in config.GetKeys("Keybindings"))
        {
            var value = config.GetString("Keybindings", key);
            if (Enum.TryParse<McAction>(value, true, out var action))
                Bind(key, action);
        }
    }

    public void SaveToConfig(McConfig config)
    {
        foreach (var (key, action) in _panelBindings)
            config.Set("Keybindings", key, action.ToString());
        foreach (var (key, action) in _editorBindings)
            config.Set("Keybindings", "editor:" + key, action.ToString());
    }
}
