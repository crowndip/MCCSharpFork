# Functions Checklist 3 — Full Visual & Functional Parity with GNU MC

**Date:** 2026-02-28
**Status flags:** `[ ]` Not started · `[~]` Partial / setting stored but not applied · `[x]` Fixed
**Scope:** Complete comparison of every clickable item, menu item, submenu, key binding, dialog, and
visual detail between the .NET port and the original GNU Midnight Commander.
**Excluded:** The custom _Tools_ menu is intentional and must remain.
**Reference:** https://github.com/MidnightCommander/mc
**Goal:** A regular user must not be able to distinguish any difference from the original.

---

## Summary Table

| Tier | Total | Fixed | Partial | Not started |
|------|-------|-------|---------|-------------|
| 1 — Critical | 8 | 7 | 0 | 1 |
| 2 — High | 16 | 11 | 0 | 5 |
| 3 — Medium | 24 | 12 | 0 | 12 |
| 4 — Low | 15 | 7 | 0 | 8 |
| **Total** | **63** | **37** | **0** | **26** |

---

## Tier 1 — Critical (breaks common daily workflows)

| # | Status | Area | What is missing / wrong | Original MC reference |
|---|--------|------|--------------------------|----------------------|
| 1 | `[x]` | **Copy/Move: overwrite confirmation not shown** | Fixed: `CopyFiles()` pre-scans for conflicts and shows a confirmation dialog when `ConfirmOverwrite` is set. | `src/filemanager/file.c` — `query_overwrite()` |
| 2 | `[x]` | **Copy: "Preserve attributes" has no effect** | Fixed: `CopyMarkedAsync()` now accepts `preserveAttributes` and `FileOperations.CopySingleFileAsync()` calls `PreserveAttrs()` to copy timestamps and Unix permissions. | `src/filemanager/file.c` — `copy_file_file()` |
| 3 | `[x]` | **Editor: no status bar** | Fixed (was already working): `EditorView.OnDrawingContent()` draws a cyan status bar at `contentHeight` row showing filename, line/col, INS/OVR, modified state. | `src/editor/editdraw.c` — `edit_status()` |
| 4 | `[x]` | **Panel: "Show backup files" setting has no effect** | Fixed: `ApplyFilterSettings()` propagates `ShowBackupFiles` and `ShowHiddenFiles` from `McSettings` into both panels' `DirectoryListing.Filter` objects, then reloads. Called at startup and after settings changes. | `src/filemanager/panel.c` — `show_backup_files` flag |
| 5 | `[x]` | **Alt+C — Quick CD shortcut not bound** | Fixed: `Alt+C` → `QuickCd()` added to `McApplication.OnKeyDown()`. | `src/filemanager/midnight.c` — `quick_cd_cmd()` |
| 6 | `[x]` | **Alt+? — Find File shortcut not bound** | Fixed: `Alt+?` → `ShowFindDialog()` added (matched via `keyEvent.AsRune.Value == '?'` in default clause). | `src/filemanager/find.c` — `find_cmd()` |
| 7 | `[x]` | **Command line: no history navigation (Alt+P / Alt+N)** | Fixed: `Alt+P` scrolls back through history, `Alt+N` scrolls forward, added to `CommandLineView.OnInputKeyDown()`. `CursorUp`/`CursorDown` also still work. | `src/filemanager/command.c` — `cmdline_hist_prev/next` |
| 8 | `[ ]` | **Command line: no Tab completion** | Original MC provides `Alt+Tab` (or bare `Tab` when command line is focused) for filename/command completion. `CommandLineView` has no completion logic. | `src/filemanager/command.c` — `complete_callback()` |

---

## Tier 2 — High impact (noticeable in regular daily use)

