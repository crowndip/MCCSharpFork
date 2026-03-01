# Missing / Incomplete Functions Checklist

**Generated:** 2026-03-01
**Method:** Every item in `originalFunctions.md` verified against actual source code.
Items marked ✅ in originalFunctions.md but not truly complete are included here.

**Status flags:**
- `[ ]` Not started
- `[~]` Partial — code exists but logic incomplete or option ignored
- `[x]` Fixed

**Verification sources:** McApplication.cs, FilePanelView.cs, ViewerView.cs, EditorView.cs,
CommandLineView.cs, FindDialog.cs, CopyMoveDialog.cs, FileOperations.cs, HelpDialog.cs

---

## Summary

| Tier | Total | Fixed | Remaining |
|------|-------|-------|-----------|
| 1 — Critical | 5 | 5 | 0 |
| 2 — High | 12 | 6 | 6 |
| 3 — Medium | 18 | 6 | 12 |
| 4 — Low | 11 | 0 | 11 |
| **Total** | **46** | **17** | **29** |

---

## Tier 1 — Critical (breaks visible everyday workflows)

| # | Status | Area | What is missing | Evidence |
|---|--------|------|-----------------|----------|
| 1 | `[x]` | **Copy/Move: source mask pattern rename not applied** | Fixed: `FileOperations.CopyAsync()` now accepts `sourceMask` parameter; files not matching the glob pattern are skipped. `McApplication.CopyFiles/MoveFiles` pass `opts.SourceMask`. `MatchesGlob()` helper converts glob to regex. | `CopyMoveDialog.cs:110`, `McApplication.cs`, `FileOperations.cs` |
| 2 | `[x]` | **Copy/Move: "Dive into subdirectory" option never applied** | Fixed: `diveIntoSubdir` parameter now wired through `CopyMarkedAsync()` → `FileOperations.CopyAsync()`. `McApplication.CopyFiles()` passes `opts.DiveIntoSubdir`. | `CopyMoveDialog.cs`, `McApplication.cs`, `FileOperations.cs` |
| 3 | `[x]` | **Copy: "Follow symlinks" option never applied** | Fixed: `followSymlinks` parameter added. When `false`, symlinks are copied as symlinks using `VfsRegistry.CreateSymlink()`. When `true`, the target file content is copied. Passed from dialog through all layers. | `FileOperations.cs`, `FileManagerController.cs`, `McApplication.cs` |
| 4 | `[x]` | **Command line: Tab / Alt+Tab filename completion** | Fixed: `CommandLineView` now intercepts Tab key. `TabComplete()` searches `_currentDirectory` for matches, completes to longest common prefix, shows a popup for multiple matches. | `CommandLineView.cs` |
| 5 | `[x]` | **Viewer: Ctrl+F / Ctrl+B — open next / previous file in directory** | Fixed: `ViewerView` now accepts `fileList` and `fileListIndex` parameters. `Ctrl+F` calls `NavigateFile(+1)`, loads the next file. `McApplication.ViewFile()` passes the active panel's file list. | `ViewerView.cs`, `McApplication.cs` |

---

## Tier 2 — High Impact (noticeable in regular use)

