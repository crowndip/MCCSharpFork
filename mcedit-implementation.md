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
| **Command** (Alt+C)              | ✅     | Goto Line, Toggle Line Numbers, Match Bracket, Toggle Syntax, Right Margin, Encoding, Refresh, Macro (record/delete), Spell Check (word/language), Mail |
| **Format** (Alt+M)               | ✅     | Insert Literal, Insert Date/Time, Format Paragraph, Sort, Paste Output, External Formatter |
| **Window** (Alt+W)               | 🔄     | Toggle fullscreen (stub), List (current file only), Open Another File |
| **Options** (Alt+O)              | ✅     | General options dialog, Save mode dialog, Syntax Highlighting chooser, Visible tabs toggle, Learn keys, Syntax file, Menu file |

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
| Shift+PgUp/PgDn           | ✅     | Extend selection one page up/down |
| Ctrl+Shift+Home/End       | ✅     | Extend selection to file start/end |
| Ctrl+Shift+Left/Right     | ✅     | Extend selection to word boundary |
| Ctrl+Shift+Up/Down        | ✅     | Extend selection one page up/down (scroll) |
| Ctrl+Shift+PgUp/PgDn      | ✅     | Extend selection to file start/end |
| Ctrl+A (select all)       | ✅     | |
| Alt+B (toggle column mode)| ✅     | |
| Column selection (Alt+Arrows) | ✅  | Alt+Arrow keys expand column selection in `EditorView` |

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
| Ruby syntax                      | ✅     | `.rb` |
| PHP syntax                       | ✅     | `.php` |
| Java syntax                      | ✅     | `.java` |
| CSS syntax                       | ✅     | `.css` |
| YAML syntax                      | ✅     | `.yaml`/`.yml` |
| TOML syntax                      | ✅     | `.toml` |
| Lua syntax                       | ✅     | `.lua` |
| R syntax                         | ✅     | `.r`/`.R` |
| Swift syntax                     | ✅     | `.swift` |
| Kotlin syntax                    | ✅     | `.kt`/`.kts` |
| Syntax chooser dialog            | ✅     | Full ListView with all 20 languages; applies selection |
| Toggle syntax (Ctrl+S / menu)    | ✅     | |
| 152 language support (MC style)  | ❌     | 20 built-in; no .syntax file loading |
| First-line regex matching        | ✅     | Shebang (`#!`) + Emacs `-*- mode: -*-` detection |
| User override syntax files       | ❌     | Not implemented |

---

## 5. Dialog Boxes

| Dialog                           | Status | Notes |
|----------------------------------|--------|-------|
| Search dialog                    | ✅     | Case-sensitive, regex, backward, whole-word |
| Replace dialog                   | ✅     | Find Next + Replace All |
| Save As dialog                   | ✅     | Filename prompt + LF/CRLF/CR/As-is line-ending choice |
| Go to Line dialog                | ✅     | |
| Word Completion popup            | ✅     | ListView with completions |
| About dialog                     | ✅     | |
| General Options dialog           | ✅     | Tab width, expand tabs, line numbers, syntax, right margin, visible tabs |
| Save Mode dialog                 | ✅     | Quick/Safe/Backup modes wired to `EditorSettings.SaveMode` |
| Sort dialog                      | ✅     | Sort command input |
| External Command dialog          | ✅     | Shell command → insert output |
| Insert File dialog               | ✅     | |
| Save Block dialog                | ✅     | |
| File History dialog              | ✅     | MRU list with ListView |
| Spell Check dialog               | ✅     | aspell integration |
| Syntax Highlighting dialog       | ✅     | Full ListView with all 20 languages |
| Encoding Selection dialog        | ✅     | Common encodings list; note about UTF-8 session |
| Quit Confirmation dialog         | ✅     | Save/Discard/Cancel |
| Macro Management dialogs         | ✅     | Record/play + delete macro dialog |
| Mail dialog                      | ✅     | Informational stub (no mail binary dependency) |
| etags/Find Declaration dialog    | ❌     | Not implemented |
| Window List dialog               | ✅     | Shows current file; multi-window stub |

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
| Macro recording & playback       | ✅     | Persisted to `~/.local/share/mc/mc.macros` |
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
| Visible tabs/whitespace          | ✅     | Tabs as `→`; trailing spaces as `·` |
| Line number gutter               | ✅     | |
| File load/save                   | ✅     | UTF-8; line ending detection |
| Line ending preservation         | ✅     | Detected on load; Save As offers LF/CRLF/CR/As-is choice |
| Multi-window editing             | ❌     | One window at a time |
| Window move/resize               | ❌     | |
| etags/ctags navigation           | ❌     | |
| Encoding selection               | ❌     | |
| User menu (F11)                  | ✅     | Parses `~/.local/share/mc/mcedit/menu`; `%f/%n/%x/%d/%l/%c` macros |
| Typewriter word-wrap while typing| ✅     | `CheckTypewriterWrap()` on each char insert |
| Dynamic paragraph mode           | ❌     | |
| Macro key assignment/persistence | ✅     | Auto-saved to `~/.local/share/mc/mc.macros` on stop |
| File locking                     | ❌     | |
| Backup save mode                 | ✅     | `SaveMode=2` in `EditorController.Save()` |
| Binary file display (caret notation) | ❌  | |

