# Function Analysis — Midnight Commander .NET Port vs. Original GNU MC

**Legend**
- ✅ **Matches** — behaviour is functionally equivalent to the original GNU MC
- ⚠️ **Partial** — core behaviour works but specific details differ
- ❌ **Stub / Not implemented** — menu item exists but functionality is missing or trivially incomplete

Reference: original MC source at <https://github.com/MidnightCommander/mc>

---

## Button Bar (F1 – F10)

| Key | Label | Status | Differences |
|-----|-------|--------|-------------|
| F1 | Help | ⚠️ Partial | Section-based viewer with Contents/Back navigation implemented. Missing: full hyperlink system (ctrl-char links between nodes), bold/italic text rendering, mouse-clickable links. Original MC uses a binary `.hlp` format; we use plain-text sections. |
| F2 | Menu | ⚠️ Partial | User menu shown with hotkey letters, direct letter-key execution, and "Edit menu" button. Missing: `+`/`=` **condition lines** (e.g. `+ f text/*` restricts entry to text files) are silently skipped instead of evaluated and filtering entries. "Using shell patterns" scope is not evaluated. |
| F3 | View | ✅ Matches | Views file in internal viewer; navigates into directory if cursor is on a directory — matches `do_view_cmd()`. |
| F4 | Edit | ⚠️ Partial | Opens internal editor for the selected file. Missing: `edit_cmd_new()` path — pressing F4 with no file selected should open the editor prompting for a new filename; currently opens editor with a blank path. |
| F5 | Copy | ⚠️ Partial | Copy dialog with destination and checkboxes. Missing: **source file-mask / pattern field** ("Using shell patterns" toggle, regex support), "Preserve ext2 attributes" checkbox, "Dive into subdirectory if exists" checkbox, "Stable symlinks" checkbox, **Background** button for background copy operation. |
| F6 | RenMov | ⚠️ Partial | Single-file pre-fills filename for rename; multiple marks pre-fill other panel path. Missing: same options as F5 (source mask, shell patterns, background button, ext2 attrs, dive into subdir). |
| F7 | Mkdir | ✅ Matches | Title "Create a new Directory", prompt "Enter directory name:". Supports recursive parent creation. |
| F8 | Delete | ⚠️ Partial | Deletes marked files with confirmation. Missing: when deleting a directory the original shows a secondary confirmation "Delete directory … recursively?" before recursing; our implementation deletes recursively without the extra prompt. |
| F9 | PullDn | ✅ Matches | Opens the top menu bar (`MenuBar.OpenMenu()`). |
| F10 | Quit | ✅ Matches | Asks for confirmation (if enabled), saves panel paths, quits. |

---

## Left / Right Panel Menu

Both panels share identical menus (`PanelMenuItems(bool left)`).

| Menu Item | Status | Differences |
|-----------|--------|-------------|
| **File listing** | ⚠️ Partial | Shows a two-option radio (Full / Brief). Original MC offers full column-layout configuration: Long, Half, Brief, Full, User-defined column widths; our dialog only toggles Brief ↔ Full. |
| **Quick view** | ⚠️ Partial | Opens the full-screen viewer. Original MC switches the **inactive panel** to a permanent quick-view mode that updates as the cursor moves — our implementation opens a one-shot full-screen viewer instead. |
| **Info** | ⚠️ Partial | Opens `InfoDialog` showing file properties. Original MC switches the panel to **Info mode** (permanent panel showing disk usage, file attributes for the cursor selection); our version shows a modal dialog. |
| **Tree** | ⚠️ Partial | Shows a collapsible tree dialog. Original MC switches the panel to **Tree mode** (persistent, navigable tree widget replacing the file list). Our implementation is a modal dialog and does not replace the panel. |
| **Panelize** | ⚠️ Partial | Prompts for a shell command and shows file count returned. Missing: actual **virtual panel injection** — the original replaces the panel listing with the command's output filenames so they can be operated on with F5/F6/F8 etc. |
| **Listing format...** | ⚠️ Partial | Same as "File listing" above — only Full/Brief; missing Long, User-defined column layout. |
| **Sort order...** | ✅ Matches | Supports all sort fields (Name, Ext, Size, MTime, ATime, CTime, Owner, Group, Inode), Reverse, Directories first, Case sensitive. |
| **Filter...** | ✅ Matches | Prompts for pattern, applies to active panel listing. |
| **Encoding...** | ⚠️ Partial | Shows a list of 16 common encodings and reloads with `#enc:` suffix in the VFS path. Original MC supports any iconv-known encoding and integrates with the VFS layer; our list is hardcoded. |
| **FTP link...** | ⚠️ Partial | Shows URL input dialog and attempts to navigate via VfsPath. Functional only if an FTP VFS provider is registered; no provider ships by default, so navigation fails silently. Original MC has a built-in FTPFS. |
| **Shell link...** | ❌ Not implemented | Shows "not implemented" message. Original MC implements the FISH (FIles transferred over SHell) protocol. |
| **SFTP link...** | ⚠️ Partial | Same as FTP link — dialog exists but no SFTP VFS provider ships. |
| **Rescan** (Ctrl+R) | ✅ Matches | Reloads the panel directory listing. |

