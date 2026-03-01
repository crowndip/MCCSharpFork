# Original GNU Midnight Commander — Complete Function Reference

**Purpose:** Master reference of every feature, function, key binding, menu item, and dialog
in the original GNU Midnight Commander. Used to verify completeness of the .NET port.

**Status legend:**
- `✅` Implemented and matches original behaviour
- `⚠️` Partially implemented (see notes)
- `❌` Not implemented / stub

**Reference:** https://github.com/MidnightCommander/mc
**MC version:** 4.8.x (current mainline)

---

## 1. Main Screen Layout

| Feature | Status | Notes |
|---------|--------|-------|
| Two-panel side-by-side layout (default) | ✅ | |
| Horizontal split layout (panels top/bottom) | ✅ | Toggle via Alt+, |
| Menu bar at top | ✅ | |
| Two file panels | ✅ | |
| Single divider `│` between panels | ✅ | |
| Command line at bottom | ✅ | |
| Function key bar (F1–F10) at bottom | ✅ | |
| Hints bar (rotating tips strip) | ❌ | |
| Panel path in top border, centered, with trailing `/` | ✅ | |
| Active panel has brighter frame colour | ✅ | |
| Panel summary line (N files, M dirs, X free) | ✅ | |
| Free space shown in summary | ✅ | |
| Mini-status line per panel (file info) | ✅ | |
| Scrollbar on right inner edge of panel | ✅ | |
| Scrollbar thumb position indicator | ✅ | |

---

## 2. File Panel — Display

| Feature | Status | Notes |
|---------|--------|-------|
| Full listing mode (name + size + date) | ✅ | |
| Brief listing mode (two columns of names) | ✅ | |
| Long listing mode (permissions + owner + group + size + date + name) | ✅ | |
| User-defined listing mode (custom column format string) | ❌ | |
| Column header with sort field + ↑/↓ indicator | ✅ | |
| Header: `Name  Size  Modify time` spacing | ✅ | |
| Brief mode: two equal-width columns with `│` separator | ✅ | |
| Long mode: permissions in `drwxr-xr-x` format | ✅ | |
| `..` entry shows `<UP-DIR>` in size column | ✅ | |
| Subdirectory shows `<DIR>` in size column | ✅ | |
| File entry with marker column (`*` when marked) | ✅ | |
| Date format: recent = `MMM dd HH:mm`; old = `MMM dd  yyyy` | ✅ | |
| Symlink target shown in mini-status (`name -> target`) | ✅ | |
| Panel top-border path left-biased centering (odd extra dash goes left) | ✅ | |
| Inactive panel cursor shown (muted highlight) | ✅ | |
| Active panel cursor (black on cyan) | ✅ | |
| Marked files cursor (bright yellow on cyan) | ✅ | |
| Marked file count: singular/plural + "tagged" label | ✅ | |

### File Type Colour Coding

| Type | Colour | Status |
|------|--------|--------|
| Regular file | Gray on Blue | ✅ |
| Directory | White on Blue | ✅ |
| Executable | Bright Green on Blue | ✅ |
| Symlink | Cyan on Blue | ✅ |
| Archive / compressed | Bright Cyan on Blue | ✅ |
| Marked file | Bright Yellow on Blue | ✅ |
| Cursor (active panel) | Black on Cyan | ✅ |
| Cursor (inactive panel) | White on Blue | ✅ |
| Block device | Bright Magenta on Blue | ❌ |
| Character device | Magenta on Blue | ❌ |
| FIFO / named pipe | Dark Gray on Blue | ❌ |
| Socket | Bright Magenta on Blue | ❌ |
| Executable with `*` suffix (like `ls -F`) | ❌ | Optional panel option |
| Symlink-to-dir shown in directory colour | ❌ | Requires stat() on target |

---

## 3. File Panel — Navigation & Interaction

