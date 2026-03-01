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
| 1 — Critical | 5 | 0 | 5 |
| 2 — High | 12 | 0 | 12 |
| 3 — Medium | 18 | 0 | 18 |
| 4 — Low | 11 | 0 | 11 |
| **Total** | **46** | **0** | **46** |

---

## Tier 1 — Critical (breaks visible everyday workflows)

| # | Status | Area | What is missing | Evidence |
|---|--------|------|-----------------|----------|
| 1 | `[ ]` | **Copy/Move: source mask pattern rename not applied** | `CopyMoveOptions.SourceMask` is captured in `CopyMoveDialog` (`destInput.Text` line 110) but `McApplication.CopyFiles()` never passes it to `CopyMarkedAsync`, and `FileOperations.CopyAsync()` has no mask substitution logic. All files are copied with their original names regardless of what the user types in the "From:" field. | `CopyMoveDialog.cs:110`, `McApplication.cs:804-806`, `FileOperations.cs:31-92` |
| 2 | `[ ]` | **Copy/Move: "Dive into subdirectory" option never applied** | Checkbox is shown and captured (`DiveIntoSubdir = diveCb.CheckedState`) but never forwarded — `CopyMarkedAsync()` accepts no such parameter, and `FileOperations.CopyAsync()` always copies files directly into the destination root. | `CopyMoveDialog.cs:90-93`, `McApplication.cs:806`, `FileOperations.cs:61-88` |
| 3 | `[ ]` | **Copy: "Follow symlinks" option never applied** | Checkbox captured in `CopyMoveOptions.FollowSymlinks` but not passed to `CopyMarkedAsync()` or `FileOperations.CopyAsync()`. Symlinks are always copied as symlinks, never dereferenced. | `CopyMoveDialog.cs:85-89`, `McApplication.cs:806`, `FileOperations.cs` |
| 4 | `[ ]` | **Command line: Tab / Alt+Tab filename completion** | `CommandLineView.OnInputKeyDown()` handles Enter, history navigation, Ctrl+H/Alt+H for popup, Esc — but Tab is never intercepted. No completion logic anywhere in the command line. | `CommandLineView.cs:53-91` |
| 5 | `[ ]` | **Viewer: Ctrl+F / Ctrl+B — open next / previous file in directory** | `ViewerView.OnKeyDown()` does not handle `Ctrl+F` or `Ctrl+B` for file cycling. Ctrl+F is intercepted for find (F7 alias), Ctrl+B for the single bookmark. The viewer has no reference back to the panel's file list. | `ViewerView.cs:188-274` |

---

## Tier 2 — High Impact (noticeable in regular use)

