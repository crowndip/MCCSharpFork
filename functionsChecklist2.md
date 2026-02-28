# Functions Checklist 2 — Fresh Gap Analysis vs GNU Midnight Commander

**Date:** 2026-02-28
**Status flags:** `[ ]` Not started · `[~]` Partial / setting stored but not applied · `[x]` Fixed
**Scope:** New issues found via fresh code review. Does NOT duplicate items already in `functionChecklist.md`.
**Only difference allowed from original MC:** The custom _Tools_ menu is intentional and should stay.

Reference: https://github.com/MidnightCommander/mc

---

## Tier 1 — Critical (breaks common daily workflows)

| # | Status | Area | What is missing / wrong | MC source reference |
|---|--------|------|--------------------------|---------------------|
| 1 | `[x]` | **Internal editor — Find & Replace missing** | ~~`ShowFind()` in EditorView only implements forward search (F7). Original mcedit has full Find+Replace via F4 (or Ctrl+H).~~ Fixed: F4 opens a Find+Replace dialog with Find next / Replace all / Cancel buttons, regex and case-sensitive options. Calls `EditorController.ReplaceAll()`. | `src/editor/editsearch.c` — `edit_replace_cmd()` |
| 2 | `[x]` | **Internal editor — Redo missing** | ~~EditorView binds Ctrl+Z → `Undo()` but there is no Redo binding. EditorController has `UndoStack` but no `RedoStack`.~~ Fixed: `EditorController` already had `Redo()` and `_redoStack`. Added `Ctrl+R` binding in EditorView. | `src/editor/edit.c` — `edit_redo()` |
| 3 | `[x]` | **Internal editor — block selection (mark) not implemented** | ~~No Shift+Arrow or F3-start-of-block key for selecting text regions.~~ Fixed: Shift+Arrow extends selection; F3 toggles block-mark mode; F5 copies block; F6 moves block. Selection is highlighted in reverse video. `EditorController.GetSelectionOffsets()` added. | `src/editor/editwidget.c` — `edit_mark_cmd()` |
| 4 | `[x]` | **Internal editor — missing F-key bindings** | ~~EditorView only handled: F2=Save, F7=Find, F10/Esc=Close.~~ Fixed: F3=Block mark, F4=Find+Replace, F5=Copy block, F6=Move block, F8=Delete line, F9=Toggle line numbers. | `src/editor/editkeys.c` |
| 5 | `[x]` | **Panel — quick search case-sensitivity setting not applied** | ~~`QuickSearchCaseSensitive` was stored but `SearchInPanel()` always used `OrdinalIgnoreCase`.~~ Fixed: `FilePanelView.QuickSearchCaseSensitive` property; `SearchInPanel()` now switches between `Ordinal` and `OrdinalIgnoreCase`. Set from McSettings in `ApplyPanelSettings()`. | `src/filemanager/panel.c` — `panel_quick_search()` case flag |
| 6 | `[x]` | **Panel — Lynx-like motion not applied** | ~~`LynxLikeMotion` setting was ignored.~~ Fixed: `FilePanelView.LynxLikeMotion` property; `OnKeyDown()` now handles `CursorLeft` (go to parent) and `CursorRight on dir` (enter it) when the option is on. Set from McSettings. | `src/filemanager/panel.c` — `panel_move_left/right()` |
| 7 | `[x]` | **Panel — Mark-moves-cursor setting not applied** | ~~`MarkMovesCursor` was stored but `ToggleMark()` always advanced the cursor.~~ Fixed: `FilePanelView.MarkMovesCursor` property; `ToggleMark()` only advances cursor when the setting is true. | `src/filemanager/panel.c` — `mark_file()` |
| 8 | `[x]` | **Ctrl+Enter — copy filename to command line** | ~~Not handled.~~ Fixed: `McApplication.OnKeyDown()` handles `Ctrl+Enter` → `PasteFilenameToCommandLine()`. `CommandLineView.AppendText()` method added. | `src/filemanager/midnight.c` — `mc_ctrl_enter_cmd()` |
| 9 | `[x]` | **Panel — directory navigation history (Alt+Y / Alt+U)** | ~~No per-panel navigation history stack.~~ Fixed: `DirectoryListing` now maintains `_history` list and `_historyIndex`. `GoBack()`, `GoForward()`, `CanGoBack`, `CanGoForward` added. `McApplication` handles `Alt+Y` / `Alt+U`. `Load()` pushes to history. | `src/filemanager/panel.c` — `panel_do_cd_hist()` |
| 10 | `[x]` | **Layout — horizontal (top/bottom) split not implemented** | ~~`HorizontalSplit` setting was stored but `ApplyLayoutSettings()` only adjusted `Dim.Percent` for vertical split.~~ Fixed: `ApplyLayoutSettings()` now has two branches — when `_settings.HorizontalSplit` is true it repositions all four panel views (left/right panel + overlays) in top/bottom configuration. | `src/filemanager/layout.c` — `setup_panels_and_shells()` |