---

## File Menu

| Menu Item | Status | Differences |
|-----------|--------|-------------|
| **View** (F3) | ✅ Matches | See F3 above. |
| **View file...** | ✅ Matches | Prompts for a filename, opens in internal viewer. |
| **Filtered view** | ⚠️ Partial | Prompts for a filter command and pipes the file through it before viewing. Missing: original passes the current filename as argument; our implementation shells the command and captures stdout, but does not pass `%f` automatically. |
| **Edit** (F4) | ⚠️ Partial | See F4 above. |
| **Copy** (F5) | ⚠️ Partial | See F5 above. |
| **Chmod** (Ctrl+X C) | ⚠️ Partial | Checkbox grid for rwx bits + octal input. Missing: **special bits** (setuid, setgid, sticky); "Set all" / "Set marked" / "Clear marked" multi-file buttons; the original iterates through all marked files sequentially, one dialog per file. |
| **Link** | ✅ Matches | Prompts for link name, creates hard link via `ln`. |
| **Symlink** | ✅ Matches | Prompts for link name, creates absolute symlink. |
| **Relative symlink** | ✅ Matches | Creates symlink using `Path.GetRelativePath`. |
| **Edit symlink** | ⚠️ Partial | Reads current target, prompts for new target, recreates symlink. Missing: original asks "do you want to update the symlink?" as a confirmation step before modifying; our implementation edits without that secondary confirmation. |
| **Chown** (Ctrl+X O) | ⚠️ Partial | Free-text owner/group inputs. Missing: original uses **listboxes** populated from `/etc/passwd` and `/etc/group` so the user can pick from existing users/groups; we only provide text fields. Also missing "Set all" / "Set groups" / "Set users" multi-file buttons. |
| **Advanced chown** | ⚠️ Partial | Shows ChownDialog then ChmodDialog sequentially. Same limitations as Chown and Chmod above. Original has a single combined dialog with owner/group listboxes alongside the permission checkboxes. |
| **Rename/Move** (F6) | ⚠️ Partial | See F6 above. |
| **Mkdir** (F7) | ✅ Matches | See F7 above. |
| **Delete** (F8) | ⚠️ Partial | See F8 above. |
| **Quick cd** | ✅ Matches | Prompts for a directory path, navigates active panel. |
| **Select group** (+) | ⚠️ Partial | Prompts for a pattern, marks matching files. Missing: "Files only" checkbox, "Case sensitive" checkbox, "Using shell patterns" checkbox — original shows a dedicated dialog with these options. |
| **Unselect group** (-) | ⚠️ Partial | Same limitations as "Select group". |
| **Invert selection** (*) | ✅ Matches | Inverts marking on all entries. |
| **Exit** (F10) | ✅ Matches | See F10 above. |

> **Note:** Original MC also has a **"Chattr"** menu item (change ext2 file attributes) that only appears on Linux with ext2fs support. This item is absent from the .NET port.

---

## Command Menu

