# Original GNU Midnight Commander — Complete Function Reference

**Purpose:** Master reference of every feature, function, key binding, menu item, and dialog
in the original GNU Midnight Commander. Used to verify completeness of the .NET port.

**Status legend:**
- `✅` Implemented and matches original behaviour
- `⚠️` Partially implemented (see notes)
- `❌` Not implemented / stub

**Reference:** https://github.com/MidnightCommander/mc
**MC version:** 4.8.x (current mainline, source file `src/filemanager/filemanager.c`)

---

## 1. Main Screen Layout

| Feature | Status | Notes |
|---------|--------|-------|
| Two-panel side-by-side layout (default) | ✅ | |
| Horizontal split layout (panels top/bottom) | ✅ | Toggle via Alt+, |
| Menu bar at top (Left \| File \| Command \| Options \| Right) | ✅ | |
| Two file panels with independent navigation | ✅ | |
| Single `│` divider between panels | ✅ | |
| Command line at bottom | ✅ | |
| Function key bar (F1–F10 labels) at very bottom | ✅ | |
| Hints bar (rotating tip strip between panels and cmdline) | ❌ | Uses ~/.config/mc/hints |
| Panel path in top border, centered, with trailing `/` | ✅ | |
| Active panel has brighter frame colour | ✅ | |
| Panel summary line ("N files, M dirs, X bytes free") | ✅ | |
| Free disk space shown in summary | ✅ | |
| Mini-status line per panel (file info below listing) | ✅ | |
| Scrollbar on right inner edge of panel | ✅ | |
| Scrollbar thumb position indicator | ✅ | |
| Split ratio adjustable (Alt+Shift+Left/Right, or = for equal) | ✅ | |
| `update_menu()` sets panel menu title to panel's current path | ✅ | |

---

## 2. Left / Right Panel Menu
*(source: `create_panel_menu()` in `src/filemanager/filemanager.c`)*

Accessed via **F9 → Left** / **F9 → Right**, or by clicking the panel title.
Both Left and Right menus are **identical** — same `create_panel_menu()` function;
only their titles differ (set dynamically by `update_menu()`).

### 2.1 Panel Mode Items

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **File listing** | `CK_PanelListing` | | ✅ | Switches the inactive panel to a normal file listing (used when it was in Quick View / Info / Tree mode to restore it to normal) |
| **Quick view** | `CK_PanelQuickView` | Ctrl+X Q | ✅ | Toggles Quick View mode on the **inactive** panel — shows a live auto-refreshed preview of the file under cursor in the active panel. The inactive panel shows file content (text / hex). Uses internal viewer logic. |
| **Info** | `CK_PanelInfo` | Ctrl+X I | ✅ | Toggles Info mode on inactive panel — shows owner, group, permissions, size, timestamps, hard-link count, inode, device of the file under cursor |
| **Tree** | `CK_PanelTree` | | ✅ | Toggles Tree mode on inactive panel — shows navigable directory tree; cursor keys navigate, Enter changes the active panel to selected dir; F2 rescans tree |
| **Panelize** | `CK_Panelize` | Ctrl+X ! | ⚠️ | Runs a shell command (prompted) and loads the output filenames as a virtual static listing in the active panel; operations work on those files; re-runs to refresh |

*(separator)*

### 2.2 Panel Configuration Items

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Listing format...** | `CK_SetupListingFormat` | | ✅ | Opens listing format dialog: choose Full (name+size+date), Brief (two-column names), Long (perm+owner+group+size+date+name), or User-defined (custom column spec string like `name:30,size:8,perm:10,mtime:12`) |
| **Sort order...** | `CK_Sort` | Ctrl+S | ✅ | Opens sort dialog: fields = Name, Extension, Modified time, Accessed time, Changed time, Size, Inode; checkboxes for Reverse, Dirs first, Case sensitive, Shell patterns |
| **Filter...** | `CK_Filter` | | ✅ | Opens filter dialog: shell glob pattern (e.g. `*.c`) applied to panel; files not matching are hidden; empty pattern = show all |
| **Encoding...** | `CK_SelectCodepage` | Alt+E | ✅ | Opens encoding selector; chosen encoding applied to filename display in panel with `#enc:NAME` VFS suffix appended to path |

*(separator)*

### 2.3 VFS Connection Items
*(conditionally compiled — shown when respective VFS is enabled)*

| Menu Item | CK_ Command | Compile flag | Status | What it does |
|-----------|-------------|--------------|--------|--------------|
| **FTP link...** | `CK_ConnectFtp` | `ENABLE_VFS_FTP` | ⚠️ | Opens FTP connection dialog; prompts for `[user[:pass]@]host[:port][/path]`; navigates active panel to that FTP URL via FTP VFS |
| **Shell link...** | `CK_ConnectShell` | `ENABLE_VFS_SHELL` | ❌ | Opens FISH (FIles over SHell) connection dialog; navigates via SSH + shell commands; not implemented |
| **SFTP link...** | `CK_ConnectSftp` | `ENABLE_VFS_SFTP` | ⚠️ | Opens SFTP connection dialog; navigates via SFTP VFS |

*(separator)*

### 2.4 Panel Refresh

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Rescan** | `CK_Reread` | Ctrl+R | ✅ | Re-reads the current panel directory from disk; resets the listing; preserves cursor position on the same filename if still present |

---

## 3. File Menu
*(source: `create_file_menu()` in `src/filemanager/filemanager.c`)*

Accessed via **F9 → File**.

### 3.1 View / Edit

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **View** | `CK_View` | F3 | ✅ | Opens current file in internal viewer (mcview); if cursor is on a directory, navigates into it |
| **View file...** | `CK_ViewFile` | | ✅ | Prompts for filename then opens it in internal viewer |
| **Filtered view** | `CK_ViewFiltered` | Alt+! | ✅ | Prompts for shell command with `%f` = current filename; pipes output to viewer (e.g. `man %f` or `hexdump %f`) |
| **Edit** | `CK_Edit` | F4 | ✅ | Opens current file in internal editor (mcedit); if no file is under cursor, prompts for filename of new file to create |

### 3.2 Copy / Move / Delete

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Copy** | `CK_Copy` | F5 | ⚠️ | Opens Copy dialog: destination (defaults to other panel path), source mask, using shell patterns toggle, preserve attributes, follow symlinks, dive into subdirs, stable symlinks, ext2 attrs; copies marked files (or current if none marked) recursively |
| **Rename/Move** | `CK_Move` | F6 | ⚠️ | Opens Move/Rename dialog: same options as Copy; for single file = rename in-place; for multiple = move to destination directory; uses rename() syscall (falls back to copy+delete on cross-device) |
| **Mkdir** | `CK_MakeDir` | F7 | ✅ | Prompts for directory name; creates it including intermediate parents if path contains `/` |
| **Delete** | `CK_Delete` | F8 | ✅ | Shows confirmation dialog listing count of marked files (or current if none); for directories asks "Delete recursively?"; deletes via remove()/rmdir() |
| **Quick cd** | `CK_CdQuick` | Alt+C | ✅ | Inline cd dialog with tab-completion; navigates active panel to typed path |

