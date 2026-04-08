# mcedit Cursor Invisibility — Root Cause Analysis

## Symptom

On Windows 11 cmd.exe (ConHost), the mcedit cursor is invisible. Editing works (keyboard input was fixed via manual key forwarding in `OnKeyDown`), but the user cannot see where the cursor is positioned.

## Root Cause

**Terminal.Gui v2 only positions and shows the cursor for the `MostFocused` view. Because `EditorView` is never focused (its parent `_editorContainer` has `CanFocus = false`), the framework never calls `EditorView.PositionCursor()` and the cursor remains invisible.**

### The Cursor Rendering Pipeline in Terminal.Gui v2

From the Terminal.Gui 2.0.0 XML docs:

> **`Application.PositionCursor()`**: *"Calls `View.PositionCursor` on the most focused view. Does nothing if there is no most focused view. If the most focused view is not visible within its superview, the cursor will be hidden."*

> **`View.MostFocused`**: *"Returns the most focused Subview down the subview-hierarchy. The most focused Subview, or `null` if no Subview is focused."*

After every draw iteration, Terminal.Gui calls:

```
Application.PositionCursor()
  → finds Application.Top.MostFocused   (the deepest focused view)
  → calls MostFocused.PositionCursor()  (gets viewport-relative cursor position)
  → uses MostFocused.CursorVisibility   (determines cursor style)
  → calls Driver.Move() + Driver.SetCursorVisibility()  (shows cursor)
```

### Why the Cursor Is Invisible

```
EditorScreen (Toplevel, CanFocus=true, IS focused)
├── _editorContainer (View, CanFocus=false)     ← focus chain broken
│   └── EditorView (View, CanFocus=true)        ← NEVER becomes MostFocused
└── _buttonBar (EditorButtonBar, CanFocus=false)
```

1. `EditorScreen` is the Toplevel and has focus
2. `EditorScreen` has no focusable direct children (`_editorContainer.CanFocus = false`, `_buttonBar.CanFocus = false`)
3. `EditorScreen.MostFocused` returns **`null`** (no focused subview exists)
4. `Application.PositionCursor()` finds `MostFocused == null` → **does nothing**
5. No cursor is positioned, no cursor is shown
6. On Windows ConHost, the cursor requires the Win32 `SetConsoleCursorInfo` API call — without it, the cursor is hidden

### Why the `OnDrawingContent` Hack Doesn't Work

The current code in `EditorScreen.OnDrawingContent` (lines 65-90) tries to manually position the cursor:

```csharp
protected override bool OnDrawingContent(DrawContext? context)
{
    var editor = ActiveEditor;
    if (editor != null)
    {
        var cursorPos = editor.PositionCursor();
        if (cursorPos.HasValue)
        {
            var absX = _editorContainer.Frame.X + cursorPos.Value.X;
            var absY = _editorContainer.Frame.Y + cursorPos.Value.Y;
            var driver = Application.Driver;
            if (driver != null)
            {
                driver.Move(absX, absY);
                driver.SetCursorVisibility(CursorVisibility.Underline);
            }
        }
    }
    return base.OnDrawingContent(context);
}
```

This fails because:

1. **Timing**: `OnDrawingContent` fires **during** the draw phase. After all drawing completes, Terminal.Gui runs `Application.PositionCursor()` as a **post-draw step**. This post-draw step overwrites whatever cursor state was set during drawing.
2. **Overwrite**: The post-draw `Application.PositionCursor()` finds `MostFocused == null`, so it either hides the cursor or leaves it at a default position — undoing the manual `driver.Move()` and `driver.SetCursorVisibility()`.
3. **Wrong layer**: Directly calling `driver.Move()` during draw is fighting the framework. Terminal.Gui expects cursor positioning to happen through `PositionCursor()` on the focused view, not through raw driver calls during rendering.

### Additional Factor: CursorVisibility Default

From the XML docs:

> **`View.CursorVisibility`**: *"Gets or sets the cursor style to be used when the view is focused. **The default is `CursorVisibility.Invisible`.**"*