| Menu Item | Status | Differences |
|-----------|--------|-------------|
| **User menu** (F2) | ⚠️ Partial | See F2 above. |
| **Directory tree** | ⚠️ Partial | Modal collapsible tree dialog. Original MC switches the active panel to Tree mode. Additionally the original tree is a full widget with F-key bindings (F2 rescan, F8 delete dir, etc.) and persistent expand/collapse state; ours is a one-shot dialog. |
| **Find file** | ⚠️ Partial | Pattern + content search, results in a list, Go-to navigates. Missing: **Start directory browser** (tree button), "Follow symlinks" option, "Skip hidden dirs" option, "Ignore directories" list, real-time search progress with Suspend/Continue buttons, **Panelize** button to load results into the panel, **View (F3)** / **Edit (F4)** buttons operating on the found file, **Again** button to restart search. Our results are computed in one shot (max 500 files) without progress display. |
| **Swap panels** (Ctrl+U) | ✅ Matches | Exchanges left and right panel directories. |
| **Switch panels on/off** (Ctrl+O) | ✅ Matches | Suspends TUI, launches shell, resumes on exit. |
| **Compare directories** | ⚠️ Partial | Marks files that differ by name or size. Missing: original offers three methods via a query dialog: **Quick** (timestamps), **Size only**, **Thorough** (byte-by-byte). Our implementation always uses name+size comparison with no method selector. |
| **Compare files** | ⚠️ Partial | Opens internal diff viewer for current left/right entries. Missing: if no file is selected in one panel, original falls back gracefully; our implementation silently does nothing when either entry is null or is a directory. Original also supports `mcdiff` external tool as a fallback. |
| **External panelize** | ⚠️ Partial | Runs shell command, captures output file list, shows count. Missing: actual panelization — the file list is NOT injected into the panel so F5/F6/F8 cannot operate on the results. This is explicitly documented as "not yet implemented" in the code. |
| **Show directory sizes** | ✅ Matches | Computes sizes for marked directories (or current if none marked), shows dialog. |
| **Command history** | ⚠️ Partial | Shows session + shell history, pastes to command line. Missing: original integrates command history directly into the command line widget as an in-line history box (not a separate dialog); also does not execute on selection — matches our "paste only" behaviour. The history source is identical. |
| **Viewed/edited files history** | ✅ Matches | Shows MRU list of viewed and edited files with View / Edit / Panel buttons. |
| **Directory hotlist** (Ctrl+\) | ⚠️ Partial | List with Go-to / Add / Remove. Missing: **hierarchical groups** (New group, Move entry between groups), "New entry" button for arbitrary path, "Up" button to navigate to parent group. Original stores entries with `GROUP`/`ENTRY`/`ENDGROUP` blocks; our hotlist is a flat list. |
| **Active VFS list** | ⚠️ Partial | Detects remote panel paths, offers Browse / Free VFSs. Missing: original shows each mounted VFS as a separate line (path + type + connection info); we only show the panel paths. No VFS providers ship so in practice the list is always empty. |
| **Background jobs** | ⚠️ Partial | Shows a static informational message. Original MC shows running background operations with file counts and progress. Our port runs all operations as foreground async tasks, so there are no background jobs to list. |
| **Screen list** | ❌ Not implemented | Shows "not implemented". Original MC supports multiple simultaneous pseudo-terminal screens (subshells) accessible via a screen manager dialog. |
| **Edit extension file** | ✅ Matches | Opens `~/.config/mc/mc.ext` in the internal editor (creates from system template if missing). |
| **Edit menu file** | ✅ Matches | Opens `~/.config/mc/menu` in the internal editor (creates default if missing). |
| **Edit highlighting group file** | ✅ Matches | Opens `~/.config/mc/mc.filecolor` in the internal editor. |

> **Note:** Original MC also has a conditional **"Listing format edit"** item (only on some builds). Not present in the .NET port.

---

## Options Menu

| Menu Item | Status | Differences |
|-----------|--------|-------------|
| **Configuration...** | ⚠️ Partial | Covers internal viewer/editor toggle and external editor/viewer paths. Missing: many original settings — "Verbose operation", "Compute totals", "Use shell patterns", "Auto save setup", "Drop caches after copy/move/delete", "Show output of commands", "Use internal diff viewer", "Ask before running programs", "Midnight Commander keyboard shortcuts", "Subshell usage", "Always show mini status". |
| **Layout...** | ⚠️ Partial | Vertical vs. horizontal split toggle. Missing: show/hide individual UI regions (menubar, command line, key bar, hintbar), panel split ratio slider, "Equal split" toggle. |
| **Panel options...** | ⚠️ Partial | Show hidden files, show backup files, mark moves cursor down. Missing: "Show mini status", "Use Lynx-like motion" (left arrow goes to parent), "Scrollbar in panels", "File highlighting" (by type/permissions), "Mix all files" (dirs and files interleaved), "Quick search" mode selection (case sensitive / case insensitive), "Navigate tree", "Real path of symlinks", panel display of free space. |
| **Confirmation...** | ✅ Matches | Confirm delete, confirm overwrite, confirm exit — all three respected throughout the application. |
| **Appearance...** | ❌ Not implemented | Shows "not implemented". Original MC supports skin files (INI-based colour themes). |
| **Learn keys...** | ❌ Not implemented | Shows "not implemented". Original MC provides an interactive key-binding editor where each action can be re-bound to any key. |
| **Virtual FS...** | ❌ Not implemented | Shows "not implemented". Original shows a VFS settings dialog (cache timeout, FTP proxy, etc.). |
| **Save setup** | ✅ Matches | Persists current settings to `~/.config/mc/ini`. |
| **About...** | ✅ Matches | Shows version, description, and copyright information. |