| # | Status | Area | What is missing | Evidence |
|---|--------|------|-----------------|----------|
| 6 | `[ ]` | **Command line: Emacs-style editing keys** | `Ctrl+A` (start), `Ctrl+E` (end), `Ctrl+K` (kill to end), `Ctrl+W` (delete word), `Ctrl+Y` (yank), `Alt+B` (word left), `Alt+F` (word right) are not handled in `CommandLineView`. The underlying `TextField` may provide some, but they are not explicitly bound. | `CommandLineView.cs:53-91` |
| 7 | `[ ]` | **Find file: date/time filter** | `FindOptions` record has no date fields. `FindDialog` has no date/time input widgets. Original MC allows filtering by modification date (newer/older than N days). | `FindDialog.cs:5-17` (FindOptions), `FindDialog.cs:25-109` |
| 8 | `[ ]` | **Find file: file size filter** | `FindOptions` has no `MinSize`/`MaxSize` fields. Original MC allows filtering by file size (larger/smaller than N bytes/KB/MB). | `FindDialog.cs:5-17` |
| 9 | `[ ]` | **Viewer: F5 prompts for line number in text mode (not byte offset)** | `ViewerView.cs` always shows "Enter byte offset" regardless of display mode. In text mode original mcview's F5 prompts for a line number. | `ViewerView.cs:314-338` |
| 10 | `[ ]` | **Editor: Ctrl+O — open file dialog** | No `Ctrl+O` handler in `EditorView.OnKeyDown()`. Original mcedit opens a file-selection dialog to load a new file into the editor. | `EditorView.cs` (no Ctrl+O case) |
| 11 | `[ ]` | **Editor: Shift+F4 — Repeat last find-and-replace** | No `Shift+F4` binding in `EditorView.OnKeyDown()`. Original mcedit repeats the previous replace operation without showing the dialog. | `EditorView.cs` |
| 12 | `[ ]` | **Viewer: "/" — start forward search** | `ViewerView.OnKeyDown()` binds F7 for search but does not bind the `/` key as a shortcut (original mcview convention). | `ViewerView.cs:209` |
| 13 | `[ ]` | **Viewer: F9 — toggle nroff/formatted display** | No F9 handler in `ViewerView`. Original mcview's F9 toggles processing of nroff backspace sequences (bold=`char\bchar`, underline=`_\bchar`). | `ViewerView.cs` (no F9 case) |
| 14 | `[ ]` | **Hints bar below panels** | No hints-bar view, no hints text, no `ShowHintsBar` setting. Original MC shows a rotating tips strip between the panels and the command line. | Entire codebase — no hints code |
| 15 | `[ ]` | **Screen list (multiple open editors/viewers)** | `Command > Screen list` shows "Not implemented". Original MC maintains a list of open editor/viewer subshell screens accessible via a popup. | `McApplication.cs` — screen list stub |
| 16 | `[ ]` | **VFS: FTP / SFTP providers not registered** | `Mc.Vfs.Ftp` and `Mc.Vfs.Sftp` projects exist but are not registered in the VFS registry at startup. FTP/SFTP menu items silently fail. | `Mc.App` DI setup |
| 17 | `[ ]` | **Copy: "Stable symlinks" option never applied** | Checkbox shown and captured (`StableSymlinks`) but `McApplication.CopyFiles()` does not pass it to `CopyMarkedAsync()`, and `FileOperations.CopyAsync()` has no stable-symlinks logic (copy target of symlink instead of symlink itself). | `CopyMoveDialog.cs:95-99`, `McApplication.cs:806` |

---

## Tier 3 — Medium Impact (power users notice)

| # | Status | Area | What is missing | Evidence |
|---|--------|------|-----------------|----------|
| 18 | `[ ]` | **Viewer: Alt+R — toggle column ruler** | No `Alt+R` handler in `ViewerView`. Original mcview shows a horizontal ruler indicating column positions. | `ViewerView.cs` |
| 19 | `[ ]` | **Viewer: Alt+E — change encoding** | No `Alt+E` handler in `ViewerView`. Original mcview opens an encoding selection dialog and re-renders the file in the chosen encoding. | `ViewerView.cs` |
| 20 | `[ ]` | **Viewer: numeric bookmarks 0–9 (`[n]m` set, `[n]r` goto)** | `ViewerView` supports only one bookmark (hardcoded index 0 at `SetBookmark(0)` / `GoToBookmark(0)`). Original mcview allows 10 bookmarks accessible via digit prefix. | `ViewerView.cs:241-269` |
| 21 | `[ ]` | **Viewer: F1 inside viewer opens viewer-specific help** | Pressing F1 inside `ViewerView` routes to the main help dialog rather than a viewer-specific help section. | `ViewerView.cs` — F1 handler |
| 22 | `[ ]` | **Editor: Ctrl+R conflict — macro recording** | `Ctrl+R` is bound to Redo in `EditorView.cs:319`. Original mcedit uses `Ctrl+R` for macro recording (start/stop). Neither macro recording nor the conflict resolution is implemented. | `EditorView.cs:319` |
| 23 | `[ ]` | **Editor: word completion (Ctrl+Tab)** | No `Ctrl+Tab` handler, no completion popup, no word list logic in `EditorView`. | `EditorView.cs` |
| 24 | `[ ]` | **Editor: column/rectangular block selection** | `EditorView` implements linear (stream) selection via Shift+Arrow keys. Original mcedit supports column (rectangular) block selection (Alt+B or column mode). | `EditorView.cs:186-202` |
| 25 | `[ ]` | **Editor: spell checking** | No spell-check integration. Original mcedit can invoke aspell/enchant for spell checking. | `EditorView.cs` |
| 26 | `[ ]` | **Command line: Ctrl+Q — quote next character** | `Ctrl+Q` (quote-next, inserts the next keypress literally as a control character) is not handled in `CommandLineView`. | `CommandLineView.cs` |
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
