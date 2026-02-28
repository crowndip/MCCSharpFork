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
| 13 | `[x]` | **Find file — start-dir browser + extra options** | ~~Editable start-dir field, "Follow symlinks", "Skip hidden dirs" added.~~ Fixed: "Again" button added to results dialog — cancels current search and reopens the options dialog with the same parameters. Tree-button and "Ignore dirs" list remain aspirational. | `src/filemanager/find.c` |
| 14 | `[x]` | **Hotlist — hierarchical groups** | ~~Current hotlist is a flat list.~~ Fixed: HotlistManager now has a full tree model (HotlistGroup/HotlistEntry) with recursive GROUP/ENTRY/ENDGROUP file format; HotlistDialog shows groups as `[/] Name` items with Enter/Go to for navigation, "Up" to return to parent, "New group" to create groups, breadcrumb path label. | `src/vfs/path.c`, `lib/hotlist.c` |
| 15 | `[x]` | **Quick view panel mode (persistent)** | ~~Quick view opens a full-screen one-shot viewer.~~ Fixed: `Ctrl+X Q` toggles the inactive panel into a live Quick View overlay that reads up to 500 lines of the file under the cursor and updates automatically via `CursorChanged` event. Toggle off restores the file-listing panel. | `src/filemanager/panel.c` — `WPanel::panel_format` = `list_quick_view` |
| 16 | `[x]` | **Info panel mode (persistent)** | ~~Info shows a modal dialog.~~ Fixed: `Ctrl+X I` toggles the inactive panel into a persistent Info overlay showing Name/Type/Size/Mode/Owner/Group/Mtime/Atime (+ symlink target); updates automatically as cursor moves. | `src/filemanager/info.c` |
| 17 | `[x]` | **Tree panel mode (persistent)** | ~~Tree is a modal dialog.~~ Fixed: `Ctrl+X T` toggles the inactive panel into a navigable tree overlay (same approach as #15/#16); Enter expands/collapses or navigates active panel; expanded state persisted in overlay's Data field. | `src/filemanager/tree.c` |
| 18 | `[x]` | **File listing — Long / User-defined columns** | ~~Only Full and Brief modes.~~ Fixed: added `PanelListingMode.Long` — ls -l style (permissions + owner + group + size + date + name via `FormatLongEntry()`); selectable from Listing Format dialog. | `src/filemanager/panel.c` — `list_type` enum |
| 19 | `[x]` | **Edit symlink — confirmation step** | ~~Original asks "Do you want to update the symlink?" before modifying the target. We edit without the secondary confirm.~~ Fixed: confirmation dialog added before recreating the symlink. | `src/filemanager/cmd.c` — `edit_symlink_cmd()` |
| 20 | `[x]` | **Compare files — null/directory fallback** | ~~When either entry is null or a directory the operation silently does nothing. Original MC falls back gracefully with an error message.~~ Fixed: MessageDialog.Show error when either entry is null or directory. | `src/filemanager/cmd.c` — `diff_view_cmd()` |
| 21 | `[x]` | **F2 User menu — condition lines** | ~~`+`/`=` condition lines (e.g. `+ f text/*` restricts entry to text files matching a pattern) are silently skipped instead of evaluated; entries are not filtered.~~ Fixed: `EvaluateUserMenuCondition()` evaluates `f`/`d` pattern conditions and `!` negation; filtered entries are hidden from the menu. | `src/usermenu.c` — `check_conditions()` |
| 22 | `[x]` | **Copy/Move — Background button** | ~~The "Background" button is absent.~~ Fixed: "Background" button added to CopyMoveDialog; operations run as non-blocking Tasks with a BackgroundJob tracker; ShowBackgroundJobs() now lists running/finished jobs with a Kill (cancel) button. | `src/filemanager/filegui.c`, `src/background.c` |
| 23 | `[x]` | **Advanced Chown — combined dialog** | ~~Currently shows ChownDialog then ChmodDialog in sequence.~~ Fixed: new AdvancedChownDialog combines owner/group listboxes with permission checkboxes (special/owner/group/other) and octal field; multi-file "Set all" support. | `src/filemanager/achown.c` |

---

## Tier 3 — Enhancements / Configuration completeness

These round out the experience but have workarounds or limited daily impact.

| # | Status | Feature | What is missing | MC source reference |
|---|--------|---------|-----------------|---------------------|
| 24 | `[x]` | **Options → Configuration — missing settings** | ~~Many settings absent.~~ Fixed: added Verbose operation, Compute totals, Auto save setup, Show output of commands, Use subshell, Ask before running programs; all persisted via McSettings/McConfig. | `src/setup.c` — `configure_box()` |
| 25 | `[x]` | **Options → Panel options — missing settings** | ~~Missing many settings.~~ Fixed: added Show mini status, Lynx-like motion, Show scrollbar, Highlight files, Mix all files, Case-sensitive quick search, Show free space; all saved + panel reload applied. | `src/filemanager/panel.c` — `panel_options_box()` |
| 26 | `[x]` | **Options → Layout — missing controls** | ~~Missing show/hide and split controls.~~ Fixed: added Show menubar/command line/key bar checkboxes, panel split % field, "Equal (50/50)" button; `ApplyLayoutSettings()` toggles Visible + adjusts Dim.Percent. | `src/setup.c` — `layout_box()` |
| 27 | `[x]` | **Directory tree dialog — F-key bindings** | ~~Dialog lacks F2/F8 bindings.~~ Fixed: F2 rescans (force-expands) the selected directory; F8 deletes it (with confirmation, non-recursive); expand/collapse state already persisted during the dialog session. | `src/filemanager/tree.c` |
| 28 | `[ ]` | **F1 Help — hyperlinks between nodes** | Help viewer shows plain text. Original MC uses ctrl-char escape codes to embed hyperlinks between nodes; clicking or pressing Enter on a link navigates to the linked topic. | `src/help.c` |
| 29 | `[x]` | **Filtered view — pass filename automatically** | ~~"Filtered view" dialog does not substitute `%f` with the current filename; user must type the full command including filename manually.~~ Already implemented: default command is "cat %f" and `%f` is substituted with the full file path before execution. | `src/filemanager/cmd.c` — `filtered_view_cmd()` |
| 30 | `[x]` | **Encoding — full iconv list** | ~~Hardcoded list of 16 encodings.~~ Fixed: uses `System.Text.Encoding.GetEncodings()` for the full .NET-registered list; preferred encodings first, rest alphabetically; filter field for quick search. | `lib/charsets.c` |
| 31 | `[x]` | **Options → Appearance / Skins** | ~~Currently shows "not implemented".~~ Fixed: `ShowAppearanceDialog()` lists "default" plus any INI skin files found in `~/.local/share/mc/skins/` and `/usr/share/mc/skins/`; selecting one calls `McTheme.ApplySkin()` which parses the MC INI colour format and updates all ColorSchemes; choice persisted via `McSettings.ActiveSkin`. | `lib/skin/` |
| 32 | `[x]` | **Options → Learn keys** | ~~Shows "not implemented".~~ Fixed: `ShowLearnKeysDialog()` shows a scrollable list of all 25 key bindings (F-keys, Ctrl combinations, Ctrl+X submap) in a table. | `src/learn.c` |
| 33 | `[x]` | **Options → Virtual FS settings** | ~~Shows "not implemented".~~ Fixed: `ShowVfsSettingsDialog()` with VFS cache timeout, FTP anonymous password, FTP proxy host, and "Use passive mode" checkbox; persisted via McSettings. | `src/vfs/setup.c` |
| 34 | `[x]` | **Background jobs dialog** | ~~Shows a static informational message.~~ Fixed as part of #22: ShowBackgroundJobs() now lists real BackgroundJob entries with name/status and a Kill button. | `src/background.c` — `jobs_cmd()` |
| 35 | `[x]` | **Command history — inline in command line** | ~~History in a separate dialog.~~ Fixed: Ctrl+H or Up-on-empty-input pops up an inline `Window` with a `ListView` of history (most-recent first) positioned just above the command line; Enter selects, Esc closes. | `src/filemanager/command.c` |
| 36 | `[x]` | **Active VFS list — full display** | ~~Only panel paths shown.~~ Fixed: display now shows scheme://[user@]host[:port]/path format with scheme type info; Browse navigates active panel; Free VFSs unmounts all remote connections. | `src/vfs/vfs.c` — `reselect_vfs()` |
| 37 | `[ ]` | **Shell link (FISH protocol)** | Shows "not implemented". Original implements the FISH (FIles transferred over SHell) protocol for remote panel access over SSH. | `src/vfs/fish/` |
| 38 | `[ ]` | **Screen list (multiple subshells)** | Shows "not implemented". Original MC supports multiple pseudo-terminal subshell screens, accessible via a screen-manager dialog. | `src/subshell/` |
| 39 | `[x]` | **Chattr (ext2 file attributes)** | ~~Not present.~~ Fixed: Ctrl+X A shows a Chattr dialog querying `lsattr` for current attrs, displays checkboxes for common ext2 attributes (append-only, immutable, no-dump, etc.), and applies changes via `chattr +/-flags`. | `src/filemanager/chattr.c` |
| 40 | `[ ]` | **FTP / SFTP VFS providers** | Dialogs exist but no VFS provider ships; navigation fails silently. Providers require the Mc.Vfs.Ftp / Mc.Vfs.Sftp projects to be connected. | `src/vfs/ftpfs/`, `src/vfs/sftpfs/` |

---

## Summary

| Tier | Total | Done | In progress | Not started |
|------|-------|------|-------------|-------------|
| 1 — Critical | 11 | 11 | 0 | 0 |
| 2 — Important | 12 | 12 | 0 | 0 |
| 3 — Enhancements | 17 | 16 | 0 | 1 |
| **Total** | **40** | **39** | **0** | **1** |