---

## Key Bindings (not in menus)

| Binding | Status | Differences |
|---------|--------|-------------|
| **Tab / Shift+Tab** — switch panel | ✅ Matches | |
| **Ctrl+R** — refresh panels | ✅ Matches | |
| **Ctrl+U** — swap panels | ✅ Matches | |
| **Ctrl+L** — show file info | ⚠️ Partial | Mapped to ShowInfo (InfoDialog). Original Ctrl+L is "Refresh screen" / redraw. Ctrl+I or `*` opens file info in the original. |
| **Ctrl+X C** — chmod | ⚠️ Partial | See Chmod above. |
| **Ctrl+X O** — chown | ⚠️ Partial | See Chown above. |
| **Ctrl+T** — open terminal | ✅ Matches | Opens terminal emulator in the active panel's directory. |
| **Insert** — mark file | ✅ Matches | Toggles mark and advances cursor. |
| **Backspace** — parent directory | ✅ Matches | Navigates to `..`. |
| **Quick search** (incremental) | ❌ Not implemented | Original MC activates incremental search when the user types a letter in the panel (or presses `/`); the cursor moves to the first matching filename as characters are typed. No equivalent in the .NET port. |
| **Ctrl+F** — hotlist | ✅ Matches | Opens the directory hotlist. |
| **Ctrl+\** — hotlist (alt binding) | ✅ Matches | Same as Ctrl+F. |
| **Alt+Y / Alt+U** — dir history back/forward | ✅ Matches | Navigates the per-panel directory history. |
| **Alt+I** — synchronise panels | ✅ Matches | Navigates inactive panel to active panel's directory. |
| **Alt+H** — dir history dialog | ✅ Matches | Shows navigable directory history. |
| **Ctrl+H / Alt+.** — toggle hidden | ✅ Matches | Toggles display of dot-files. |
| **Ctrl+S** — sort dialog | ✅ Matches | Opens the sort order dialog. |
| **+** — select group | ⚠️ Partial | Pattern input only; missing "Files only"/"Case sensitive"/"Shell patterns" checkboxes. |
| **-** — unselect group | ⚠️ Partial | Same. |
| **\*** — invert selection | ✅ Matches | |

---

## Tools Menu (not present in original MC)
this is correct


## Summary Table

| Category | Total items | ✅ Matches | ⚠️ Partial | ❌ Not implemented |
|----------|-------------|-----------|-----------|-------------------|
| Button bar (F1–F10) | 10 | 4 | 6 | 0 |
| Left/Right panel menu | 15 | 5 | 8 | 2 |
| File menu | 17 | 7 | 9 | 1 (Chattr) |
| Command menu | 18 | 6 | 9 | 3 |
| Options menu | 9 | 2 | 3 | 4 |
| Key bindings | 18 | 12 | 4 | 2 |
| **Total** | **87** | **36 (41%)** | **39 (45%)** | **12 (14%)** |

---

## Top Priority Gaps (most impactful to address)

1. **Copy/Move dialog** — missing source mask field, background button, extra attribute checkboxes (affects F5, F6).
2. **Chmod dialog** — missing setuid/setgid/sticky bits and multi-file iteration.
3. **Chown dialog** — should use system user/group listboxes, not free-text.
4. **Select/Unselect group** — missing "Files only", "Case sensitive", "Shell patterns" checkboxes.
5. **Find file** — missing real-time progress, Panelize/View/Edit buttons on results, start-dir browser.
6. **Quick search** — missing incremental in-panel search (typing in the panel jumps to matching filename).
7. **Panel modes** — Quick view, Info, Tree as persistent panel modes instead of modal dialogs.
8. **External Panelize** — result files must be injected into the panel for further operations.
9. **Compare directories** — method selector (Quick / Size only / Thorough) is missing.
10. **Hotlist** — hierarchical groups not supported.