| Feature | Status | Notes |
|---------|--------|-------|
| Arrow keys move cursor | ✅ | |
| Enter — open file/directory | ✅ | |
| Backspace — navigate to parent | ✅ | |
| Left arrow — navigate to parent (Lynx-like motion) | ⚠️ | Setting exists but must be verified |
| Right arrow — enter directory (Lynx-like motion) | ⚠️ | Setting exists but must be verified |
| Page Up / Page Down | ✅ | |
| Home — jump to first entry | ✅ | |
| End — jump to last entry | ✅ | |
| Alt+G — jump to first entry | ✅ | |
| Alt+R — jump to middle entry | ✅ | |
| Alt+J — jump to last entry | ✅ | |
| Insert — toggle mark on current file and advance cursor | ✅ | |
| `*` — invert selection (all marks) | ✅ | |
| `+` — select group (mark by pattern) | ✅ | Dialog with Files only / Case sensitive / Shell patterns |
| `\` (backslash) — unselect group | ✅ | |
| Ctrl+R — refresh/rescan panel | ✅ | |
| Tab / Shift+Tab — switch active panel | ✅ | |
| Quick search (type letter to jump to filename) | ✅ | |
| Alt+S / Ctrl+S — activate quick search | ✅ | |
| Quick search cursor blink at position | ❌ | Terminal.Gui cursor placement |
| Ctrl+Space — calculate directory size | ✅ | |
| Mouse click — move cursor | ✅ | |
| Mouse double-click — open entry | ✅ | |
| Mouse click on inactive panel — switch panel | ✅ | |
| Alt+T — cycle listing mode (Full→Brief→Long→Full) | ✅ | |

---

## 4. Function Key Bar (F1–F10)

| Key | Label | Status | Notes |
|-----|-------|--------|-------|
| F1 | Help | ⚠️ | Section viewer with Contents/Back. Missing: hyperlink navigation, ctrl-char links, bold/italic rendering |
| F2 | Menu | ⚠️ | User menu with condition lines evaluated. Missing: "Using shell patterns" scope evaluation for conditions |
| F3 | View | ✅ | Internal viewer; navigates into directory |
| F4 | Edit | ✅ | Opens internal editor; prompts for filename if no file under cursor |
| F5 | Copy | ⚠️ | Copies with source mask, preserve attrs, background. Missing: "Stable symlinks" option, ext2 attrs checkbox |
| F6 | RenMov | ⚠️ | Rename (single) or move (multiple). Same option gaps as F5 |
| F7 | Mkdir | ✅ | Creates directory; supports recursive parent creation |
| F8 | Delete | ✅ | Delete with primary confirm + per-directory recursive confirm |
| F9 | PullDn | ✅ | Opens menu bar |
| F10 | Quit | ✅ | Confirms if setting on, saves panel paths |
| Mouse click on F-key label | ✅ | Fires the same callback |

---

## 5. Left / Right Panel Menu

Accessed via F9 → Left / Right, or by clicking the panel.

| Menu Item | Shortcut | Status | Notes |
|-----------|----------|--------|-------|
| Listing format... | | ✅ | Full / Brief / Long. Missing: User-defined columns |
| Quick view | Ctrl+X Q | ✅ | Persistent live-preview overlay on inactive panel |
| Info | Ctrl+X I | ✅ | Persistent info overlay on inactive panel |
| Tree | | ✅ | Persistent navigable tree overlay on inactive panel |
| Panelize | | ⚠️ | Runs shell command; injects matching filenames into panel as virtual listing |
| Listing format... (duplicate) | | ✅ | Same as above (menu consolidation done) |
| Sort order... | Ctrl+S | ✅ | All sort fields; reverse, dirs first, case sensitive |
| Filter... | | ✅ | Pattern filter on active panel listing |
| Encoding... | | ✅ | Full system encoding list with filter field |
| FTP link... | | ⚠️ | Dialog exists; FTP VFS provider not wired by default |
| Shell link... | | ❌ | FISH protocol not implemented |
| SFTP link... | | ⚠️ | Dialog exists; SFTP VFS provider not wired by default |
| Rescan | Ctrl+R | ✅ | |

---

## 6. File Menu (F9 → File)

| Menu Item | Shortcut | Status | Notes |
|-----------|----------|--------|-------|
| View | F3 | ✅ | |
| View file... | | ✅ | Prompts for filename |
| Filtered view | | ✅ | Pipes file through command (`%f` substituted) |
| Edit | F4 | ✅ | |
| Copy | F5 | ⚠️ | See F5 above |
| Chmod | Ctrl+X C | ✅ | Full rwx + setuid/setgid/sticky; multi-file Set all |
| Link | Ctrl+X L | ✅ | Hard link |
| Symlink | Ctrl+X S | ✅ | Absolute symlink |
| Relative symlink | Ctrl+X V | ✅ | Relative path symlink |
| Edit symlink | | ✅ | Reads current target, confirms, re-creates |
| Chown | Ctrl+X O | ✅ | System user/group listboxes from /etc/passwd, /etc/group |
| Advanced chown | | ✅ | Combined owner/group + permissions in one dialog |
| Rename/Move | F6 | ⚠️ | See F6 above |
| Mkdir | F7 | ✅ | |
| Delete | F8 | ✅ | |
| Quick cd | Alt+C | ✅ | |
| Select group | + | ✅ | Dialog with Files only / Case sensitive / Shell patterns |
| Unselect group | \ | ✅ | |
| Invert selection | * | ✅ | |
| Chattr | Ctrl+X A | ✅ | ext2 file attributes via lsattr/chattr |
| Exit | F10 | ✅ | |

---

## 7. Command Menu (F9 → Command)

| Menu Item | Shortcut | Status | Notes |
|-----------|----------|--------|-------|
| User menu | F2 | ⚠️ | Condition lines evaluated. Missing: full shell-pattern scope |
| Directory tree | | ✅ | Persistent tree overlay |
| Find file | Alt+? | ⚠️ | Pattern + content + date/size filters. Missing: date/size filter fields, Panelize button on results |
| Swap panels | Ctrl+U | ✅ | |
| Switch panels on/off | Ctrl+O | ✅ | Suspends TUI, opens shell |
| Compare directories | Ctrl+X D | ✅ | Quick / Size-only / Thorough method selector |
| Compare files | | ✅ | Opens diff viewer; error if null/directory |
| External panelize | | ⚠️ | Injects matching filenames from shell command into panel |
| Show directory sizes | | ✅ | Computes sizes for marked dirs (or current) |
| Command history | | ✅ | Shows session history; Enter pastes to command line |
| Viewed/edited files history | | ✅ | MRU list with View / Edit / Panel buttons |
| Directory hotlist | Ctrl+\ / Ctrl+F | ✅ | Hierarchical groups; new group; breadcrumb navigation |
| Active VFS list | | ✅ | Shows mounted VFS paths; Browse / Free VFSs |
| Background jobs | | ✅ | Lists running/finished jobs with Kill button |
| Screen list | | ❌ | Multiple subshell screens not implemented |
| Edit extension file | | ✅ | Opens ~/.config/mc/mc.ext |
| Edit menu file | | ✅ | Opens ~/.config/mc/menu |
| Edit highlight group file | | ✅ | Opens ~/.config/mc/mc.filecolor |

---

## 8. Options Menu (F9 → Options)

| Menu Item | Shortcut | Status | Notes |
|-----------|----------|--------|-------|
| Configuration... | | ✅ | Verbose, Compute totals, Auto save, Show output, Subshell, Ask before run, Pause after run |
| Layout... | | ✅ | Menubar/cmdline/keybar toggle, split %, equal split, horizontal/vertical |
| Panel options... | | ✅ | Hidden files, backup files, mini status, Lynx-like, scrollbar, file highlight, mix files, case-sensitive quick search, free space |
| Confirmation... | | ✅ | Confirm delete, confirm overwrite, confirm exit |
| Appearance... | | ✅ | Skin file selector from system + user skins directories |
| Learn keys... | | ✅ | Scrollable table of all key bindings |
| Virtual FS... | | ✅ | Cache timeout, FTP proxy, FTP anon password, passive mode |
| Save setup | | ✅ | Persists to ~/.config/mc/ini |
| About... | | ✅ | Version, description, copyright |

---

## 9. Command Line

| Feature | Status | Notes |
|---------|--------|-------|
| Text input field | ✅ | |
| Directory prompt (`~/path> `) | ✅ | Truncated to last 2 path components |
| Execute command on Enter | ✅ | |
| History stored per session | ✅ | |
| Up / Down arrows — navigate history | ✅ | |
| Alt+P — previous history entry | ✅ | |
| Alt+N — next history entry | ✅ | |
| Ctrl+H / Alt+H — show inline history popup | ✅ | Window with ListView above command line |
| Inline history popup: Enter selects, Esc closes | ✅ | |
| Inline history popup: double-click selects | ✅ | |
| Ctrl+Enter — paste filename from active panel | ✅ | |
| Ctrl+Shift+Enter — paste filename from inactive panel | ✅ | |
| Alt+Tab — filename / command completion | ❌ | |
| Tab — completion when command line is focused | ❌ | |
| Ctrl+A — jump to beginning of line (Emacs) | ⚠️ | Depends on TextField behaviour |
| Ctrl+E — jump to end of line (Emacs) | ⚠️ | |
| Ctrl+K — kill to end of line (Emacs) | ⚠️ | |
| Ctrl+W — kill word backwards (Emacs) | ⚠️ | |
| Ctrl+Y — yank killed text (Emacs) | ⚠️ | |
| Alt+B — word left (Emacs) | ⚠️ | |
| Alt+F — word right (Emacs) | ⚠️ | |
| Ctrl+Q — quote next character (insert control char) | ❌ | |
| Ctrl+X T — paste tagged filenames from active panel | ✅ | |
| Ctrl+X Ctrl+P — paste other panel's path | ✅ | |

---

## 10. Built-in Viewer (mcview / F3)

### Navigation

| Feature | Status | Notes |
|---------|--------|-------|
| Scroll up/down (arrow keys, PgUp/PgDn) | ✅ | |
| Go to beginning (Home / Ctrl+Home) | ✅ | |
| Go to end (End / Ctrl+End) | ✅ | |
| Go to byte offset (F5) | ✅ | |
| Go to line number (F5 in text mode) | ⚠️ | Port uses byte offset for all modes |
| Next file in directory (Ctrl+F) | ❌ | |
| Previous file in directory (Ctrl+B) | ❌ | |
| Ctrl+F — search forward | ✅ | |
| Ctrl+B / F17 — search backward | ✅ | |
| n — repeat search | ✅ | |
| Shift+F7 — search backward | ✅ | |

### Display Modes

| Feature | Status | Notes |
|---------|--------|-------|
| ASCII text mode | ✅ | |
| Hex view mode (F4) | ✅ | |
| Toggle wrap (F2) | ✅ | |
| Toggle hex/text (F3) | ✅ | |
| Toggle nroff/formatted output (F9) | ❌ | |
| Ruler line toggle (Alt+R) | ❌ | |
| Change charset encoding (Alt+E) | ❌ | |

### Search

| Feature | Status | Notes |
|---------|--------|-------|
| Search dialog (F7) | ✅ | |
| Case-sensitive toggle in search | ✅ | |
| Regular expression search | ✅ | |
| Hex pattern search | ✅ | |
| `/` key — start regex search | ❌ | |

### Bookmarks

| Feature | Status | Notes |
|---------|--------|-------|
| Set bookmark (Ctrl+B) | ✅ | One bookmark |
| Go to bookmark (Ctrl+P) | ✅ | |
| Numeric bookmarks 0–9 (`[n]m` set, `[n]r` jump) | ❌ | Port only has one bookmark |

### Other

| Feature | Status | Notes |
|---------|--------|-------|
| View exit (F10 / Esc / Q) | ✅ | |
| F1 in viewer — viewer-specific help | ❌ | Opens main help instead |
| Display file size and offset in status | ✅ | |

---

## 11. Built-in Editor (mcedit / F4)

### File Operations

| Feature | Status | Notes |
|---------|--------|-------|
| Open existing file | ✅ | |
| Create new file (F4 on empty / Ctrl+N) | ✅ | Prompts for filename |
| Open file dialog (Ctrl+O) | ❌ | |
| Save (F2 / Ctrl+S) | ✅ | |
| Save As (Shift+F2) | ✅ | |
| Close / quit (F10 / Esc) | ✅ | Prompts if modified |
| "Modified" indicator in status bar | ✅ | |
| Auto-detect line endings (LF / CRLF) | ✅ | |

### Cursor Movement

| Feature | Status | Notes |
|---------|--------|-------|
| Arrow keys | ✅ | |
| Ctrl+Left / Ctrl+Right — word left/right | ✅ | |
| Home / End | ✅ | |
| Ctrl+Home / Ctrl+End | ✅ | |
| Page Up / Page Down | ✅ | |
| Go to line (Ctrl+G / Alt+L) | ✅ | |

### Editing

| Feature | Status | Notes |
|---------|--------|-------|
| Insert / overwrite mode toggle (Insert key) | ✅ | INS/OVR in status bar |
| Backspace / Delete | ✅ | |
| Enter — new line with auto-indent | ✅ | |
| Shift+Enter — new line without auto-indent | ✅ | |
| Tab — insert tab | ✅ | |
| Ctrl+D — insert current date/time | ✅ | |

### Selection & Clipboard

| Feature | Status | Notes |
|---------|--------|-------|
| Shift+Arrow — select text | ✅ | |
| Ctrl+C — copy selection | ✅ | |
| Ctrl+X — cut selection | ✅ | |
| Ctrl+V — paste | ✅ | |
| Ctrl+Insert — copy (alternate) | ✅ | |
| Shift+Insert — paste (alternate) | ✅ | |
| Shift+Delete — cut (alternate) | ✅ | |
| Ctrl+A — select all | ✅ | |
| Select column block (Alt+I) | ⚠️ | May vary |

### Search & Replace

| Feature | Status | Notes |
|---------|--------|-------|
| Find (F7) | ✅ | Dialog with case-sensitive + regex; pre-fills last search |
| Find again / Shift+F7 | ✅ | Repeats last search without dialog |
| Replace (F4 in search context / Ctrl+H in some builds) | ✅ | |
| Replace again (Shift+F4) | ❌ | |

### Undo / Redo

| Feature | Status | Notes |
|---------|--------|-------|
| Undo (Ctrl+Z / Ctrl+U) | ✅ | |
| Redo (Ctrl+Y / Ctrl+R) | ✅ | Note: original uses Ctrl+R for macro record |

### Display

| Feature | Status | Notes |
|---------|--------|-------|
| Syntax highlighting (language auto-detected) | ✅ | |
| Toggle syntax highlighting (Ctrl+T) | ✅ | Status bar shows "NoHL" when off |
| Toggle line numbers (via F9 menu) | ✅ | |
| Status bar with filename, line/col, INS/OVR, modified | ✅ | |

### Editor Menu (F9)

| Feature | Status | Notes |
|---------|--------|-------|
| Save | ✅ | |
| Save As | ✅ | |
| Find | ✅ | |
| Replace | ✅ | |
| Go to line | ✅ | |
| Toggle line numbers | ✅ | |
| Toggle syntax highlighting | ✅ | |
| Close | ✅ | |

### Advanced

| Feature | Status | Notes |
|---------|--------|-------|
| Macro recording (Ctrl+R start/stop in original) | ❌ | Ctrl+R is Redo in port |
| Word completion (Ctrl+Tab) | ❌ | |
| Spell checking | ❌ | |
| Bookmarks (Ctrl+B set, Ctrl+P go to) | ✅ | |
| Column/block selection | ⚠️ | |

---

## 12. Built-in Diff Viewer (mcdiff)

| Feature | Status | Notes |
|---------|--------|-------|
| Side-by-side diff of two files | ✅ | |
| Navigate diff hunks (next/previous) | ✅ | |
| Syntax highlighting | ✅ | |
| Search in diff | ✅ | |
| Edit file from diff viewer | ✅ | |
| Merge hunk (apply change) | ⚠️ | Basic; may not cover all cases |

---

## 13. Virtual File System (VFS)

### Architecture

| Feature | Status | Notes |
|---------|--------|-------|
| Pluggable VFS provider interface | ✅ | IVfsProvider |
| VFS path with scheme (local://, ftp://, sftp://, tar://) | ✅ | |
| VFS path with `#enc:` encoding suffix | ✅ | |
| Navigate into archives as directories | ⚠️ | Tar/zip providers exist |
| VFS cache with configurable timeout | ✅ | |