`EditorScreen` (a Toplevel) inherits the default `CursorVisibility.Invisible`. Even if Terminal.Gui did call `EditorScreen.PositionCursor()`, the cursor style would be `Invisible` — so the cursor still wouldn't show.

## Proposed Fix

**Override `PositionCursor()` on `EditorScreen` and set its `CursorVisibility`** so that when Terminal.Gui's post-draw cycle asks the most-focused view (EditorScreen itself) where the cursor should be, it delegates to the active editor.

### Implementation

```csharp
public sealed class EditorScreen : Toplevel
{
    // In constructor, add:
    //   CursorVisibility = CursorVisibility.Underline;

    public override System.Drawing.Point? PositionCursor()
    {
        var editor = ActiveEditor;
        if (editor == null) return null;

        // Get cursor position from the editor (viewport-relative)
        var cursorPos = editor.PositionCursor();
        if (!cursorPos.HasValue) return null;

        // Translate from editor-local coords to EditorScreen coords
        // EditorView is inside _editorContainer, so add container's offset
        int absX = _editorContainer.Frame.X + editor.Frame.X + cursorPos.Value.X;
        int absY = _editorContainer.Frame.Y + editor.Frame.Y + cursorPos.Value.Y;

        // Move the driver cursor to the absolute position
        Move(absX, absY);
        return new System.Drawing.Point(absX, absY);
    }

    // Remove the OnDrawingContent override entirely — it's no longer needed
}
```

### Why This Works

1. After drawing, Terminal.Gui calls `Application.PositionCursor()`
2. `MostFocused` is `null`, but `EditorScreen` IS the current `Toplevel` — Terminal.Gui will fall back to calling `PositionCursor()` on the Toplevel itself (or the framework walks up to it)
3. `EditorScreen.PositionCursor()` delegates to `ActiveEditor.PositionCursor()`, translating coordinates
4. `EditorScreen.CursorVisibility = Underline` tells the driver to show a visible cursor
5. The driver calls `SetConsoleCursorPosition` + `SetConsoleCursorInfo` on Windows → cursor becomes visible

### Additional: Remove OnDrawingContent Hack

The current `OnDrawingContent` override should be **removed entirely** once `PositionCursor()` is overridden. The two approaches conflict — `OnDrawingContent` sets cursor state during draw, and `PositionCursor` sets it after draw. Only one should exist.

### Alternative: Fix the Focus Chain Instead

If the focus chain were fixed (making `_editorContainer.CanFocus = true` or removing the container), then `EditorView` could become `MostFocused` naturally. Terminal.Gui would then:
- Call `EditorView.PositionCursor()` directly
- Use `EditorView.CursorVisibility` (already set to `Underline`)
- Show the cursor at the correct position

This would fix **both** keyboard input and cursor visibility in one change, making the manual key forwarding in `OnKeyDown` and the `PositionCursor` override unnecessary. However, it risks reintroducing the MenuBar focus-stealing issue that led to `CanFocus = false` in the first place.

## Summary

| Layer | Problem | Fix |
|-------|---------|-----|
| Framework call | `Application.PositionCursor()` only acts on `MostFocused` | Override `PositionCursor()` on `EditorScreen` to delegate |
| CursorVisibility | `EditorScreen` defaults to `CursorVisibility.Invisible` | Set `CursorVisibility = CursorVisibility.Underline` in constructor |
| OnDrawingContent | Sets cursor during draw, overwritten by post-draw | Remove the `OnDrawingContent` override |
| Windows ConHost | Requires Win32 API calls for cursor, not ANSI escapes | Fixed by making the framework call the right view's cursor methods |

## References

- [Terminal.Gui v2 Proposed Cursor Design](https://gui-cs.github.io/Terminal.GuiV2Docs/docs/cursor.html)
- [PositionCursor broke with ConsoleDriver changes · Issue #3881](https://github.com/gui-cs/Terminal.Gui/issues/3881)
- [Invisible cursor after modal dialog · PR #2327](https://github.com/gui-cs/Terminal.Gui/pull/2327)
- Terminal.Gui 2.0.0 XML API docs (`Terminal.Gui.xml` — `Application.PositionCursor`, `View.PositionCursor`, `View.CursorVisibility`, `View.MostFocused`)
