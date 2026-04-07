# McEdit (Built-in Editor) - Current Functionality Analysis

**Date:** 2026-04-07  
**Purpose:** Comprehensive analysis of existing editor features before implementing new functionality

---

## Architecture Overview

### Core Components

1. **EditorView.cs** (~2800 lines)
   - Main UI component (Terminal.Gui View)
   - Handles rendering, input, mouse events
   - Implements all Execute* commands
   - Manages display modes (text, hex)

2. **EditorController.cs** (~1200 lines)
   - Business logic layer
   - Text buffer manipulation
   - Undo/redo stack
   - Search/replace operations
   - Bookmark management

3. **TextBuffer.cs** (~300 lines)
   - Gap buffer implementation
   - Efficient text editing
   - Line/column to offset conversion
   - File I/O

4. **LargeFileBuffer.cs** (~600 lines)
   - Read-only view for large files
   - Windowed loading (doesn't load entire file)
   - Stream-based search
   - Async index building

5. **SyntaxHighlighter.cs** (~800 lines)
   - Regex-based tokenization
   - 20+ language support
   - Extensible rule system

6. **EditorScreen.cs** (~400 lines)
   - Top-level window container
   - Menu bar and button bar
   - Multi-file management
   - File history

---

## Current Features (73 Execute Commands)

### File Operations
- ✅ **New file** (Ctrl+N)
- ✅ **Open file** (F3)
- ✅ **Save** (F2)
- ✅ **Save As** (Shift+F2)
- ✅ **Close** (F10)
- ✅ **Insert file** at cursor
- ✅ **Save block** to file
- ✅ **Load full file** (for large files in view mode)

### Edit Operations
- ✅ **Undo** (Ctrl+U)
- ✅ **Redo** (Ctrl+Y)
- ✅ **Cut** (Ctrl+X / Shift+Del)
- ✅ **Copy** (Ctrl+C / Ctrl+Ins)
- ✅ **Paste** (Ctrl+V / Shift+Ins)
- ✅ **Delete block**
- ✅ **Move block**
- ✅ **Copy block**

### Selection Modes
- ✅ **Toggle mark** (F3) - stream selection
- ✅ **Mark column** (Alt+B) - rectangular/column block
- ✅ **Mark all** (Ctrl+A)
- ✅ **Unmark** (Esc)

### Clipboard Support
- ✅ **System clipboard** (Ctrl+C/X/V)
- ✅ **Internal clipfile** (F5/F6/F8 for copy/cut/paste)
- ✅ **Column block** clipboard (rectangular paste)

### Search & Replace
- ✅ **Search** (F7) with options:
  - Case sensitive
  - Regular expression
  - Backwards search
  - Whole words
- ✅ **Search continue** (Shift+F7)
- ✅ **Replace** (F4)
- ✅ **Replace continue** (Shift+F4)
- ✅ **Replace all**
- ✅ **Stream search** for large files

### Navigation
- ✅ **Go to line** (Alt+L)
- ✅ **Go to top** (Ctrl+Home)
- ✅ **Go to bottom** (Ctrl+End)
- ✅ **Match bracket** (Alt+B) - finds matching (), [], {}
- ✅ **Word left/right** (Ctrl+Left/Right)
- ✅ **Page up/down**
- ✅ **Scroll without cursor** (Ctrl+Up/Down)

### Bookmarks
- ✅ **Toggle bookmark** (Ctrl+K)
- ✅ **Next bookmark** (Alt+Down)
- ✅ **Previous bookmark** (Alt+Up)
- ✅ **Flush bookmarks** (clear all)

### Display Modes
- ✅ **Toggle hex mode** (F8)
- ✅ **Toggle line numbers** (Alt+N)
- ✅ **Toggle syntax highlighting** (Ctrl+S)
- ✅ **Toggle right margin** (Alt+R)
- ✅ **Toggle show tabs/trailing whitespace** (Alt+T)
- ✅ **Toggle insert/overwrite** (Ins)

### Syntax Highlighting
- ✅ **20+ languages supported:**
  - C#, C/C++, Python, JavaScript/TypeScript
  - Go, Rust, Shell/Bash, JSON, XML/HTML
  - Markdown, Ruby, PHP, Java, CSS
  - YAML, TOML, Lua, R, Swift, Kotlin, Perl
- ✅ **Auto-detect** from file extension
- ✅ **Manual selection** via menu
- ✅ **Shebang detection** (#!/bin/bash, etc.)

### Text Formatting
- ✅ **Format paragraph** (Alt+P) - word wrap to margin
- ✅ **Sort block** (Alt+S) - pipe through `sort` command
- ✅ **Pretty print JSON** (Ctrl+J)
- ✅ **Pretty print XML** (Ctrl+X)
- ✅ **Shift block left** (Ctrl+Shift+Left)
- ✅ **Shift block right** (Ctrl+Shift+Right)

### Validation
- ✅ **Validate XML** well-formedness
- ✅ **Validate XSD** schema
- ✅ **Validate XML against XSD**

### Advanced Features
- ✅ **Macro recording** (Ctrl+R start/stop)
- ✅ **Macro playback** (Ctrl+P)
- ✅ **Macro save/load** to ~/.local/share/mc/mc.macros
- ✅ **Word completion** (Alt+Tab) - context-aware
- ✅ **Spell check** (Ctrl+P) - via aspell/hunspell
- ✅ **User menu** (~/.local/share/mc/mcedit/menu)
- ✅ **External command** (Ctrl+`) - paste output
- ✅ **External formatter** - pipe through command
- ✅ **Insert literal** (Ctrl+Q) - quote next char
- ✅ **Insert date/time** (Ctrl+D)

### Hex Editor Mode
- ✅ **Hex view/edit** toggle (F8)
- ✅ **16 bytes per row** display
- ✅ **ASCII column** on right
- ✅ **Nibble editing** (type hex digits)
- ✅ **Navigate in hex/ASCII** areas
- ✅ **Save modified bytes**

### Mouse Support
- ✅ **Click to position cursor**
- ✅ **Drag to select** text
- ✅ **Triple-click** to select line
- ✅ **Mouse wheel** scrolling
- ✅ **Column block** selection with mouse

### Settings & Configuration
- ✅ **Options dialog** - configure:
  - Tab width
  - Auto-indent
  - Typewriter wrap
  - Right margin column
  - Confirm save
  - Show line numbers
  - Show tabs/trailing whitespace
- ✅ **Save mode dialog** - line endings:
  - Unix (LF)
  - Windows (CRLF)
  - Mac (CR)
- ✅ **Encoding selection** (UTF-8, ASCII, etc.)
- ✅ **Persistent settings** (~/.config/mc/mcedit.ini)
- ✅ **File position memory** (remembers cursor position per file)

### About/Help
- ✅ **License** information
- ✅ **GitHub** link
- ✅ **Fork from** details
- ✅ **Why forked** explanation
- ✅ **New functions** list
- ✅ **System info** display

---

## Technical Capabilities

### Text Buffer
- **Gap buffer** for efficient editing
- **Undo/redo** with composite operations
- **Line ending detection** (LF/CRLF/CR)
- **Large file support** (windowed loading for files >10MB)
- **UTF-8** encoding support

### Search Engine
- **Literal** text search
- **Regular expression** (System.Text.RegularExpressions)
- **Case-sensitive/insensitive**
- **Whole word** matching
- **Backward** search
- **Stream search** for large files (doesn't load entire file)

### Performance
- **Lazy syntax highlighting** (only visible lines)
- **Incremental rendering**
- **Efficient gap buffer** (O(1) insert/delete at cursor)
- **Windowed large file** loading (constant memory)

---

## Comparison with GNU mcedit

| Feature | GNU mcedit | Our Implementation | Status |
|---------|-----------|-------------------|--------|
| Basic editing | ✅ | ✅ | Equal |
| Syntax highlighting | ✅ Limited | ✅ 20+ languages | Superior |
| Hex editor | ✅ | ✅ | Equal |
| Macro recording | ✅ | ✅ | Equal |
| Large file support | ✅ | ✅ Windowed | Superior |
| Column blocks | ✅ | ✅ | Equal |
| Search/replace | ✅ | ✅ Regex | Superior |
| Spell check | ✅ | ✅ | Equal |
| User menu | ✅ | ✅ | Equal |
| Pretty print JSON/XML | ❌ | ✅ | Superior |
| XML validation | ❌ | ✅ | Superior |
| Mouse support | ✅ Basic | ✅ Full | Superior |
| File position memory | ❌ | ✅ | Superior |

---

## Missing Features (Potential Enhancements)

### From Total Commander / Other Editors

1. **Code Folding**
   - Collapse/expand code blocks
   - Fold by indentation or syntax

2. **Multiple Cursors**
   - Edit multiple locations simultaneously
   - Column editing mode

3. **Split View**
   - Horizontal/vertical split
   - View same file in two panes

4. **Diff Mode**
   - Side-by-side comparison
   - Merge changes

5. **Auto-completion**
   - Context-aware suggestions
   - Snippet expansion

6. **Refactoring**
   - Rename symbol
   - Extract method
   - Organize imports

7. **Git Integration**
   - Show git blame
   - Stage/unstage hunks
   - Commit from editor

8. **LSP Support**
   - Language Server Protocol
   - Real-time diagnostics
   - Go to definition
   - Find references

9. **Terminal Integration**
   - Embedded terminal
   - Run commands in editor

10. **Session Management**
    - Save/restore open files
    - Workspace support

11. **Advanced Search**
    - Search in files (grep)
    - Search results panel
    - Replace in files

12. **Minimap**
    - Code overview
    - Quick navigation

13. **Breadcrumbs**
    - Show current function/class
    - Navigate hierarchy

14. **Snippets**
    - Template expansion
    - Custom snippets

15. **Emmet Support**
    - HTML/CSS abbreviations

---

## Code Quality

### Strengths
- ✅ Clean separation of concerns (View/Controller/Buffer)
- ✅ Comprehensive undo/redo system
- ✅ Efficient data structures (gap buffer)
- ✅ Extensive mouse support
- ✅ Good error handling
- ✅ Persistent settings

### Areas for Improvement
- ⚠️ Large EditorView.cs file (~2800 lines)
- ⚠️ Some complex methods (OnKeyDown ~500 lines)
- ⚠️ Limited unit test coverage
- ⚠️ Regex-based syntax highlighting (could use tree-sitter)

---

## Performance Characteristics

### Fast Operations (O(1) or O(log n))
- Insert/delete at cursor (gap buffer)
- Move cursor
- Undo/redo
- Bookmark operations

### Moderate Operations (O(n))
- Search (linear scan)
- Replace all
- Syntax highlighting (per line)
- Format paragraph

### Slow Operations (O(n²) or worse)
- Sort block (external process)
- Spell check (external process)
- Large file initial indexing

---

## Dependencies

### External Tools (Optional)
- **aspell/hunspell** - spell checking
- **sort** - block sorting
- **xclip/xsel/pbcopy** - system clipboard (Linux/macOS)

### .NET Libraries
- **System.Text.RegularExpressions** - search/syntax
- **System.Xml** - XML validation
- **System.Text.Json** - JSON formatting
- **Terminal.Gui** - UI framework

---

## Configuration Files

### User Settings
- `~/.config/mc/mcedit.ini` - editor settings
- `~/.local/share/mc/mc.macros` - saved macros
- `~/.local/share/mc/mcedit/menu` - user menu
- `~/.local/share/mc/mcedit.filepos` - file positions

### System Files
- `/usr/share/mc/mcedit/menu` - default user menu
- `/usr/share/mc/syntax/` - syntax definitions (not used, we have built-in)

---

## Key Bindings Summary

### File
- F2 - Save
- Shift+F2 - Save As
- F3 - Open
- Ctrl+N - New
- F10 - Close

### Edit
- Ctrl+U - Undo
- Ctrl+Y - Redo
- Ctrl+C - Copy
- Ctrl+X - Cut
- Ctrl+V - Paste
- F3 - Mark
- Alt+B - Column block
- Ctrl+A - Select all

### Search
- F7 - Find
- Shift+F7 - Find next
- F4 - Replace
- Shift+F4 - Replace next

### Navigation
- Alt+L - Go to line
- Ctrl+Home - Top
- Ctrl+End - Bottom
- Ctrl+K - Toggle bookmark
- Alt+Up/Down - Prev/next bookmark

### View
- F8 - Hex mode
- Alt+N - Line numbers
- Ctrl+S - Syntax highlighting
- Alt+R - Right margin
- Alt+T - Show tabs

### Advanced
- Ctrl+R - Record macro
- Ctrl+P - Play macro / Spell check
- Alt+Tab - Word completion
- Ctrl+Q - Insert literal
- Ctrl+D - Insert date/time
- Alt+S - Sort block
- Ctrl+J - Pretty print JSON
- Ctrl+X - Pretty print XML

---

## Recommendations for New Features

### High Priority (Most Useful)
1. **Code folding** - improves navigation in large files
2. **Split view** - compare/edit multiple sections
3. **Search in files** - find across project
4. **Git integration** - show changes, blame

### Medium Priority
5. **Multiple cursors** - productivity boost
6. **Auto-completion** - context-aware suggestions
7. **Minimap** - quick navigation
8. **Session management** - restore workspace

### Low Priority (Nice to Have)
9. **LSP support** - requires significant infrastructure
10. **Refactoring tools** - language-specific
11. **Terminal integration** - complex UI changes
12. **Emmet support** - niche use case

---

## Conclusion

The current mcedit implementation is **feature-complete** for a traditional text editor, with several **superior features** compared to GNU mcedit:

- Better syntax highlighting (20+ languages)
- JSON/XML pretty printing and validation
- Large file windowed loading
- File position memory
- Full mouse support

The architecture is **solid and extensible**, making it straightforward to add new features. The gap buffer and controller/view separation provide a clean foundation for enhancements.

**Next steps:** Identify specific features to implement based on user needs and Total Commander comparison.