### VFS Providers

| Provider | Status | Notes |
|----------|--------|-------|
| Local filesystem | ✅ | |
| FTP | ⚠️ | Provider code exists; not registered by default |
| SFTP | ⚠️ | Provider code exists; not registered by default |
| TAR archives (.tar, .tar.gz, .tar.bz2, .tar.xz) | ⚠️ | Archive VFS exists |
| ZIP archives | ⚠️ | |
| CPIO archives | ⚠️ | |
| FISH (FIles over SHell) | ❌ | |
| External filesystem (extfs scripts) | ❌ | |
| SFS (single-file filesystem) | ❌ | |

---

## 14. File Operations

### Copy (F5)

| Feature | Status | Notes |
|---------|--------|-------|
| Copy single file (cursor entry when nothing marked) | ✅ | |
| Copy multiple marked files | ✅ | |
| Copy directory recursively | ✅ | |
| Destination defaults to other panel path | ✅ | |
| User-editable destination field | ✅ | |
| Source mask / From: pattern field | ✅ | |
| Using shell patterns toggle | ✅ | |
| Preserve attributes (timestamps, permissions) | ✅ | |
| Overwrite confirmation | ✅ | |
| Overwrite all / Skip all per-session | ✅ | |
| Dive into subdirectory if exists | ⚠️ | Checkbox shown; not yet applied |
| Stable symlinks option | ⚠️ | Checkbox shown; not yet applied |
| Preserve ext2 attributes | ❌ | |
| Background copy operation | ✅ | |
| Progress dialog with file/byte counts | ✅ | |
| Cancel operation | ✅ | |

