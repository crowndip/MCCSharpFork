# mcedit Focus / Keyboard Input Problem Analysis

## Symptom

On Windows 11 in cmd.exe (ConHost), opening a file with F4 in mcedit:
- **Mouse selection works** (can highlight text by clicking/dragging)
- **Cursor is not visible**
- **Keyboard input does nothing** (typing doesn't insert characters)

## Root Cause

**`_editorContainer` has `CanFocus = false`, which removes the entire EditorView from Terminal.Gui v2's focus chain, preventing keyboard event delivery.**

### Detailed Explanation

In `EditorScreen.cs` (line 38-44), the editor container is created with:

```csharp
_editorContainer = new View
{
    X = 0, Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill(1),
    CanFocus = false,   // ← THE PROBLEM
};
```

Then at line 62:
```csharp
Add(_editorContainer, _buttonBar);
```

The `EditorView` (which has `CanFocus = true`) is a child of `_editorContainer`. The `_buttonBar` also has `CanFocus = false`.

**This means EditorScreen has ZERO focusable direct children.**

### How Terminal.Gui v2 Dispatches Keyboard Events

In Terminal.Gui v2 (source: [keyboard.md](https://github.com/gui-cs/Terminal.Gui/blob/v2_develop/docfx/docs/keyboard.md)), the keyboard event dispatch chain is:

1. Console driver generates `KeyDown` event
2. `Application` forwards it to the focused `Toplevel` via `View.NewKeyDownEvent()`
3. `NewKeyDownEvent()` recursively calls `NewKeyDown` on the **most focused subview** first
4. If the most-focused subview handles it (returns `true`), processing stops
5. If not, it bubbles up through the parent chain

The critical rule from the Terminal.Gui v2 [Navigation docs](https://gui-cs.github.io/Terminal.GuiV2Docs/docs/navigation.html):

> **If `CanFocus == true` but the `SuperView.CanFocus == false`, an `InvalidOperationException` is thrown during `EnterFocus`.**

This means:
- `EditorView` has `CanFocus = true`
- Its parent `_editorContainer` has `CanFocus = false`
- When `SetFocus()` is called on `EditorView`, it either **throws** or **silently fails**
- `EditorView` never actually enters the focus chain
- When a key arrives at `EditorScreen`, it finds **no focused subview** to forward to
- `EditorScreen.OnKeyDown()` returns `false` for non-special keys (line 115)
- The key event is **dropped**

### Why Mouse Works But Keyboard Doesn't

| Input Type | Dispatch Mechanism | Result |
|---|---|---|
| **Mouse** | Coordinate-based hit-testing | Works — Terminal.Gui finds `EditorView` by screen position, fires `MouseClick`/`MouseEvent` regardless of focus |
| **Keyboard** | Focus-chain-based dispatch | Fails — `EditorView` is not in the focus chain because its parent container is not focusable |

Mouse events are delivered based on which view occupies the screen coordinates where the click occurred. This bypasses the focus system entirely. That's why text selection via mouse works fine.

### Why the `OnDrawingContent` Fix Doesn't Work

Commit `2d45944` added:

```csharp
protected override bool OnDrawingContent(DrawContext? context)
{
    if (_editors.Count > 0 && !_editors[_currentTab].HasFocus)
    {
        _editors[_currentTab].SetFocus();  // ← This call fails silently
    }
    return base.OnDrawingContent(context);
}
```

`SetFocus()` on `EditorView` cannot succeed because the parent `_editorContainer` has `CanFocus = false`. The call either throws (caught internally by Terminal.Gui) or is a no-op. `HasFocus` remains `false` on every frame, so it retries every draw — and fails every time.

### Why Previous Fix Commits Didn't Solve It

| Commit | What It Did | Why It Didn't Fix Keyboard |
|---|---|---|
| `f95d333` | Override `OnKeyDown` to pass keys to focused editor | Returns `false` for non-special keys, but there's no focused child to receive them |
| `f6b5a3d` | Set `_editorContainer.CanFocus = false` | **This is the commit that introduced the root cause** — it removed the container from the focus chain |
| `5f9fd5b` | Remove MenuBar from view hierarchy | Correct fix for MenuBar interception, but didn't address the container focus issue |
| `2d45944` | Force focus in `OnDrawingContent` | `SetFocus()` fails because parent `CanFocus = false` |

### View Hierarchy Focus Analysis

```
EditorScreen (Toplevel, CanFocus=true)          ← receives keyboard from Application
├── _editorContainer (View, CanFocus=false)      ← BLOCKS focus chain
│   └── EditorView (View, CanFocus=true)         ← CANNOT receive focus
└── _buttonBar (EditorButtonBar, CanFocus=false)  ← not focusable (correct)
```

For keyboard to reach `EditorView`, the focus chain must be unbroken from `EditorScreen` down to `EditorView`. With `_editorContainer.CanFocus = false`, the chain is broken.

## Proposed Solutions

### Option A: Remove the Container (Simplest)

Don't wrap `EditorView` in `_editorContainer`. Add `EditorView` directly to `EditorScreen` and adjust its `Y`/`Height` directly when showing/hiding the menu bar.

```csharp
// Instead of adjusting _editorContainer, adjust the editor directly:
// When menu shown:
_editors[_currentTab].Y = 1;
_editors[_currentTab].Height = Dim.Fill(1);
// When menu hidden:
_editors[_currentTab].Y = 0;
_editors[_currentTab].Height = Dim.Fill(1);
```

Pros: Eliminates the intermediate container entirely, simplest fix.
Cons: Need to adjust all editors on tab switch if menu is visible.

### Option B: Make Container Focusable (Minimal Change)

Set `_editorContainer.CanFocus = true` and configure `TabStop` to pass-through:

```csharp
_editorContainer = new View
{
    X = 0, Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill(1),
    CanFocus = true,
    TabStop = TabBehavior.TabGroup,  // acts as a focus group, not a tab stop
};
```

Pros: Minimal code change, focus flows through container to children.
Cons: Container might intercept some navigation keys; needs testing.

### Option C: Manual Key Forwarding (Workaround)

In `EditorScreen.OnKeyDown`, explicitly forward all unhandled keys to the active editor:

```csharp
protected override bool OnKeyDown(Key keyEvent)
{
    // F9 / Esc handling stays the same...

    // Forward all other keys directly to the active editor
    if (_editors.Count > 0)
        return _editors[_currentTab].NewKeyDownEvent(keyEvent);
    return false;
}
```

Pros: Works regardless of focus state; guaranteed keyboard delivery.
Cons: Bypasses Terminal.Gui's focus system; may cause subtle issues with dialogs, menus, or nested views that expect proper focus routing.

## Recommended Fix

**Option A** (remove the container) is the cleanest solution. The `_editorContainer` exists solely for layout adjustment when the MenuBar is toggled. This can be done by adjusting the `EditorView` directly. It eliminates the focus chain break entirely and aligns with how Terminal.Gui v2 expects views to be structured.

If Option A proves too invasive due to multi-tab management, **Option C** (manual key forwarding) is the quickest workaround that guarantees keyboard delivery.

## References

- [Terminal.Gui v2 Navigation Deep Dive](https://gui-cs.github.io/Terminal.GuiV2Docs/docs/navigation.html)
- [Terminal.Gui v2 Keyboard Event Processing](https://github.com/gui-cs/Terminal.Gui/blob/v2_develop/docfx/docs/keyboard.md)
- [Terminal.Gui v2 What's New](https://gui-cs.github.io/Terminal.Gui/docs/newinv2)
- [Driver "v2win" broken in conhost · Issue #4004](https://github.com/gui-cs/Terminal.Gui/issues/4004)