---

## Tier 2 — High impact (noticeable gaps for daily users)

| # | Status | Area | What is missing / wrong | MC source reference |
|---|--------|------|--------------------------|---------------------|
| 11 | `[x]` | **Panel — toggle hidden files via keyboard** | ~~No keyboard shortcut to toggle dotfiles.~~ Fixed: `Alt+.` (detected via `keyEvent.AsRune.Value == '.'` in default clause since `KeyCode.Period` doesn't exist in TG2) calls `ToggleHiddenFiles()` which flips `ShowHiddenFiles` and reloads both panels. | `src/filemanager/panel.c` — `panel_toggle_hidden()` |
| 12 | `[x]` | **Alt+I — sync inactive panel to active panel path** | ~~Not implemented.~~ Fixed: `Alt+I` → `SyncInactivePanelToActive()` loads the active panel's current path into the inactive panel. | `src/filemanager/midnight.c` — `sync_tree()` |
| 13 | `[x]` | **Alt+O — open other panel at current file's directory** | ~~Not implemented.~~ Fixed: `Alt+O` → `OpenOtherPanelAtCurrentDir()` loads the directory containing the current file into the inactive panel. | `src/filemanager/midnight.c` — `open_other_panel_cmd()` |
| 14 | `[x]` | **Internal editor — Go to line (Ctrl+G / F5)** | ~~No go-to-line command.~~ Fixed: `Ctrl+G` → `ShowGotoLine()` prompts for line number and calls `EditorController.GotoLine()`. | `src/editor/editsearch.c` — `edit_goto_cmd()` |
| 15 | `[x]` | **Internal viewer — search result highlighting** | ~~Found text not highlighted.~~ Fixed: `ViewerController` stores `LastMatchOffset` / `LastMatchLength`; `ViewerView.DrawText()` calls `DrawLineWithHighlight()` which paints the match in reverse-cyan. | `src/viewer/search.c` — `mcview_display_search()` |
| 16 | `[x]` | **Internal viewer — go to byte offset (F5)** | ~~F5 not bound.~~ Fixed: F5 → `ShowGotoOffset()` which prompts for decimal or `0x` hex offset and calls `ViewerController.GotoOffset()`. | `src/viewer/actions_cmd.c` — `mcview_cmd_goto()` |
| 17 | `[x]` | **Internal viewer — backward search (N key / Shift+N)** | ~~Only forward search.~~ Fixed: `Shift+N` → `ViewerController.FindPrev()` which scans backwards from the current match. `N` still does forward. | `src/viewer/search.c` — `mcview_cmd_search_prev()` |
| 18 | `[x]` | **User menu — %s / %t macros not implemented** | ~~Only `%f`, `%b`, `%e`, `%d`, `%D` were substituted.~~ Fixed: Macro expansion now also handles `%s` and `%t` (space-separated list of tagged files, or current file if none marked), and `%{Prompt}` (interactive user-prompt via `InputDialog`). | `src/usermenu.c` — `expand_format()` |
| 19 | `[x]` | **User menu — no "Press Enter to continue" after command** | ~~Control returned immediately.~~ Fixed: `ExecuteUserMenuCommand()` now prints `"\nPress Enter to continue..."` and calls `Console.ReadLine()` before re-initialising the TUI driver. | `src/usermenu.c` — `execute_menu_command()` press-enter prompt |
| 20 | `[x]` | **Archive VFS — entering .zip/.tar.gz not supported** | ~~`Mc.Vfs.Archives` providers were not consulted on Enter.~~ Fixed: `OnPanelEntryActivated()` now calls `TryGetArchiveVfsPath()` which maps `.zip`, `.tar`, `.tgz`, `.tar.gz`, `.tar.bz2`, `.tar.xz` etc. to the appropriate VFS scheme before falling through to the viewer. Providers were already registered in AppSetup. | `src/vfs/plugins/` — archive plugin activation |
| 21 | `[x]` | **Sort dialog — missing sort fields** | ~~`SortEntries()` only handled Name, Extension, Size, ModificationTime; all others fell through to name sort.~~ Fixed: `DirectoryListing.SortEntries()` now has explicit cases for AccessTime, CreationTime, Permissions, Owner, Group, Inode, and Unsorted (noop). The dialog already showed all fields via `Enum.GetValues<SortField>()`. | `src/filemanager/panel.c` — `sort_orders[]` |
| 22 | `[x]` | **Internal editor — overwrite mode does not overwrite** | ~~Insert key toggled the OVR indicator but `InsertChar()` always inserted.~~ Fixed: EditorView calls `_editor.ReplaceChar()` instead of `InsertChar()` when in overwrite mode. `ReplaceChar()` added to `EditorController`: it deletes the character under the cursor then inserts the new one; falls back to insert at end-of-line. | `src/editor/edit.c` — `edit_insert_overwrite()` |

---

## Tier 3 — Medium impact (feature completeness gaps)

| # | Status | Area | What is missing / wrong | MC source reference |
|---|--------|------|--------------------------|---------------------|
| 23 | `[x]` | **Internal editor — no line number display** | ~~No line-number column.~~ Fixed: `_showLineNumbers` flag toggled by F9; `GutterWidth` computes gutter width; line numbers are drawn in gray in `OnDrawingContent`. | `src/editor/editdraw.c` — `edit_draw_this_line()` |
| 24 | `[x]` | **Internal editor — no auto-indent** | ~~`InsertChar('\n')` inserted a bare newline.~~ Fixed: Enter key in EditorView calls `_editor.InsertNewlineWithIndent()` which measures the leading whitespace of the current line and reproduces it on the new line. | `src/editor/edit.c` — `edit_auto_indent()` |
| 25 | `[ ]` | **Internal editor — clipboard uses internal buffer only** | `_clipboardText` is a private field in EditorView; it cannot exchange data with the system clipboard. Deferred — requires OS-specific clipboard integration (xclip/wl-copy). | `lib/clipboard.c` |
| 26 | `[ ]` | **Internal editor — no word-wrap / paragraph formatting** | No F8 (wrap paragraph), no configurable line-length limit. Deferred. | `src/editor/editcmd.c` — `edit_wrap_paragraph()` |
| 27 | `[ ]` | **Internal editor — bookmarks not implemented** | No Ctrl+K / Ctrl+I bookmark commands. Deferred. | `src/editor/editbookmark.c` |
| 28 | `[ ]` | **Internal editor — no column (rectangular) block mode** | No Shift+F3 column-block selection mode. Deferred. | `src/editor/edit.c` — column block mode |
| 29 | `[x]` | **Internal viewer — regex search not available** | ~~`ShowSearch()` always created a Normal search.~~ Fixed: the search dialog now has a "Regular expression" checkbox; when checked the search type is set to `SearchType.Regex`. | `src/viewer/search.c` — regex option |
| 30 | `[ ]` | **Panel — `*` / `+` / `-` numpad keys for group select/unselect** | Numpad key variants not mapped separately. Deferred — terminal emulators often deliver numpad keys as regular characters. | `src/filemanager/panel.c` — numpad keybindings |
| 31 | `[x]` | **Confirmation dialog — missing "Confirm move" option** | ~~Dialog had only Delete, Overwrite, Exit.~~ Fixed: `ShowConfirmationDialog()` now shows five checkboxes: Delete, Overwrite, Move, Execute, Exit. `McSettings.ConfirmMove` and `McSettings.ConfirmExecute` properties added. | `src/setup.c` — `confirm_box()` |
| 32 | `[ ]` | **Panel — "User defined" listing format not available** | Only Full / Brief / Long offered. A user-defined column format string (e.g. `name size perm`) would require significant format-parser work. Deferred. | `src/filemanager/panel.c` — `list_user` type |
| 33 | `[x]` | **Find file — "Ignore dirs containing" filter** | ~~No ignore-dirs field.~~ Fixed: `FindOptions.IgnoreDirs` (colon-separated list); Find dialog now shows an "Ignore dirs" text field; `ShowFindResults()` builds a `HashSet<string>` of ignored names and skips matching subdirectories during enumeration. | `src/filemanager/find.c` — `find_ignore_dirs` |
| 34 | `[x]` | **Internal viewer — bookmarks** | ~~No bookmark support.~~ Fixed: `ViewerController` has `_bookmarks` dictionary, `SetBookmark(slot)`, `GotoBookmark(slot, …)`. ViewerView binds `Ctrl+B` = set bookmark 0, `Ctrl+P` = goto bookmark 0. | `src/viewer/actions_cmd.c` — bookmarks |
| 35 | `[x]` | **Panel — double-click to enter directory / open file** | ~~Mouse single-click moved cursor but no activation.~~ Fixed: `OnMouseClick()` now detects `MouseFlags.Button1DoubleClicked` and fires `EntryActivated` for the hit-tested entry. | `src/filemanager/panel.c` — mouse handling |
| 36 | `[ ]` | **Command line — Tab completion for filenames** | No Tab-completion in the command line. Requires readline-style completion integration. Deferred. | `src/filemanager/command.c` — `complete_engine()` |
| 37 | `[x]` | **Panel — "Copy current directory to command line" (Ctrl+Shift+Enter)** | ~~Not implemented.~~ Fixed: `Ctrl+Shift+Enter` → `PastePathToCommandLine()` appends the full path of the selected file (or current directory) to the command line. Also accessible via `Ctrl+X P`. | `src/filemanager/midnight.c` — `paste_panel_path_cmd()` |
| 38 | `[ ]` | **Help — no clickable hyperlinks within topics** | Help topics show plain text; in-text references not navigable. Deferred — requires MC-format help-file parser. | `src/help.c` — `interactive_display()` |

---

## Tier 4 — Low impact (edge cases / completeness)

| # | Status | Area | What is missing / wrong | MC source reference |
|---|--------|------|--------------------------|---------------------|
| 39 | `[x]` | **User menu — `t` condition checks no tag types** | ~~`EvaluateSingleCondition()` for type `'t'` always returned `true`.~~ Fixed: now returns `_controller.ActivePanel.MarkedCount > 0` — true only when at least one file is tagged. | `src/usermenu.c` — `check_conditions()` type 't' |
| 40 | `[ ]` | **User menu — per-marked-file iteration with `%f`** | When multiple files are marked, `%f` only expands to the file under the cursor. Full per-file iteration (run command once per marked file) is deferred. `%s`/`%t` (bulk list) are now available as the practical substitute. | `src/usermenu.c` — `do_execute_menu_command()` loop |
| 41 | `[ ]` | **Internal editor — macro recording / playback** | Ctrl+\ macro recording not implemented. Deferred. | `src/editor/editmacros.c` |
| 42 | `[x]` | **Internal editor — tab size / expansion settings** | ~~`InsertChar('\t')` always inserted a literal tab.~~ Fixed: `EditorController.InsertTab()` checks `ExpandTabs`: when true it inserts `TabWidth - (col % TabWidth)` spaces; otherwise a literal tab. `EditorController.TabWidth` and `ExpandTabs` properties added (sourced from `McSettings`). | `src/editor/edit-impl.h` — `option_tab_spacing` |
| 43 | `[x]` | **Internal viewer — F8 raw mode toggle** | ~~F8 not bound.~~ Fixed: F8 toggles `ViewMode.Raw` ↔ `ViewMode.Text`. Raw mode replaces control characters (<32) with `.` before rendering. | `src/viewer/actions_cmd.c` — `mcview_cmd_toggle_parsed()` |
| 44 | `[x]` | **Internal viewer — `g` / `G` goto-line** | ~~Not bound.~~ Fixed: `case KeyCode.G` in `OnKeyDown` — if not shifted calls `GoToStart()`, else calls `GoToEnd()`. `Home`/`End` keys remain separate cases. | `src/viewer/display.c` — keybindings |
| 45 | `[x]` | **Panel — file info via Alt+Enter** | ~~Not implemented.~~ Fixed: `Alt+Enter` → `ShowInfo()` (the existing Info overlay). | `src/filemanager/panel.c` — `panel_cmd_info()` |
| 46 | `[x]` | **Confirmation dialog — "Confirm execute" missing** | ~~Not in the Confirmation dialog.~~ Fixed: added as part of the #31 fix — "Confirm execute" checkbox is now the 4th item in `ShowConfirmationDialog()`. `McSettings.ConfirmExecute` property added. | `src/setup.c` — `confirm_execute` |
| 47 | `[x]` | **Extension file associations not applied on activation** | ~~`ExtensionRegistry` was never consulted from `OnPanelEntryActivated`.~~ Fixed: before falling through to the internal viewer, `OnPanelEntryActivated` now calls `_controller.Extensions.GetOpenCommand(entry.Name)` and if a non-null result is returned, runs it via `ProcessHelper.RunDetached()`. `RunDetached()` added to ProcessHelper. | `src/filemanager/ext.c` — `regex_command()` |
| 48 | `[x]` | **Internal editor — "Save as" prompt** | ~~No way to save under a different filename.~~ Fixed: `Shift+F2` → `SaveAs()` which prompts via `PromptInput` and calls `EditorController.SaveAs()`. The controller method already existed. | `src/editor/editcmd.c` — `edit_save_as_cmd()` |
| 49 | `[ ]` | **Internal viewer — percentage / position indicator accuracy** | Scroll percentage is computed per visual line which is imprecise for wrapped files. Deferred — a byte-accurate counter would require tracking the raw byte position through the line-wrap engine. | `src/viewer/display.c` — `mcview_percent()` |
| 50 | `[x]` | **Panel — "Copy current path" Ctrl+X P binding** | ~~Documented in help but not implemented.~~ Fixed: `Ctrl+X P` in the Ctrl+X submap now calls `PastePathToCommandLine()`. | `src/filemanager/midnight.c` — `Ctrl+X P` |

---

## Summary

| Tier | Total | Fixed | Partial | Not started |
|------|-------|-------|---------|-------------|
| 1 — Critical | 10 | 10 | 0 | 0 |
| 2 — High | 12 | 12 | 0 | 0 |
| 3 — Medium | 16 | 10 | 0 | 6 |
| 4 — Low | 12 | 9 | 0 | 3 |
| **Total** | **50** | **41** | **0** | **9** |

### Remaining not-started items (deferred)

| # | Reason deferred |
|---|-----------------|
| 25 | System clipboard integration requires OS-specific tooling (xclip/wl-copy) beyond project scope |
| 26 | Word-wrap / paragraph-reflow editor feature — significant UI work |
| 27 | Editor bookmarks — requires UI indicator in gutter |
| 28 | Column (rectangular) block selection — complex selection model change |
| 30 | Numpad key mapping — terminal emulators deliver numpad as regular keys |
| 32 | User-defined listing column format — requires format-string parser |
| 36 | Command-line Tab completion — requires readline/shell integration |
| 38 | Help hyperlinks — requires MC-format help-file parser |
| 40 | Per-marked-file user-menu iteration — `%s`/`%t` (bulk list) available as substitute |
| 41 | Editor macro recording — non-trivial keystroke recorder |
| 49 | Viewer byte-accurate percentage — minor precision issue |