### Move / Rename (F6)

| Feature | Status | Notes |
|---------|--------|-------|
| Rename single file (pre-fills current name) | ✅ | |
| Move multiple marked files | ✅ | |
| Source mask field | ✅ | |
| Cross-device move (copy + delete) | ✅ | |
| Background move | ✅ | |
| Progress dialog | ✅ | |

### Delete (F8)

| Feature | Status | Notes |
|---------|--------|-------|
| Delete single file | ✅ | |
| Delete multiple marked files | ✅ | |
| Primary confirmation dialog | ✅ | |
| Secondary per-directory "Delete recursively?" confirmation | ✅ | |
| Progress dialog | ✅ | |
| Cancel operation | ✅ | |

### Mkdir (F7)

| Feature | Status | Notes |
|---------|--------|-------|
| Create single directory | ✅ | |
| Recursive parent creation | ✅ | |

### Links

| Feature | Status | Notes |
|---------|--------|-------|
| Hard link (Ctrl+X L) | ✅ | |
| Absolute symlink (Ctrl+X S) | ✅ | |
| Relative symlink (Ctrl+X V) | ✅ | |
| Edit symlink target | ✅ | With confirmation |

### Permissions / Ownership

| Feature | Status | Notes |
|---------|--------|-------|
| Chmod — rwx checkboxes per user/group/other | ✅ | |
| Chmod — special bits (setuid, setgid, sticky) | ✅ | |
| Chmod — octal input field | ✅ | |
| Chmod — Set all (apply to all marked files) | ✅ | |
| Chown — owner listbox from /etc/passwd | ✅ | |
| Chown — group listbox from /etc/group | ✅ | |
| Chown — Set all | ✅ | |
| Advanced chown (combined chmod + chown dialog) | ✅ | |
| Chattr — ext2 file attributes (append-only, immutable, etc.) | ✅ | Via lsattr/chattr |

