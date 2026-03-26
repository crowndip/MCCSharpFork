# Possible New Functions for MCCSharpFork

Compiled from GNU MC GitHub issues, Reddit (r/commandline, r/linux, r/linuxquestions),
Unix Stack Exchange, and MC mailing list discussions.
Items are grouped by area and rated for implementation effort.

Effort scale: 🟢 Low · 🟡 Medium · 🔴 High

---

## File Panels

| # | Feature | Effort | Notes |
|---|---------|--------|-------|
| 1 | ~~**Filter/search-as-you-type in panel** — type to narrow visible entries in real time~~ | 🟢 | ~~Very frequently requested; like Ctrl+S but live~~ |
| 2 | **Custom column layout** — choose which columns to show (size, date, permissions, owner) and their order | 🟡 | Stored in settings per panel |
| 3 | **Panel tabs** — multiple directories open as tabs in each panel | 🟡 | High-demand feature; similar to tabbed terminals |
| 4 | ~~**Recent directories list** — quick-jump to previously visited paths~~ | 🟢 | ~~Persist in settings; show via hotkey~~ |
| 5 | **Git status indicators** — show M/A/? markers next to files in git repos | 🟡 | Run `git status --short` in background; cache result |
| 6 | **Color rules by extension or pattern** — user-defined rules to colour-code files | 🟢 | MC has this in C; ours could do it via skin rules |
| 7 | **File count and size in directory entries** — show `<DIR 12>` or `<4 items>` | 🟢 | Optional async background scan |
| 8 | **Breadcrumb / path bar** — clickable path segments above panel | 🟡 | Useful with mouse support already in place |
| 9 | ~~**Pinned / bookmarked panel paths** — persistent across sessions~~ | 🟢 | ~~Already have FavoritesManager; extend to panels~~ |
| 10 | **Flat / recursive view** — show all files under current dir recursively | 🟡 | "Find files mode" as a panel view |
| 11 | **Dual-pane sync** — button/key to mirror the path of the active panel into the other | 🟢 | One-liner in controller |
| 12 | **Split file name / extension in separate columns** | 🟢 | Layout option |
| 13 | **Trash/recycle bin support** — move to trash instead of immediate delete | 🟡 | `~/.local/share/Trash` on Linux; Recycle Bin on Windows |

---

## Search & Find

| # | Feature | Effort | Notes |
|---|---------|--------|-------|
| 14 | **Fuzzy finder integration (fzf-style)** — Ctrl+P quick-open across current tree | 🟡 | Can be self-contained without fzf binary |
| 15 | **Content search with in-panel results** — find files by content, show matches in panel | 🟡 | Reuse existing search providers |
| 16 | **Find & replace across multiple files** — from Find dialog, apply replacement to all matches | 🟡 | Extend current search; show confirmation dialog |
| 17 | **Ripgrep / ag integration** — use faster external searcher when available | 🟢 | Fall back to built-in if not found |
| 18 | **Search history** — remember previous search terms across sessions | 🟢 | Persist last N queries in settings |

---

## Editor (mcedit)

| # | Feature | Effort | Notes |
|---|---------|--------|-------|
| 19 | **Multiple open files as tabs** — switch between files without closing | 🟡 | Window > List already exists; tabs would be the next step |
| 20 | **Auto-close brackets/quotes** — type `(` and get `()` with cursor inside | 🟢 | Insert-mode key handler |
| 21 | **Code folding** — collapse/expand blocks delimited by `{}`/indent | 🔴 | Needs syntax tree awareness |
| 22 | **Line diff / change gutter** — show +/~/- indicators for unsaved or git changes | 🟡 | Compare buffer to saved file |
| 23 | ~~**Word wrap toggle** — soft-wrap long lines without inserting newlines~~ | 🟡 | ~~Rendering-layer change~~ |
| 24 | ~~**Show whitespace** — render tabs and spaces as visible glyphs~~ | 🟢 | ~~Toggle in Options; already have tab-display support~~ |
| 25 | ~~**Go-to line number dialog** — Ctrl+G or similar~~ | 🟢 | ~~One dialog + scroll to line~~ |
| 26 | **Duplicate line** — hotkey to copy current line down | 🟢 | Insert-mode operation |
| 27 | **Move line up/down** — Alt+Up/Down to swap lines | 🟢 | Simple buffer operation |
| 28 | ~~**Auto-indent on Enter** — preserve current line's leading whitespace~~ | 🟢 | ~~Insert-mode Enter handler~~ |
| 29 | **Block comment/uncomment** — toggle `//` or `#` on selected lines | 🟢 | Language-aware via syntax file |
| 30 | **Persistent undo across sessions** — reload file with undo history intact | 🔴 | Serialise undo stack |
| 31 | **Minimap / scrollbar position indicator** | 🟡 | Narrow right-side column showing position |

