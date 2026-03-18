# MCEdit (Midnight Commander Internal Editor) - Complete Specification

## Table of Contents

1. [Visual Layout](#1-visual-layout)
2. [Menu System](#2-menu-system)
3. [Keyboard Shortcuts](#3-keyboard-shortcuts)
4. [Syntax Highlighting](#4-syntax-highlighting)
5. [Dialog Boxes](#5-dialog-boxes)
6. [Editor Features](#6-editor-features)
7. [Mouse Support](#7-mouse-support)
8. [Configuration Options](#8-configuration-options)
9. [Status Bar](#9-status-bar)
10. [Color Scheme / Visual Styling](#10-color-scheme--visual-styling)

---

## 1. Visual Layout

MCEdit uses a multi-window terminal UI with the following top-to-bottom layout:

```
+------------------------------------------------------------------+
| Menu Bar (F/E/S/C/M/W/O)                                        |
+------------------------------------------------------------------+
|                                                                  |
| +--[filename.c]--[^]--[X]-+  <- Window Title Bar (non-fullscreen)|
| | Editor Area              |                                     |
| | (with optional line #s)  |                                     |
| |                          |                                     |
| |                          |                                     |
| +--------------------------#  <- Resize handle (bottom-right)    |
|                                                                  |
+------------------------------------------------------------------+
| Status Bar (line/col/position info)                              |
+------------------------------------------------------------------+
| 1Help 2Save 3Mark 4Replac 5Copy 6Move 7Search 8Delete 9PullDn 0Quit |
+------------------------------------------------------------------+
```

### Title Bar (Window Frame)
- Only shown in non-fullscreen (windowed) mode
- Contains: `[filename]` centered in the title
- Modified indicator: `(M)` appended to filename when file has unsaved changes
- Fullscreen toggle button: `[^]` character (configurable via skin `[widget-editor] window-state-char`)
- Close button: `[X]` character (configurable via skin `[widget-editor] window-close-char`)
- Active windows get double-line frame borders; inactive windows get single-line borders
- Double-clicking the title bar toggles fullscreen

### Menu Bar
- Located at the very top of the screen
- 7 menus accessed via F9 or Alt+letter hotkeys:
  - **F**ile (Alt+F), **E**dit (Alt+E), **S**earch (Alt+S), **C**ommand (Alt+C), for**M**at (Alt+M), **W**indow (Alt+W), **O**ptions (Alt+O)

### Editor Area
- Text content area with optional left gutter for line numbers
- Horizontal offset: `EDIT_TEXT_HORIZONTAL_OFFSET` (7) + line_state_width (when line numbers enabled)
- Vertical offset: `EDIT_TEXT_VERTICAL_OFFSET` (1)
- In non-fullscreen mode, additional +1 offset in each direction for the window frame
- Supports multiple simultaneous editor windows (tiled/overlapping)

### Status Bar
- Located between the editor area and the button bar
- Shows file state and cursor position (see Section 9 for details)

### Button Bar (F-Key Bar)
- Fixed at the bottom of the screen
- 10 function key labels:

| Key | Label   | Command          |
|-----|---------|------------------|
| F1  | Help    | CK_Help          |
| F2  | Save    | CK_Save          |
| F3  | Mark    | CK_Mark          |
| F4  | Replac  | CK_Replace       |
| F5  | Copy    | CK_Copy          |
| F6  | Move    | CK_Move          |
| F7  | Search  | CK_Search        |
| F8  | Delete  | CK_Remove        |
| F9  | PullDn  | CK_Menu          |
| F10 | Quit    | CK_Quit          |

---

## 2. Menu System

### 2.1 File Menu (Alt+F)

| Menu Item              | Accelerator Letter | Command          |
|------------------------|--------------------|------------------|
| Open file...           | O                  | CK_EditFile      |
| New                    | N                  | CK_EditNew       |
| Close                  | C                  | CK_Close         |
| History...             | H                  | CK_History       |
| Save                   | S                  | CK_Save          |
| Save as...             | a                  | CK_SaveAs        |
| Insert file...         | I                  | CK_InsertFile    |
| Copy to file...        | y                  | CK_BlockSave     |
| User menu...           | U                  | CK_UserMenu      |
| About...               | A                  | CK_About         |
| Quit                   | Q                  | CK_Quit          |

### 2.2 Edit Menu (Alt+E)

| Menu Item              | Accelerator Letter | Command             |
|------------------------|--------------------|---------------------|
| Undo                   | U                  | CK_Undo             |
| Redo                   | R                  | CK_Redo             |
| Toggle ins/overw       | T                  | CK_InsertOverwrite  |
| Toggle mark            | g                  | CK_Mark             |
| Mark columns           | M                  | CK_MarkColumn       |
| Mark all               | a                  | CK_MarkAll          |
| Unmark                 | k                  | CK_Unmark           |
| Copy                   | y                  | CK_Copy             |
| Move                   | v                  | CK_Move             |
| Delete                 | D                  | CK_Remove           |
| Copy to clipfile       | p                  | CK_Store            |
| Cut to clipfile        | C                  | CK_Cut              |
| Paste from clipfile    | s                  | CK_Paste            |
| Beginning              | B                  | CK_Top              |
| End                    | E                  | CK_Bottom           |

### 2.3 Search Menu (Alt+S)

| Menu Item              | Accelerator Letter | Command             |
|------------------------|--------------------|---------------------|
| Search...              | S                  | CK_Search           |
| Search again           | a                  | CK_SearchContinue   |
| Replace...             | R                  | CK_Replace          |
| Toggle bookmark        | T                  | CK_Bookmark         |
| Next bookmark          | N                  | CK_BookmarkNext     |
| Prev bookmark          | P                  | CK_BookmarkPrev     |
| Flush bookmarks        | F                  | CK_BookmarkFlush    |

### 2.4 Command Menu (Alt+C)

| Menu Item                       | Accelerator Letter | Command                    |
|---------------------------------|--------------------|----------------------------|
| Go to line...                   | G                  | CK_Goto                   |
| Toggle line state               | T                  | CK_ShowNumbers             |
| Go to matching bracket          | b                  | CK_MatchBracket            |
| Toggle syntax highlighting      | y                  | CK_SyntaxOnOff             |
| Toggle right margin             | l                  | CK_ShowMargin              |
| Find declaration                | F                  | CK_Find                   |
| Back from declaration           | d                  | CK_FilePrev                |
| Forward to declaration          | w                  | CK_FileNext                |
| Encoding...                     | i                  | CK_SelectCodepage          |
| Refresh screen                  | R                  | CK_Refresh                 |
| Start/Stop record macro         | S                  | CK_MacroStartStopRecord    |
| Delete macro...                 | o                  | CK_MacroDelete             |
| Record/Repeat actions           | a                  | CK_RepeatStartStopRecord   |
| Spell check*                    | p                  | CK_SpellCheck              |
| Check word*                     | h                  | CK_SpellCheckCurrentWord   |
| Change spelling language...*    | l                  | CK_SpellCheckSelectLang    |
| Mail...                         | M                  | CK_EditMail                |

*Spell check items only appear when compiled with HAVE_ASPELL.

### 2.5 Format Menu (Alt+M)

| Menu Item              | Accelerator Letter | Command             |
|------------------------|--------------------|---------------------|
| Insert literal...      | l                  | CK_InsertLiteral    |
| Insert date/time       | d                  | CK_Date             |
| Format paragraph       | F                  | CK_ParagraphFormat  |
| Sort...                | S                  | CK_Sort             |
| Paste output of...     | P                  | CK_ExternalCommand  |
| External formatter     | E                  | CK_PipeBlock(0)     |

### 2.6 Window Menu (Alt+W)

| Menu Item              | Accelerator Letter | Command              |
|------------------------|--------------------|----------------------|
| Move                   | M                  | CK_WindowMove        |
| Resize                 | R                  | CK_WindowResize      |
| Toggle fullscreen      | T                  | CK_WindowFullscreen  |
| Next                   | N                  | CK_WindowNext        |
| Previous               | P                  | CK_WindowPrev        |
| List...                | L                  | CK_WindowList        |

### 2.7 Options Menu (Alt+O)

| Menu Item                  | Accelerator Letter | Command              |
|----------------------------|--------------------|----------------------|
| General...                 | G                  | CK_Options           |
| Save mode...               | m                  | CK_OptionsSaveMode   |
| Learn keys...              | k                  | CK_LearnKeys         |
| Syntax highlighting...     | h                  | CK_SyntaxChoose      |
| Syntax file                | y                  | CK_EditSyntaxFile    |
| Menu file                  | M                  | CK_EditUserMenu      |
| Save setup                 | S                  | CK_SaveSetup         |

---

## 3. Keyboard Shortcuts

### 3.1 Default Keymap (mc.default.keymap)

#### Navigation

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| Up                   | Up                | Move cursor up                  |
| Down                 | Down              | Move cursor down                |
| Left                 | Left              | Move cursor left                |
| Right                | Right             | Move cursor right               |
| Ctrl+Left / Ctrl+Z  | WordLeft          | Move to previous word           |
| Ctrl+Right / Ctrl+X | WordRight         | Move to next word               |
| Home                 | Home              | Move to beginning of line       |
| End                  | End               | Move to end of line             |
| PgUp                 | PageUp            | Scroll one page up              |
| PgDn                 | PageDown          | Scroll one page down            |
| Ctrl+Home / Alt+<    | Top               | Move to beginning of file       |
| Ctrl+End / Alt+>     | Bottom            | Move to end of file             |
| Ctrl+Up              | ScrollUp          | Scroll display up               |
| Ctrl+Down            | ScrollDown        | Scroll display down             |
| Ctrl+PgUp            | TopOnScreen       | Move to top of visible screen   |
| Ctrl+PgDn            | BottomOnScreen    | Move to bottom of visible screen|
| Alt+L                | Goto              | Go to line number dialog        |
| Alt+B                | MatchBracket      | Go to matching bracket          |

#### Editing

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| Enter                | Enter             | Insert newline                  |
| Shift+Enter / Ctrl+Enter | Return       | Insert newline (alternate)      |
| Backspace / Ctrl+H   | BackSpace        | Delete character before cursor  |
| Delete / Ctrl+D      | Delete           | Delete character at cursor      |
| Tab / Shift+Tab      | Tab              | Insert tab / handle tab         |
| Insert               | InsertOverwrite   | Toggle insert/overwrite mode    |
| Ctrl+Y               | DeleteLine        | Delete entire line              |
| Ctrl+K               | DeleteToEnd       | Delete to end of line           |
| Alt+Backspace         | DeleteToWordBegin| Delete to start of word         |
| Alt+D                 | DeleteToWordEnd  | Delete to end of word           |
| Ctrl+U               | Undo             | Undo last action                |
| Alt+R                 | Redo             | Redo last undone action         |

#### File Operations

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| F2                   | Save              | Save file                       |
| F12 / Ctrl+F2        | SaveAs           | Save file as...                 |
| Ctrl+N               | EditNew           | Create new file                 |
| F10 / Esc            | Quit              | Quit editor                     |
| Alt+Shift+E          | History           | Open file history               |
| Ctrl+O               | Shell             | Toggle subshell                 |

#### Selection (Mark)

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| F3                   | Mark              | Toggle mark (start/stop select) |
| F13 (Shift+F3)       | MarkColumn       | Toggle column mark mode         |
| Shift+Up             | MarkUp            | Extend selection up             |
| Shift+Down           | MarkDown          | Extend selection down           |
| Shift+Left           | MarkLeft          | Extend selection left           |
| Shift+Right          | MarkRight         | Extend selection right          |
| Shift+Home           | MarkToHome        | Select to beginning of line     |
| Shift+End            | MarkToEnd         | Select to end of line           |
| Shift+PgUp           | MarkPageUp        | Select one page up              |
| Shift+PgDn           | MarkPageDown      | Select one page down            |
| Ctrl+Shift+Home      | MarkToFileBegin   | Select to beginning of file     |
| Ctrl+Shift+End       | MarkToFileEnd     | Select to end of file           |
| Ctrl+Shift+Left      | MarkToWordBegin   | Select to word beginning        |
| Ctrl+Shift+Right     | MarkToWordEnd     | Select to word end              |
| Ctrl+Shift+Up        | MarkScrollUp      | Select and scroll up            |
| Ctrl+Shift+Down      | MarkScrollDown    | Select and scroll down          |
| Ctrl+Shift+PgUp      | MarkToPageBegin   | Select to page beginning        |
| Ctrl+Shift+PgDn      | MarkToPageEnd     | Select to page end              |

#### Column Selection

| Shortcut             | Command              | Description                  |
|----------------------|----------------------|------------------------------|
| Alt+Up               | MarkColumnUp         | Column select up             |
| Alt+Down             | MarkColumnDown       | Column select down           |
| Alt+Left             | MarkColumnLeft       | Column select left           |
| Alt+Right            | MarkColumnRight      | Column select right          |
| Alt+PgUp             | MarkColumnPageUp     | Column select page up        |
| Alt+PgDn             | MarkColumnPageDown   | Column select page down      |

#### Block Operations

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| F5                   | Copy              | Copy block to cursor            |
| F6                   | Move              | Move block to cursor            |
| F8                   | Remove            | Delete selected block           |
| Ctrl+Insert          | Store             | Copy block to clipfile          |
| Shift+Insert         | Paste             | Paste from clipfile             |
| Shift+Delete         | Cut               | Cut block to clipfile           |
| Ctrl+F               | BlockSave         | Save block to file              |

#### Search & Replace

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| F7                   | Search            | Open search dialog              |
| F17 (Shift+F7)       | SearchContinue   | Search again                    |
| F4                   | Replace           | Open replace dialog             |
| F14 (Shift+F4)       | ReplaceContinue  | Replace again                   |

#### Bookmarks

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| Alt+K                | Bookmark          | Toggle bookmark on current line |
| Alt+J                | BookmarkNext      | Go to next bookmark             |
| Alt+I                | BookmarkPrev      | Go to previous bookmark         |
| Alt+O                | BookmarkFlush     | Remove all bookmarks            |

#### Macros

| Shortcut             | Command                | Description                |
|----------------------|------------------------|----------------------------|
| Ctrl+R               | MacroStartStopRecord   | Start/stop recording macro |

#### Display Options

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| Alt+N                | ShowNumbers       | Toggle line numbers             |
| Alt+_ (underscore)   | ShowTabTws       | Toggle visible tabs/whitespace  |
| Ctrl+S               | SyntaxOnOff      | Toggle syntax highlighting      |

#### Code Navigation (etags/ctags)

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| Alt+Enter            | Find              | Find declaration (TAGS file)    |
| Alt+Minus            | FilePrev          | Go back from declaration        |
| Alt+Plus             | FileNext          | Go forward to declaration       |

#### Other

| Shortcut             | Command           | Description                     |
|----------------------|-------------------|---------------------------------|
| Alt+Tab              | Complete          | Word completion                 |
| F15 (Shift+F5)       | InsertFile       | Insert file at cursor           |
| F1                   | Help              | Open help                       |
| F9                   | Menu              | Activate pull-down menus        |
| F11                  | UserMenu          | Open user menu                  |
| Ctrl+L               | Refresh           | Refresh screen                  |
| Ctrl+Q               | InsertLiteral     | Insert literal character        |
| Alt+E                | SelectCodepage    | Select character encoding       |
| Alt+T                | Sort              | Sort selected text              |
| Alt+M                | Mail              | Mail buffer                     |
| Alt+P                | ParagraphFormat   | Format paragraph                |
| Alt+U                | ExternalCommand   | Paste output of command         |

### 3.2 Emacs Keymap Differences (mc.emacs.keymap)

| Shortcut             | Command           | Notes                           |
|----------------------|-------------------|---------------------------------|
| Ctrl+P               | Up                | (like emacs previous-line)      |
| Ctrl+N               | Down              | (like emacs next-line)          |
| Ctrl+B               | Left              | (like emacs backward-char)      |
| Ctrl+F               | Right             | (like emacs forward-char)       |
| Alt+B                | WordLeft          | (like emacs backward-word)      |
| Alt+F                | WordRight         | (like emacs forward-word)       |
| Ctrl+A               | Home              | (like emacs beginning-of-line)  |
| Ctrl+E               | End               | (like emacs end-of-line)        |
| Alt+V                | PageUp            | (like emacs scroll-down)        |
| Ctrl+V               | PageDown          | (like emacs scroll-up)          |
| Alt+W                | Store             | (like emacs kill-ring-save)     |
| Ctrl+Y               | Paste             | (like emacs yank)               |
| Ctrl+W               | Cut               | (like emacs kill-region)        |
| Ctrl+S               | Search            | (like emacs isearch-forward)    |
| Ctrl+@ / Ctrl+Space  | Mark              | (like emacs set-mark-command)   |
| Ctrl+X               | ExtendedKeyMap    | Prefix for extended commands    |

### 3.3 Complete CK_* Command List (Editor-Specific)

```
CK_EditFile         CK_EditNew          CK_Close            CK_History
CK_Save             CK_SaveAs           CK_InsertFile       CK_BlockSave
CK_UserMenu         CK_About            CK_Quit             CK_QuitQuiet
CK_Undo             CK_Redo             CK_InsertOverwrite  CK_Mark
CK_MarkColumn       CK_MarkAll          CK_Unmark           CK_Copy
CK_Move             CK_Remove           CK_Store            CK_Cut
CK_Paste            CK_Top              CK_Bottom           CK_Search
CK_SearchContinue   CK_Replace          CK_ReplaceContinue  CK_Bookmark
CK_BookmarkNext     CK_BookmarkPrev     CK_BookmarkFlush    CK_Goto
CK_ShowNumbers      CK_MatchBracket     CK_SyntaxOnOff      CK_ShowMargin
CK_Find             CK_FilePrev         CK_FileNext         CK_SelectCodepage
CK_Refresh          CK_MacroStartStopRecord  CK_MacroStartRecord
CK_MacroStopRecord  CK_MacroDelete      CK_RepeatStartStopRecord
CK_SpellCheck       CK_SpellCheckCurrentWord  CK_SpellCheckSelectLang
CK_EditMail         CK_InsertLiteral    CK_Date
CK_ParagraphFormat  CK_Sort             CK_ExternalCommand  CK_PipeBlock(n)
CK_WindowMove       CK_WindowResize     CK_WindowFullscreen
CK_WindowList       CK_WindowNext       CK_WindowPrev
CK_Options          CK_OptionsSaveMode  CK_LearnKeys
CK_SyntaxChoose     CK_EditSyntaxFile   CK_EditUserMenu     CK_SaveSetup
CK_Tab              CK_Enter            CK_Return           CK_BackSpace
CK_Delete           CK_DeleteLine       CK_DeleteToEnd      CK_DeleteToWordBegin
CK_DeleteToWordEnd  CK_Up               CK_Down             CK_Left
CK_Right            CK_WordLeft         CK_WordRight        CK_Home
CK_End              CK_PageUp           CK_PageDown         CK_ScrollUp
CK_ScrollDown       CK_TopOnScreen      CK_BottomOnScreen
CK_ParagraphUp      CK_ParagraphDown    CK_MarkWord         CK_MarkLine
CK_MarkLeft         CK_MarkRight        CK_MarkUp           CK_MarkDown
CK_MarkToHome       CK_MarkToEnd        CK_MarkToWordBegin  CK_MarkToWordEnd
CK_MarkPageUp       CK_MarkPageDown     CK_MarkToFileBegin  CK_MarkToFileEnd
CK_MarkToPageBegin  CK_MarkToPageEnd    CK_MarkScrollUp     CK_MarkScrollDown
CK_MarkParagraphUp  CK_MarkParagraphDown
CK_MarkColumnUp     CK_MarkColumnDown   CK_MarkColumnLeft   CK_MarkColumnRight
CK_MarkColumnPageUp CK_MarkColumnPageDown
CK_MarkColumnScrollUp  CK_MarkColumnScrollDown
CK_MarkColumnParagraphUp  CK_MarkColumnParagraphDown
CK_BlockShiftLeft   CK_BlockShiftRight
CK_InsertLiteral    CK_ShowTabTws       CK_ShowMargin
CK_Complete         CK_Shell            CK_Menu             CK_Help
CK_ExecuteScript    CK_InsertChar
```

---

## 4. Syntax Highlighting

### 4.1 Architecture

- Syntax rules are defined in `.syntax` files located in `misc/syntax/`
- Master index file: `Syntax.in` (installed as `Syntax`) maps filename patterns to syntax files
- User overrides: `~/.local/share/mc/syntax/Syntax`
- System files: `%pkgdatadir%/syntax/`

### 4.2 File Matching

Files are matched to syntax definitions via three methods (in priority order):
1. Explicit type selection by user
2. Regex matching against filename
3. First-line content regex (for shebangs, XML declarations, etc.)

Master index format:
```
file <filename_regex> <description> [<first_line_regex>]
include <syntax_file>
```

### 4.3 Syntax File Format

#### Contexts
```
context [exclusive] [whole|wholeright|wholeleft] [linestart] <left_delim> [linestart] <right_delim> [foreground] [background] [attributes]
context default [foreground] [background] [attributes]
```

Contexts define regions of text (e.g., strings, comments) bounded by delimiters.

#### Keywords
```
keyword [whole|wholeright|wholeleft] [linestart] <string> <foreground> [background] [attributes]
```

Keywords are specific text patterns highlighted within a context.

#### Other Directives
- `wholechars [left|right] <characters>` - Define word boundary characters
- `caseinsensitive` - Case-insensitive matching for context
- `define <name> <expansion>` - Macro definitions
- `include <file>` - Include another syntax file

#### Special Characters in Patterns
- `\t` - Tab
- `\s` - Space
- `\n` - Newline
- `\\` - Literal backslash
- `\*` - Literal asterisk
- `*` - Wildcard (any characters, not as first/last char)

### 4.4 Available Colors

**Basic 16 colors:**
black, gray, red, brightred, green, brightgreen, brown, yellow, blue, brightblue, magenta, brightmagenta, cyan, brightcyan, lightgray, white

**Special colors:**
- `default` - terminal default
- `base` - MC main colors

**256-color support:**
- `color16` through `color255`
- `rgb000` through `rgb555` (6x6x6 color cube)
- `gray0` through `gray23` (grayscale ramp)

**Text attributes:**
bold, italic, underline, reverse, blink (combined with `+`)

### 4.5 Supported Languages (152 syntax files)

Programming: C, C++, Java, Python, JavaScript, TypeScript, Go, Rust, Ruby, Perl, PHP, C#, Kotlin, Swift, D, Pascal, Ada, Eiffel, Haskell, Erlang, Nemerle, Smalltalk, Lisp, ML/OCaml, Lua, Tcl, R, Fortran, COBOL, Assembly, VHDL, Verilog

Scripting: Shell (bash/sh/zsh), AWK, Sed, Python, Cython

Web: HTML, CSS, JavaScript, JSON, XML, PHP, ASPX

Markup: LaTeX, Texinfo, Nroff, Markdown

Config: INI, YAML, TOML, Conf, Properties, Dockerfile, Caddyfile

Build: Makefile, CMake, Meson, Spec (RPM), PKGBUILD, Ebuild

Data: SQL, HiveQL, JSON, YAML, XML, Turtle/RDF, Protobuf

GPU: CUDA, OpenCL, GLSL, OSL

Other: Diff, Mail, Changelog, PO (gettext), Strace, POV-Ray, Spice, and more

### 4.6 Syntax Chooser Dialog

An interactive listbox dialog offering:
- "Auto" detection (default)
- Reload current syntax
- Alphabetically sorted list of all available syntax types
- Selection persists via `auto_syntax` flag

### 4.7 Internal Color Application

Characters carry 16-bit style values with modifiers:
- `MOD_ABNORMAL (1<<8)` - Non-printable/control characters
- `MOD_BOLD (1<<9)` - Matched brackets, search results
- `MOD_MARKED (1<<10)` - Selected text regions
- `MOD_CURSOR (1<<11)` - Cursor position
- `MOD_WHITESPACE (1<<12)` - Visible tabs/spaces

Syntax colors are retrieved via `edit_get_syntax_color()` which uses a cache at 512-byte intervals for performance.

---

## 5. Dialog Boxes

### 5.1 Search Dialog

- **Title:** "Search"
- **Fields:**
  - Search string input (with history)
- **Options (two columns):**
  - Case sensitive (checkbox)
  - Backwards (checkbox)
  - In selection only (checkbox)
  - Whole words (checkbox)
  - All charsets (checkbox)
  - Search type (radio buttons): Normal, Regex, Hex
- **Buttons:** [OK] [Find All] [Cancel]

### 5.2 Replace Dialog

- **Title:** "Replace"
- **Fields:**
  - Search string input (with history)
  - Replacement string input (with history)
- **Options:** Same as Search Dialog
- **Buttons:** [OK] [Cancel]
- **Replace confirmation prompt per match:** [Replace] [Skip] [Replace All] [Cancel]

### 5.3 Save As Dialog

- **Fields:**
  - Filename input (with history)
  - Line break format (radio): LF (Unix), CRLF (Windows), CR (Mac)
- **Buttons:** [OK] [Cancel]
- Includes overwrite protection confirmation

### 5.4 Go to Line Dialog

- **Title:** "Go to line"
- **Fields:**
  - Line number input
- Supports negative numbers (count from end of file)
- **Buttons:** [OK] [Cancel]

### 5.5 Save Mode Dialog

- **Options (radio buttons):**
  - Quick save (truncate and write)
  - Safe save (write to temp, then rename)
  - Create backups (backup extension configurable)
- **Fields:**
  - Backup extension input (e.g., `~`)
- **Checkboxes:**
  - Check POSIX newline at EOF
- **Buttons:** [OK] [Cancel]

### 5.6 General Options Dialog

Two-column layout:

**Left Column - Wrap Mode & Tabulation:**
- Wrap mode (radio): None, Dynamic paragraphing, Typewriter wrap
- Fake half tabs (checkbox)
- Backspace through tabs (checkbox)
- Fill tabs with spaces (checkbox)
- Tab spacing input field

**Right Column - Other Options:**
- Return does autoindent (checkbox)
- Confirm before saving (checkbox)
- Save file position (checkbox)
- Visible trailing spaces (checkbox)
- Visible tabs (checkbox)
- Syntax highlighting (checkbox)
- Cursor after inserted block (checkbox)
- Persistent selection (checkbox)
- Cursor beyond end of line (checkbox)
- Group undo (checkbox)
- Word wrap line length input field

**Buttons:** [OK] [Cancel]

### 5.7 Insert Literal Dialog

- Raw key capture dialog
- Captures a single keypress and inserts it as a literal character (including control characters)
- Optional Cancel button

### 5.8 Sort Dialog

- **Fields:**
  - Sort command/options input (default: `sort` with flags)
- Operates on selected block
- Saves block to temp file, runs sort, reinserts result

### 5.9 External Command Dialog ("Paste Output Of")

- **Fields:**
  - Shell command input
- Executes command, captures stdout, inserts at cursor

### 5.10 Mail Dialog

- **Fields:**
  - To: (recipient address)
  - Subject: (mail subject)
  - CC: (carbon copy)
- Pipes buffer content through mail command

### 5.11 Open File Dialog

- **Fields:**
  - Filename input (with history and completion)
- **Buttons:** [OK] [Cancel]

### 5.12 Insert File Dialog

- **Fields:**
  - Filename input (with history and completion)
- **Buttons:** [OK] [Cancel]

### 5.13 Save Block Dialog

- **Fields:**
  - Filename input (defaults to clipfile path)
- **Buttons:** [OK] [Cancel]

### 5.14 About Dialog

- Shows version and copyright information
- Modal informational dialog

### 5.15 Word Completion Dialog

- **Title:** "[Completion]"
- Shows listbox of matching completions
- Positioned near cursor
- Single match: auto-inserts without dialog
- Multiple matches: user selects from list

### 5.16 Window List Dialog

- Shows list of all open editor windows
- Each entry shows filename
- Hotkey selection (1-9, a-z)
- Selecting entry switches to that window

### 5.17 Spell Check Dialog (requires aspell)

- Shows misspelled word and current language
- Listbox of suggested replacements
- **Buttons:** [Add word] [Replace] [Skip] [Cancel]

### 5.18 Encoding Selection Dialog

- Listbox of available character encodings/codepages
- Selecting changes the document encoding

### 5.19 Find Declaration Dialog (etags)

- Shows list of matching definitions from TAGS file
- Format: `shortname -> filename:linenum`
- Selecting navigates to that location

### 5.20 Syntax Highlighting Selection Dialog

- Listbox with "Auto" option plus all available syntax types
- Alphabetically sorted
- Selection changes syntax highlighting mode

### 5.21 Quit Confirmation Dialog

- Appears when closing a modified file
- **Buttons:** [Save] [Discard] [Cancel]

### 5.22 Macro Key Assignment Dialog

- Prompts user to press a key to bind the recorded macro to
- Validates key is not reserved

### 5.23 Repeat Macro Dialog

- **Fields:**
  - Repeat count input
- Replays recorded macro N times

---

## 6. Editor Features

### 6.1 Multi-File / Multi-Window Editing

- Multiple files can be open simultaneously
- Each file gets its own editor window
- Windows can be:
  - Moved (drag title bar or Ctrl+F5 equivalent via menu)
  - Resized (drag bottom-right corner or menu command)
  - Toggled fullscreen (double-click title bar or menu)
  - Navigated: Next (Alt+}) / Previous (Alt+{) / List
- Maximum file size: 64MB per file
- Window list dialog shows all open files

### 6.2 Block Operations

**Regular (stream) selection:**
- F3 toggles mark mode (start/stop selection)
- Shift+arrow keys for immediate selection
- Selection between mark1 and mark2 positions

**Column (rectangular) selection:**
- Shift+F3 or F13 toggles column mark mode
- Alt+arrow keys for column selection
- Column operations maintain rectangular boundaries
- Width calculation based on column positions

**Block commands:**
- Copy (F5) - copies block to cursor position
- Move (F6) - moves block to cursor position
- Delete (F8) - deletes selected block
- Copy to clipfile (Ctrl+Insert) - copies to `~/.cache/mc/mcedit/mcedit.clip`
- Cut to clipfile (Shift+Delete)
- Paste from clipfile (Shift+Insert)
- Save block to file (Ctrl+F)
- Sort selection (Alt+T)
- Shift block left/right (Tab/Shift+Tab on selection, CK_BlockShiftLeft/CK_BlockShiftRight)

**Column block specifics:**
- Insert column of text with EOL padding
- Delete column between margin boundaries
- VERTICAL_MAGIC marker in saved column blocks for detection on reload

### 6.3 Undo/Redo

- Full undo/redo with configurable stack depth (default max: 32,768 entries)
- Compressed stack recording: identical consecutive actions use negative count prefix
- Stack auto-doubles when full
- Actions tracked: cursor moves, character insertions, deletions, backspaces, mark positions
- Group undo option: groups related actions (e.g., typing a word) into single undo step
- Warning for undo on large block deletions
- Mark positions (mark1, mark2, end_mark_curs) are preserved across undo/redo

### 6.4 Bookmarks

- Toggle bookmark on current line (Alt+K)
- Navigate to next/previous bookmark (Alt+J / Alt+I)
- Flush all bookmarks (Alt+O)
- Each bookmark has a color value
- Multiple bookmarks can exist on one line
- Bookmarks stored as doubly-linked list
- Serialized for persistence across sessions (capped at MAX_SAVED_BOOKMARKS)
- Line numbers auto-adjust on insertion/deletion (book_mark_inc/book_mark_dec)
- Visual indicators: bookmarked lines render with special background color

### 6.5 Macros

**Recording:**
- Ctrl+R starts recording; press Ctrl+R again to stop
- During recording, all key commands are stored in `record_macro_buf[]`
- After stopping, user assigns a hotkey to the macro
- Reserved keys (CK_MacroStartRecord, CK_MacroStopRecord) cannot be used

**Storage:**
- Macros saved in `~/.local/share/mc/mc.macros` under `[editor]` section
- Format: `"actionname:charvalue;"` serialized strings
- Sorted by hotkey for binary search lookup

**Playback:**
- Press assigned hotkey to execute macro
- Each stored action replayed sequentially

**Repeat actions:**
- Record/Repeat actions (CK_RepeatStartStopRecord) records then prompts for repeat count
- Replay macro N times

**External scripts:**
- Macros can reference external shell scripts
- Scripts stored in `~/.local/share/mc/mcedit/macros.d/macro.XXXX.sh`
- Script directives: `#silent`, `%c` (col), `%i` (indent), `%y` (syntax type), `%b` (block file), `%f` (filename), `%n` (name without ext), `%x` (extension), `%d` (directory)

**Management:**
- Delete macro via Command menu
- Macro list maintained as sorted global array

### 6.6 Word Wrap

**Typewriter wrap:**
- When `typewriter_wrap` is enabled, lines automatically wrap at `word_wrap_line_length`
- `check_and_wrap_line()` searches backward for whitespace to insert newline
- Configurable wrap length (default: 72 characters)

**Dynamic paragraphing:**
- `auto_para_formatting` mode
- Reformats current paragraph as you type
- Respects paragraph boundaries (blank lines, stop-format characters)

### 6.7 Auto-Indent

- When `return_does_auto_indent` is enabled
- On Enter, copies leading whitespace from previous line as template
- `edit_auto_indent()` implements the logic

### 6.8 Tab Handling

- **Tab spacing:** configurable (default: 8, stored as `option_tab_spacing`)
- **Fill tabs with spaces:** converts tab keypresses to appropriate number of spaces
- **Fake half tabs:** simulates half-width (4-space) tabs within 8-space tab format
- **Backspace through tabs:** backspace jumps to previous tab stop instead of deleting one space
- Visible tab rendering: `<----->` (when visible tabs enabled)

### 6.9 Paragraph Formatting

- Alt+P formats current paragraph
- Detects paragraph boundaries via blank lines and stop-format characters
- Preserves consistent indentation
- Reflows text to `word_wrap_line_length`
- Handles UTF-8 wide characters for width calculation
- Stop-format characters configurable via `editor_stop_format_chars`

### 6.10 Bracket Matching

- Alt+B jumps to matching bracket
- Supports: `()`, `[]`, `{}`, `<>`
- Depth-counting algorithm for nested brackets
- Matched bracket highlighted with `EDITOR_BOLD_COLOR`

### 6.11 Word Completion

- Alt+Tab triggers completion
- Scans current buffer for matching words starting with typed prefix
- Options:
  - `editor_wordcompletion_collect_entire_file` - search whole document vs. up to cursor
  - `editor_wordcompletion_collect_all_files` - search all open editor buffers
- Single match: auto-inserts
- Multiple matches: shows completion listbox dialog
- Character set conversion between display and input encodings

### 6.12 Code Navigation (etags/ctags)

- Requires TAGS file generated by etags or ctags
- Alt+Enter: find declaration of word under cursor
- Alt+Minus: navigate back in declaration history
- Alt+Plus: navigate forward in declaration history
- Recursively searches parent directories for TAGS file
- Shows selection dialog when multiple matches found
- Declaration history stack: MAX_HISTORY_MOVETO = 50 entries

### 6.13 Spell Checking (optional, requires aspell)

- Full document spell check (CK_SpellCheck)
- Check word under cursor (CK_SpellCheckCurrentWord)
- Language selection dialog (CK_SpellCheckSelectLang)
- Uses GNU Aspell library (dynamically loaded via GModule)
- Suggestion dialog with Add/Replace/Skip/Cancel
- Configurable language via `spell_language` option

### 6.14 External Commands

- **Sort** (Alt+T): Saves selection, runs sort command, reinserts result
- **Paste output of** (Alt+U): Runs shell command, inserts stdout
- **External formatter** (Format menu): Pipes block through numbered macro script
- **Pipe block** (CK_PipeBlock(n)): Executes numbered script against block
- **User menu** (F11): Custom menu with templates per language

### 6.15 Clipboard

- Internal clipboard file: `~/.cache/mc/mcedit/mcedit.clip`
- Ctrl+Insert copies to clipfile (also triggers external clipboard utility if configured)
- Shift+Insert pastes from clipfile
- Shift+Delete cuts to clipfile
- Column blocks saved with VERTICAL_MAGIC marker

### 6.16 File Locking

- Editor uses file locking to prevent concurrent editing
- Lock checked on open, released on close
- Modification time conflict detection on save

### 6.17 Insert Literal

- Ctrl+Q opens raw key capture
- Allows inserting control characters and special characters
- Character displayed as caret notation (e.g., `^M` for CR)

### 6.18 Date/Time Insertion

- Insert date/time at cursor position (CK_Date)
- Available from Format menu

### 6.19 Line Operations

- Delete line (Ctrl+Y)
- Delete to end of line (Ctrl+K)
- Delete to beginning of line
- Delete word left (Alt+Backspace)
- Delete word right (Alt+D)
- Mark current word (double-click)
- Mark current line (triple-click or CK_MarkLine)

### 6.20 Binary File Editing

- Can edit binary files
- Non-printable characters displayed as dots or caret notation
- Insert literal allows entering any byte value

### 6.21 Line Break Handling

- Supports three line break formats:
  - LF (Unix) - `LB_UNIX`
  - CRLF (Windows) - `LB_WIN`
  - CR (Mac) - `LB_MAC`
  - As-is (preserve original) - `LB_ASIS`
- Line break format selectable on Save As
- Auto-detection on file load

### 6.22 Encoding Support

- Multiple character encoding support via iconv
- Encoding selection dialog (Alt+E)
- UTF-8 aware cursor movement and display
- Multibyte character handling in buffer operations
- Source codepage configurable

### 6.23 Save Modes

Three save strategies:
1. **Quick save** (`EDIT_QUICK_SAVE`): Truncate file and write directly
2. **Safe save** (`EDIT_SAFE_SAVE`): Write to temp file, then rename
3. **Backup** (`EDIT_DO_BACKUP`): Create backup copy with configurable extension before overwriting

Features:
- Hard-link detection
- Modification time conflict checking
- Pipe filtering support (write filters)
- POSIX EOF newline checking

### 6.24 User Menu

- F11 opens user-defined menu
- Menu file: `~/.local/share/mc/mcedit/menu` or system default
- Language-specific templates (C, Perl, Shell, etc.)
- Macro substitution: `%f` (filename), `%n` (name), `%x` (ext), `%d` (dir), `%b` (block file), `%i` (indent), `%y` (syntax type)
- Common operations: sort, case conversion, compile, debug

---

## 7. Mouse Support

### 7.1 Window Title Bar (non-fullscreen mode)

| Action                     | Effect                          |
|----------------------------|---------------------------------|
| Click and drag title bar   | Move window (CK_WindowMove)     |
| Double-click title bar     | Toggle fullscreen               |
| Click close button [X]     | Close window (CK_Close)         |
| Click fullscreen button [^]| Toggle fullscreen               |

### 7.2 Text Area

| Action              | Effect                              |
|----------------------|-------------------------------------|
| Click                | Position cursor at click location   |
| Click and drag       | Select text (mark region)           |
| Double-click         | Select current word (CK_MarkWord)   |
| Triple-click         | Select current line (CK_MarkLine)   |
| Scroll wheel up      | Scroll up 2 lines                   |
| Scroll wheel down    | Scroll down 2 lines                 |

### 7.3 Window Resize

| Action                          | Effect                     |
|---------------------------------|----------------------------|
| Click and drag bottom-right corner | Resize window (CK_WindowResize) |

### 7.4 Button Bar

| Action          | Effect                              |
|-----------------|-------------------------------------|
| Click F-key area | Execute corresponding F-key command |

### 7.5 Menu Bar

| Action          | Effect                    |
|-----------------|---------------------------|
| Click menu name | Open that menu            |

### 7.6 Drag State Machine

- `MCEDIT_DRAG_NONE` - Normal mode
- `MCEDIT_DRAG_MOVE` - Window being moved
- `MCEDIT_DRAG_RESIZE` - Window being resized
- Dragging constrained within dialog bounds
- `drag_state_start` tracks initial mouse X position for relative movement

### 7.7 Cursor Positioning

Mouse clicks in the text area are translated to editor coordinates accounting for:
- Horizontal scroll offset (`start_col`)
- Vertical scroll offset
- Line number column width (when enabled)
- Window frame offset (non-fullscreen)
- Tab expansion for correct column calculation

---

## 8. Configuration Options

All options stored in `~/.config/mc/ini` under appropriate sections.

### 8.1 Text Editing Options

| Option                              | Type    | Default | Description                                      |
|-------------------------------------|---------|---------|--------------------------------------------------|
| `editor_tab_spacing`                | int     | 8       | Tab stop width in characters                     |
| `editor_fill_tabs_with_spaces`      | bool    | false   | Convert tabs to spaces on insertion              |
| `editor_return_does_auto_indent`    | bool    | true    | Auto-indent on newline                           |
| `editor_backspace_through_tabs`     | bool    | false   | Backspace jumps to previous tab stop             |
| `editor_fake_half_tabs`             | bool    | false   | Simulate half-width tabs (4 in 8-space format)   |
| `editor_word_wrap_line_length`      | int     | 72      | Line length for word wrap and paragraph format   |
| `editor_option_typewriter_wrap`     | bool    | false   | Enable automatic line wrapping while typing      |
| `editor_option_auto_para_formatting`| bool    | false   | Dynamic paragraph reformatting while typing      |
| `editor_stop_format_chars`          | string  | "-+*\\0"| Characters that stop paragraph formatting        |

### 8.2 Selection & Cursor Options

| Option                                | Type    | Default | Description                                     |
|---------------------------------------|---------|---------|--------------------------------------------------|
| `editor_persistent_selections`        | bool    | true    | Keep selection after cursor movement             |
| `editor_drop_selection_on_copy`       | bool    | true    | Clear selection after copy operation             |
| `editor_cursor_beyond_eol`            | bool    | false   | Allow cursor past end of line                    |
| `editor_cursor_after_inserted_block`  | bool    | false   | Place cursor after inserted block (vs. before)   |

### 8.3 Display Options

| Option                          | Type    | Default | Description                                     |
|---------------------------------|---------|---------|--------------------------------------------------|
| `editor_syntax_highlighting`    | bool    | true    | Enable syntax highlighting                       |
| `editor_line_state`             | bool    | false   | Show line numbers in left gutter                 |
| `editor_line_state_width`       | int     | 8       | Width of line number column                      |
| `editor_visible_spaces`         | bool    | false   | Display trailing spaces as dots                  |
| `editor_visible_tabs`           | bool    | false   | Display tabs as `<---->`                         |
| `editor_show_right_margin`      | bool    | false   | Highlight text beyond wrap line length           |
| `editor_state_full_filename`    | bool    | false   | Show full path in status line                    |
| `editor_simple_statusbar`       | bool    | false   | Use simplified status bar format                 |

### 8.4 File Handling Options

| Option                          | Type    | Default | Description                                     |
|---------------------------------|---------|---------|--------------------------------------------------|
| `editor_option_save_mode`       | int     | 0       | 0=quick, 1=safe, 2=backup                       |
| `editor_backup_extension`       | string  | "~"     | Backup file extension                            |
| `editor_edit_confirm_save`      | bool    | true    | Show confirmation before saving                  |
| `editor_option_save_position`   | bool    | true    | Remember cursor position across sessions         |
| `editor_check_nl_at_eof`        | bool    | false   | Verify POSIX newline at end of file              |
| `editor_filesize_threshold`     | string  | "64M"   | Maximum file size for editing                    |

### 8.5 Undo Options

| Option                | Type    | Default | Description                          |
|----------------------|---------|---------|--------------------------------------|
| `editor_group_undo`   | bool    | true    | Group related actions into one undo  |
| `max_undo`            | int     | 32768   | Maximum undo stack depth             |

### 8.6 Word Completion Options

| Option                                      | Type | Default | Description                           |
|---------------------------------------------|------|---------|---------------------------------------|
| `editor_wordcompletion_collect_entire_file`  | bool | true    | Search whole file for completions     |
| `editor_wordcompletion_collect_all_files`    | bool | false   | Search all open files for completions |

### 8.7 Spell Check Options

| Option            | Type   | Default | Description                             |
|-------------------|--------|---------|-----------------------------------------|
| `spell_language`  | string | "en"    | Aspell language code (NONE to disable)  |

### 8.8 Source Encoding

| Option            | Type   | Default | Description                              |
|-------------------|--------|---------|------------------------------------------|
| `source_codepage` | string | "~"     | Source codepage (~ for system default)   |

---

## 9. Status Bar

### 9.1 Status Bar Format

The status bar displays differently based on mode:

#### Simple Status Bar Format
```
%c%c%c%c %3ld %5ld/%ld %6ld/%ld [%s] %s
```

Fields in order:
1. Column highlight indicator: `C` (column mode) or `-`
2. Modified flag: `M` (modified) or `-`
3. Macro recording flag: `R` (recording) or `-`
4. Overwrite mode flag: `O` (overwrite) or `-`
5. Current column number
6. Current line / total lines
7. Current byte offset / total bytes
8. Codepage identifier
9. Filename (truncated if needed, preferred minimum 16 chars)

#### Extended Status Bar Format
```
[%c%c%c%c] %2ld L:[%3ld+%2ld %3ld/%3ld] *(%-4ld/%4ldb) [%s] %s
```

Fields in order:
1. `[CMRO]` - State flags (Column/Modified/Recording/Overwrite)
2. Column position
3. `L:[line+row line/total]` - Line position info
4. `*(offset/sizeb)` - Byte position info
5. `[codepage]` - Character encoding
6. Filename

#### Character Code Display (at cursor)
- ASCII (0-127): `"%3u 0x%02X"` (decimal + hex)
- Unicode (>127): `"U+%04X"` (Unicode codepoint)
- Invalid UTF-8: Raw byte value
- At end of file: `"<EOF>"`

#### Scroll Percentage (fullscreen, terminal width > 30)
- Shows percentage through file based on cursor position

### 9.2 State Flag Characters

| Flag | Active | Inactive | Meaning                    |
|------|--------|----------|----------------------------|
| C/B  | `C`/`B`| `-`      | Column/Block selection mode|
| M    | `M`    | `-`      | File has been modified     |
| R    | `R`    | `-`      | Macro recording in progress|
| O    | `O`    | `-`      | Overwrite mode active      |

---

## 10. Color Scheme / Visual Styling

### 10.1 Skin System

Colors are defined in skin `.ini` files located in `misc/skins/`. The default skin (`default.ini`) defines:

### 10.2 Editor Color Definitions

| Skin Key                    | Default Colors          | Purpose                           |
|-----------------------------|-------------------------|-----------------------------------|
| `editor._default_`         | lightgray on blue       | Normal text                       |
| `editor.bold`              | yellow on green         | Matched brackets, search results  |
| `editor.marked`            | black on cyan           | Selected text                     |
| `editor.whitespace`        | brightblue on blue      | Visible tabs and trailing spaces  |
| `editor.nonprintable`      | (black background)      | Control/non-printable characters  |
| `editor.linestate`         | white on cyan           | Line number gutter                |
| `editor.bookmark`          | white on red            | Bookmarked lines                  |
| `editor.bookmarkfound`     | black on green          | Bookmarks found by search         |
| `editor.right-margin`      | brightblue on black     | Text beyond wrap column           |
| `editor.frame`             | (default)               | Inactive window frame             |
| `editor.frame-active`      | white                   | Active window frame               |
| `editor.frame-drag`        | green                   | Frame during drag operation       |

### 10.3 Internal Color Constants

| Constant                      | Usage                                |
|-------------------------------|--------------------------------------|
| `EDITOR_NORMAL_COLOR`         | Default text rendering               |
| `EDITOR_BOLD_COLOR`           | Bracket matches, search highlights   |
| `EDITOR_MARKED_COLOR`         | Selected regions                     |
| `EDITOR_WHITESPACE_COLOR`     | Visible tabs and spaces              |
| `EDITOR_NONPRINTABLE_COLOR`   | Control characters                   |
| `EDITOR_RIGHT_MARGIN_COLOR`   | Beyond word-wrap boundary            |
| `EDITOR_BOOKMARK_COLOR`       | Bookmarked lines                     |
| `EDITOR_BOOKMARK_FOUND_COLOR` | Bookmark lines found by search       |
| `EDITOR_FRAME_COLOR`          | Inactive window border               |
| `EDITOR_FRAME_ACTIVE_COLOR`   | Active window border                 |
| `EDITOR_FRAME_DRAG_COLOR`     | Window border during drag            |
| `EDITOR_LINE_STATE_COLOR`     | Line number display                  |

### 10.4 Widget Characters (from skin)

| Skin Key                            | Default | Purpose                    |
|-------------------------------------|---------|----------------------------|
| `widget-editor.window-state-char`   | `*`     | Fullscreen toggle button   |
| `widget-editor.window-close-char`   | `X`     | Close button               |

### 10.5 Window Frame Drawing

- Active windows: double-line box drawing characters
- Inactive windows: single-line box drawing characters
- Drag indicator: marker at bottom-right corner (except during active drag)
- Frame drawn using `tty_draw_box()` with skin-defined line characters

### 10.6 Text Rendering Details

| Display Element          | Rendering                                        |
|--------------------------|--------------------------------------------------|
| Normal text              | Character with syntax color or EDITOR_NORMAL_COLOR|
| Tabs (visible)           | `<----->` with EDITOR_WHITESPACE_COLOR           |
| Tabs (invisible)         | Expanded to spaces                               |
| Trailing spaces (visible)| `.` (dot) with EDITOR_WHITESPACE_COLOR           |
| Control characters       | `^X` caret notation with EDITOR_NONPRINTABLE_COLOR|
| Non-printable chars      | `.` (dot)                                        |
| Cursor position          | Highlighted with MOD_CURSOR                      |
| Selected text            | EDITOR_MARKED_COLOR background                   |
| Right margin area        | EDITOR_RIGHT_MARGIN_COLOR background             |
| Line numbers             | Right-aligned 7-digit format with EDITOR_LINE_STATE_COLOR |
| Bookmarked lines         | `*` marker in line number area                   |
| Search results           | EDITOR_BOLD_COLOR highlighting                   |
| Matched bracket          | EDITOR_BOLD_COLOR highlighting                   |

### 10.7 Available Skin Files

42 skin files ship with MC, including:
- `default.ini` - Standard blue theme
- `dark.ini`, `darkfar.ini` - Dark themes
- `nicedark.ini` - Enhanced dark theme
- `gotar.ini` - Alternative theme
- `mc46.ini` - Classic MC 4.6 look
- `modarcon16*.ini` - 16-color variants (8 files with root/thin/defbg variants)
- `modarin256*.ini` - 256-color variants (8 files with root/thin/defbg variants)
- `julia256.ini`, `julia256root.ini` - Julia-inspired 256-color
- `sand256.ini` - Sand-colored 256-color
- `xoria256.ini`, `xoria256root-thin.ini` - Xoria 256-color
- `gray-green-purple256.ini`, `gray-orange-blue256.ini` - Colored 256
- `seasons-*.ini` - Seasonal 16M (truecolor) themes (autumn, spring, summer, winter)
- `double-lines.ini` - Double-line box drawing
- `featured.ini`, `featured-plus.ini` - Feature-rich themes
- `yadt256.ini`, `yadt256-defbg.ini` - Yet Another Dark Theme
- `mashdark256.ini` - Mash dark 256-color

---

## Appendix A: Buffer Architecture

### Gap Buffer Implementation

MCEdit uses a gap buffer with two dynamically allocated arrays:

- `EDIT_BUF_SIZE` = 2^16 = 65,536 bytes per block
- `b1` array: stores text from file beginning (grows rightward toward cursor)
- `b2` array: stores text from file end (grows leftward toward cursor)
- `curs1`: byte offset from file start (position in b1)
- `curs2`: byte offset from file end (position in b2)
- Gap sits between curs1 and curs2

Operations:
- Insert: add byte at cursor, increment curs1
- Delete forward: decrement curs2
- Backspace: decrement curs1
- Cursor move: transfer bytes between b1 and b2

### Undo Stack

- Circular buffer with power-of-2 sizing (starts at 32, doubles as needed, max 32768)
- Action codes: CURS_LEFT(601), CURS_RIGHT(602), DELCHAR(603), BACKSPACE(604), STACK_BOTTOM(605), CURS_LEFT_LOTS(606), CURS_RIGHT_LOTS(607), COLUMN_ON(608), COLUMN_OFF(609), DELCHAR_BR(610), BACKSPACE_BR(611)
- Mark tracking: MARK_1(1000), MARK_2(500000000), MARK_CURS(1000000000), KEY_PRESS(1500000000)
- Compression: repeated identical actions stored as negative count prefix

---

## Appendix B: Redraw Constants

| Constant              | Value | Purpose                          |
|-----------------------|-------|----------------------------------|
| REDRAW_LINE           | 1     | Redraw current line only         |
| REDRAW_LINE_ABOVE     | 2     | Redraw line above cursor         |
| REDRAW_LINE_BELOW     | 4     | Redraw line below cursor         |
| REDRAW_AFTER_CURSOR   | 8     | Redraw from cursor to line end   |
| REDRAW_BEFORE_CURSOR  | 16    | Redraw from line start to cursor |
| REDRAW_PAGE           | 32    | Redraw entire visible page       |
| REDRAW_IN_BOUNDS      | 64    | Redraw within bounds             |
| REDRAW_CHAR_ONLY      | 128   | Redraw single character          |
| REDRAW_COMPLETELY     | 256   | Full screen redraw               |

---

## Appendix C: Scroll/Display Constants

| Constant                      | Value | Purpose                              |
|-------------------------------|-------|--------------------------------------|
| EDIT_TEXT_HORIZONTAL_OFFSET   | 7     | Left margin for text area            |
| EDIT_TEXT_VERTICAL_OFFSET     | 1     | Top margin for text area             |
| EDIT_RIGHT_EXTREME            | 0     | Right scroll margin                  |
| EDIT_LEFT_EXTREME             | 0     | Left scroll margin                   |
| EDIT_TOP_EXTREME              | 0     | Top scroll margin                    |
| EDIT_BOTTOM_EXTREME           | 0     | Bottom scroll margin                 |
| LINE_STATE_WIDTH              | 8     | Default line number column width     |
| N_LINE_CACHES                 | 32    | Number of line offset cache entries  |

---

## Appendix D: File Paths

| Path                                      | Purpose                              |
|-------------------------------------------|--------------------------------------|
| `~/.config/mc/ini`                        | User configuration file              |
| `~/.local/share/mc/mc.macros`             | User macros file                     |
| `~/.local/share/mc/mcedit/`              | User editor data directory           |
| `~/.local/share/mc/mcedit/macros.d/`     | User macro scripts                   |
| `~/.local/share/mc/syntax/Syntax`        | User syntax index override           |
| `~/.cache/mc/mcedit/mcedit.clip`         | Clipboard file                       |
| `%pkgdatadir%/syntax/`                   | System syntax highlighting files     |
| `%pkgdatadir%/mc.ini`                    | System default configuration         |
| `%pkgdatadir%/mc.lib`                    | Global settings for all users        |
| `%pkgdatadir%/help/mc.hlp`              | Help file                            |

---

## Appendix E: Command-Line Options

```
mcedit [-bcCdfhstVx?] [+lineno] [file1] [file2] ...
mcedit [-bcCdfhstVx?] file1:lineno[:] file2:lineno[:] ...
```

| Option         | Description                                    |
|----------------|------------------------------------------------|
| `+lineno`      | Navigate to specified line in first file        |
| `-b`           | Force monochrome display                        |
| `-c`           | Enable ANSI colors on limited terminals         |
| `-d`           | Disable mouse                                   |
| `-f`           | Show compiled-in search paths                   |
| `-S arg`       | Specify skin name                               |
| `-t`           | Use termcap instead of terminfo                 |
| `-V`           | Show version                                    |
| `-x`           | Force xterm mode                                |