---

## 15. Search / Find

### Find File Dialog (Alt+?)

| Feature | Status | Notes |
|---------|--------|-------|
| Filename pattern (shell glob) | ✅ | |
| Content search (grep-like) | ✅ | |
| Case sensitive toggle | ✅ | |
| Start directory field | ✅ | |
| Follow symlinks toggle | ✅ | |
| Skip hidden directories toggle | ✅ | |
| Date/time filter (modified before/after) | ❌ | |
| File size filter (larger/smaller than) | ❌ | |
| Ignore directories list | ❌ | |
| Real-time incremental results | ✅ | |
| Suspend / Continue search | ✅ | |
| View found file (F3) | ✅ | |
| Edit found file (F4) | ✅ | |
| Navigate panel to found file's directory | ✅ | |
| Panelize results into panel | ⚠️ | Injects matching files into panel |
| Again button — reopen search dialog | ✅ | |

---

## 16. Directory Features

| Feature | Status | Notes |
|---------|--------|-------|
| Directory hotlist (Ctrl+\ / Ctrl+F) | ✅ | Hierarchical groups |
| Add current dir to hotlist (Ctrl+X H) | ✅ | |
| Per-panel directory history (Alt+Y back, Alt+U forward) | ✅ | |
| Directory history dialog (Alt+H) | ✅ | |
| Swap panels (Ctrl+U) | ✅ | |
| Synchronise panels — inactive = active path (Alt+I) | ✅ | |
| Compare directories (Ctrl+X D) | ✅ | Quick / Size-only / Thorough |
| Show directory sizes | ✅ | |
| Quick CD (Alt+C) | ✅ | |
| Directory tree (persistent panel mode) | ✅ | |
| Tree with F2 rescan / F8 delete-dir | ✅ | |