| # | Status | Area | What is missing | Evidence |
|---|--------|------|-----------------|----------|
| 6 | `[x]` | **Command line: Emacs-style editing keys** | Fixed: `Ctrl+A` (start), `Ctrl+E` (end), `Ctrl+K` (kill to end), `Ctrl+W` (kill word), `Ctrl+Y` (yank kill ring), `Alt+B` (word left), `Alt+F` (word right) all implemented in `CommandLineView.OnInputKeyDown()`. | `CommandLineView.cs` |
| 7 | `[ ]` | **Find file: date/time filter** | `FindOptions` record has no date fields. `FindDialog` has no date/time input widgets. Original MC allows filtering by modification date (newer/older than N days). | `FindDialog.cs:5-17` (FindOptions), `FindDialog.cs:25-109` |
| 8 | `[ ]` | **Find file: file size filter** | `FindOptions` has no `MinSize`/`MaxSize` fields. Original MC allows filtering by file size (larger/smaller than N bytes/KB/MB). | `FindDialog.cs:5-17` |
| 9 | `[x]` | **Viewer: F5 prompts for line number in text mode (not byte offset)** | Fixed: `ShowGotoPosition()` checks `_viewer.Mode`. In text mode shows "Line number:" prompt and sets `_viewer.ScrollLine`. In hex mode shows "Byte offset:" prompt. | `ViewerView.cs` |
| 10 | `[x]` | **Editor: Ctrl+O — open file dialog** | Fixed: `Ctrl+O` handler added. Prompts user (with unsaved-changes dialog if needed), then calls `_editor.LoadFile()` which resets the buffer and undo stack. `TextBuffer.SetContent()` and `EditorController.LoadFile()` added. | `EditorView.cs`, `EditorController.cs`, `TextBuffer.cs` |
| 11 | `[x]` | **Editor: Shift+F4 — Repeat last find-and-replace** | Fixed: `Shift+F4` binding added, calls `RepeatLastReplace()` which re-runs the last replace operation without showing the dialog. Falls back to `ShowFindReplace()` if no prior search. | `EditorView.cs` |
| 12 | `[x]` | **Viewer: "/" — start forward search** | Fixed: `keyEvent.AsRune.Value == '/'` check before the switch statement calls `ShowSearch(backward: false)`. | `ViewerView.cs` |
| 13 | `[x]` | **Viewer: F9 — toggle nroff/formatted display** | Fixed: F9 toggles `_nroffMode`. `StripNroff()` removes `char\bchar` (bold) and `_\bchar` (underline) escape sequences. Mode label shows "NROFF". | `ViewerView.cs` |
| 14 | `[x]` | **Hints bar below panels** | Fixed: `HintsBarView` created with 20 rotating tips. Added to layout between panels and command line. `ShowHints` setting in `McSettings`. Tips advance on panel cursor movement. | `HintsBarView.cs`, `McApplication.cs`, `McSettings.cs` |
| 15 | `[ ]` | **Screen list (multiple open editors/viewers)** | `Command > Screen list` shows "Not implemented". Original MC maintains a list of open editor/viewer subshell screens accessible via a popup. | `McApplication.cs` — screen list stub |
| 16 | `[ ]` | **VFS: FTP / SFTP providers not registered** | `Mc.Vfs.Ftp` and `Mc.Vfs.Sftp` projects exist but are not registered in the VFS registry at startup. FTP/SFTP menu items silently fail. | `Mc.App` DI setup |
| 17 | `[x]` | **Copy: "Stable symlinks" option never applied** | Fixed: `stableSymlinks` parameter added throughout the copy chain. When true, `MakeRelativeSymlinkTarget()` converts symlink targets to relative paths before creating at destination. | `FileOperations.cs`, `FileManagerController.cs`, `McApplication.cs` |

---

## Tier 3 — Medium Impact (power users notice)

