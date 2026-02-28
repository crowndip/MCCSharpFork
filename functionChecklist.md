# Function Checklist — MC .NET Port vs. GNU Midnight Commander

**Status flags:** `[ ]` Not started · `[~]` In progress / partial · `[x]` Done

Reference source: <https://github.com/MidnightCommander/mc>

Items are ordered from highest to lowest impact on user experience.

---

## Tier 1 — Critical / Core correctness

These fix wrong behaviour or missing safety guards that affect everyday use.

| # | Status | Feature | What is missing | MC source reference |
|---|--------|---------|-----------------|---------------------|
| 1 | `[x]` | **Quick search** (typing in panel) | ~~Typing a letter in active panel should activate incremental search; cursor jumps to first matching filename. ESC/Enter/navigation exits.~~ | `src/filemanager/panel.c` — `panel_quick_search()` |
| 2 | `[x]` | **Chmod — special bits + multi-file** | ~~Missing setuid/setgid/sticky checkboxes; no "Set all" button; single-file only.~~ | `src/filemanager/chmod.c` |
| 3 | `[x]` | **Chown — system user/group lists** | ~~Owner/Group should be listboxes from `/etc/passwd` + `/etc/group`; "Set all" for marked files.~~ | `src/filemanager/chown.c` |
| 4 | `[x]` | **Select/Unselect group dialog** | ~~Missing "Files only", "Case sensitive", "Using shell patterns" checkboxes.~~ | `src/filemanager/panel.c` — `panel_select_files()` |
| 5 | `[x]` | **Compare directories — method selector** | ~~Only one method (name+size); missing Quick (timestamps) / Size-only / Thorough (byte-by-byte) selector.~~ | `src/filemanager/cmd.c` — `compare_dirs_cmd()` |
| 6 | `[x]` | **Find file — real-time + action buttons** | ~~Results computed in one shot; no progress display; no Panelize / View / Edit buttons.~~ | `src/filemanager/find.c` — `find_cmd()` |
| 7 | `[x]` | **Copy/Move — extra checkboxes** | ~~Missing "Dive into subdir if exists" and "Stable symlinks" checkboxes.~~ | `src/filemanager/filegui.c` — `file_mask_dialog()` |
| 8 | `[x]` | **Ctrl+L key binding** | ~~Ctrl+L is mapped to "Show file info" but original MC uses it as "Refresh/redraw screen". The info panel is opened with Ctrl+I or `*` in original.~~ Fixed: Ctrl+L → `Application.LayoutAndDraw`, Ctrl+I → ShowInfo. | `src/filemanager/midnight.c` keybindings |
| 9 | `[x]` | **F8 Delete — recursive directory confirmation** | ~~When deleting a non-empty directory the original shows a second dialog "Delete directory `<name>` recursively?". We recurse without this extra prompt.~~ Fixed: secondary confirmation per directory before deletion. | `src/filemanager/file.c` — `erase_dir()` |
| 10 | `[x]` | **F4 Edit new file** | ~~F4 with no file under cursor (or an empty panel) should prompt "Enter file name:" and open the editor for a new file. Currently opens editor with a blank path.~~ Fixed: InputDialog prompt when cursor is on dir or panel is empty. | `src/filemanager/cmd.c` — `edit_cmd_new()` |
| 11 | `[x]` | **Copy/Move — source mask / pattern field** | ~~Dialog is missing the top "From:" source mask input with "Using shell patterns" toggle that lets the user filter which marked files are processed.~~ Fixed: "From:" field + "Using shell patterns" checkbox added; dialog height 17. | `src/filemanager/filegui.c` — `file_mask_dialog()` |

---

## Tier 2 — Important / Significant usability gaps

These are present in everyday original MC but not critical blockers.