---

## 17. Subshell Integration

| Feature | Status | Notes |
|---------|--------|-------|
| Ctrl+O — suspend MC, run interactive shell | ✅ | |
| Shell inherits panel's current directory | ✅ | |
| Return to MC from shell (type `exit` or Ctrl+D) | ✅ | |
| Subshell with typed command (type command then Ctrl+O) | ⚠️ | |
| Shell prompt shown in command line | ⚠️ | Simplified prompt |
| Shell command output displayed | ⚠️ | Via "Show output" setting |
| Multiple subshell screens (screen list) | ❌ | |

---

## 18. User Menu (F2)

| Feature | Status | Notes |
|---------|--------|-------|
| Load from ~/.config/mc/menu or system default | ✅ | |
| Display menu entries with hotkey letters | ✅ | |
| Execute entry by pressing hotkey letter | ✅ | |
| Condition lines (`+` / `=`) filter entries | ✅ | `f`/`d` pattern conditions + `!` negation |
| `%f` substitution (current filename) | ✅ | |
| `%d` substitution (current directory) | ✅ | |
| `%p` / `%P` substitutions | ⚠️ | |
| Other `%` macro substitutions | ⚠️ | |
| Edit user menu file | ✅ | |

---

## 19. Configuration System

| Feature | Status | Notes |
|---------|--------|-------|
| Config file: `~/.config/mc/ini` | ✅ | |
| INI format sections: `[Midnight-Commander]`, `[Panels]`, `[Layout]` | ✅ | |
| Auto-save on exit | ✅ | When "Auto save setup" is on |
| Save setup explicitly (Options → Save setup) | ✅ | |
| Save/restore panel paths | ✅ | |
| Save/restore sort order per panel | ✅ | |
| Save/restore active skin | ✅ | |
| Skin/theme system (INI-format skin files) | ✅ | |
| System skin files from /usr/share/mc/skins/ | ✅ | |
| User skin files from ~/.local/share/mc/skins/ | ✅ | |
| Extension file (`mc.ext`) — file open rules | ✅ | |
| File highlight/colour file (`mc.filecolor`) | ✅ | |
| Key bindings configurable (keybind file) | ⚠️ | Basic table shown; runtime rebinding not supported |