| # | Status | Area | What is missing | Evidence |
|---|--------|------|-----------------|----------|
| 18 | `[x]` | **Viewer: Alt+R — toggle column ruler** | Fixed: `Alt+R` toggles `_showRuler`. `DrawRuler()` renders a column marker strip (`---+----0---+...`) between content and status bar. Reserved rows adjusted dynamically. | `ViewerView.cs` |
| 19 | `[x]` | **Viewer: Alt+E — change encoding** | Fixed: `Alt+E` opens `ShowEncodingDialog()` — filterable list of all .NET encodings. On select, sets `_viewer.Encoding` and reloads the file. | `ViewerView.cs` |
| 20 | `[x]` | **Viewer: numeric bookmarks 0–9 (`[n]m` set, `[n]r` goto)** | Fixed: digit keys (0-9) set `_digitPrefix=true`. Next 'm' saves `_viewer.ScrollLine` to `_bookmarks[n]`; next 'r' restores it. `Ctrl+B` with no prefix saves bookmark 0. | `ViewerView.cs` |
| 21 | `[x]` | **Viewer: F1 inside viewer opens viewer-specific help** | Fixed: F1 handler calls `ShowViewerHelp()` which shows a dialog listing all viewer key bindings. | `ViewerView.cs` |
| 22 | `[ ]` | **Editor: Ctrl+R conflict — macro recording** | `Ctrl+R` is bound to Redo in `EditorView.cs:319`. Original mcedit uses `Ctrl+R` for macro recording (start/stop). Neither macro recording nor the conflict resolution is implemented. | `EditorView.cs:319` |
| 23 | `[ ]` | **Editor: word completion (Ctrl+Tab)** | No `Ctrl+Tab` handler, no completion popup, no word list logic in `EditorView`. | `EditorView.cs` |
| 24 | `[ ]` | **Editor: column/rectangular block selection** | `EditorView` implements linear (stream) selection via Shift+Arrow keys. Original mcedit supports column (rectangular) block selection (Alt+B or column mode). | `EditorView.cs:186-202` |
| 25 | `[ ]` | **Editor: spell checking** | No spell-check integration. Original mcedit can invoke aspell/enchant for spell checking. | `EditorView.cs` |
| 26 | `[x]` | **Command line: Ctrl+Q — quote next character** | Fixed: `Ctrl+Q` sets `_quoteNext=true`; the next keypress is inserted literally via `InsertAtCursor()`. | `CommandLineView.cs` |
| 27 | `[ ]` | **Panel: device/socket/FIFO colour coding** | `FilePanelView.GetEntryAttr()` has no branches for block devices, character devices, FIFOs, or sockets. `FileEntry`/`VfsDirEntry` models have no `IsDevice`/`IsSocket`/`IsFifo` fields. | `FilePanelView.cs:402-416`, `FileEntry.cs` |
| 28 | `[ ]` | **Panel listing: User-defined column format mode** | `ShowListingFormatDialog()` offers Full / Brief / Long. Original MC has a fourth mode ("User defined") with a custom format string specifying columns and widths. | Listing format dialog code |
| 29 | `[ ]` | **Help: hypertext link navigation** | `HelpDialog` shows sections and has Back/Contents buttons but no clickable or keyboard-navigable links embedded in the help text. Original MC help uses ctrl-char sequences to embed hot-links between topics. | `HelpDialog.cs:372-500` |
| 30 | `[ ]` | **User menu: missing `%p`, `%P`, `%n`, `%m`, `%a` macro substitutions** | `McApplication.ExpandMacros()` handles `%f`, `%b`, `%e`, `%d`, `%D`, `%s`, `%t`, `%{Prompt}` — but `%p` (relative source path), `%P` (relative dest path), `%n` (filter), `%m` (marked files list), `%a` (archive name) are absent. | `McApplication.cs:2662-2689` |
| 31 | `[ ]` | **VFS: FISH protocol (files over SSH shell)** | `Left > Shell link…` shows "Not implemented". Original MC implements the FISH protocol for remote file access over an SSH connection using shell commands. | Menu stub |
| 32 | `[ ]` | **VFS: CPIO / ZIP archives as navigable VFS** | `Mc.Vfs.Archives` project exists but coverage of CPIO and ZIP browsing as VFS directories is incomplete/unverified. | `Mc.Vfs.Archives` |
| 33 | `[ ]` | **VFS: External filesystem scripts (extfs)** | No extfs handler. Original MC supports scripts in `/usr/lib/mc/extfs.d/` that expose e.g. RPM packages, Debian packages, audio CDs as VFS directories. | (no extfs code) |
| 34 | `[ ]` | **VFS: SFS (single-file filesystem)** | No SFS provider. Original MC uses `mc.sfs` config to mount single-file containers (e.g. ISO images) via external helpers. | (no SFS code) |
| 35 | `[ ]` | **Copy: preserve ext2 file attributes** | "Preserve ext2 attributes" checkbox is absent from the `CopyMoveDialog`. Original MC can copy immutable, append-only, and other ext2 attributes alongside timestamps and permissions. | `CopyMoveDialog.cs` |

