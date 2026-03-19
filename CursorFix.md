# McEdit Cursor Invisible on Windows cmd.exe — Root Cause & Fix

## Symptom

When running McEdit inside `cmd.exe` (Windows ConHost) the text cursor is
completely invisible.  The user cannot tell where they are typing.
The cursor **is** visible on Linux and in Windows Terminal.

## Root Cause

Terminal.Gui 2.0.0 `View` has a property:

```csharp
public CursorVisibility CursorVisibility { get; set; }
```

Its **default value is `CursorVisibility.Invisible`**.

`EditorView` never sets this property, so the cursor visibility stays
`Invisible` for the lifetime of the view.

### Why it appears to work on Linux / Windows Terminal

In `OnHasFocusChanged`, the code calls:

```csharp
EscSeqUtils.CSI_SetCursorStyle(EscSeqUtils.DECSCUSR_Style.BlinkingUnderline);
```

This writes the raw ANSI escape `ESC[5 q` directly to stdout.  On terminals
that support DECSCUSR (Linux, macOS, Windows Terminal with VTP enabled), this
**overrides** Terminal.Gui's `Invisible` setting at the driver level — the
terminal hardware cursor becomes a blinking underline regardless of what
Terminal.Gui thinks the visibility should be.

### Why it fails on cmd.exe / ConHost

Windows ConHost (the legacy console host behind `cmd.exe`) does **not**
process DECSCUSR escape sequences, so `ESC[5 q` is silently ignored.

After every redraw, Terminal.Gui calls `PositionCursor()` on the focused view,
then applies the view's `CursorVisibility` through the `WindowsDriver`, which
calls the Win32 API `SetConsoleCursorInfo`.  Since `CursorVisibility` is
`Invisible`, the Win32 call sets `CONSOLE_CURSOR_INFO.bVisible = FALSE` — the
cursor disappears and stays hidden.

### The `PositionCursor()` override (v1.8.3) — necessary but not sufficient

Commit ad379d5 added a `PositionCursor()` override to `EditorView`.  This is
**correct and necessary** for positioning, but it does not fix visibility
because Terminal.Gui checks `CursorVisibility` *after* `PositionCursor()`
returns.  With `CursorVisibility == Invisible` the cursor is hidden even when
`PositionCursor()` returns a valid point.

## Proposed Fix

### 1. Set `CursorVisibility` in the `EditorView` constructor

```csharp
public EditorView(string? filePath = null)
{
    // ... existing code ...
    CursorVisibility = CursorVisibility.Underline;   // ← ADD THIS
}
```

This tells Terminal.Gui's driver (including `WindowsDriver`) to show a
blinking underline cursor at whatever position `PositionCursor()` returns.
On Windows this flows through `SetConsoleCursorInfo` with `bVisible = TRUE`
and the appropriate cursor size.

### 2. Remove the raw ANSI escape calls (optional cleanup)

With `CursorVisibility` set properly, the `EscSeqUtils.CSI_SetCursorStyle`
calls in `OnHasFocusChanged` become redundant — Terminal.Gui handles cursor
shape through the driver.  They can be removed or kept as a belt-and-
suspenders measure (they are harmless on ConHost — just no-ops).

### 3. Toggle visibility when entering/leaving hex mode (optional refinement)

```csharp
private void ToggleHexMode()
{
    // ... existing toggle logic ...
    // Cursor shape: underline for text editing, box for hex editing
    CursorVisibility = _hexMode
        ? CursorVisibility.Box
        : CursorVisibility.Underline;
}
```

## Available `CursorVisibility` Values (Terminal.Gui 2.0.0)

| Value          | Meaning                        |
|----------------|--------------------------------|
| `Default`      | Terminal default shape          |
| `Invisible`    | Hidden (the default for View!) |
| `Underline`    | Blinking underline             |
| `UnderlineFix` | Steady underline               |
| `Vertical`     | Blinking vertical bar          |
| `VerticalFix`  | Steady vertical bar            |
| `Box`          | Blinking block                 |
| `BoxFix`       | Steady block                   |

## Summary

| Layer               | Linux / Windows Terminal       | Windows cmd.exe (ConHost)     |
|---------------------|-------------------------------|-------------------------------|
| ANSI `ESC[5 q`      | Works — forces blinking underline | No-op — silently ignored     |
| `View.CursorVisibility` | Overridden by ANSI escape | **Honored by WindowsDriver** |
| `PositionCursor()`  | Positions cursor              | Positions cursor              |
| Net effect          | Cursor visible (by accident)  | **Cursor invisible**          |

**The one-line fix:** set `CursorVisibility = CursorVisibility.Underline` in
the EditorView constructor.  This makes Terminal.Gui's driver show the cursor
on **all** platforms through the platform-native API.
