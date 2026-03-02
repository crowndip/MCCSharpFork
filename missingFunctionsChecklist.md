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
| 2 — High | 12 | 12 | 0 |
| 3 — Medium | 18 | 17 | 1 |
| 4 — Low | 11 | 9 | 2 |
| **Total** | **46** | **43** | **3** |

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
| 7 | `[x]` | **Find file: date/time filter** | Fixed: `FindOptions` now has `NewerThanDays`/`OlderThanDays` fields. `FindDialog` has two new text fields. `McApplication.ShowFindResults()` applies date filtering in the background search task. | `FindDialog.cs`, `McApplication.cs` |
| 8 | `[x]` | **Find file: file size filter** | Fixed: `FindOptions` now has `MinSizeKB`/`MaxSizeKB` fields. `FindDialog` has two new text fields. `McApplication.ShowFindResults()` filters files by size. | `FindDialog.cs`, `McApplication.cs` |
| 9 | `[x]` | **Viewer: F5 prompts for line number in text mode (not byte offset)** | Fixed: `ShowGotoPosition()` checks `_viewer.Mode`. In text mode shows "Line number:" prompt and sets `_viewer.ScrollLine`. In hex mode shows "Byte offset:" prompt. | `ViewerView.cs` |
| 10 | `[x]` | **Editor: Ctrl+O — open file dialog** | Fixed: `Ctrl+O` handler added. Prompts user (with unsaved-changes dialog if needed), then calls `_editor.LoadFile()` which resets the buffer and undo stack. `TextBuffer.SetContent()` and `EditorController.LoadFile()` added. | `EditorView.cs`, `EditorController.cs`, `TextBuffer.cs` |
| 11 | `[x]` | **Editor: Shift+F4 — Repeat last find-and-replace** | Fixed: `Shift+F4` binding added, calls `RepeatLastReplace()` which re-runs the last replace operation without showing the dialog. Falls back to `ShowFindReplace()` if no prior search. | `EditorView.cs` |
| 12 | `[x]` | **Viewer: "/" — start forward search** | Fixed: `keyEvent.AsRune.Value == '/'` check before the switch statement calls `ShowSearch(backward: false)`. | `ViewerView.cs` |
| 13 | `[x]` | **Viewer: F9 — toggle nroff/formatted display** | Fixed: F9 toggles `_nroffMode`. `StripNroff()` removes `char\bchar` (bold) and `_\bchar` (underline) escape sequences. Mode label shows "NROFF". | `ViewerView.cs` |
| 14 | `[x]` | **Hints bar below panels** | Fixed: `HintsBarView` created with 20 rotating tips. Added to layout between panels and command line. `ShowHints` setting in `McSettings`. Tips advance on panel cursor movement. | `HintsBarView.cs`, `McApplication.cs`, `McSettings.cs` |
| 15 | `[x]` | **Screen list (multiple open editors/viewers)** | Fixed: `ShowScreenList()` shows a numbered list of MC + all files opened in editor/viewer this session. Selecting a screen re-opens the file. Wired to `Command > Screen list` menu. | `McApplication.cs` — `ShowScreenList()` |
| 16 | `[x]` | **VFS: FTP / SFTP providers not registered** | Already done: `FtpVfsProvider` and `SftpVfsProvider` are registered in `AppSetup.cs`. `ConnectVfsLink("ftp")` / `ConnectVfsLink("sftp")` menu items work. | `AppSetup.cs` |
| 17 | `[x]` | **Copy: "Stable symlinks" option never applied** | Fixed: `stableSymlinks` parameter added throughout the copy chain. When true, `MakeRelativeSymlinkTarget()` converts symlink targets to relative paths before creating at destination. | `FileOperations.cs`, `FileManagerController.cs`, `McApplication.cs` |

---

## Tier 3 — Medium Impact (power users notice)