| # | Status | Feature | What is missing | MC source reference |
|---|--------|---------|-----------------|---------------------|
| 12 | `[x]` | **External Panelize — inject into panel** | ~~Shell command output filenames are captured but NOT loaded into the panel; F5/F6/F8 cannot operate on the results.~~ Fixed: files from command output are now marked in the active panel; files outside the current directory are counted and reported. | `src/filemanager/cmd.c` — `external_panelize()` |
| 13 | `[~]` | **Find file — start-dir browser + extra options** | ~~Editable start-dir field, "Follow symlinks", "Skip hidden dirs" added.~~ Still missing: tree-button to browse start directory; "Ignore dirs" list; "Again" button to restart search. | `src/filemanager/find.c` |
| 14 | `[x]` | **Hotlist — hierarchical groups** | ~~Current hotlist is a flat list.~~ Fixed: HotlistManager now has a full tree model (HotlistGroup/HotlistEntry) with recursive GROUP/ENTRY/ENDGROUP file format; HotlistDialog shows groups as `[/] Name` items with Enter/Go to for navigation, "Up" to return to parent, "New group" to create groups, breadcrumb path label. | `src/vfs/path.c`, `lib/hotlist.c` |
| 15 | `[x]` | **Quick view panel mode (persistent)** | ~~Quick view opens a full-screen one-shot viewer.~~ Fixed: `Ctrl+X Q` toggles the inactive panel into a live Quick View overlay that reads up to 500 lines of the file under the cursor and updates automatically via `CursorChanged` event. Toggle off restores the file-listing panel. | `src/filemanager/panel.c` — `WPanel::panel_format` = `list_quick_view` |
| 16 | `[x]` | **Info panel mode (persistent)** | ~~Info shows a modal dialog.~~ Fixed: `Ctrl+X I` toggles the inactive panel into a persistent Info overlay showing Name/Type/Size/Mode/Owner/Group/Mtime/Atime (+ symlink target); updates automatically as cursor moves. | `src/filemanager/info.c` |
| 17 | `[ ]` | **Tree panel mode (persistent)** | Tree is a modal dialog. Original MC switches the panel to a **persistent navigable tree widget** with F2 (rescan), F8 (delete dir), and expand/collapse per node. | `src/filemanager/tree.c` |
| 18 | `[ ]` | **File listing — Long / User-defined columns** | Only Full and Brief modes are offered. Original also has: Long (ls -l style), Half (2-column brief), User-defined (configurable column list). | `src/filemanager/panel.c` — `list_type` enum |
| 19 | `[x]` | **Edit symlink — confirmation step** | ~~Original asks "Do you want to update the symlink?" before modifying the target. We edit without the secondary confirm.~~ Fixed: confirmation dialog added before recreating the symlink. | `src/filemanager/cmd.c` — `edit_symlink_cmd()` |
| 20 | `[x]` | **Compare files — null/directory fallback** | ~~When either entry is null or a directory the operation silently does nothing. Original MC falls back gracefully with an error message.~~ Fixed: MessageDialog.Show error when either entry is null or directory. | `src/filemanager/cmd.c` — `diff_view_cmd()` |
| 21 | `[x]` | **F2 User menu — condition lines** | ~~`+`/`=` condition lines (e.g. `+ f text/*` restricts entry to text files matching a pattern) are silently skipped instead of evaluated; entries are not filtered.~~ Fixed: `EvaluateUserMenuCondition()` evaluates `f`/`d` pattern conditions and `!` negation; filtered entries are hidden from the menu. | `src/usermenu.c` — `check_conditions()` |
| 22 | `[ ]` | **Copy/Move — Background button** | The "Background" button is absent. Original MC allows long copy/move operations to run in the background with progress tracking in the "Background jobs" dialog. | `src/filemanager/filegui.c`, `src/background.c` |
| 23 | `[x]` | **Advanced Chown — combined dialog** | ~~Currently shows ChownDialog then ChmodDialog in sequence.~~ Fixed: new AdvancedChownDialog combines owner/group listboxes with permission checkboxes (special/owner/group/other) and octal field; multi-file "Set all" support. | `src/filemanager/achown.c` |

---

## Tier 3 — Enhancements / Configuration completeness

These round out the experience but have workarounds or limited daily impact.