| # | Status | Area | What is missing / wrong | Original MC reference |
|---|--------|------|--------------------------|----------------------|
| 9 | `[x]` | **Panel: scrollbar not drawn** | Fixed: `FilePanelView` now has `ShowScrollbar` property and `DrawScrollbar()` method that renders `░`/`▓` on the rightmost inner column. `ApplyPanelSettings()` propagates the setting from `McSettings`. | `src/filemanager/panel.c` — panel scrollbar |
| 10 | `[x]` | **Ctrl+Space — calculate directory size** | Fixed: `Ctrl+Space` → `ShowDirSizeForCurrentEntry()` calculates size in background (`Task.Run`) and updates the status bar via `Application.Invoke`. | `src/filemanager/panel.c` — `compute_dir_size_cmd()` |
| 11 | `[x]` | **Alt+H — Command history popup from command line** | Fixed: `Alt+H` added to `CommandLineView.OnInputKeyDown()` as alias for history popup (alongside existing `Ctrl+H`). | `src/filemanager/command.c` — `cmdline_show_hist()` |
| 12 | `[x]` | **Ctrl+X H — Add current directory to hotlist** | Fixed: `Ctrl+X H` → `AddCurrentDirToHotlist()` added to the Ctrl+X submap in `McApplication`. | `src/filemanager/midnight.c` — `add2hotlist_cmd()` |
| 13 | `[x]` | **Ctrl+X D — Compare directories shortcut** | Fixed: `Ctrl+X D` → `CompareDirs()` added to the Ctrl+X submap. | `src/filemanager/midnight.c` — `compare_dirs_cmd()` |
| 14 | `[x]` | **Ctrl+X L / Ctrl+X S / Ctrl+X V — Link shortcuts** | Fixed: `Ctrl+X L` → `CreateLink()`, `Ctrl+X S` → `CreateSymlink()`, `Ctrl+X V` → `CreateRelativeSymlink()` added to the Ctrl+X submap. Menu labels updated to show shortcuts. | `src/filemanager/midnight.c` — link/symlink_cmd |
| 15 | `[ ]` | **Viewer: Ctrl+F / Ctrl+B — next/previous file in directory** | In original mcview, `Ctrl+F` opens the next file and `Ctrl+B` the previous file in the current directory. Not implemented. | `src/viewer/mcviewer.c` — `mcview_next_file()` |
| 16 | `[x]` | **Editor: F7 Find dialog has no case-sensitive option** | Fixed: `ShowFind()` now opens a full dialog with "Case sensitive" and "Regular expression" checkboxes, pre-filled from `_editor.LastSearch`. | `src/editor/editsearch.c` — `edit_search_dialog()` |
| 17 | `[x]` | **Editor: Shift+F7 — Find again (repeat search)** | Fixed: `Shift+F7` → `FindAgain()` repeats last search without dialog; if no previous search, opens the dialog. | `src/editor/editsearch.c` — `edit_search_cmd()` |
| 18 | `[x]` | **Editor: F9 should open editor pull-down menu** | Fixed: `F9` now calls `ShowEditorMenu()` which shows a `MessageBox.Query` with editor actions (Save, Save As, Find, Replace, Go to line, Toggle line numbers, Toggle syntax highlighting, Close). | `src/editor/editwidget.c` — `edit_execute_key_command()` |
| 19 | `[ ]` | **Listing format dialog: missing "User-defined" option** | The `ShowListingFormatDialog()` radio group shows only `Full / Brief / Long`. Original MC has a fourth `User defined` option with a custom format string. | `src/filemanager/panel.c` — `list_user` |
| 20 | `[ ]` | **Copy/Move: "Follow symlinks" and "Dive into subdir" options not applied** | Both checkboxes are shown in `CopyMoveDialog` but never read during the actual operation. | `src/filemanager/file.c` — `FileOpContext` flags |
| 21 | `[ ]` | **Copy/Move: source mask pattern rename not applied** | `CopyMoveOptions.SourceMask` is captured but never used — all files are copied with their original names. | `src/filemanager/filegui.c` — mask substitution |
| 22 | `[x]` | **Panel: file type coloring — archives not highlighted** | Fixed: `FilePanelView.GetEntryAttr()` now has an `IsArchiveFile()` branch that returns `McTheme.PanelArchive` (bright cyan) for `.zip`, `.tar`, `.gz`, `.bz2`, `.7z`, and 20+ other archive extensions. | `src/filemanager/panel.c` — file type color |
| 23 | `[x]` | **Editor: status bar modified indicator** | Fixed (with #3): Status bar at bottom of editor shows `Modified` / `Saved` state inline. | `src/editor/editdraw.c` — modified indicator |
| 24 | `[x]` | **Panel: "Show mini status" setting has no effect** | Fixed: `FilePanelView` now has `ShowMiniStatus` property. When `false`, the status strip row is omitted and `ContentRows` grows by 1. `ApplyPanelSettings()` propagates the value from `McSettings`. | `src/filemanager/panel.c` — `panels_options.show_mini_status` |

---

## Tier 3 — Medium impact (power users will notice)

| # | Status | Area | What is missing / wrong | Original MC reference |
|---|--------|------|--------------------------|----------------------|
| 25 | `[x]` | **Alt+T — cycle panel listing mode** | Fixed: `Alt+T` → `CycleListingMode()` added to `McApplication`, which calls `FilePanelView.CycleListingMode()` cycling Full → Brief → Long → Full. | `src/filemanager/panel.c` — `cycle_listing_format_cmd()` |
| 26 | `[x]` | **Alt+, (Alt+comma) — toggle split direction** | Fixed: `Alt+,` → `ToggleSplitDirection()` toggles `_settings.HorizontalSplit` and calls `ApplyLayoutSettings()`. | `src/filemanager/layout.c` — `toggle_panels_split_cmd()` |
| 27 | `[x]` | **Alt+G / Alt+R / Alt+J — jump to first/middle/last in panel** | Fixed: All three keys bound in `McApplication.OnKeyDown()`. `FilePanelView.JumpToFirst/Middle/Last()` methods added. | `src/filemanager/panel.c` — `panel_jump_to_top/middle/bottom()` |
| 28 | `[ ]` | **Viewer: F5 = "Go to line" (original) vs "Go to byte offset" (port)** | In text mode, original mcview's `F5` prompts for a line number. The port uses byte offset for all modes. | `src/viewer/actions.c` — `mcview_cmd_goto_line()` |
| 29 | `[ ]` | **Viewer: F9 — toggle nroff/formatted mode** | Original mcview's `F9` toggles nroff-formatted output (bold/underline via backspace sequences). Not implemented. | `src/viewer/display.c` — `mcview_toggle_fmt_cmd()` |
| 30 | `[ ]` | **Viewer: Alt+E — change charset encoding** | `Alt+E` opens an encoding selection dialog in original mcview. Not implemented. | `src/viewer/mcviewer.c` — `mcview_select_encoding_cmd()` |
| 31 | `[ ]` | **Viewer: Alt+R — toggle ruler** | Original mcview shows/hides a ruler line (column position indicator) with `Alt+R`. Not implemented. | `src/viewer/actions.c` — `mcview_cmd_toggle_ruler()` |
| 32 | `[ ]` | **Viewer: numeric bookmarks [n]m / [n]r** | Original mcview supports 10 bookmarks (0–9). The port only supports one bookmark (Ctrl+B/Ctrl+P). | `src/viewer/mcviewer.c` — `mcview_set_byte_cmd()` |
| 33 | `[x]` | **Editor: Ctrl+U = Undo (original binding)** | Fixed: `Ctrl+U` → `_editor.Undo()` added as alias to the existing `Ctrl+Z` undo binding. | `src/editor/edit.c` — `edit_undo_cmd()` |
| 34 | `[ ]` | **Editor: Ctrl+R conflicts — should be macro recording** | Original mcedit uses `Ctrl+R` for macro recording; the port uses it for Redo. The conflict is not resolved. | `src/editor/edit.c` — `edit_execute_macro_cmd()` |
| 35 | `[ ]` | **Editor: Shift+F4 — Replace again** | `Shift+F4` in original mcedit repeats the last find-and-replace. Not bound. | `src/editor/editsearch.c` — replace again |
| 36 | `[x]` | **Editor: Ctrl+Insert / Shift+Insert / Shift+Delete — original clipboard keys** | Fixed: `Ctrl+Insert` → copy, `Shift+Insert` → paste, `Shift+Delete` → cut added to `EditorView.OnKeyDown()`. | `src/editor/editwidget.c` — clipboard bindings |
| 37 | `[x]` | **Editor: Alt+L — Go to line (original shortcut)** | Fixed: `Alt+L` → `ShowGotoLine()` added as alias alongside existing `Ctrl+G`. | `src/editor/editwidget.c` — `edit_goto_line_cmd()` |
| 38 | `[ ]` | **Editor: Ctrl+N / Ctrl+O — New file / Open file** | `Ctrl+N` (new file) and `Ctrl+O` (open file) are not implemented in `EditorView`. | `src/editor/editwidget.c` — `edit_new_cmd()` / `edit_load_cmd()` |
| 39 | `[ ]` | **Learn Keys dialog is static — should be interactive terminal tester** | Original MC's Learn keys is interactive. The port shows a static table. | `src/learn.c` — `learn_keys()` |
| 40 | `[ ]` | **Help dialog: topic sections not navigable** | Topic links in the help window do nothing. Original MC has linked hypertext navigation. | `src/help.c` — `help_select_topic()` |
| 41 | `[x]` | **"Unselect group" shortcut should be "\" (backslash) not "-"** | Fixed: menu label updated to `"\\"` and the key binding accepts `\`. | `src/filemanager/midnight.c` — `unmark_files_cmd()` |
| 42 | `[ ]` | **Command line: Emacs-style editing keys** | `Ctrl+A`, `Ctrl+E`, `Ctrl+K`, `Ctrl+W`, `Ctrl+Y`, `Alt+B`, `Alt+F` not all honoured in `CommandLineView`. | `src/filemanager/command.c` — `cmdline_key()` |
| 43 | `[ ]` | **Find dialog: missing date/time and file size filters** | Original MC's Find dialog allows filtering by modification date and file size. `FindDialog` has no such fields. | `src/filemanager/find.c` — `age_spec` / `size_spec` |
| 44 | `[ ]` | **Configuration dialog: missing original options** | Several options from GNU MC's `Options > Configuration` are absent: `Shell patterns`, `Safe delete`, `Auto save setup`, `Rotating dash`, `Cd follows links`, `Pause after run`. | `src/setup.c` — `configure_box()` |
| 45 | `[x]` | **Sort dialog: field labels use raw C# enum names** | Fixed: `SortDialog` now uses a `Label()` local function that maps enum values to human-readable strings: `"Modify time"`, `"Access time"`, `"Change time"`, etc. | `src/filemanager/panel.c` — sort dialog labels |
| 46 | `[x]` | **File menu: Chmod and Chown items missing shortcut labels** | Fixed: Menu items now show `"Ctrl+X C"` and `"Ctrl+X O"` shortcut labels. | `src/filemanager/midnight.c` — menu_entry shortcut |
| 47 | `[x]` | **Panel: "Ctrl+X T" conflict — Tree vs tagged-files copy** | Fixed: `Ctrl+X T` now pastes tagged files to the command line (matching original MC). Tree overlay remains accessible via the Left/Right menu. | `src/filemanager/midnight.c` — `Ctrl+X T` |
| 48 | `[x]` | **Ctrl+X Ctrl+P — copy OTHER panel's path to command line** | Fixed: `Ctrl+X Ctrl+P` → `PasteOtherPanelPathToCommandLine()` added to the Ctrl+X submap. | `src/filemanager/midnight.c` — `copy_other_panel_path_cmd()` |

---

## Tier 4 — Low impact / cosmetic deviations

| # | Status | Area | What is missing / wrong | Original MC reference |
|---|--------|------|--------------------------|----------------------|
| 49 | `[ ]` | **Hints bar not implemented** | Original MC displays a rotating hints/tips strip below the panels. The port has no hints bar. | `src/filemanager/midnight.c` — `show_hint()` |
| 50 | `[x]` | **Editor: Ctrl+D — Insert current date and time** | Fixed: `Ctrl+D` → `InsertDateTime()` inserts `DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")` at cursor. | `src/editor/editwidget.c` — `edit_date_cmd()` |
| 51 | `[x]` | **Editor: Ctrl+T — Toggle syntax highlighting** | Fixed: `Ctrl+T` toggles `_syntaxHighlightingOn`; drawing code checks `_syntaxHighlightingOn && _editor.Highlighter != null`. Status bar shows `NoHL` when off. | `src/editor/editwidget.c` — `edit_syntax_toggle_cmd()` |
| 52 | `[ ]` | **Editor: Ctrl+Tab — Word completion** | `Ctrl+Tab` word-completion popup not implemented. | `src/editor/edit.c` — `edit_complete_word_cmd()` |
| 53 | `[x]` | **Editor: Shift+Enter — insert newline without auto-indent** | Fixed: `Shift+Enter` detected in default clause and calls `_editor.InsertChar('\n')` without triggering auto-indent. | `src/editor/editwidget.c` — `edit_newline()` |
| 54 | `[ ]` | **Viewer: F1 in viewer does not show viewer-specific help** | Pressing F1 inside the viewer opens the main help instead of viewer-specific help. | `src/viewer/mcviewer.c` — `mcview_help_cmd()` |
| 55 | `[ ]` | **Viewer: "/" key for regex search shortcut** | Original mcview starts regex search with `/`. Not bound in `ViewerView`. | `src/viewer/actions.c` — `/` binding |
| 56 | `[ ]` | **Panel: file type coloring — device, socket, pipe files** | Block/char devices, FIFOs, sockets are not given distinct colors. Requires new fields in `VfsDirEntry`. | `src/filemanager/panel.c` — `skin_colorize()` |
| 57 | `[x]` | **Panel: scrollbar position marker** | Fixed (with #9): `DrawScrollbar()` calculates `thumbPos` based on `_scrollOffset` / total-entries ratio and renders `▓` at the correct row. | `src/filemanager/panel.c` — scrollbar draw |
| 58 | `[ ]` | **Panel Options: "Fast directory reload" option missing** | "Fast directory reload" setting (reload only if mtime changed) is absent from `McSettings` and the Panel Options dialog. | `src/filemanager/panel.c` — `panels_options.fast_reload` |
| 59 | `[ ]` | **Command line: Ctrl+Q — quote next character** | `Ctrl+Q` quotes the next keystroke (insert control char literally). Not implemented. | `src/filemanager/command.c` — `quote_next` |
| 60 | `[ ]` | **Command > Screen list not implemented** | `Command > Screen list` shows "Not implemented". | `src/filemanager/midnight.c` — `show_editor_history_cmd()` |
| 61 | `[x]` | **Panel: Alt+S / Ctrl+S as alternative quick-search trigger** | Fixed: `Alt+S` in `FilePanelView.OnKeyDown()` clears `_quickSearch`, sets `_quickSearchActive = true`, and triggers a redraw — same behaviour as typing a character. | `src/filemanager/panel.c` — `panel_quick_search()` |
| 62 | `[x]` | **File menu: Symlink items show no keyboard shortcuts in menu** | Fixed: `File > Link`, `File > Symlink`, `File > Relative symlink` menu items now show `"Ctrl+X L"`, `"Ctrl+X S"`, `"Ctrl+X V"`. | `src/filemanager/midnight.c` — menu entry shortcuts |
| 63 | `[x]` | **Editor: F7 Find dialog doesn't remember last search options** | Fixed: `ShowFind()` now pre-fills the search field with `_editor.LastSearch.Pattern` and initialises the "Case sensitive" and "Regular expression" checkboxes from `_editor.LastSearch`. | `src/editor/editsearch.c` — `edit_search_options` persistence |

---

## Cross-references to earlier checklists

Items marked `[x]` in `functionsChecklist2.md` are already fixed and are **not** repeated here.
Items from `functionChecklist.md` (visual parity) are also **not** repeated here.
This checklist focuses exclusively on functional and key-binding gaps not addressed in either earlier pass.