| # | Status | Area | What is missing | Evidence |
|---|--------|------|-----------------|----------|
| 18 | `[x]` | **Viewer: Alt+R — toggle column ruler** | Fixed: `Alt+R` toggles `_showRuler`. `DrawRuler()` renders a column marker strip (`---+----0---+...`) between content and status bar. Reserved rows adjusted dynamically. | `ViewerView.cs` |
| 19 | `[x]` | **Viewer: Alt+E — change encoding** | Fixed: `Alt+E` opens `ShowEncodingDialog()` — filterable list of all .NET encodings. On select, sets `_viewer.Encoding` and reloads the file. | `ViewerView.cs` |
| 20 | `[x]` | **Viewer: numeric bookmarks 0–9 (`[n]m` set, `[n]r` goto)** | Fixed: digit keys (0-9) set `_digitPrefix=true`. Next 'm' saves `_viewer.ScrollLine` to `_bookmarks[n]`; next 'r' restores it. `Ctrl+B` with no prefix saves bookmark 0. | `ViewerView.cs` |
| 21 | `[x]` | **Viewer: F1 inside viewer opens viewer-specific help** | Fixed: F1 handler calls `ShowViewerHelp()` which shows a dialog listing all viewer key bindings. | `ViewerView.cs` |
| 22 | `[x]` | **Editor: Ctrl+R conflict — macro recording** | Fixed: `Ctrl+R` now toggles macro recording (start/stop). Redo moved to `Ctrl+Shift+Z`. `_macroKeys` list stores keystrokes. `Ctrl+E` plays back. `MessageBox.Query()` shows status. | `EditorView.cs` — `ToggleMacroRecord()`, `PlayMacro()` |
| 23 | `[x]` | **Editor: word completion (Ctrl+Tab)** | Fixed: `Ctrl+Tab` calls `WordComplete()`. Scans buffer for words starting with the prefix before cursor. Single match completes immediately; multiple matches show a popup ListView. | `EditorView.cs` — `WordComplete()`, `ShowWordCompletePopup()` |
| 24 | `[x]` | **Editor: column/rectangular block selection** | Fixed: `_colBlock` mode toggle via Alt+B. When active, `IsInSelection()` highlights a rectangular region instead of a linear stream. F5/F6 copy/move the column block via `EditorController.CopyColumnBlock()` / `DeleteColumnBlock()`. Ctrl+V pastes column-block back via `PasteColumnBlock()`. Status bar shows "COL" indicator. | `EditorView.cs`, `EditorController.cs` |
| 25 | `[x]` | **Editor: spell checking** | Fixed: `Ctrl+F5` calls `ShowSpellCheck()`. Expands word under cursor, invokes `aspell -a` subprocess in pipe mode, parses suggestions, shows dialog with Skip / Add to dictionary / replacement choices. | `EditorView.cs` — `ShowSpellCheck()` |
| 26 | `[x]` | **Command line: Ctrl+Q — quote next character** | Fixed: `Ctrl+Q` sets `_quoteNext=true`; the next keypress is inserted literally via `InsertAtCursor()`. | `CommandLineView.cs` |
| 27 | `[x]` | **Panel: device/socket/FIFO colour coding** | Fixed: `VfsDirEntry` and `FileEntry` now have `IsBlockDevice`, `IsCharDevice`, `IsFifo`, `IsSocket`. `LocalVfsProvider` uses P/Invoke `lstat()` to detect special file types. `GetEntryAttr()` maps them to `PanelDevice` (yellow) and `PanelSpecialFile` (magenta). `McTheme` has new attributes for both. | `VfsDirEntry.cs`, `FileEntry.cs`, `LocalVfsProvider.cs`, `FilePanelView.cs`, `McTheme.cs` |
| 28 | `[x]` | **Panel listing: User-defined column format mode** | Fixed: `PanelListingMode.User` added. `FilePanelView.UserFormatString` holds a comma-separated `field[:width]` spec (e.g. `name:30,size:8,mtime:12`). `FormatUserEntry()` and `DrawUserColumnHeader()` render it. `ShowListingFormatDialog()` has a 4th radio + format-string TextField (enabled when "User defined" selected). | `FilePanelView.cs`, `McApplication.cs` |
| 29 | `[x]` | **Help: hypertext link navigation** | Fixed: Topic bodies embed `{topicid}` cross-references; `RunViewer()` strips them to `[Title]` in displayed text, extracts them via `ExtractLinks()`, and renders them as Tab-navigable `→Title` buttons below the body. Clicking/Enter navigates to that topic with full Back-stack support. | `HelpDialog.cs` — `ExtractLinks()`, `RunViewer()` |
| 30 | `[x]` | **User menu: missing `%p`, `%P`, `%n`, `%m`, `%a` macro substitutions** | Fixed: Added `%p` (active panel current file name), `%P` (inactive panel current file name), `%n` (name without extension), `%m` (marked file names only, not full paths), `%a` (archive name from VFS path). Wired through the macro expansion block in `ShowUserMenu()`. | `McApplication.cs` — macro expansion |
| 31 | `[ ]` | **VFS: FISH protocol (files over SSH shell)** | `Left > Shell link…` shows "Not implemented". Original MC implements the FISH protocol for remote file access over an SSH connection using shell commands. | Menu stub |
| 32 | `[x]` | **VFS: CPIO / ZIP archives as navigable VFS** | Fixed: ZIP was already implemented; CPIO newc (SVR4) format now supported by `CpioVfsProvider`. Handles `.cpio` files and RPM payload extraction (scans for gzip magic). `CpioReader` parses 8-char hex header fields, names, and data with 4-byte alignment. Registered in `AppSetup.cs`. | `Mc.Vfs.Archives/CpioVfsProvider.cs`, `AppSetup.cs` |
| 33 | `[x]` | **VFS: External filesystem scripts (extfs)** | Fixed: `ExtfsVfsProvider` scans `/usr/lib/mc/extfs.d/` at startup, maps file extensions to script paths. `ListDirectory()` invokes `list`, `OpenRead()` invokes `copyout` to a temp file. Parses `ls -l` style output. Registered in `AppSetup.cs`. | `Mc.Vfs.Archives/ExtfsVfsProvider.cs`, `AppSetup.cs` |
| 34 | `[x]` | **VFS: SFS (single-file filesystem)** | Fixed: `SfsVfsProvider` reads `mc.sfs` config (from `/usr/lib/mc/mc.sfs` etc.), maps extensions to mount/umount commands. `EnsureMounted()` runs the mount helper into a temp directory; `ListDirectory()`/`OpenRead()` delegate to the mounted temp dir. `Dispose()` unmounts all. Registered in `AppSetup.cs`. | `Mc.Vfs.Archives/SfsVfsProvider.cs`, `AppSetup.cs` |
| 35 | `[x]` | **Copy: preserve ext2 file attributes** | Fixed: "Preserve ext2 attributes" checkbox added to `CopyMoveDialog` (disabled for Move, Linux-only). `CopyMoveOptions.PreserveExt2Attributes` wired through `FileManagerController.CopyMarkedAsync()` → `FileOperations.CopyAsync()` → `CopySingleFileAsync()`. `TryCopyExt2Attributes()` uses `lsattr`/`chattr` subprocess on Linux. | `CopyMoveDialog.cs`, `FileOperations.cs`, `FileManagerController.cs` |