| # | Status | Feature | What is missing | MC source reference |
|---|--------|---------|-----------------|---------------------|
| 24 | `[ ]` | **Options → Configuration — missing settings** | Many settings absent: Verbose operation, Compute totals, Use shell patterns, Auto save setup, Drop caches after copy/move/delete, Show output of commands, Subshell usage, Always show mini status, Ask before running programs. | `src/setup.c` — `configure_box()` |
| 25 | `[ ]` | **Options → Panel options — missing settings** | Missing: Show mini status, Lynx-like motion (left arrow = parent dir), Scrollbar in panels, File highlighting by type/permissions, Mix all files (dirs interleaved), Quick search mode (case sensitivity), Real path of symlinks, Free space display. | `src/filemanager/panel.c` — `panel_options_box()` |
| 26 | `[ ]` | **Options → Layout — missing controls** | Missing: show/hide menubar / command line / key bar / hintbar individually, panel split ratio slider, "Equal split" toggle. | `src/setup.c` — `layout_box()` |
| 27 | `[ ]` | **Directory tree dialog — F-key bindings** | Our modal tree dialog lacks the F-key bindings of the original tree widget (F2 = rescan subtree, F8 = delete dir) and does not persist expand/collapse state across opens. | `src/filemanager/tree.c` |
| 28 | `[ ]` | **F1 Help — hyperlinks between nodes** | Help viewer shows plain text. Original MC uses ctrl-char escape codes to embed hyperlinks between nodes; clicking or pressing Enter on a link navigates to the linked topic. | `src/help.c` |
| 29 | `[x]` | **Filtered view — pass filename automatically** | ~~"Filtered view" dialog does not substitute `%f` with the current filename; user must type the full command including filename manually.~~ Already implemented: default command is "cat %f" and `%f` is substituted with the full file path before execution. | `src/filemanager/cmd.c` — `filtered_view_cmd()` |
| 30 | `[ ]` | **Encoding — full iconv list** | Encoding dialog shows a hardcoded list of 16 encodings. Original MC queries all iconv-known encodings dynamically. | `lib/charsets.c` |
| 31 | `[ ]` | **Options → Appearance / Skins** | Currently shows "not implemented". Original MC supports INI-based colour skin files from `~/.local/share/mc/skins/`. | `lib/skin/` |
| 32 | `[ ]` | **Options → Learn keys** | Currently shows "not implemented". Original provides an interactive key-binding editor. | `src/learn.c` |
| 33 | `[ ]` | **Options → Virtual FS settings** | Currently shows "not implemented". Original shows cache timeout, FTP proxy, anonymous password settings. | `src/vfs/setup.c` |
| 34 | `[ ]` | **Background jobs dialog** | Shows a static informational message. Original lists running background copy/move jobs with file counts and a Kill button. Requires Background copy/move (item 22) to be useful. | `src/background.c` — `jobs_cmd()` |
| 35 | `[ ]` | **Command history — inline in command line** | History is shown in a separate dialog and pasted. Original integrates history as an inline dropdown in the command-line widget itself. | `src/filemanager/command.c` |
| 36 | `[ ]` | **Active VFS list — full display** | Only panel paths shown. Original shows each mounted VFS with path + type + connection info, and "Free VFSs" to unmount. | `src/vfs/vfs.c` — `reselect_vfs()` |
| 37 | `[ ]` | **Shell link (FISH protocol)** | Shows "not implemented". Original implements the FISH (FIles transferred over SHell) protocol for remote panel access over SSH. | `src/vfs/fish/` |
| 38 | `[ ]` | **Screen list (multiple subshells)** | Shows "not implemented". Original MC supports multiple pseudo-terminal subshell screens, accessible via a screen-manager dialog. | `src/subshell/` |
| 39 | `[ ]` | **Chattr (ext2 file attributes)** | Not present in the .NET port. Original MC shows a Chattr dialog on Linux with ext2fs. | `src/filemanager/chattr.c` |
| 40 | `[ ]` | **FTP / SFTP VFS providers** | Dialogs exist but no VFS provider ships; navigation fails silently. Providers require the Mc.Vfs.Ftp / Mc.Vfs.Sftp projects to be connected. | `src/vfs/ftpfs/`, `src/vfs/sftpfs/` |

---

## Summary

| Tier | Total | Done | In progress | Not started |
|------|-------|------|-------------|-------------|
| 1 — Critical | 11 | 11 | 0 | 0 |
| 2 — Important | 12 | 9 | 1 | 2 |
| 3 — Enhancements | 17 | 1 | 0 | 16 |
| **Total** | **40** | **21** | **1** | **18** |
