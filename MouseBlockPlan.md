# Mouse Block Selection in mcedit — Implementation Plan

## Current State

Mouse **click-to-position** works on all platforms.  Mouse **drag selection** was
added in commit `246e60a` but **does not work** on Windows cmd.exe (and may be
unreliable on other terminals too).

## Root Cause

The drag implementation in `EditorView.OnMouseEvent` listens for the
`MouseFlags.ReportMousePosition` flag, but the view never opts in to receiving
these events:

```csharp
// EditorView constructor — MISSING:
WantMousePositionReports = true;
```

Terminal.Gui v2 requires `View.WantMousePositionReports = true` for the view to
receive `ReportMousePosition` events.  Without it, no mouse-move events reach
`OnMouseEvent` during a drag, so the selection is never extended.

Additionally, the view should use `Application.GrabMouse(this)` during drag so
events that leave the view's bounds (e.g. user drags above/below the text area)
are still routed to it, and `Application.UngrabMouse()` on release.

## Proposed Fix

### Step 1 — Enable mouse position reports (the actual fix)

In the `EditorView` constructor, after the existing mouse event subscriptions:

```csharp
WantMousePositionReports    = true;   // receive ReportMousePosition events
WantContinuousButtonPressed = true;   // fallback: re-fire Button1Pressed on move
```

### Step 2 — Grab/ungrab mouse during drag

In `OnMouseClicked`, when `Button1Pressed` starts a drag:

```csharp
case Button1Pressed:
    ...
    _mouseButtonHeld = true;
    Application.GrabMouse(this);       // ← ADD: route all events to us
```

In `OnMouseClicked`, when `Button1Released` ends a drag:

```csharp
case Button1Released:
    _mouseButtonHeld = false;
    Application.UngrabMouse();          // ← ADD: release grab
```

### Step 3 — Handle continuous-press fallback

With `WantContinuousButtonPressed = true`, the `MouseClick` event will fire
repeatedly with `Button1Pressed` while the button is held and the mouse moves.
Add a second check in `OnMouseClicked` so that these repeated presses extend
the selection:

```csharp
// In OnMouseClicked, after the existing Button1Pressed initial-press handler:
if (e.Flags.HasFlag(MouseFlags.Button1Pressed) && _mouseButtonHeld)
{
    // Repeated press during drag → extend selection
    ExtendSelectionToMousePos(e);
    return;
}
```

This provides a fallback for any driver that supports continuous-press but not
position reports.

### Step 4 — Auto-scroll on drag past visible area

When the user drags above or below the visible text area, the view should
scroll.  In `ExtendSelectionToMousePos`:

```csharp
if (screenRow < 0)
{
    _topLine = Math.Max(0, _topLine - 1);
    screenRow = 0;
}
else if (screenRow >= contentHeight)
{
    _topLine = Math.Min(_editor.Buffer.GetLineCount() - 1, _topLine + 1);
    screenRow = contentHeight - 1;
}
```

### Step 5 — Clean up release handling

Ensure `Button1Released` is handled robustly:

```csharp
if (e.Flags.HasFlag(MouseFlags.Button1Released))
{
    if (_mouseButtonHeld)
    {
        _mouseButtonHeld = false;
        Application.UngrabMouse();
    }
    e.Handled = true;
    return;
}
```

## Progress

| Step | Status |
|------|--------|
| 1 — `WantMousePositionReports` + `WantContinuousButtonPressed` in constructor | ✅ Done |
| 2 — `GrabMouse` on press, `UngrabMouse` on release                            | ✅ Done |
| 3 — Continuous-press fallback in `OnMouseClicked`                             | ✅ Done |
| 4 — Auto-scroll in `ExtendSelectionToMousePos`                                | ✅ Done |
| 5 — Robust `Button1Released` handling                                         | ✅ Done |

## Files to Change

| File | Change |
|------|--------|
| `src/Mc.Editor/EditorView.cs` | All five steps above — constructor, OnMouseClicked, ExtendSelectionToMousePos |

No changes needed in EditorController — the existing `StartSelection()` /
`ExtendSelection()` / `ClearSelection()` API is sufficient.

## Testing

1. **Windows cmd.exe**: Click and drag to select text → selection highlight
   should follow the mouse in real time.
2. **Windows Terminal**: Same test.
3. **Linux terminal (xterm, gnome-terminal)**: Same test — should still work.
4. **Auto-scroll**: Drag below visible area → text should scroll down and
   selection should extend; drag above → scroll up.
5. **Double-click + drag**: Double-click to select a word, then drag to extend
   by words (existing behaviour should not regress).
6. **Triple-click**: Triple-click to select a line (existing behaviour should
   not regress).
7. **Release outside view**: Drag mouse outside the editor view bounds, then
   release → selection should remain, no crash.

## Summary of Changes

The fix is small (< 20 lines of code changes in one file).  The core issue is
just two missing property assignments in the constructor.  Steps 2–5 add
robustness for edge cases (drag outside bounds, platform fallback, auto-scroll).