---

## 20. Global Key Bindings

| Key | Action | Status |
|-----|--------|--------|
| F1 | Help | ✅ |
| F2 | User menu | ✅ |
| F3 | View | ✅ |
| F4 | Edit | ✅ |
| F5 | Copy | ✅ |
| F6 | Rename/Move | ✅ |
| F7 | Mkdir | ✅ |
| F8 | Delete | ✅ |
| F9 | Menu bar | ✅ |
| F10 | Quit | ✅ |
| Tab | Switch panel | ✅ |
| Shift+Tab | Switch panel (reverse) | ✅ |
| Ctrl+R | Redraw screen | ✅ |
| Ctrl+L | Redraw screen (alias) | ✅ |
| Ctrl+U | Swap panels | ✅ |
| Ctrl+O | Shell | ✅ |
| Ctrl+F | Hotlist | ✅ |
| Ctrl+\ | Hotlist (alt) | ✅ |
| Ctrl+H / Alt+. | Toggle hidden files | ✅ |
| Ctrl+S | Sort dialog | ✅ |
| Ctrl+Space | Calculate dir size | ✅ |
| Alt+C | Quick CD | ✅ |
| Alt+? | Find file | ✅ |
| Alt+I | Synchronise panels | ✅ |
| Alt+H | Directory history | ✅ |
| Alt+Y | Dir history back | ✅ |
| Alt+U | Dir history forward | ✅ |
| Alt+T | Cycle listing mode | ✅ |
| Alt+, | Toggle split direction | ✅ |
| Alt+G | Jump to first entry | ✅ |
| Alt+R | Jump to middle entry | ✅ |
| Alt+J | Jump to last entry | ✅ |
| Ctrl+I | Show file info | ✅ |
| Ctrl+X C | Chmod | ✅ |
| Ctrl+X O | Chown | ✅ |
| Ctrl+X A | Chattr | ✅ |
| Ctrl+X D | Compare directories | ✅ |
| Ctrl+X H | Add to hotlist | ✅ |
| Ctrl+X L | Create link | ✅ |
| Ctrl+X S | Create symlink | ✅ |
| Ctrl+X V | Create relative symlink | ✅ |
| Ctrl+X T | Paste tagged filenames | ✅ |
| Ctrl+X Ctrl+P | Paste other panel path | ✅ |
| Ctrl+X Q | Toggle quick view | ✅ |
| Ctrl+X I | Toggle info panel | ✅ |
| Ctrl+Enter | Paste filename to cmdline | ✅ |
| Ctrl+Shift+Enter | Paste other panel filename | ✅ |
| Insert | Toggle mark | ✅ |
| + | Select group | ✅ |
| \ | Unselect group | ✅ |
| * | Invert selection | ✅ |
| Backspace | Navigate to parent | ✅ |