### 3.3 Permissions / Attributes

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Chmod** | `CK_ChangeMode` | Ctrl+X C | ✅ | Opens chmod dialog: checkboxes for owner/group/other read/write/execute, plus set-uid, set-gid, sticky; octal field auto-updates; "Set all" applies same mode to all marked files |
| **Chown** | `CK_ChangeOwn` | Ctrl+X O | ✅ | Opens chown dialog: owner listbox from /etc/passwd, group listbox from /etc/group; "Set all" applies to all marked |
| **Advanced chown** | `CK_ChangeOwnAdvanced` | | ✅ | Combined dialog: chmod checkboxes + chown owner/group in one screen |
| **Chattr** | `CK_ChangeAttributes` | Ctrl+X A / Ctrl+X E | ✅ | Opens ext2 file attributes dialog; shows/toggles: append-only (a), compressed (c), no-dump (d), immutable (i), journal data (j), secure-delete (s), no-tail (t), undeletable (u); uses lsattr/chattr |

### 3.4 Links

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Link** | `CK_Link` | Ctrl+X L | ✅ | Prompts for name of new hard link to create for current file |
| **Symlink** | `CK_LinkSymbolic` | Ctrl+X S | ✅ | Prompts for absolute symlink target; creates symlink with given name pointing to given target |
| **Relative symlink** | `CK_LinkSymbolicRelative` | Ctrl+X V | ✅ | Like Symlink but auto-converts absolute path to relative path from link location |
| **Edit symlink** | `CK_LinkSymbolicEdit` | Ctrl+X Ctrl+S | ✅ | Pre-fills dialog with current symlink target; user edits it; deletes old symlink and creates new one; confirmation dialog shown |

### 3.5 Selection

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Select group** | `CK_Select` | + (numpad) / Alt++ | ✅ | Opens pattern selection dialog: shell glob or regex pattern, "Files only" checkbox, "Case sensitive" checkbox; marks all matching files |
| **Unselect group** | `CK_Unselect` | - (numpad) / Alt+- | ✅ | Same dialog as Select group; unmarks all matching files |
| **Invert selection** | `CK_SelectInvert` | * (numpad) / Alt+* | ✅ | Toggles mark on every entry in the panel |

### 3.6 Exit

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Exit** | `CK_Quit` | F10 | ✅ | Quits MC; if "Confirm exit" setting is on, shows Y/N dialog; saves setup if "Auto save" is on |

---

## 4. Command Menu
*(source: `create_command_menu()` in `src/filemanager/filemanager.c`)*

Accessed via **F9 → Command**.

### 4.1 User / Utility

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **User menu** | `CK_UserMenu` | F2 | ⚠️ | Loads `~/.config/mc/menu` (or system default); shows hotkey-navigable menu; each entry is a shell command with `%f`/`%d`/etc. substitution; condition lines (`+ condition` / `= condition`) filter which entries show; executes selected entry in shell |
| **Directory tree** | `CK_Tree` | | ✅ | Opens navigable directory tree overlay (same as panel Tree mode but from menu); uses tree built from panel listings |
| **Find file** | `CK_Find` | Alt+? | ⚠️ | Opens Find File dialog: Filename (shell glob), Content (grep pattern), Start dir, Case-sensitive, Follow symlinks, Skip hidden, Date range, Size range, Ignore dirs list; real-time results list; buttons: View (F3), Edit (F4), Panelize, Again, Stop, Continue, Quit |
| **Swap panels** | `CK_Swap` | Ctrl+U | ✅ | Exchanges the directories shown in left and right panels; cursor positions preserved |
| **Switch panels on/off** | `CK_Shell` | Ctrl+O | ✅ | Suspends MC, drops to interactive shell in same directory; MC resumes when shell exits (type `exit` or Ctrl+D); if subshell enabled, shell is persistent PTY |
| **Compare directories** | `CK_CompareDirs` | Ctrl+X D | ✅ | Marks files in both panels that differ: method dialog offers Quick (names+sizes), Size-only, Thorough (byte-by-byte MD5 comparison) |
| **Compare files** | `CK_CompareFiles` | Ctrl+X Ctrl+D | ✅ | Opens internal diff viewer (mcdiff) comparing the file under cursor in left vs. right panel |
| **External panelize** | `CK_ExternalPanelize` | Ctrl+X ! | ⚠️ | Prompts for shell command (e.g. `find . -name "*.c"`); injects each output line as a filename into active panel as virtual listing |
| **Show directory sizes** | `CK_DirSize` | Ctrl+Space | ✅ | Calculates disk usage for marked directories (or current if none marked); updates the size shown in the panel listing |