---

## Viewer (mcview)

| # | Feature | Effort | Notes |
|---|---------|--------|-------|
| 32 | **Image preview via Sixel / Kitty protocol** — render images inline in terminal | 🔴 | Terminal capability detection needed |
| 33 | **JSON / XML pretty-print view** — auto-format structured files | 🟡 | Use System.Text.Json; detect by extension |
| 34 | **CSV / TSV table view** — render delimited files as aligned columns | 🟡 | Split on delimiter, pad columns |
| 35 | ~~**Syntax-highlighted read-only view** — apply mcedit highlighting in viewer~~ | 🟡 | ~~Reuse existing highlighter in read-only mode~~ |
| 36 | **Follow mode** — auto-scroll to end as file grows (like `tail -f`) | 🟢 | FileSystemWatcher + scroll-to-end |
| 37 | **PDF text extraction view** — show extracted text from PDF | 🔴 | Needs a PDF library (PdfPig etc.) |

---

## Archive / VFS

| # | Feature | Effort | Notes |
|---|---------|--------|-------|
| 38 | ~~**7-Zip / RAR browse support** — enter .7z and .rar as virtual directories~~ | 🟡 | ~~Add VFS providers; SharpCompress supports both~~ |
| 39 | **Create archive from panel selection** — right-click / menu to zip selected files | 🟢 | Wrap System.IO.Compression |
| 40 | **Cloud storage VFS (S3 / Dropbox / OneDrive)** — browse cloud buckets as panels | 🔴 | One provider per service; AWSSDK etc. |
| 41 | **WebDAV VFS** — browse WebDAV shares as panels | 🟡 | HttpClient-based; protocol is well-documented |
| 42 | ~~**Progress dialog for large archive operations** — show extraction %~~ | 🟢 | ~~SharpCompress provides progress callbacks~~ |

---

## Terminal / Shell Integration

| # | Feature | Effort | Notes |
|---|---------|--------|-------|
| 43 | **Embedded terminal pane** — split view with a live shell at the bottom | 🔴 | Requires PTY hosting; complex on all platforms |
| 44 | ~~**`cd` to panel directory on exit** — write a shell function that picks up MC's last dir~~ | 🟢 | ~~Write path to a temp file; shell wrapper sources it~~ |
| 45 | **Shell command with output to viewer** — run a command and pipe stdout to mcview | 🟢 | Already have "Paste output of…"; extend to full viewer |
| 46 | **User menu improvements** — richer scripting with variables, env, conditional entries | 🟡 | Extend existing user-menu parser |

---

## UI / Appearance

| # | Feature | Effort | Notes |
|---|---------|--------|-------|
| 47 | ~~**More built-in themes / skins** — dark, light, solarized, dracula, nord presets~~ | 🟢 | ~~Pure data; add JSON skin files~~ (dark + monochrome already exist) |
| 48 | **Theme switcher dialog** — live preview of skins without restarting | 🟡 | Apply skin and redraw in-place |
| 49 | **Mouse scroll in menus and dialogs** | 🟢 | Terminal.Gui v2 supports mouse wheel events |
| 50 | **Notification / status toasts** — transient messages at bottom bar for async operations | 🟢 | Timed overlay on the hints bar |
| 51 | **Configurable function-key bar** — let user reassign F1–F10 labels and actions | 🟡 | Store mapping in settings |
| 52 | **Panel sort memory** — remember sort column and direction per directory | 🟢 | Persist in session state |
| 53 | **Startup directory from CLI argument** — `mc /some/path /other/path` sets both panels | 🟢 | Parse args in Mc.App entry point |

---

## Misc / Quality of Life

| # | Feature | Effort | Notes |
|---|---------|--------|-------|
| 54 | **Session restore** — remember open panels, paths and editor files across restarts | 🟡 | Serialise app state on exit |
| 55 | **Hotkey cheat-sheet dialog** — searchable list of all active keybindings | 🟢 | Read from KeyBindingManager; show in dialog |
| 56 | **File association editor** — GUI to map extensions to open/view/edit commands | 🟡 | Extend existing "Open with" logic |
| 57 | **Bulk permission change (chmod) with preview** — select multiple files, preview changes before apply | 🟢 | Extend existing Chmod dialog |
| 58 | ~~**Directory comparison** — highlight files that differ between left and right panels~~ | 🟡 | ~~Compare by name+size+date or checksum~~ |
| 59 | **Disk usage bar in panel footer** — visual bar showing free/used space of current drive | 🟢 | DriveInfo + simple bar rendering |
| 60 | **Clipboard history** — cycle through last N copied paths/names | 🟡 | Ring buffer in ClipboardManager |
