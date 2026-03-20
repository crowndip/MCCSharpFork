# Main Screen Visual Checklist — .NET Port vs. GNU Midnight Commander

**Goal:** Make the main screen look pixel-perfect identical to the original GNU Midnight Commander.

**Status flags:** `[ ]` Not fixed · `[~]` Partial · `[x]` Fixed

Reference: GNU MC source `src/filemanager/panel.c`, `lib/widget/buttonbar.c`, `src/filemanager/layout.c`

---

## Tier 1 — Critical (immediately obvious to any MC user)

| # | Status | Area | Current behaviour | Expected (original MC) behaviour | Source reference |
|---|--------|------|-------------------|----------------------------------|-----------------|
| 1 | `[x]` | **File panel — directory name prefix** | ~~Every directory entry was shown with a leading `/` character: `/Documents`.~~ Fixed: directories now display as plain names (`Documents`, `Downloads`) — colour alone distinguishes them (`PanelDirectory`). | Directories are **not** prefixed. Colour alone (white on blue) identifies them. | `panel.c` — `format_file_name()` |
| 2 | `[x]` | **File panel — symlink name prefix** | ~~Every symlink was shown with a leading `~` character: `~mylink`.~~ Fixed: symlinks display as plain names — cyan colour identifies them (`PanelSymlink`). | Symlinks are **not** prefixed. Colour alone (cyan) identifies them. | `panel.c` — `format_file_name()` |
| 3 | `[x]` | **File panel — truncation marker conflicts with symlink prefix** | ~~Symlink prefix `~` and truncation marker `~` were the same character.~~ Resolved by removing the symlink prefix (#2); truncation `~` at end of long names is unambiguous. | Truncation appends `~` at end; no symlink prefix to conflict with. | `panel.c` — `format_file_name()` |
| 4 | `[x]` | **File panel — brief mode is single-column** | ~~Brief mode showed one column of file names.~~ Fixed: brief mode now shows **two equal-width columns** of file names side by side, separated by `│`. `EnsureCursorVisible` updated for 2× visibility; mouse click targets both columns. | Two columns, `(innerWidth-1)/2` wide each, `│` separator. | `panel.c` — `list_type = list_brief` |
| 5 | `[x]` | **Inter-panel divider — double bar** | ~~Left panel right border `│` and right panel left border `│` were at adjacent columns, producing `││`.~~ Fixed: right panel starts at `Pos.Right(_leftPanelView) - 1` so its left border overwrites the left panel's right border — single `│` shared. | One shared `│` character between panels. | `layout.c` — `setup_panels_and_shells()` |
| 6 | `[x]` | **File panel — active frame colour (sides and bottom)** | ~~Only the path text was drawn brighter for the active panel; the side bars and bottom border always used `PanelFrame`.~~ Fixed: `frameAttr = _isActive ? PanelHeader : PanelFrame` applied to ALL border elements — corners, dashes, side bars, and bottom. | Entire border frame uses `PanelHeader` when active, `PanelFrame` when inactive. | `panel.c` — `draw_frame()` |
| 7 | `[x]` | **File panel — parent dir size column shows `<DIR>`** | ~~`FormatPanelSize` returned `<DIR>` for the `..` entry.~~ Fixed: `FormatEntry` / `FormatLongEntry` detect `entry.IsParentDir` and emit `<UP-DIR>` instead. | `..` shows `<UP-DIR>`; subdirectories show `<DIR>`. | `panel.c` — size column for `..` vs subdirs |

---

## Tier 2 — High impact (clearly visible to experienced MC users)

| # | Status | Area | Current behaviour | Expected (original MC) behaviour | Source reference |
|---|--------|------|-------------------|----------------------------------|-----------------|
| 8 | `[x]` | **File panel — inactive cursor not shown** | ~~Inactive panel had no row highlight at all.~~ Fixed: added `PanelInactiveCursor` attribute (White on Blue) and `GetEntryAttr()` now returns it when `!_isActive && entryIdx == _cursorIndex`. | Inactive panel cursor row visible in a muted colour. | `panel.c` — drawing for inactive panel |
| 9 | `[x]` | **File panel — no sort indicator in column header** | ~~Column header always showed `Name  Size Modify time` with no indicator.~~ Fixed: `DrawColumnHeader` appends `↑` or `↓` to the active sort column name (Name / Size / Modify time) based on `_listing.Sort.Field` and `_listing.Sort.Descending`. | Sort column header shows `Name↑`, `Size↓`, etc. | `panel.c` — `sort_orders[]`, header drawing |
| 10 | `[x]` | **File panel — date format fixed (no age variation)** | ~~All dates used `MMM dd HH:mm` regardless of age.~~ Fixed: `FormatDate()` helper uses `"MMM dd HH:mm"` for files modified within 6 months and `"MMM dd  yyyy"` (two spaces) for older files, matching `ls -l`. | Recent → `Feb 28 14:30`; old → `Jan 01  2023`. | `panel.c` — `format_time()` |
| 11 | `[ ]` | **File panel — no scrollbar** | No scrollbar is drawn anywhere on the panel. | When the entry list exceeds the visible rows, MC draws a **one-character-wide scrollbar** on the right inner edge of the panel using block characters. Visibility controlled by "Show scrollbar" in Panel Options. | `panel.c` — `draw_scrollbar()` |
| 12 | `[x]` | **File panel — mini-status date uses ISO format** | ~~Status line showed `2026-02-28 14:30`.~~ Fixed: `UpdateStatus()` now calls `FormatDate()` which produces the same ls-style format used in the file list. | `file.txt  1234  Feb 28 14:30` | `panel.c` — `show_dir()` mini-status |
| 13 | `[x]` | **File panel — `..` row in Long mode** | ~~`FormatLongEntry` returned `<DIR>` for `..` in the size column.~~ Fixed: same `entry.IsParentDir` guard as in #7 applied to `FormatLongEntry`. | Long mode `..` size column shows `<UP-DIR>`. | `panel.c` — long format for parent dir |
| 14 | `[ ]` | **File panel — special file types lack distinct colour** | Character devices, block devices, FIFOs, and sockets all fall through to `PanelFile`. | MC colours these with distinct attributes (magenta, dark gray, etc.). Requires `VfsDirEntry`/`FileEntry` model additions. | `panel.c` — `file_compute_color()` |
| 15 | `[x]` | **File panel — column header label spacing** | Header spacing matches `" Name".PadRight(nameWidth) + "Size".PadLeft(sizeWidth) + " " + "Modify time"` — size right-aligned, "Modify time" one space after size. This already matches the original MC layout. | `Name              Size Modify time` — verified correct. | `panel.c` — `adjust_top_file()` |
| 16 | `[x]` | **File panel — panel summary line (no-selection state)** | ~~Status showed only `N files, M dirs`.~~ Fixed: `UpdateStatus()` now appends `, X free` when `ShowFreeSpace = true` (uses `DriveInfo`). `FilePanelView.ShowFreeSpace` property set from `McSettings.ShowFreeSpace`. | `14 files, 3 dirs, 12.4G free`. | `panel.c` — `show_dir()` free space |

---

## Tier 3 — Medium impact (subtle but consistent MC visual identity)

| # | Status | Area | Current behaviour | Expected (original MC) behaviour | Source reference |
|---|--------|------|-------------------|----------------------------------|-----------------|
| 17 | `[x]` | **File panel — Long mode applies same wrong prefixes** | ~~`FormatLongEntry` used `/` for directories and `~` for symlinks in the name field.~~ Fixed together with #1/#2: `FormatLongEntry` now uses plain `entry.Name` for all types. | Long mode name field: plain name, no prefix characters. | `panel.c` — long format drawing |
| 18 | `[x]` | **File panel — Brief mode header** | ~~Brief mode header was ` Name` (single column).~~ Fixed: two-column brief header is now ` Name`.PadRight(colWidth) + `│` + ` Name`.PadRight(col2Width). | `" Name             │ Name"` — two labels with `│` separator. | `panel.c` — brief header |
| 19 | `[x]` | **File panel — selected entry in inactive panel** | ~~No visual indication in inactive panel.~~ Fixed by #8 (`PanelInactiveCursor` = White on Blue applied when `!_isActive && entryIdx == _cursorIndex`). | Inactive cursor row: subtle White-on-Blue highlight. | `panel.c` — `display()` for inactive panel |
| 20 | `[x]` | **File panel — symlink target not shown in status** | ~~Status showed `linkname  0  date` with no target path.~~ Fixed: `UpdateStatus()` reads `entry.SymlinkTarget` (exposed via `FileEntry.SymlinkTarget → DirEntry.SymlinkTarget`) and appends ` -> target` to the status string. | `mylink -> /usr/bin/actual  Feb 28 14:30` | `panel.c` — `show_dir()`, `show_file_info()` |
| 21 | `[x]` | **Button bar — remainder pixel column** | ~~`totalWidth % 10` remainder left uncoloured at right edge.~~ Fixed: `ButtonBarView.OnDrawingContent` now assigns the remainder columns to the last button (`10Quit`) via `btnWidth = i == count-1 ? baseWidth + remainder : baseWidth`. | Button bar always fills 100% of terminal width. | `buttonbar.c` — `buttonbar_draw()` |
| 22 | `[ ]` | **Button bar — exact default label text** | Labels verified: `1Help 2Menu 3View 4Edit 5Copy 6RenMov 7Mkdir 8Delete 9PullDn 10Quit` — spacing is column-width-controlled, no embedded padding spaces. No action needed. | Matches original MC labels. | `layout.c` — `init_labels()` |
| 23 | `[ ]` | **Command line — prompt format** | `CommandLineView` shows `…/parent/dir> ` prompt (truncated path with `> ` suffix). | Original MC prompt looks like the subshell's prompt. The current behaviour is a reasonable approximation; no change needed without subshell integration. | `command.c` — `do_cd()` |
| 24 | `[x]` | **Menu bar — "Left" and "Right" menu item duplication** | ~~`PanelMenuItems` had `_File listing` + `_Listing format...` (same dialog) and `_Tree` (overlay) + `_Tree` (dialog-based).~~ Fixed: consolidated to single `_Listing format...` item; dialog-based `_Tree` removed; menu tests updated. | Clean Left/Right panel menu with no duplicate items. | `midnight.c` — left/right panel menu |
| 25 | `[x]` | **File panel — "free space" line in panel footer** | Fixed as part of #16. `UpdateStatus()` appends `, X free` using `DriveInfo`. | `14 files, 3 dirs, 12.4G free`. | `panel.c` — `show_dir()` free space |
| 26 | `[ ]` | **File panel — quick search: cursor blink position** | Quick-search indicator ends with painted `_` instead of a blinking terminal cursor. | MC positions the actual terminal cursor after the last typed character. Requires Terminal.Gui cursor placement API — deferred. | `panel.c` — `display_mini_info()` during search |
| 27 | `[x]` | **File panel — panel border colour for active/inactive** | Fixed as part of #6: the full frame (all four sides, all corners) uses `PanelHeader` when active and `PanelFrame` when inactive. | Entire border in `PanelHeader` when active; `PanelFrame` when inactive. | `panel.c` — `draw_frame()` |
| 28 | `[~]` | **File panel — column header marker column** | Header starts with ` Name` (leading space = marker column). Already correct — the leading space aligns with the `*` marker column in file entries. Confirmed no change needed. | `[space][Name][Size][space][Modify time]` — correct. | `panel.c` — `adjust_top_file()` |

---

## Tier 4 — Low impact (subtle, rarely noticed except by power users)

| # | Status | Area | Current behaviour | Expected (original MC) behaviour | Source reference |
|---|--------|------|-------------------|----------------------------------|-----------------|
| 29 | `[ ]` | **File panel — executable files: no `*` suffix** | Executables shown as plain name in BrightGreen. No `*` suffix option. | MC can append `*` to executable names (like `ls -F`). Requires panel option + name-width adjustment. | `panel.c` — `file_name_len()` |
| 30 | `[ ]` | **File panel — symlink-to-dir: should use directory colour** | Symlink-to-dir coloured as symlink (cyan). | Should use `PanelDirectory` when "Follow symlinks" is on. Requires `stat()` on target in `VfsDirEntry`. | `panel.c` — `file_compute_color()` |
| 31 | `[~]` | **File panel — column header: sort field highlighted** | Sort column name now has `↑`/`↓` indicator appended (fixed in #9). The column name is not drawn in a different colour (e.g. bold/inverse) — the indicator alone gives the visual cue. | Ideally the sorted column name would also use a distinct colour attribute. Deferred — the indicator is sufficient. | `panel.c` — column header drawing |
| 32 | `[x]` | **File panel — panel top-border: trailing slash after path** | ~~Path shown as `/home/user/Documents` (no trailing slash).~~ Fixed: `DrawBorderAndPath` appends `/` if `pathStr` doesn't already end with one. | `┌─── ~/Documents/ ───┐` | `panel.c` — `repaint_file()`, title path |
| 33 | `[x]` | **File panel — `.` (current dir) entry** | Confirmed: `DirectoryListing` does not include `.` entries. `VfsDirEntry.IsCurrentDir` exists but the VFS provider filters it. No change needed. | `.` never appears in panel listing. | `dir.c` — `do_load_dir()` filtering |
| 34 | `[x]` | **File panel — marked file count: "file" vs "files" singular/plural** | ~~Status always said "files" for any count.~~ Fixed: `UpdateStatus()` uses `marked == 1 ? "file" : "files"`. Also changed "marked" → "tagged" to match original MC wording. | `1 file, 1.2K tagged`; `3 files, 4.5M tagged`. | `panel.c` — `show_dir()` |
| 35 | `[x]` | **Button bar — remainder pixel column** | Fixed as part of #21. | Bar fills 100% of terminal width. | `buttonbar.c` — `buttonbar_draw()` |
| 36 | `[x]` | **Menu bar — `_Tools` menu** | `_Tools` menu is present (added in a previous session) between `_Command` and `_Options`. No change needed — user confirmed to keep it. | `_Tools` menu present. | `midnight.c` — `tools_menu[]` |
| 37 | `[ ]` | **Overall layout — top menubar background bleeds** | `McApplication.ColorScheme = McTheme.Panel` may let blue bleed between the menu bar and the panels. Terminal.Gui renders this correctly in practice — no visible artefact observed. | Deferred — not visually observable in current builds. | `layout.c` — `mc_main_loop()` background fill |
| 38 | `[x]` | **File panel — long path centering in title** | ~~Extra dash went to the right side when `dashTotal` was odd.~~ Fixed: `dashRight = dashTotal / 2; dashLeft = dashTotal - dashRight;` — extra dash now goes to the left, matching MC. | Left side gets the extra dash when centering is uneven. | `panel.c` — `draw_frame()` title centering |

---

## Summary

| Tier | Total | Done | Partial | Not started |
|------|-------|------|---------|-------------|
| 1 — Critical | 7 | 7 | 0 | 0 |
| 2 — High | 9 | 7 | 0 | 2 |
| 3 — Medium | 12 | 8 | 1 | 3 |
| 4 — Low | 10 | 6 | 1 | 3 |
| **Total** | **38** | **28** | **2** | **8** |