---

## Tier 4 — Low Impact / Cosmetic / Edge Cases

| # | Status | Area | What is missing | Evidence |
|---|--------|------|-----------------|----------|
| 36 | `[x]` | **Panel: executable files — optional `*` suffix (like `ls -F`)** | Fixed: `FilePanelView.ShowExecutableSuffix` property added. `FormatEntry()` and `FormatBriefCell()` append `*` to executable names when enabled. `McSettings.ShowExecutableSuffix` persists to config. Added checkbox in Panel Options dialog. | `FilePanelView.cs`, `McSettings.cs`, `McApplication.cs` |
| 37 | `[x]` | **Panel: symlink-to-directory shown in directory colour** | Fixed: `VfsDirEntry.IsSymlinkToDirectory` added. `LocalVfsProvider` checks if symlink target is a directory via `Directory.Exists()`. `FilePanelView.FollowSymlinks` property controls the color. `GetEntryAttr()` returns `PanelDirectory` for symlinks-to-dirs when `FollowSymlinks` is on. Added checkbox in Panel Options. | `VfsDirEntry.cs`, `LocalVfsProvider.cs`, `FilePanelView.cs`, `McSettings.cs` |
| 38 | `[x]` | **Panel: quick search cursor positioned after typed chars** | Fixed: `FilePanelView.PositionCursor()` overridden to return the column after the last typed character in the quick search status line when `_quickSearchActive` is true. Hardware cursor now tracks the search term. | `FilePanelView.cs` — `PositionCursor()` |
| 39 | `[x]` | **Panel: sort column name drawn in distinct colour (not just ↑/↓ suffix)** | Fixed: `McTheme.PanelHeaderSorted` added (bright white on blue). `DrawColumnHeader()` rewritten to draw each column segment individually, applying `PanelHeaderSorted` to the active sort column and `PanelHeader` to the others. | `FilePanelView.cs` — `DrawColumnHeader()`, `McTheme.cs` |
| 40 | `[x]` | **Subshell: typed command carried into shell (type then Ctrl+O)** | Fixed: `LaunchShell()` reads `_commandLine.Text` before suspending MC. For bash, writes a temp init file that sources `.bashrc` and sets `READLINE_LINE`/`READLINE_POINT`, then starts bash with `--init-file`. For zsh, uses `print -z` via ZDOTDIR. Other shells get `MC_PENDING_CMD` env var. | `McApplication.cs` — `LaunchShell()` |
| 41 | `[ ]` | **Subshell: shell prompt shown in command line (live)** | Original MC with subshell support shows the actual shell prompt (e.g. `user@host:~/dir$`) in the command line. Current implementation shows a simplified truncated-path prompt constructed by MC, not the real shell prompt. | `CommandLineView.cs` — `SetDirectory()` |
| 42 | `[ ]` | **Subshell: multiple screens** | `Command > Screen list` is not implemented. Original MC supports multiple pseudo-terminal subshell sessions, each accessible from the screen manager. | `McApplication.cs` — screen list stub |
| 43 | `[x]` | **Viewer: per-file display settings not remembered** | Fixed: `_perFileSettings` dictionary added to `ViewerView` keyed by file path. Settings (mode, wrapLines, nroffMode) are saved via `SaveCurrentFileSettings()` on every toggle (F2/F4/F8/F9) and on `NavigateFile()`. Restored when navigating back to a previously-viewed file. | `ViewerView.cs` — `_perFileSettings`, `SaveCurrentFileSettings()`, `NavigateFile()` |
| 44 | `[x]` | **Learn Keys dialog: interactive key tester** | Fixed: Dialog now has two sections — the static binding table (top) and an interactive key tester (bottom). `d.KeyDown` handler decodes the pressed key (Ctrl/Alt/Shift modifiers + key name or rune) and looks it up in the binding table. Shows matching action next to the decoded key. | `McApplication.cs` — `ShowLearnKeysDialog()` |
| 45 | `[x]` | **Configuration: "Rotating dash" busy indicator** | Fixed: `DirectoryListing.Reloading` event added, fires at start of each `Reload()`. `FilePanelView` subscribes and sets `_isLoading = true`, advances `_spinnerIndex`. `DrawBorderAndPath()` prepends `SpinnerChars[_spinnerIndex]` to the path in the title. `OnListingChanged` clears `_isLoading`. | `DirectoryListing.cs`, `FilePanelView.cs` |
| 46 | `[x]` | **Configuration: "Cd follows links" option** | Fixed: `McSettings.CdFollowsLinks` property added (persisted to config). `OnPanelEntryActivated()` resolves symlink targets to real paths via `Path.GetFullPath()` when option is on and entry is a symlink-to-dir. | `McSettings.cs`, `McApplication.cs` |

---

## Notes on Items Verified as Truly Implemented

The following were marked ⚠️ in `originalFunctions.md` but are confirmed fully working after code verification:

- **Lynx-like motion** — `FilePanelView.cs:595-605`, `McSettings.cs:187-191` ✅
- **Panelize (find results → panel)** — `McApplication.cs:1238-1416` ✅
- **Ctrl+O subshell** — `McApplication.cs:405-407, 1454-1465` ✅
- **User menu condition lines** — `EvaluateUserMenuCondition()` handles `f`/`d`/`!` ✅
- **External panelize** — shell command output injected into panel ✅