### 4.2 History

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Command history** | `CK_History` | Alt+H | ✅ | Shows session command line history in popup list; Up/Down to navigate; Enter pastes selected command to command line; double-click pastes |
| **Viewed/edited files history** | `CK_EditorViewerHistory` | Alt+Shift+E | ✅ | Shows MRU list of files opened in viewer/editor this session; columns show V/E tag and path; buttons: View (open in viewer), Edit (open in editor), To panel (navigate panel to file's directory) |

### 4.3 Navigation Aids

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Directory hotlist** | `CK_HotList` | Ctrl+\ | ✅ | Shows hierarchical bookmark list; Add (Ctrl+H adds current dir), New group, Up, Remove, Goto; breadcrumb shows current group path; Enter navigates to bookmarked dir |
| **Active VFS list** | `CK_VfsList` | Ctrl+X A | ✅ | Lists all currently mounted VFS paths (FTP, SFTP, tar, etc.); Browse button changes active panel to selected VFS; Free VFSs button unmounts all; individual Free button per entry |
| **Background jobs** | `CK_Jobs` | Ctrl+X J | ✅ | Lists running/finished background copy/move operations; shows filename + status; Kill button to terminate a job; dialog auto-refreshes |
| **Screen list** | `CK_ScreenList` | Alt+\` | ❌ | Lists open editor/viewer screens (MC's internal screen multiplexer); not implemented in .NET port |

### 4.4 Editor Files

| Menu Item | CK_ Command | Shortcut | Status | What it does |
|-----------|-------------|----------|--------|--------------|
| **Edit extension file** | `CK_EditExtensionsFile` | | ✅ | Opens `~/.config/mc/mc.ext` (or creates it) in the internal editor; this file maps file extensions/patterns to open/view/edit actions |
| **Edit menu file** | `CK_EditUserMenu` | | ✅ | Opens `~/.config/mc/menu` (or creates it) in the internal editor; this is the User Menu definition file |
| **Edit highlighting group file** | `CK_EditFileHighlightFile` | | ✅ | Opens `~/.config/mc/mc.filecolor` (or creates it) in the internal editor; this file defines per-extension or per-type colour rules |

---

## 5. Options Menu
*(source: `create_options_menu()` in `src/filemanager/filemanager.c`)*

Accessed via **F9 → Options**.

| Menu Item | CK_ Command | Status | What it does |
|-----------|-------------|--------|--------------|
| **Configuration...** | `CK_Options` | ✅ | General settings: Verbose operation (show filenames during copy/move), Compute totals before starting operation, Auto save setup on exit, Show command output (Verbose), Use subshell (PTY), Ask before running program, Pause after each external command |
| **Layout...** | `CK_OptionsLayout` | ✅ | Toggle visibility of: menu bar, command line, key bar; split direction (vertical/horizontal); split percentage; "Equal split" toggle |
| **Panel options...** | `CK_OptionsPanel` | ✅ | Show hidden files (dot-files), Show backup files (`*~`, `.bak`), Show mini-status line, Lynx-like motion (←/→ navigate parent/child), Show scrollbar, File highlight (colour by type), Mix files and dirs, Case-sensitive quick search, Show free space in summary |
| **Confirmation...** | `CK_OptionsConfirm` | ✅ | Toggle per-operation confirm dialogs: Confirm delete, Confirm overwrite, Confirm execute (for executables), Confirm exit, Confirm directory hotlist delete |
| **Appearance...** | `CK_OptionsAppearance` | ✅ | Skin selector: lists all `.ini`-format skin files from `/usr/share/mc/skins/` and `~/.local/share/mc/skins/`; applies theme live on selection |
| **Learn keys...** | `CK_LearnKeys` | ✅ | Interactive key binding table; each row shows action + current binding; press Enter on a row then press a key to rebind it; tests if terminal sends the key correctly |
| **Virtual FS...** | `CK_OptionsVfs` | ✅ | VFS settings: timeout (minutes before auto-unmount of idle VFS), FTP proxy host, FTP anonymous password, Use passive mode for FTP |
| **Save setup** | `CK_SaveSetup` | ✅ | Writes all current settings to `~/.config/mc/ini` immediately; also done automatically on exit when "Auto save" is on |
| **About...** | `CK_About` | ✅ | Shows version string, copyright, website URL, and list of enabled VFS/features |

---

## 6. File Panel — Display Modes
*(toggled by Alt+T, or via Left/Right → Listing format...)*

| Mode | Function | Status | Column layout |
|------|----------|--------|---------------|
| **Full** (default) | `CK_SetupListingFormat` / `CK_PanelListing` | ✅ | Name (fills remaining), Size (right-justified), Modify time |
| **Brief** | same | ✅ | Two equal-width columns of names with `│` separator |
| **Long** | same | ✅ | Permissions, nlink, owner, group, size, modify-time, name |
| **User-defined** | same | ✅ | Custom columns specified as `field:width` pairs separated by commas, e.g. `name:30,size:8,perm:10,mtime:12,owner:8,group:8`; supported fields: `name`, `size`, `perm`/`permissions`, `type`, `mtime`/`modify`, `atime`/`access`, `ctime`/`change`, `owner`, `group`, `inode` |

### Column Header Details (Full mode)

| Column | Sort key | Click behaviour |
|--------|----------|-----------------|
| Name | `SortField.Name` | Clicking header toggles sort by name / reverse |
| Size | `SortField.Size` | Clicking header toggles sort by size / reverse |
| Modify time | `SortField.ModificationTime` | Clicking header toggles sort by mtime / reverse |
| ↑/↓ indicator | | Shows on active sort column; ↑ = ascending, ↓ = descending |

---

## 7. File Panel — Navigation & Interaction

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Move cursor up/down | ↑ / ↓, Ctrl+P / Ctrl+N | ✅ | |
| Move cursor left/right (Brief: column switch) | ← / → | ✅ | In Brief mode switches between the two columns |
| Enter — open file/enter directory | Enter | ✅ | Runs file with extension handler from mc.ext; directories enter; executables run |
| Backspace / Ctrl+PgUp — navigate to parent | Backspace / Ctrl+PgUp | ✅ | |
| Ctrl+PgDn — enter directory under cursor | Ctrl+PgDn | ✅ | |
| Left arrow — navigate to parent (Lynx mode) | ← | ⚠️ | Only when "Lynx-like motion" is on in Panel options |
| Right arrow — enter directory (Lynx mode) | → | ⚠️ | Only when "Lynx-like motion" is on |
| Page Up / Page Down | PgUp / PgDn, Ctrl+V / Alt+V | ✅ | |
| Home — jump to first entry | Home / Alt+< | ✅ | |
| End — jump to last entry | End / Alt+> | ✅ | |
| Alt+G — jump to first visible entry | Alt+G | ✅ | |
| Alt+R — jump to middle visible entry | Alt+R | ✅ | |
| Alt+J — jump to last visible entry | Alt+J | ✅ | |
| Insert / Ctrl+T — toggle mark and advance | Insert | ✅ | Marks current file, moves cursor down |
| Shift+Down — mark and move down | Shift+↓ | ✅ | |
| Shift+Up — mark and move up | Shift+↑ | ✅ | |
| * (numpad) / Alt+* — invert selection | * / Alt+* | ✅ | |
| + (numpad) / Alt++ — select group | + / Alt++ | ✅ | Pattern dialog |
| - (numpad) / Alt+- — unselect group | - / Alt+- | ✅ | Pattern dialog |
| Ctrl+R — refresh/rescan panel | Ctrl+R | ✅ | |
| Tab / Ctrl+I — switch active panel | Tab | ✅ | |
| Shift+Tab — switch panel (reverse) | Shift+Tab | ✅ | |
| Quick search (type letter) | Any printable char when panel focused | ✅ | Types into incremental search; highlights matching entry |
| Ctrl+S / Alt+S — activate quick search | Ctrl+S / Alt+S | ✅ | |
| Ctrl+Space — calculate directory size | Ctrl+Space | ✅ | Shows subdirectory disk usage in size column |
| Mouse click — move cursor | click | ✅ | |
| Mouse double-click — open entry | double-click | ✅ | |
| Mouse click on inactive panel — switch panel | click | ✅ | |
| Alt+T — cycle listing mode | Alt+T | ✅ | Full → Brief → Long → User → Full |
| Alt+O — navigate other panel to cursor dir | Alt+O | ✅ | Other panel goes to directory under cursor |
| Alt+L — navigate other panel to symlink target | Alt+L | ✅ | If cursor is on a symlink to directory |
| Alt+I — synchronise other panel to current | Alt+I | ✅ | Other panel navigates to same directory as active |

---

## 8. Function Key Bar (F1–F10)

| Key | Label | CK_ Command | Status | What it does |
|-----|-------|-------------|--------|--------------|
| F1 | Help | `CK_Help` | ⚠️ | Opens built-in help viewer with table of contents; hypertext cross-references; topic navigation history |
| F2 | Menu | `CK_UserMenu` | ⚠️ | Shows User Menu (loaded from `~/.config/mc/menu`); supports condition lines |
| F3 | View | `CK_View` | ✅ | Opens file in internal viewer; if on directory, navigates into it |
| F4 | Edit | `CK_Edit` | ✅ | Opens file in internal editor; prompts for filename if on empty space |
| F5 | Copy | `CK_Copy` | ⚠️ | Copy dialog; copies marked files (or current) to other panel or specified path |
| F6 | RenMov | `CK_Move` | ⚠️ | Rename/Move dialog |
| F7 | Mkdir | `CK_MakeDir` | ✅ | Create directory dialog |
| F8 | Delete | `CK_Delete` | ✅ | Delete with confirmation |
| F9 | PullDn | `CK_Menu` | ✅ | Opens/activates menu bar |
| F10 | Quit | `CK_Quit` | ✅ | Exits MC |
| F13 (Shift+F3) | | `CK_ViewRaw` | ✅ | View file in raw mode (no encoding interpretation) |
| F14 (Shift+F4) | | `CK_EditNew` | ✅ | Create new (empty) file in editor, prompting for filename |
| F15 (Shift+F5) | | `CK_CopySingle` | ✅ | Copy current file only (ignores marks) |
| F16 (Shift+F6) | | `CK_MoveSingle` | ✅ | Move/rename current file only |
| F18 (Shift+F8) | | `CK_DeleteSingle` | ✅ | Delete current file only (ignores marks) |
| F19 | | `CK_MenuLastSelected` | ✅ | Re-opens last-used menu with last selection highlighted |
| Mouse click on F-key label | | | ✅ | Fires the same callback as pressing the key |

---

## 9. Command Line

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Text input field | | ✅ | |
| Directory prompt (`~/path> `) | | ✅ | Truncated to last 2 path components |
| Execute command on Enter | Enter | ✅ | |
| History stored per session | | ✅ | |
| Navigate history up/down | ↑ / ↓ | ✅ | |
| Alt+P — previous history entry | Alt+P | ✅ | |
| Alt+N — next history entry | Alt+N | ✅ | |
| Alt+H / Ctrl+H — show inline history popup | Alt+H / Ctrl+H | ✅ | Popup ListView above cmdline; Enter selects; Esc closes |
| Ctrl+Enter — paste filename from active panel | Ctrl+Enter | ✅ | |
| Ctrl+Shift+Enter — paste filename from inactive panel | Ctrl+Shift+Enter | ✅ | |
| Alt+Enter / Ctrl+A (PutCurrentSelected) | Alt+Enter | ✅ | Pastes current filename to command line cursor position |
| Alt+A — put current panel path | Alt+A | ✅ | Pastes active panel's current directory path |
| Alt+Shift+A — put other panel path | Alt+Shift+A | ✅ | Pastes inactive panel's current directory path |
| Ctrl+X T — paste tagged filenames from active panel | Ctrl+X T | ✅ | Appends all marked filenames (space-separated) |
| Ctrl+X Ctrl+T — paste tagged from other panel | Ctrl+X Ctrl+T | ✅ | |
| Ctrl+X P — put active panel path | Ctrl+X P | ✅ | |
| Ctrl+X Ctrl+P — put other panel path | Ctrl+X Ctrl+P | ✅ | |
| Alt+Tab — filename / command completion | Alt+Tab | ❌ | Completes against directory listing and commands in PATH |
| Tab — completion when cmdline focused | Tab | ❌ | |
| Ctrl+A — beginning of line (Emacs) | Ctrl+A | ⚠️ | Depends on TextField widget |
| Ctrl+E — end of line | Ctrl+E | ⚠️ | |
| Ctrl+K — kill to end of line | Ctrl+K | ⚠️ | |
| Ctrl+W — kill word backwards | Ctrl+W | ⚠️ | |
| Ctrl+Y — yank killed text | Ctrl+Y | ⚠️ | |
| Alt+B — word left | Alt+B | ⚠️ | |
| Alt+F — word right | Alt+F | ⚠️ | |
| Ctrl+Q — quote next character | Ctrl+Q | ❌ | Inserts next keystroke as literal character (e.g. inserts Ctrl+C as text) |

---

## 10. Global Key Bindings
*(source: `default_filemanager_keymap[]` and `default_filemanager_x_keymap[]` in `src/keymap.c`)*

### Main Bindings

| Key | CK_ Action | Status | Notes |
|-----|-----------|--------|-------|
| Tab / Ctrl+I | `CK_ChangePanel` | ✅ | Switch active panel |
| F1 | `CK_Help` | ⚠️ | Help |
| F2 | `CK_UserMenu` | ⚠️ | User menu |
| F3 | `CK_View` | ✅ | Viewer |
| F4 | `CK_Edit` | ✅ | Editor |
| F5 | `CK_Copy` | ⚠️ | Copy |
| F6 | `CK_Move` | ⚠️ | Move/Rename |
| F7 | `CK_MakeDir` | ✅ | Mkdir |
| F8 | `CK_Delete` | ✅ | Delete |
| F9 | `CK_Menu` | ✅ | Open menu bar |
| F10 | `CK_Quit` | ✅ | Quit |
| F20 | `CK_QuitQuiet` | ✅ | Quit without confirmation |
| Alt+H | `CK_History` | ✅ | Command history popup |
| Alt+Shift+E | `CK_EditorViewerHistory` | ✅ | View/edit file history |
| Ctrl+Space | `CK_DirSize` | ✅ | Calculate directory sizes |
| Alt+A | `CK_PutCurrentPath` | ✅ | Paste current panel path to cmdline |
| Alt+Shift+A | `CK_PutOtherPath` | ✅ | Paste other panel path |
| Alt+Enter / Ctrl+Enter | `CK_PutCurrentSelected` | ✅ | Paste current filename |
| Ctrl+Shift+Enter | `CK_PutCurrentFullSelected` | ✅ | Paste full path of current file |
| Alt+C | `CK_CdQuick` | ✅ | Quick cd |
| Ctrl+\ | `CK_HotList` | ✅ | Directory hotlist |
| Ctrl+Z | `CK_Suspend` | ✅ | Suspend MC to background (SIGTSTP) |
| Alt+! | `CK_ViewFiltered` | ✅ | Filtered view |
| Alt+? | `CK_Find` | ✅ | Find file |
| Ctrl+R | `CK_Reread` | ✅ | Rescan active panel |
| Ctrl+U | `CK_Swap` | ✅ | Swap panels |
| Alt+= | `CK_SplitEqual` | ✅ | Equal split (50/50) |
| Alt+Shift+→ | `CK_SplitMore` | ✅ | Increase active panel size |
| Alt+Shift+← | `CK_SplitLess` | ✅ | Decrease active panel size |
| Ctrl+O | `CK_Shell` | ✅ | Shell (suspend to subshell) |
| Alt+. | `CK_ShowHidden` | ✅ | Toggle hidden files |
| Alt+, | `CK_SplitVertHoriz` | ✅ | Toggle vertical/horizontal split |
| Ctrl+X | `CK_ExtendedKeyMap` | ✅ | Prefix for Ctrl+X extended bindings |
| + (numpad) | `CK_Select` | ✅ | Select group |
| - (numpad) | `CK_Unselect` | ✅ | Unselect group |
| * (numpad) | `CK_SelectInvert` | ✅ | Invert selection |
| Alt+\` | `CK_ScreenList` | ❌ | Screen list |

### Ctrl+X Extended Bindings

| Key | CK_ Action | Status | Notes |
|-----|-----------|--------|-------|
| Ctrl+X D | `CK_CompareDirs` | ✅ | Compare directories |
| Ctrl+X Ctrl+D | `CK_CompareFiles` | ✅ | Compare files (diff viewer) |
| Ctrl+X A | `CK_VfsList` | ✅ | Active VFS list |
| Ctrl+X P | `CK_PutCurrentPath` | ✅ | Put current path |
| Ctrl+X Ctrl+P | `CK_PutOtherPath` | ✅ | Put other panel path |
| Ctrl+X T | `CK_PutCurrentTagged` | ✅ | Put tagged filenames |
| Ctrl+X Ctrl+T | `CK_PutOtherTagged` | ✅ | Put other panel tagged |
| Ctrl+X C | `CK_ChangeMode` | ✅ | Chmod |
| Ctrl+X O | `CK_ChangeOwn` | ✅ | Chown |
| Ctrl+X E / Ctrl+X A | `CK_ChangeAttributes` | ✅ | Chattr |
| Ctrl+X R | `CK_PutCurrentLink` | ✅ | Put current panel path as relative |
| Ctrl+X Ctrl+R | `CK_PutOtherLink` | ✅ | Put other panel path as relative |
| Ctrl+X L | `CK_Link` | ✅ | Create hard link |
| Ctrl+X S | `CK_LinkSymbolic` | ✅ | Create absolute symlink |
| Ctrl+X V | `CK_LinkSymbolicRelative` | ✅ | Create relative symlink |
| Ctrl+X Ctrl+S | `CK_LinkSymbolicEdit` | ✅ | Edit symlink |
| Ctrl+X I | `CK_PanelInfo` | ✅ | Toggle Info panel |
| Ctrl+X Q | `CK_PanelQuickView` | ✅ | Toggle Quick View |
| Ctrl+X H | `CK_HotListAdd` | ✅ | Add current dir to hotlist |
| Ctrl+X J | `CK_Jobs` | ✅ | Background jobs |
| Ctrl+X ! | `CK_ExternalPanelize` | ⚠️ | External panelize |

### Panel-Specific Bindings
*(source: `default_panel_keymap[]`)*

| Key | CK_ Action | Status | Notes |
|-----|-----------|--------|-------|
| Alt+T | `CK_CycleListingFormat` | ✅ | Cycle Full/Brief/Long/User modes |
| Alt+O | `CK_PanelOtherCd` | ✅ | Other panel → dir under cursor |
| Alt+L | `CK_PanelOtherCdLink` | ✅ | Other panel → symlink target dir |
| F13/Shift+F3 | `CK_ViewRaw` | ✅ | View file raw (no encoding) |
| F14/Shift+F4 | `CK_EditNew` | ✅ | Create new file in editor |
| F15/Shift+F5 | `CK_CopySingle` | ✅ | Copy only cursor file |
| F16/Shift+F6 | `CK_MoveSingle` | ✅ | Move only cursor file |
| F18/Shift+F8 | `CK_DeleteSingle` | ✅ | Delete only cursor file |
| Insert / Ctrl+T | `CK_Mark` | ✅ | Mark/unmark current file |
| Shift+↓ | `CK_MarkDown` | ✅ | Mark and move down |
| Shift+↑ | `CK_MarkUp` | ✅ | Mark and move up |
| Alt+Shift+H | `CK_History` | ✅ | Panel directory history dialog |
| Alt+U | `CK_HistoryNext` | ✅ | Navigate history forward |
| Alt+Y | `CK_HistoryPrev` | ✅ | Navigate history back |
| Alt+J | `CK_BottomOnScreen` | ✅ | Jump cursor to bottom of visible area |
| Alt+R | `CK_MiddleOnScreen` | ✅ | Jump cursor to middle of visible area |
| Alt+G | `CK_TopOnScreen` | ✅ | Jump cursor to top of visible area |
| Ctrl+S / Alt+S | `CK_Search` | ✅ | Start/continue quick search |
| Alt+I | `CK_PanelOtherSync` | ✅ | Sync other panel to active panel dir |
| Alt+E | `CK_SelectCodepage` | ✅ | Change panel encoding |

---

## 11. Built-in Viewer (mcview / F3)
*(source: `src/viewer/`)*

### 11.1 Navigation

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Scroll down one line | ↓ / J / Enter | ✅ | |
| Scroll up one line | ↑ / K / Y | ✅ | |
| Scroll down one page | Space / PgDn / Ctrl+V / F | ✅ | |
| Scroll up one page | PgUp / B / Alt+V | ✅ | |
| Go to beginning | G (lowercase) / Home / Ctrl+Home | ✅ | |
| Go to end | G (uppercase) / End / Ctrl+End | ✅ | |
| Scroll right | → / L | ✅ | Horizontal scroll in no-wrap mode |
| Scroll left | ← / H | ✅ | |
| Go to byte offset / line number | F5 | ✅ | In hex mode: byte offset; in text mode: line number |
| Next file in directory | Ctrl+F | ✅ | Cycles through files in same directory |
| Previous file in directory | Ctrl+B | ✅ | |
| Close viewer | F10 / Esc / Q | ✅ | |

### 11.2 Display Modes

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Toggle text word-wrap | F2 | ✅ | Wraps long lines at screen width |
| Toggle hex/text mode | F4 | ✅ | Hex shows offset + hex bytes + ASCII repr |
| Toggle raw mode | F8 | ✅ | Shows control chars as `.` instead of interpreting them |
| Toggle nroff formatting | F9 | ✅ | Strips `char\bchar` bold/underline sequences used by man pages |
| Toggle column ruler | Alt+R | ✅ | Shows ruler line with column numbers below content |
| Change encoding | Alt+E | ✅ | Opens encoding selection dialog; reloads file in chosen charset |
| F1 — viewer-specific help | F1 | ✅ | Shows viewer key bindings help dialog |

### 11.3 Search

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Search forward | F7 / / | ✅ | Dialog: pattern, case-sensitive, regex checkboxes |
| Search backward | Shift+F7 | ✅ | Same dialog with Backward flag |
| Repeat last search forward | N | ✅ | |
| Repeat last search backward | Shift+N | ✅ | |
| Regex search | | ✅ | `^`, `$`, `.`, `*`, `[...]` etc. |
| Hex pattern search | | ✅ | Enter hex bytes as `\xNN` or raw hex string |
| Highlight matched text | | ✅ | First match highlighted in cyan |

### 11.4 Bookmarks (0–9)

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Set bookmark 0 | Ctrl+B | ✅ | |
| Go to bookmark 0 | Ctrl+P | ✅ | |
| Set bookmark n (0–9) | [n]m (digit then m) | ✅ | Digit prefix selects bookmark slot |
| Go to bookmark n (0–9) | [n]r (digit then r) | ✅ | |
| Per-file settings remembered within session | | ✅ | mode, wrap, nroff remembered per filepath |

---

## 12. Built-in Editor (mcedit / F4)
*(source: `src/editor/`)*

### 12.1 File Operations

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Open existing file | | ✅ | Pass on command line or from panel |
| Create new file | F4 on empty slot / Ctrl+N / F14 | ✅ | Prompts for filename |
| Open file dialog | Ctrl+O | ❌ | Not implemented |
| Save | F2 / Ctrl+S | ✅ | Saves in-place; shows confirmation if file changed on disk |
| Save As | Shift+F2 | ✅ | Prompts for new filename |
| Close / quit | F10 / Esc | ✅ | Prompts "save?" if modified |
| "Modified" indicator `*` in status bar | | ✅ | |
| Auto-detect line endings (LF / CRLF) | | ✅ | Preserves original line endings |

### 12.2 Cursor Movement

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Move cursor | ↑ ↓ ← → | ✅ | |
| Word left / word right | Ctrl+← / Ctrl+→ | ✅ | Skips over word characters |
| Beginning / end of line | Home / End | ✅ | |
| Beginning / end of file | Ctrl+Home / Ctrl+End | ✅ | |
| Page up / page down | PgUp / PgDn | ✅ | |
| Go to line number | Ctrl+G / Alt+L | ✅ | Prompts for line number; scrolls to it |
| Go to matching bracket | Alt+B in some configs | ❌ | |

### 12.3 Editing

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Insert / overwrite mode toggle | Insert | ✅ | Status bar shows INS / OVR |
| Backspace / Delete | Backspace / Del | ✅ | |
| Delete line | Ctrl+Y | ✅ | Cuts entire line to clipboard |
| Delete to end of line | Alt+Y / Shift+F5? | ⚠️ | |
| Delete word right | Alt+D | ✅ | |
| Delete word left | Alt+BackSpace | ✅ | |
| Enter — new line with auto-indent | Enter | ✅ | Matches indent of previous line |
| Shift+Enter — new line without indent | Shift+Enter | ✅ | |
| Tab — insert tab or spaces | Tab | ✅ | Uses "tab expansion" setting |
| Ctrl+D — insert current date/time | Ctrl+D | ✅ | Inserts formatted timestamp |
| Ctrl+Q — insert literal character | Ctrl+Q | ❌ | |
| Transpose characters | Ctrl+T in some builds | ❌ | |

### 12.4 Selection & Clipboard

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Start/extend stream selection | Shift+Arrow | ✅ | Highlight arbitrary text region |
| Toggle column/rectangular selection | Alt+B / F19? | ✅ | Selects a rectangular block across lines |
| Copy selection to clipboard | Ctrl+C / Ctrl+Insert | ✅ | |
| Cut selection | Ctrl+X / Shift+Del | ✅ | |
| Paste clipboard | Ctrl+V / Shift+Insert | ✅ | |
| Select all | Ctrl+A | ✅ | |
| Copy column block | F5 (in column mode) | ✅ | |
| Cut column block | F6 (in column mode) | ✅ | |
| Paste column block | Ctrl+V (in column mode) | ✅ | Inserts each row at appropriate column |

### 12.5 Search & Replace

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Find dialog | F7 | ✅ | Pattern, case-sensitive, whole word, regex, backward; pre-fills last search |
| Find again (no dialog) | Shift+F7 / Ctrl+L | ✅ | Repeats last search forward |
| Replace dialog | F4 / Ctrl+H | ✅ | Search + replace pattern; "Replace all" button |
| Replace again | Shift+F4 | ❌ | Not implemented |

### 12.6 Undo / Redo / Macros

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Undo | Ctrl+Z / Ctrl+U | ✅ | Unlimited undo stack |
| Redo | Ctrl+Y / Ctrl+R | ✅ | Note: original uses Ctrl+R for macro record; port uses it for redo |
| Start/stop macro recording | Ctrl+R (original) | ❌ | Not implemented; key is used for Redo |
| Play back last macro | Ctrl+E (original) | ❌ | |
| Word completion (Ctrl+Tab) | Ctrl+Tab | ❌ | Scans buffer for completions |
| Spell check | Ctrl+F5 | ✅ | Invokes `aspell -a`; shows suggestion dialog |

### 12.7 Display

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Toggle syntax highlighting | Ctrl+T | ✅ | Status bar shows "NoHL" when off |
| Toggle line number gutter | Via F9 menu | ✅ | Status bar shows "Nums" when on |
| Status bar: filename, line/col, INS/OVR, modified | | ✅ | `file | Ln N, Col N | INS | Modified` |
| Status bar: COL when column mode active | | ✅ | |

### 12.8 Editor F9 Menu

| Item | Status | Notes |
|------|--------|-------|
| Save | ✅ | |
| Save As | ✅ | |
| Find | ✅ | |
| Replace | ✅ | |
| Go to line | ✅ | |
| Toggle line numbers | ✅ | |
| Toggle syntax highlighting | ✅ | |
| Spell check | ✅ | |
| Close | ✅ | |

---

## 13. Built-in Diff Viewer (mcdiff)
*(source: `src/diffviewer/`)*

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Open two files side-by-side | | ✅ | Left panel file vs. right panel file |
| Scroll down one line | ↓ | ✅ | |
| Scroll up one line | ↑ | ✅ | |
| Page down | PgDn | ✅ | |
| Page up | PgUp | ✅ | |
| Next change hunk | N / F7 | ✅ | |
| Previous change hunk | P / F8 | ✅ | |
| Close diff viewer | Q / F10 / Esc | ✅ | |
| Edit left file | F4 | ✅ | Opens in internal editor |
| Edit right file | F14/Shift+F4 | ✅ | |
| Save merged result | F2 | ⚠️ | |
| Syntax highlighting of diff hunks | | ✅ | Added/removed/changed lines in different colours |
| Status bar: change count + navigation info | | ✅ | |

---

## 14. Virtual File System (VFS)

### Architecture

| Feature | Status | Notes |
|---------|--------|-------|
| Pluggable `IVfsProvider` interface | ✅ | `src/Mc.Core/Vfs/IVfsProvider.cs` |
| VFS path with scheme (`local://`, `ftp://`, `sftp://`, `tar://`, `zip://`, `cpio://`, `extfs://`, `sfs://`) | ✅ | |
| Path format: `scheme:///archive|inner/path` for archive VFSs | ✅ | Uses `|` as separator between archive path and inner path |
| `#enc:NAME` encoding suffix in paths | ✅ | Applied to local paths via panel encoding dialog |
| Navigate into archives as virtual directories | ✅ | Panel calls `ListDirectory()` on VFS provider |
| VFS cache with configurable timeout | ✅ | `VfsCache` class |
| VFS registry (`VfsRegistry`) routes calls to correct provider | ✅ | |

### VFS Providers

| Provider | Scheme | Status | Notes |
|----------|--------|--------|-------|
| Local filesystem | `local://`, `file://` | ✅ | `LocalVfsProvider`; handles all native filesystem operations |
| FTP | `ftp://` | ⚠️ | `FtpVfsProvider` using `FtpWebRequest`; not registered by default |
| SFTP | `sftp://` | ⚠️ | `SftpVfsProvider` using SSH.NET; not registered by default |
| TAR archives (`.tar`, `.tar.gz`, `.tar.bz2`, `.tar.xz`) | `tar://` | ⚠️ | `TarVfsProvider`; uses `System.Formats.Tar` + decompression streams |
| ZIP archives (`.zip`) | `zip://` | ⚠️ | `ZipVfsProvider`; uses `System.IO.Compression.ZipFile`; read-only |
| CPIO archives (`.cpio`, `.rpm`) | `cpio://` | ⚠️ | `CpioVfsProvider`; parses SVR4 newc format (magic `070701`/`070702`); RPM payload extraction via gzip scan |
| External scripts (`extfs.d/`) | `extfs://` | ⚠️ | `ExtfsVfsProvider`; scans `/usr/lib/mc/extfs.d/`; invokes scripts with `list`/`copyout` commands; parses `ls -l` output |
| SFS (single-file filesystem) | `sfs://` | ⚠️ | `SfsVfsProvider`; reads `mc.sfs` config; mounts archives to temp dirs via external helpers |
| FISH (FIles over SHell) | `fish://` | ❌ | Not implemented; would use SSH + shell commands |

---

## 15. File Operations (F5 / F6 / F8)

### 15.1 Copy (F5) — `FileOperations.CopyAsync()`

| Feature | Status | Notes |
|---------|--------|-------|
| Copy single file (cursor entry when nothing marked) | ✅ | |
| Copy multiple marked files | ✅ | |
| Copy directory recursively | ✅ | `CopyDirectoryAsync()` recurses into all subdirs |
| Destination defaults to other panel path | ✅ | |
| User-editable destination field | ✅ | |
| Source mask / From: pattern field | ✅ | Filters which marked files to copy |
| Using shell patterns toggle | ✅ | |
| Preserve attributes (timestamps, permissions) | ✅ | `PreserveAttrs()` copies mtime/atime/mode |
| Overwrite confirmation dialog per-file | ✅ | |
| Overwrite all / Skip all per-session | ✅ | |
| Follow symlinks (copy target, not link) | ✅ | |
| Dive into subdirectory if destination exists | ✅ | |
| Stable symlinks (convert absolute to relative) | ✅ | `MakeRelativeSymlinkTarget()` |
| Preserve ext2 attributes (lsattr/chattr) | ✅ | `TryCopyExt2Attributes()` via lsattr+chattr; Linux only |
| Background copy operation | ✅ | Runs as `Task`; progress reported via `IProgress<>` |
| Progress dialog with file/byte counts + percentage | ✅ | |
| Cancel operation | ✅ | `CancellationToken` |
| Truncate destination file on overwrite | ✅ | `LocalVfsProvider.OpenWrite` uses `FileMode.Create` |

### 15.2 Move / Rename (F6) — `FileOperations.MoveAsync()`

| Feature | Status | Notes |
|---------|--------|-------|
| Rename single file (pre-fills current name) | ✅ | |
| Move multiple marked files | ✅ | |
| Source mask field | ✅ | |
| Same-device move via `rename()` / `File.Move()` | ✅ | |
| Same-device directory move via `Directory.Move()` | ✅ | Fixed — was using `File.Move()` which fails for dirs |
| Cross-device move fallback (copy+delete) for files | ✅ | |
| Cross-device move fallback (copy+delete) for directories | ✅ | Fixed — now uses `CopyDirectoryAsync` + `DeleteDirectory` |
| Background move | ✅ | |
| Progress dialog | ✅ | |

### 15.3 Delete (F8) — `FileOperations.DeleteAsync()`

| Feature | Status | Notes |
|---------|--------|-------|
| Delete single file | ✅ | |
| Delete multiple marked files | ✅ | |
| Primary confirmation dialog | ✅ | |
| Secondary per-directory "Delete recursively?" confirmation | ✅ | |
| Recursive directory deletion | ✅ | `Directory.Delete(path, recursive: true)` |
| Progress dialog | ✅ | |
| Cancel operation | ✅ | |

### 15.4 Mkdir (F7)

| Feature | Status | Notes |
|---------|--------|-------|
| Create single directory | ✅ | |
| Recursive parent creation (mkdir -p) | ✅ | `Directory.CreateDirectory()` creates all parents |

---

## 16. Find File Dialog (Alt+?)

| Feature | Status | Notes |
|---------|--------|-------|
| Filename pattern (shell glob, `*` and `?`) | ✅ | |
| Content search (grep-like text pattern) | ✅ | |
| Case-sensitive toggle (for both filename and content) | ✅ | |
| Use regular expressions toggle | ✅ | |
| Start directory field | ✅ | Defaults to active panel directory |
| Follow symlinks toggle | ✅ | |
| Skip hidden directories (dot-dirs) | ✅ | |
| Date/time filter (modified before/after) | ❌ | Not implemented |
| File size filter (larger/smaller than N bytes) | ❌ | Not implemented |
| Ignore directories list | ❌ | Not implemented |
| Real-time incremental results list | ✅ | Results shown as search progresses |
| Suspend / Continue search | ✅ | Stop button pauses; Continue resumes |
| View found file (F3) | ✅ | |
| Edit found file (F4) | ✅ | |
| Navigate panel to found file's directory | ✅ | |
| Panelize results into active panel | ⚠️ | Injects matching files into panel as virtual listing |
| Again button — reopen search dialog | ✅ | |

---

## 17. Directory & Navigation Features

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Directory hotlist (bookmarks) | Ctrl+\ | ✅ | Hierarchical groups; Add/Remove/New group/Goto |
| Add current dir to hotlist | Ctrl+X H | ✅ | Adds active panel path to hotlist |
| Per-panel directory history | Alt+Y / Alt+U | ✅ | Each panel has independent history stack |
| Directory history dialog | Alt+H | ✅ | Shows full history; Enter navigates there |
| Swap panels | Ctrl+U | ✅ | Exchange left↔right panel directories |
| Synchronise panels (active → inactive) | Alt+I | ✅ | Inactive panel navigates to active panel's dir |
| Compare directories | Ctrl+X D | ✅ | Quick / Size-only / Thorough |
| Show directory sizes | Ctrl+Space | ✅ | Calculates and shows sizes for marked dirs |
| Quick CD | Alt+C | ✅ | Prompts for directory path with completion |
| Directory tree panel mode | | ✅ | F9 → Left/Right → Tree |
| Tree: F2 rescan | F2 (in tree) | ✅ | |
| Tree: F8 delete dir | F8 (in tree) | ✅ | |
| Navigate other panel to dir under cursor | Alt+O | ✅ | |
| Navigate other panel to symlink target | Alt+L | ✅ | |

---

## 18. Subshell Integration

| Feature | Key | Status | Notes |
|---------|-----|--------|-------|
| Suspend MC, open interactive shell | Ctrl+O | ✅ | Uses `System.Diagnostics.Process` to launch `$SHELL` |
| Shell inherits panel's current directory | | ✅ | `WorkingDirectory` set to active panel path |
| Return to MC when shell exits | | ✅ | MC resumes after process exits |
| Subshell typed command piped (type cmd then Ctrl+O) | | ⚠️ | |
| Shell prompt shown in command line | | ⚠️ | Simplified `path> ` prompt (not real shell prompt) |
| Multiple subshell screens / screen list | Alt+\` | ❌ | Not implemented |

---

## 19. User Menu (F2)
*(source: `src/usermenu.c`)*

| Feature | Status | Notes |
|---------|--------|-------|
| Load from `~/.config/mc/menu` (user) or `/etc/mc/mc.menu` (system) | ✅ | |
| Display menu entries with hotkey letters | ✅ | First char of each entry is hotkey |
| Execute entry by pressing hotkey letter | ✅ | Runs associated shell command |
| Condition lines (`+` / `=` prefix) filter entries | ✅ | `f pattern` = current file matches; `d` = directory; `!` = negate |
| `%f` macro — current filename | ✅ | |
| `%d` macro — current directory | ✅ | |
| `%p` macro — current filename (full path) | ⚠️ | |
| `%s` macro — selected/tagged files | ⚠️ | |
| `%t` macro — tagged filenames | ⚠️ | |
| `%b` macro — filename without extension | ⚠️ | |
| `%n` macro — filename stripped of leading dot | ⚠️ | |
| `%e` macro — file extension | ⚠️ | |
| `%l` macro — symlink target | ⚠️ | |
| `%x` macro — filename stripped of extension | ⚠️ | |
| Edit user menu file | | ✅ | Opens in internal editor |

---

## 20. Configuration System

| Feature | Status | Notes |
|---------|--------|-------|
| Config file: `~/.config/mc/ini` | ✅ | INI format with `[Section]` headers |
| Sections: `[Midnight-Commander]`, `[Panels]`, `[Layout]`, `[Colors]` | ✅ | |
| Auto-save on exit (when "Auto save setup" on) | ✅ | |
| Save setup explicitly (Options → Save setup) | ✅ | |
| Save/restore panel paths | ✅ | |
| Save/restore sort order per panel | ✅ | |
| Save/restore active skin | ✅ | |
| Skin/theme system (INI-format `.ini` skin files) | ✅ | |
| System skins from `/usr/share/mc/skins/` | ✅ | |
| User skins from `~/.local/share/mc/skins/` | ✅ | |
| Extension file (`mc.ext`) — file open/view/edit rules | ✅ | Pattern matching by file extension |
| File highlight/colour file (`mc.filecolor`) | ✅ | Per-extension colour overrides |
| Key bindings configurable (key binding file) | ⚠️ | Display table shown; runtime rebinding via Learn keys dialog |
| Hints file (`~/.config/mc/hints`) | ❌ | Rotating tip bar not implemented |

---

## 21. File Type Colour Coding

| Type | Colour (default Blue skin) | Status |
|------|---------------------------|--------|
| Regular file | Gray on Blue | ✅ |
| Directory | White on Blue | ✅ |
| Executable | Bright Green on Blue | ✅ |
| Symlink | Cyan on Blue | ✅ |
| Archive / compressed (`.tar`, `.gz`, `.zip`, etc.) | Bright Cyan on Blue | ✅ |
| Marked file | Bright Yellow on Blue | ✅ |
| Cursor / selected (active panel) | Black on Cyan | ✅ |
| Cursor (inactive panel) | White on Blue | ✅ |
| Marked + cursor (active) | Bright Yellow on Cyan | ✅ |
| Block device | Bright Magenta on Blue | ✅ |
| Character device | Magenta on Blue | ✅ |
| FIFO / named pipe | Dark Gray on Blue | ✅ |
| Unix socket | Bright Magenta on Blue | ✅ |
| Symlink-to-directory | Directory colour (White) | ✅ |
| Status bar | Black on Cyan | ✅ |
| Panel header | Bright Yellow on Blue | ✅ |
| Active sort column header | Bold/highlighted | ✅ |

---

## 22. Miscellaneous / Port-specific Features

| Feature | Status | Notes |
|---------|--------|-------|
| Viewed/edited file history (MRU) | ✅ | |
| External panelize (inject shell output into panel) | ✅ | |
| Active VFS list with Browse / Free VFSs | ✅ | |
| Background file operations with job manager | ✅ | |
| Confirmation settings (delete, overwrite, exit) | ✅ | |
| "Verbose operation" (show filenames during ops) | ✅ | |
| "Compute totals" before starting operation | ✅ | |
| Quick view panel (live file preview) | ✅ | |
| Info panel (live file attributes) | ✅ | |
| Batch rename dialog | ✅ | Port-specific extra feature; `Tools` menu |
| "About..." dialog with version/copyright | ✅ | |

---

## 23. Not Implemented / Out of Scope

| Feature | Reason |
|---------|--------|
| GPM mouse (Linux GPM daemon) | Obsolete; xterm mouse protocol used instead |
| Console saver (`cons.saver.c`) | Linux VT-specific, obsolete |
| FISH protocol (FIles over SHell) | Complex; SSH/SFTP is preferred |
| Multiple subshell screens | Complex TUI multiplexing not in scope |
| Macro recording in editor (Ctrl+R) | Ctrl+R is bound to Redo in port |
| Word completion in editor (Ctrl+Tab) | Not yet implemented |
| Hints bar (rotating tips) | Not yet implemented |
| Date/size filters in Find dialog | Not yet implemented |
| Ext2/3/4 native attribute ioctl (beyond chattr) | `chattr` covers it |
| Command line Tab/Alt+Tab completion | Not yet implemented |
| Ctrl+Q quote-char in cmdline | Not yet implemented |
| Replace again (Shift+F4) in editor | Not yet implemented |
| Go to matching bracket in editor | Not yet implemented |
| Listmode editor (LISTMODE_EDITOR compile flag feature) | Niche; not enabled in standard builds |

---

*Last updated: 2026-03-02*
*Based on GNU MC 4.8.x source: https://github.com/MidnightCommander/mc*
*Source files: `src/filemanager/filemanager.c`, `src/keymap.c`, `src/viewer/`, `src/editor/`*