---

## 7. Mouse Support

| Feature                          | Status | Notes |
|----------------------------------|--------|-------|
| Click to position cursor         | ✅     | Accounts for gutter + scroll offsets |
| Drag to select text              | ❌     | Drag detection not implemented |
| Double-click select word         | ✅     | |
| Triple-click select line         | ✅     | 400ms window, selects full line |
| Scroll wheel (±2 lines)          | ✅     | |
| Click F-key button bar           | ✅     | `EditorButtonBar.OnMouseClick` |
| Click menu bar items             | ✅     | Terminal.Gui MenuBar handles this |

---

## 8. Configuration Options

| Option                           | Status | Notes |
|----------------------------------|--------|-------|
| Tab spacing (default 8)          | ✅     | `_editor.TabWidth` |
| Fill tabs with spaces            | ✅     | `_editor.ExpandTabs` |
| Auto-indent on Enter             | ✅     | Toggled via `EditorSettings.AutoIndent` |
| Show line numbers                | ✅     | Toggle Alt+N |
| Syntax highlighting              | ✅     | Toggle Ctrl+S |
| Right margin column              | ✅     | Default 72, configurable in Options |
| Visible tabs                     | ✅     | Toggle Alt+_ |
| Word wrap line length            | ✅     | Used by FormatParagraph |
| Save mode (quick/safe/backup)    | ✅     | `EditorSettings.SaveMode`; 0=quick, 1=safe, 2=backup |
| Confirm before saving            | ✅     | `_confirmSave` flag in `EditorView` |
| Save file position               | ✅     | `SaveFilePosition()` writes `~/.local/share/mc/filepos` |
| Persistent selection             | ❌     | Not implemented |
| Group undo                       | ❌     | Not implemented |
| Backspace through tab stops      | ✅     | `BackspaceThruTabs` in `EditorSettings` + `EditorController` |
| Settings persistence (~/.config) | ✅     | `EditorSettings.Load()/Save()` reads/writes `~/.config/mc/ini` |

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
| Syntax type display              | ✅     | `SyntaxHighlighter.SyntaxName` shown in status bar |

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
| Keyboard Shortcuts| 49   | 0       | 2           |
| Syntax Highlighting| 23  | 0       | 3           |
| Dialog Boxes      | 20   | 0       | 2           |
| Editor Features   | 35   | 1       | 7           |
| Mouse Support     | 6    | 0       | 1           |
| Configuration     | 13   | 0       | 3           |
| Status Bar        | 11   | 0       | 0           |
| Color Scheme      | 12   | 0       | 1           |
| **TOTAL**         | **180** | **2** | **22**     |

---

## Next Implementation Priorities

### High Priority
1. **Multi-window support** – Window menu Next/Prev/List, multiple editor views
2. **Drag-to-select** – Mouse drag text selection
3. **Persistent selection** – Keep selection after non-shift cursor movement

### Medium Priority
4. **etags/ctags navigation** – Alt+Enter find declaration
5. **Encoding at load/save** – iconv-style re-read with chosen encoding
6. **File locking** – Prevent concurrent edits
7. **Group undo** – Group related ops into single undo step
8. **User `.syntax` file loading** – Load from `~/.local/share/mc/syntax/`

### Lower Priority
9. **Skin/theme loading** – `~/.config/mc/skins/`
10. **Binary file display** – Caret notation for control chars
11. **Dynamic paragraph mode** – Live word-wrap as you type
12. **152+ language support** – Load all MC `.syntax` files
