using Mc.Core.Config;

namespace Mc.Core.KeyBinding;

/// <summary>
/// Maps keyboard keys to named actions.
/// Equivalent to src/keymap.c and lib/keybind.c
/// </summary>
public sealed class KeyBindingManager
{
    private readonly Dictionary<string, McAction> _keyToAction = new(StringComparer.Ordinal);
    private readonly Dictionary<McAction, List<string>> _actionToKeys = [];

    public KeyBindingManager()
    {
        LoadDefaults();
    }

    private void LoadDefaults()
    {
        // Global
        Bind("F1", McAction.Help);
        Bind("F2", McAction.UserMenu);
        Bind("F9", McAction.Menu);
        Bind("F10", McAction.Quit);
        Bind("Ctrl+C", McAction.Quit);
        Bind("Ctrl+O", McAction.Shell);
        Bind("Ctrl+U", McAction.SwapPanels);
        Bind("Ctrl+R", McAction.Refresh);
        Bind("Tab", McAction.SwitchPanel);
        Bind("Shift+Tab", McAction.SwitchPanel);

        // File operations
        Bind("F3", McAction.View);
        Bind("F4", McAction.Edit);
        Bind("F5", McAction.Copy);
        Bind("F6", McAction.Move);
        Bind("F7", McAction.MakeDir);
        Bind("F8", McAction.Delete);
        Bind("Ctrl+X", McAction.Rename);
        Bind("Ctrl+L", McAction.Info);

        // Selection
        Bind("Insert", McAction.Mark);
        Bind("Plus", McAction.MarkPattern);
        Bind("Backslash", McAction.UnmarkAll);
        Bind("Asterisk", McAction.InvertSelection);

        // Navigation
        Bind("Ctrl+PageUp", McAction.ParentDir);
        Bind("Ctrl+Home", McAction.RootDir);
        Bind("Alt+H", McAction.DirHistory);
        Bind("Backspace", McAction.ParentDir);

        // Sort / filter
        Bind("Ctrl+S", McAction.Sort);
        Bind("Ctrl+X+F", McAction.Filter);
        Bind("Ctrl+Backslash", McAction.ToggleShowHidden);

        // Editor-specific (bound when editor is open)
        Bind("F2", McAction.Save);
        Bind("Ctrl+Z", McAction.Undo);
        Bind("Ctrl+Y", McAction.Redo);
        Bind("Ctrl+A", McAction.SelectAll);
        Bind("Ctrl+H", McAction.Replace);
        Bind("Ctrl+G", McAction.Goto);
        Bind("F4", McAction.MacroPlay);
    }

    public void Bind(string keySpec, McAction action)
    {
        _keyToAction[keySpec] = action;
        if (!_actionToKeys.TryGetValue(action, out var keys))
            _actionToKeys[action] = keys = [];
        if (!keys.Contains(keySpec)) keys.Add(keySpec);
    }

    public void Unbind(string keySpec) => _keyToAction.Remove(keySpec);

    public bool TryGetAction(string keySpec, out McAction action)
        => _keyToAction.TryGetValue(keySpec, out action);

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
        foreach (var (key, action) in _keyToAction)
            config.Set("Keybindings", key, action.ToString());
    }
}
