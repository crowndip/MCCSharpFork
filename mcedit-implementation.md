# MCEdit Implementation Progress

Track implementation status against `mcedit-specifications.md`.

**Legend:** ✅ Done · 🔄 Partial · ❌ Not started

---

## 1. Visual Layout

| Element                          | Status | Notes |
|----------------------------------|--------|-------|
| Menu Bar (F/E/S/C/M/W/O)        | ✅     | All 7 menus in `EditorScreen.cs` |
| Editor area with line numbers    | ✅     | Gutter with optional line numbers |
| Status bar (Ln/Col/Mode/State)   | ✅     | `StatusText` property in `EditorView` |
| F-key button bar (1Help…0Quit)   | ✅     | `EditorButtonBar` in `EditorScreen.cs` |
| Window title (modified indicator)| ✅     | `Title` property shows filename + `*` |
| Windowed vs fullscreen mode      | ❌     | Always fullscreen in current impl |
| Double-line/single-line borders  | ❌     | Multi-window not implemented |
| Resize handle                    | ❌     | Requires multi-window support |

---

## 2. Menu System

| Menu                             | Status | Notes |
|----------------------------------|--------|-------|
| **File** (Alt+F)                 | ✅     | Open, New, Close, History, Save, Save As, Insert File, Copy to File, User Menu, About, Quit |
| **Edit** (Alt+E)                 | ✅     | Undo, Redo, Toggle insert, Mark, Mark Column, Mark All, Unmark, Copy, Move, Delete, Clipboard ops, Top/Bottom |
| **Search** (Alt+S)               | ✅     | Search, Search Again, Replace, Bookmarks (Toggle/Next/Prev/Flush) |
| **Command** (Alt+C)              | ✅     | Goto Line, Toggle Line Numbers, Match Bracket, Toggle Syntax, Right Margin, Refresh, Macro, Spell Check |
| **Format** (Alt+M)               | ✅     | Insert Literal, Insert Date/Time, Format Paragraph, Sort, Paste Output |
| **Window** (Alt+W)               | 🔄     | Toggle fullscreen (stub), Open Another File; no Next/Prev/List for multi-window |
| **Options** (Alt+O)              | ✅     | General options dialog, Save mode dialog, Syntax Highlighting chooser, Visible tabs toggle |

---

## 3. Keyboard Shortcuts