---

## 21. Miscellaneous Features

| Feature | Status | Notes |
|---------|--------|-------|
| Viewed/edited file history | ✅ | |
| External panelize (inject shell output into panel) | ✅ | |
| External VFS list with Browse / Free VFSs | ✅ | |
| Background file operations with job manager | ✅ | |
| Confirmation settings (delete, overwrite, exit) | ✅ | |
| "Verbose operation" (show file names during ops) | ✅ | |
| "Compute totals" before starting operation | ✅ | |
| Quick view panel (live file preview) | ✅ | |
| Info panel (live file attributes) | ✅ | |
| Panelize (inject filenames into panel) | ⚠️ | |
| Batch rename (mc does not have; port has extra Tools menu) | ✅ | Port-specific feature |

---

## 22. Not Implemented / Out of Scope

| Feature | Reason |
|---------|--------|
| GPM mouse (Linux GPM daemon) | Obsolete; xterm protocol used instead |
| Console saver (cons.saver.c) | Linux VT-specific, obsolete |
| Ext2/3/4 native attribute edit beyond chattr | Very niche; chattr covers it |
| FISH protocol (FIles over SHell) | Complex; SSH is preferred |
| Multiple subshell screens | Complex TUI multiplexing |
| Macro recording in editor | Ctrl+R conflict; not yet resolved |
| Word completion in editor (Ctrl+Tab) | Not yet implemented |
| Hyperlink navigation in help | Terminal.Gui constraint |
| Date/size filters in Find dialog | Not yet implemented |
| Viewer: next/previous file (Ctrl+F / Ctrl+B) | Not yet implemented |
| Viewer: nroff mode (F9) | Not yet implemented |
| Viewer: numeric bookmarks (0–9) | Not yet implemented |
| Command line Tab completion | Not yet implemented |

---

*Last updated: 2026-03-01*
*Based on GNU MC 4.8.x source: https://github.com/MidnightCommander/mc*
