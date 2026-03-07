# Missing / Incomplete Functions Checklist 2

**Purpose:** Track every function marked `❌` or `⚠️` in `originalFunctions.md` that is not yet
fully implemented in the .NET port, and fix them to match original GNU Midnight Commander.

**Legend:**
- `[ ]` — not yet done
- `[x]` — fixed in this session

---

## A. Editor (mcedit) Gaps

| # | Feature | Original key | Status | Notes |
|---|---------|-------------|--------|-------|
| A1 | `[x]` Go to matching bracket | `Alt+[` | ✅ fixed | `GoToMatchingBracket()` in `EditorView.cs`; Alt+[ guard + F9 menu item |
| A2 | `[x]` Delete to end of line | `Ctrl+K` | ✅ fixed | `DeleteToEndOfLine()` in `EditorController.cs`; joins lines when at EOL |
| A3 | `[x]` Insert literal character (quote-next) | `Ctrl+Q` | ✅ fixed | `_quoteNext` flag in `EditorView.cs`; status bar shows "QUOT" |

---

## B. Diff Viewer Gaps

| # | Feature | Original key | Status | Notes |
|---|---------|-------------|--------|-------|
| B1 | `[x]` Save diff output to file | `F2` | ✅ fixed | `SaveDiff()` in `DiffController.cs`; `SaveDiffToFile()` in `DiffView.cs`; generates unified diff with `@@` hunks |

---

## C. User Menu Macro Gaps

| # | Macro | Meaning | Status | Notes |
|---|-------|---------|--------|-------|
| C1 | `[x]` `%l` | Symlink target path of current file | ✅ fixed | `fileEntry.SymlinkTarget` in `ExecuteUserMenuCommand()` |
| C2 | `[x]` `%x` | File extension with dot (e.g. `.c`, `.txt`) | ✅ fixed | `Path.GetExtension(fileName)` added to macro chain |
| C3 | `[x]` `%n` correct semantics | Strip leading dot from filename | ✅ fixed | `fileName.TrimStart('.')` — correctly distinct from `%b` |

---

## D. Command Line Gaps

| # | Feature | Key | Status | Notes |
|---|---------|-----|--------|-------|
| D1 | `[x]` Alt+Tab completion | `Alt+Tab` | ✅ fixed | Added `key.KeyCode == (KeyCode.Tab | KeyCode.AltMask)` guard in `CommandLineView.cs` |

---

## E. Documentation Corrections (mark already-implemented items as ✅ in originalFunctions.md)

These items are marked `❌` or `⚠️` in `originalFunctions.md` but are already fully implemented:

| # | Section | Feature | Actual state |
|---|---------|---------|-------------|
| E1 | `[x]` §1 Hints bar | Rotating tip strip | ✅ `HintsBarView.cs` — 20 tips, `NextTip()` called on navigation |
| E2 | `[x]` §4.3 Screen list | `Alt+\`` | ✅ `ShowScreenList()` in `McApplication.cs` — lists MC + editors + viewers |
| E3 | `[x]` §9 Tab completion | `Tab` | ✅ `CommandLineView.cs` — directory/file completion with popup |
| E4 | `[x]` §9 Ctrl+Q quote-next | `Ctrl+Q` | ✅ `CommandLineView.cs` — `_quoteNext` state implemented |
| E5 | `[x]` §9 Ctrl+A/E/K/W/Y/Alt+B/F | Emacs editing | ✅ All implemented in `CommandLineView.cs` |
| E6 | `[x]` §12.1 Open file dialog | `Ctrl+O` (editor) | ✅ `EditorView.OpenFileDialog()` — with unsaved-changes check |
| E7 | `[x]` §12.6 Word completion | `Ctrl+Tab` | ✅ `EditorView.WordComplete()` — scans buffer for prefix matches |
| E8 | `[x]` §12.6 Macro recording | `Ctrl+R` / `Ctrl+E` | ✅ `EditorView` — `_macroKeys`, `ToggleMacroRecord()`, `PlayMacro()` |
| E9 | `[x]` §12.5 Replace again | `Shift+F4` | ✅ `EditorView.RepeatLastReplace()` |
| E10 | `[x]` §16 Date/time filter in Find | dialog fields | ✅ `FindDialog.cs` — `NewerThanDays` / `OlderThanDays` fields |
| E11 | `[x]` §16 File size filter in Find | dialog fields | ✅ `FindDialog.cs` — `MinSizeKB` / `MaxSizeKB` fields |
| E12 | `[x]` §16 Ignore directories | dialog field | ✅ `FindDialog.cs` — `IgnoreDirs` colon-separated field |
| E13 | `[x]` §16 Panelize from Find results | Panelize button | ✅ `McApplication.cs` — Panelize button in find-results window |
| E14 | `[x]` §19 User menu `%p`,`%s`,`%t`,`%b`,`%n`,`%e` | macros | ✅ All substituted in `ExecuteUserMenuCommand()` |
| E15 | `[x]` §2.1 CK_Panelize in panel menu | panel menu | ✅ `ExternalPanelize()` wired to panel menu item |
| E16 | `[x]` §10 Alt+\` Screen list | global key | ✅ wired in `McApplication.cs` |

---

## F. Out-of-Scope (explicitly listed in §23 "Not Implemented")

These will NOT be implemented (by design):
- Shell link / FISH protocol — complex SSH/shell protocol, SFTP preferred
- Multiple subshell screens — complex TUI multiplexing
- GPM mouse, console saver — obsolete Linux VT features

---

*Created: 2026-03-02 — All items completed: 2026-03-07*