---

## Tier 4 — Low Impact / Cosmetic / Edge Cases

| # | Status | Area | What is missing | Evidence |
|---|--------|------|-----------------|----------|
| 36 | `[ ]` | **Panel: executable files — optional `*` suffix (like `ls -F`)** | `FormatEntry()` and `FormatBriefCell()` never append `*` to executable filenames. Original MC has a panel option to show the suffix. Requires panel option + name-column-width adjustment. | `FilePanelView.cs:449-477` |
| 37 | `[ ]` | **Panel: symlink-to-directory shown in directory colour** | `GetEntryAttr()` returns `PanelSymlink` (cyan) for all symlinks. When "Follow symlinks" panel option is on, symlinks pointing to directories should use `PanelDirectory` (white). Requires `stat()` on symlink target in `VfsDirEntry`. | `FilePanelView.cs:412` |
| 38 | `[ ]` | **Panel: quick search cursor positioned after typed chars** | Quick search appends `_` to the search term in the status strip but the actual terminal cursor is not moved there. Original MC positions the blinking hardware cursor after the last typed character. | `FilePanelView.cs:187` |
| 39 | `[ ]` | **Panel: sort column name drawn in distinct colour (not just ↑/↓ suffix)** | The active sort column has `↑`/`↓` appended to its header name but no separate colour attribute. Original MC draws the sorted column name in a brighter/inverted colour. | `FilePanelView.cs` — `DrawColumnHeader()` |
| 40 | `[ ]` | **Subshell: typed command carried into shell (type then Ctrl+O)** | When the user types a command in the command line and presses `Ctrl+O`, original MC passes the typed text to the subshell so it appears ready to execute. Current `LaunchShell()` ignores any text in the command line. | `McApplication.cs` — `LaunchShell()` |
| 41 | `[ ]` | **Subshell: shell prompt shown in command line (live)** | Original MC with subshell support shows the actual shell prompt (e.g. `user@host:~/dir$`) in the command line. Current implementation shows a simplified truncated-path prompt constructed by MC, not the real shell prompt. | `CommandLineView.cs` — `SetDirectory()` |
| 42 | `[ ]` | **Subshell: multiple screens** | `Command > Screen list` is not implemented. Original MC supports multiple pseudo-terminal subshell sessions, each accessible from the screen manager. | `McApplication.cs` — screen list stub |
| 43 | `[ ]` | **Viewer: per-file display settings not remembered** | Original mcview remembers wrap/hex mode per file within a session. The port always resets to default mode when opening a new file. | `ViewerView.cs` |
| 44 | `[ ]` | **Learn Keys dialog: interactive key tester** | The current Learn Keys dialog (`ShowLearnKeysDialog()`) shows a static table of bindings. Original MC's Learn Keys is interactive: press a key and it shows what MC recognises it as, allowing the user to teach MC about terminal key sequences. | `ShowLearnKeysDialog()` implementation |
| 45 | `[ ]` | **Configuration: "Rotating dash" busy indicator** | Original MC animates a spinning dash `-\|/` in the panel title while loading large directories. The port has no such indicator. | `FilePanelView.cs` — no busy spinner |
| 46 | `[ ]` | **Configuration: "Cd follows links" option** | When this option is on, `cd` through a symlink to a directory shows the real (physical) path, not the symlinked path. Not in `McSettings` and not applied in navigation. | `McSettings.cs` — no such property |

---

## Notes on Items Verified as Truly Implemented

The following were marked ⚠️ in `originalFunctions.md` but are confirmed fully working after code verification:

- **Lynx-like motion** — `FilePanelView.cs:595-605`, `McSettings.cs:187-191` ✅
- **Panelize (find results → panel)** — `McApplication.cs:1238-1416` ✅
- **Ctrl+O subshell** — `McApplication.cs:405-407, 1454-1465` ✅
- **User menu condition lines** — `EvaluateUserMenuCondition()` handles `f`/`d`/`!` ✅
- **External panelize** — shell command output injected into panel ✅