### Navigation
| Shortcut                  | Status | Notes |
|---------------------------|--------|-------|
| Arrow keys                | ✅     | |
| Ctrl+Left/Right (word)    | ✅     | |
| Home/End                  | ✅     | |
| PgUp/PgDn                 | ✅     | |
| Ctrl+Home/End (file start/end) | ✅  | |
| Ctrl+Up/Down (scroll)     | ✅     | Scrolls viewport without moving cursor |
| Ctrl+PgUp/PgDn (top/bottom on screen) | ✅ | |
| Alt+L / Ctrl+G (Go to line) | ✅   | |
| Alt+B (match bracket)     | ✅     | Alt+[ also works |

### Editing
| Shortcut                  | Status | Notes |
|---------------------------|--------|-------|
| Enter (auto-indent)       | ✅     | |
| Shift+Enter (no indent)   | ✅     | |
| Backspace / Delete        | ✅     | |
| Ctrl+Y (delete line)      | ✅     | |
| Ctrl+K (delete to EOL)    | ✅     | |
| Alt+Backspace (del word begin) | ✅ | `DeleteToWordBegin()` |
| Alt+D (del word end)      | ✅     | `DeleteToWordEnd()` |
| Tab (with expand-tabs)    | ✅     | |
| Shift+Tab (dedent)        | ✅     | |
| Insert (toggle ins/ovr)   | ✅     | |
| Ctrl+U / Ctrl+Z (Undo)   | ✅     | |
| Ctrl+Shift+Z (Redo)       | ✅     | |

### Selection
| Shortcut                  | Status | Notes |
|---------------------------|--------|-------|
| F3 (toggle mark)          | ✅     | |
| Shift+F3 (column mark)    | ✅     | |
| Shift+Arrows              | ✅     | Stream selection |
| Ctrl+A (select all)       | ✅     | |
| Alt+B (toggle column mode)| ✅     | |
| Column selection (Alt+Arrows) | ❌  | Not yet implemented in key handler |

### Block Operations
| Shortcut                  | Status | Notes |
|---------------------------|--------|-------|
| F5 (copy block)           | ✅     | |
| F6 (move block)           | ✅     | |
| F8 (delete block)         | ✅     | |
| Ctrl+Ins (copy to clip)   | ✅     | |
| Shift+Ins (paste)         | ✅     | |
| Shift+Del (cut to clip)   | ✅     | |
| Ctrl+F (save block)       | ✅     | |
| Shift+F5 (insert file)    | ✅     | |
| Tab/Shift+Tab on selection| ✅     | Block indent/dedent |

### Search & Replace
| Shortcut                  | Status | Notes |
|---------------------------|--------|-------|
| F7 (search)               | ✅     | With case-sensitive, regex, backward, whole-word options |
| Shift+F7 (search again)   | ✅     | |
| F4 (find+replace)         | ✅     | With Replace All |
| Shift+F4 (replace again)  | ✅     | |

### Bookmarks
| Shortcut                  | Status | Notes |
|---------------------------|--------|-------|
| Alt+K (toggle bookmark)   | ✅     | Visual indicator in gutter/line highlight |
| Alt+J (next bookmark)     | ✅     | |
| Alt+I (prev bookmark)     | ✅     | Wrap-around supported |
| Alt+O (flush bookmarks)   | ✅     | |

### Display Toggles
| Shortcut                  | Status | Notes |
|---------------------------|--------|-------|
| Alt+N (toggle line numbers)| ✅    | |
| Ctrl+T / Ctrl+S (toggle syntax) | ✅ | |
| Right margin toggle       | ✅     | Via menu or `ExecuteToggleRightMargin()` |
| Visible tabs (Alt+_)      | ✅     | Tabs shown as `→` |

### Format / Misc
| Shortcut                  | Status | Notes |
|---------------------------|--------|-------|
| Ctrl+Q (quote-next / insert literal) | ✅ | |
| Ctrl+D (insert date/time) | ✅     | |
| Alt+P (format paragraph)  | ✅     | Basic word-wrap reflow |
| Alt+T (sort block)        | ✅     | Uses `sort` shell command |
| Alt+U (paste output of)   | ✅     | Runs shell command, inserts stdout |
| Ctrl+R (macro record/stop)| ✅     | |
| Ctrl+E (play macro)       | ✅     | |
| Ctrl+Tab (word completion)| ✅     | Popup for multiple matches |
| Ctrl+F5 (spell check)     | ✅     | aspell integration |
| Ctrl+L (refresh)          | ✅     | |
| F9 (menu)                 | ✅     | Activates MenuBar |
| F10 / Esc (quit)          | ✅     | With unsaved-changes prompt |

---

## 4. Syntax Highlighting

| Feature                          | Status | Notes |
|----------------------------------|--------|-------|
| C# syntax                        | ✅     | Keywords, strings, comments, numbers, types, preprocessor |
| C/C++ syntax                     | ✅     | |
| Python syntax                    | ✅     | |
| JavaScript/TypeScript            | ✅     | |
| Go syntax                        | ✅     | |
| Rust syntax                      | ✅     | |
| Shell (bash) syntax              | ✅     | |
| JSON syntax                      | ✅     | |
| XML/HTML syntax                  | ✅     | |
| Markdown syntax                  | ✅     | |
| Syntax chooser dialog            | 🔄     | Basic stub; full language list not yet wired |
| Toggle syntax (Ctrl+S / menu)    | ✅     | |
| 152 language support (MC style)  | ❌     | Only 10 built-in; no .syntax file loading |
| First-line regex matching        | ❌     | Not implemented |
| User override syntax files       | ❌     | Not implemented |

---

## 5. Dialog Boxes

| Dialog                           | Status | Notes |
|----------------------------------|--------|-------|
| Search dialog                    | ✅     | Case-sensitive, regex, backward, whole-word |
| Replace dialog                   | ✅     | Find Next + Replace All |
| Save As dialog                   | ✅     | Basic filename prompt |
| Go to Line dialog                | ✅     | |
| Word Completion popup            | ✅     | ListView with completions |
| About dialog                     | ✅     | |
| General Options dialog           | ✅     | Tab width, expand tabs, line numbers, syntax, right margin, visible tabs |
| Save Mode dialog                 | 🔄     | Informational only; settings not persisted |
| Sort dialog                      | ✅     | Sort command input |
| External Command dialog          | ✅     | Shell command → insert output |
| Insert File dialog               | ✅     | |
| Save Block dialog                | ✅     | |
| File History dialog              | ✅     | MRU list with ListView |
| Spell Check dialog               | ✅     | aspell integration |
| Syntax Highlighting dialog       | 🔄     | Basic language list; full integration pending |
| Encoding Selection dialog        | ❌     | Not implemented |
| Quit Confirmation dialog         | ✅     | Save/Discard/Cancel |
| Macro Management dialogs         | 🔄     | Record/play work; no key assignment dialog |
| Mail dialog                      | ❌     | Not implemented |
| etags/Find Declaration dialog    | ❌     | Not implemented |
| Window List dialog               | ❌     | Requires multi-window support |

---

## 6. Editor Features

| Feature                          | Status | Notes |
|----------------------------------|--------|-------|
| Gap buffer text storage          | ✅     | `TextBuffer.cs` |
| Multi-line editing               | ✅     | |
| Undo/Redo stack                  | ✅     | Insert/Delete operation recording |
| Stream (linear) selection        | ✅     | F3 + Shift+Arrows |
| Column (rectangular) selection   | ✅     | Shift+F3 + Alt+Arrows (visual) |
| Block copy/move/delete           | ✅     | F5/F6/F8 |
| Block indent/dedent              | ✅     | Tab/Shift+Tab on selection |
| Bookmarks with visual indicator  | ✅     | Yellow gutter + line highlight |
| Macro recording & playback       | ✅     | In-memory; no file persistence |
| Word completion                  | ✅     | Buffer scan + popup |
| Auto-indent on Enter             | ✅     | |
| Tab → spaces (expand tabs)       | ✅     | |
| Overwrite mode                   | ✅     | Insert key toggles |
| Delete word left/right           | ✅     | Alt+Backspace / Alt+D |
| Delete line (Ctrl+Y)             | ✅     | |
| Delete to EOL (Ctrl+K)           | ✅     | Joins lines when at EOL |
| Insert file                      | ✅     | |
| Save block to file               | ✅     | |
| Insert date/time                 | ✅     | |
| Insert literal (quote-next)      | ✅     | Ctrl+Q |
| Paragraph formatting             | ✅     | Basic word-wrap reflow |
| Sort block (via sort command)    | ✅     | |
| Paste output of command          | ✅     | |
| Spell checking                   | ✅     | Requires aspell |
| Bracket matching                 | ✅     | `()`, `[]`, `{}`, `<>` |
| Mouse click-to-position          | ✅     | |
| Mouse double-click (word select) | ✅     | |
| Mouse scroll wheel               | ✅     | ±2 lines |
| Right margin indicator           | ✅     | Vertical bar at configured column |
| Visible tabs/whitespace          | ✅     | Tabs shown as → |
| Line number gutter               | ✅     | |
| File load/save                   | ✅     | UTF-8; line ending detection |
| Line ending preservation         | 🔄     | Detected on load; Save As line-break choice not yet |
| Multi-window editing             | ❌     | One window at a time |
| Window move/resize               | ❌     | |
| etags/ctags navigation           | ❌     | |
| Encoding selection               | ❌     | |
| User menu (F11)                  | ❌     | Stub only |
| Typewriter word-wrap while typing| ❌     | |
| Dynamic paragraph mode           | ❌     | |
| Macro key assignment/persistence | ❌     | In-memory only |
| File locking                     | ❌     | |
| Backup save mode                 | ❌     | |
| Binary file display (caret notation) | ❌  | |

---

## 7. Mouse Support

| Feature                          | Status | Notes |
|----------------------------------|--------|-------|
| Click to position cursor         | ✅     | Accounts for gutter + scroll offsets |
| Drag to select text              | ❌     | Drag detection not implemented |
| Double-click select word         | ✅     | |
| Triple-click select line         | ❌     | |
| Scroll wheel (±2 lines)          | ✅     | |
| Click F-key button bar           | ✅     | `EditorButtonBar.OnMouseClick` |
| Click menu bar items             | ✅     | Terminal.Gui MenuBar handles this |

---

## 8. Configuration Options

| Option                           | Status | Notes |
|----------------------------------|--------|-------|
| Tab spacing (default 8)          | ✅     | `_editor.TabWidth` |
| Fill tabs with spaces            | ✅     | `_editor.ExpandTabs` |
| Auto-indent on Enter             | ✅     | Always enabled currently |
| Show line numbers                | ✅     | Toggle Alt+N |
| Syntax highlighting              | ✅     | Toggle Ctrl+S |
| Right margin column              | ✅     | Default 72, configurable in Options |
| Visible tabs                     | ✅     | Toggle Alt+_ |
| Word wrap line length            | ✅     | Used by FormatParagraph |
| Save mode (quick/safe/backup)    | ❌     | Not persisted |
| Confirm before saving            | ❌     | Not implemented |
| Save file position               | ❌     | Not implemented |
| Persistent selection             | ❌     | Not implemented |
| Group undo                       | ❌     | Not implemented |
| Settings persistence (~/.config) | ❌     | All options are in-memory only |

---

## 9. Status Bar

| Element                          | Status | Notes |
|----------------------------------|--------|-------|
| Filename display                 | ✅     | |
| Line number (Ln X)               | ✅     | |
| Column number (Col X)            | ✅     | |
| Insert/Overwrite mode (INS/OVR)  | ✅     | |
| Modified/Saved indicator         | ✅     | |
| Column block mode indicator      | ✅     | Shows `COL` |
| Line numbers mode indicator      | ✅     | Shows `NUMS` |
| No syntax highlight indicator    | ✅     | Shows `NOHL` |
| Quote-next indicator             | ✅     | Shows `QUOT` |
| Macro recording indicator        | ✅     | Shows `REC` |
| Syntax type display              | ❌     | Not shown yet |

---

## 10. Color Scheme

| Element                          | Status | Notes |
|----------------------------------|--------|-------|
| Normal text (white on black)     | ✅     | |
| Selected text (black on cyan)    | ✅     | |
| Status bar (black on cyan)       | ✅     | |
| Keywords (bright yellow)         | ✅     | |
| Comments (gray)                  | ✅     | |
| Strings (bright cyan)            | ✅     | |
| Numbers (bright magenta)         | ✅     | |
| Preprocessor/Type (bright green) | ✅     | |
| Bookmarked line (white on dark gray)| ✅  | |
| Bookmark gutter (yellow on dark gray)| ✅ | |
| Right margin indicator (dark gray)| ✅   | Vertical bar |
| F-key bar (white-on-blue + black-on-cyan)| ✅ | |
| Skin file loading                | ❌     | Colors hard-coded |

---

## Summary

| Category          | Done | Partial | Not Started |
|-------------------|------|---------|-------------|
| Visual Layout     | 5    | 0       | 3           |
| Menu System       | 6    | 1       | 0           |
| Keyboard Shortcuts| 42   | 0       | 3           |
| Syntax Highlighting| 11  | 2       | 3           |
| Dialog Boxes      | 13   | 3       | 6           |
| Editor Features   | 27   | 2       | 14          |
| Mouse Support     | 5    | 0       | 2           |
| Configuration     | 7    | 0       | 8           |
| Status Bar        | 10   | 0       | 1           |
| Color Scheme      | 12   | 0       | 1           |
| **TOTAL**         | **138** | **8** | **41**     |

---

## Next Implementation Priorities

### High Priority
1. **Multi-window support** – Window menu Next/Prev/List, multiple editor views
2. **Settings persistence** – Save options to `~/.config/mc/ini`
3. **More syntax languages** – Python multiline strings, more file types
4. **Drag-to-select** – Mouse drag selection
5. **Save As with line-ending choice** – LF/CRLF/CR options

### Medium Priority
6. **Triple-click line select**
7. **Column selection with Alt+Arrows**
8. **Macro persistence** (`~/.local/share/mc/mc.macros`)
9. **File locking** – Prevent concurrent edits
10. **User menu (F11)** – Parse and execute `~/.local/share/mc/mcedit/menu`

### Lower Priority
11. **etags/ctags navigation** – Alt+Enter find declaration
12. **Encoding selection** – iconv integration
13. **Skin/theme loading** – `~/.config/mc/skins/`
14. **Binary file display** – Caret notation for control chars
15. **Typewriter word-wrap** – Auto-wrap while typing
