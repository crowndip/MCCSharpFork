# Midnight Commander — .NET 8/10 Rewrite Analysis

## 1. Project Overview

Midnight Commander (mc) is a feature-rich terminal file manager written in C (~154,000 LOC across 248 source files and 138 headers). It runs on Linux, macOS, BSD, and Solaris. This document analyzes the existing codebase and plans a full rewrite targeting .NET 8 or .NET 10, replacing external C libraries with .NET built-ins wherever possible.

---

## 2. Codebase Metrics

| Module | LOC | Role |
|---|---|---|
| src/filemanager | 33,372 | File manager UI and operations |
| src/editor | 16,894 | Built-in text editor |
| src/vfs/\* | 17,667 | VFS backends (FTP, SFTP, tar, etc.) |
| lib/widget | 14,048 | TUI widget library |
| lib/vfs | 7,534 | VFS core infrastructure |
| lib/tty | 7,100 | Terminal abstraction (ncurses/slang) |
| src/viewer | 6,731 | File viewer (hex, ASCII) |
| lib/strutil | 5,572 | String utilities + encoding |
| src/diffviewer | 3,992 | Side-by-side diff viewer |
| src/subshell | 2,161 | Shell integration |
| lib/search | 2,389 | Search engine (regex, hex, glob) |
| lib/skin | ~1,300 | Theme/color system |
| lib/mcconfig | ~1,000 | INI configuration |
| lib/filehighlight | ~800 | File-type color rules |
| lib/event | ~300 | Event dispatcher |
| Other lib | ~8,500 | Utilities, compatibility |
| **Total** | **~154,000** | |

---

## 3. External Dependencies → .NET Replacements

### 3.1 Required Libraries

| C Library | Version | Used For | .NET Replacement |
|---|---|---|---|
| **GLib 2.0** | >= 2.32 | Data structures, string utils, object system, main loop | `System.Collections`, BCL |
| **GModule** | >= 2.32 | Dynamic plugin loading | `System.Reflection`, `AssemblyLoadContext` |
| **libintl (gettext)** | standard | i18n / translations | `System.Resources.ResourceManager` |

### 3.2 Terminal/Screen Library (one required)

| C Library | Used For | .NET Replacement |
|---|---|---|
| **S-Lang** | Terminal control, input, display | `System.Console` + `Terminal.Gui` |
| **NCurses** | Terminal control, input, display | `System.Console` + `Terminal.Gui` |

### 3.3 Optional Libraries

| C Library | Used For | .NET Replacement |
|---|---|---|
| **libssh2** | SFTP protocol | `SSH.NET` (Renci.SshNet) |
| **libgpm** | GPM mouse (Linux) | `System.Console` (limited) |
| **X11** | Keyboard modifiers | `System.Console` |
| **libaspell** | Spell checking | `NHunspell` |
| **ext2fs / e2p** | Ext2/3/4 attributes | `System.IO` + P/Invoke |

### 3.4 Summary: No External Libraries Needed (Built-in .NET)

The following C library functionality maps **directly** to the .NET BCL with **no third-party packages**:

- **GLib strings / byte arrays** → `System.Text`, `System.Collections.Generic`
- **GLib hash tables / linked lists** → `Dictionary<K,V>`, `LinkedList<T>`, `List<T>`
- **GLib regex (gregex)** → `System.Text.RegularExpressions`
- **GLib file/path utilities** → `System.IO.Path`, `System.IO.File`, `System.IO.Directory`
- **GLib Unicode/encoding** → `System.Text.Encoding`, `System.Globalization`
- **GLib threads / GThread** → `System.Threading`, `Task`, `async/await`
- **GLib main loop** → `System.Threading.Tasks`, custom event loop
- **gettext i18n** → `System.Resources.ResourceManager`
- **FTP protocol (ftpfs)** → `System.Net.FtpWebRequest` or built-in `HttpClient`
- **File I/O, stat, chmod** → `System.IO`, `System.IO.FileSystemInfo`
- **Tar parsing** → Custom + `System.IO.Compression`
- **Time formatting** → `System.DateTime`, `System.Globalization.DateTimeFormatInfo`
- **INI config (GKeyFile)** → `Microsoft.Extensions.Configuration.Ini`
- **Regex search** → `System.Text.RegularExpressions.Regex`
- **Sorting / comparison** → `IComparer<T>`, `StringComparer`
- **Argument parsing** → `System.CommandLine`

### 3.5 Third-Party .NET Packages Required (Minimal)

| Package | Why Needed | NuGet |
|---|---|---|
| **Terminal.Gui** | TUI framework (replaces ncurses/slang + widget system) | `Terminal.Gui` |
| **SSH.NET** | SFTP/SSH virtual filesystem | `SSH.NET` |
| **Microsoft.Extensions.Configuration.Ini** | INI config file support | built into ext libs |

Everything else should use .NET built-ins.

---

## 4. Architecture Analysis

### 4.1 Widget/Message System (lib/widget/)

The C code uses a message-passing model similar to Win32:
- Messages: `MSG_INIT`, `MSG_DRAW`, `MSG_KEY`, `MSG_FOCUS`, `MSG_VALIDATE`, etc.
- Callbacks return `MSG_HANDLED` or `MSG_NOT_HANDLED`
- Widget hierarchy: `WGroup` contains child `Widget` objects

**→ .NET equivalent:** `Terminal.Gui` provides a nearly identical model with `View`, `Dialog`, `Window`, `Button`, `Label`, `ListView`, `TextField`, etc. The message system maps to .NET events and virtual method overrides.

### 4.2 VFS Plugin Architecture (lib/vfs/ + src/vfs/)

The C code defines a `vfs_class` struct with function pointers:
```c
typedef struct vfs_class {
    int (*open)(vfs_path_t*, int flags, mode_t);
    int (*close)(void*);
    ssize_t (*read)(void*, char*, size_t);
    ssize_t (*write)(void*, const char*, size_t);
    int (*stat)(vfs_path_t*, struct stat*);
    int (*mkdir)(vfs_path_t*, mode_t);
    int (*unlink)(vfs_path_t*);
    // + 20 more
}
```

**→ .NET equivalent:** Interface `IVfsProvider` with the same operations. Concrete implementations: `LocalFileSystem`, `FtpFileSystem`, `SftpFileSystem`, `TarFileSystem`, `CpioFileSystem`, `ExtFileSystem`, `SfsFileSystem`.

### 4.3 Search Subsystem (lib/search/)

Four search types:
- Normal (plain text, forward/backward)
- Regex (GRegex → System.Text.RegularExpressions)
- Hex (byte pattern matching)
- Glob (shell-style wildcards)

**→ .NET equivalent:** Abstract `ISearchProvider` with four implementations. All can use built-in `Regex`, `ReadOnlySpan<byte>`, `string.IndexOf`.

### 4.4 Configuration System (lib/mcconfig/)

INI-based using `GKeyFile`. Sections: `[Midnight-Commander]`, `[Panels]`, `[Layout]`, `[Misc]`, etc.

**→ .NET equivalent:** `Microsoft.Extensions.Configuration` with `IniConfigurationProvider`, or `IniFile` wrapper using `System.IO`.

### 4.5 String/Encoding System (lib/strutil/)

Three backends: UTF-8, 8-bit, ASCII. Dynamic dispatch based on locale.

**→ .NET equivalent:** All strings are UTF-16 internally; `System.Text.Encoding` handles conversion. `StringComparer.CurrentCulture` / `OrdinalIgnoreCase` for sorting. No separate backends needed.

### 4.6 Event System (lib/event/)

Simple publish/subscribe dispatcher built on GLib signals.

**→ .NET equivalent:** Standard .NET `event` / `EventHandler<T>` pattern, or `IObservable<T>`.

---

## 5. Module-by-Module Rewrite Plan

### Phase 1 — Foundation (Low Complexity)

| C Module | .NET Module | Notes |
|---|---|---|
| lib/strutil/ | Built-in `string`, `Span<char>` | UTF-8/encoding handled natively |
| lib/mcconfig/ | `IConfiguration` + INI provider | Direct mapping |
| lib/event/ | .NET `event` / delegates | Trivial |
| lib/hook.c | .NET delegates | Trivial |
| lib/timefmt.c | `DateTime.ToString` | Trivial |
| lib/lock.c | `FileStream` lock | Trivial |
| lib/util.c | BCL utilities | Mostly trivial |
| lib/utilunix.c | `System.IO`, P/Invoke | Unix-specific parts need P/Invoke |
| lib/charsets.c | `System.Text.Encoding` | Built-in |
| lib/serialize.c | `System.Text.Json` | Built-in |

### Phase 2 — Core Infrastructure (Medium Complexity)

| C Module | .NET Module | Notes |
|---|---|---|
| lib/vfs/vfs.c | `VfsRegistry` class | Plugin dispatcher |
| lib/vfs/path.c | `VfsPath` record | URL-like path with encoding |
| lib/vfs/direntry.c | `VfsDirEntry` | Directory entry cache |
| lib/vfs/interface.c | `IVfsProvider` interface | 20+ method interface |
| lib/search/ | `ISearchProvider` + 4 impls | Use built-in Regex |
| lib/filehighlight/ | `FileHighlighter` class | INI-based rules |
| lib/skin/ | `SkinManager` class | INI-based colors |
| lib/keybind.c | `KeyBindingManager` | Map key sequences to actions |

### Phase 3 — VFS Backends (Medium–High Complexity)

| C Module | .NET Module | Dependencies |
|---|---|---|
| src/vfs/local/ | `LocalVfsProvider` | `System.IO` |
| src/vfs/tar/ | `TarVfsProvider` | `System.IO.Compression` + custom |
| src/vfs/cpio/ | `CpioVfsProvider` | Custom parser |
| src/vfs/ftpfs/ | `FtpVfsProvider` | `System.Net.FtpWebRequest` |
| src/vfs/sftpfs/ | `SftpVfsProvider` | `SSH.NET` |
| src/vfs/shell/ | `ShellVfsProvider` | `System.Diagnostics.Process` |
| src/vfs/extfs/ | `ExtVfsProvider` | External scripts |
| src/vfs/sfs/ | `SfsVfsProvider` | Config-driven |

### Phase 4 — TUI Layer (High Complexity)

| C Module | .NET Module | Notes |
|---|---|---|
| lib/tty/ | `Terminal.Gui` core | Replace ncurses/slang entirely |
| lib/widget/ | `Terminal.Gui` views | Map each widget type |
| lib/tty/key.c | `Terminal.Gui` keyboard | Input handling |
| lib/tty/mouse.c | `Terminal.Gui` mouse | Mouse event handling |
| lib/tty/color.c | `Terminal.Gui` colors | Color attribute system |

**Widget mapping:**

| C Widget | Terminal.Gui Equivalent |
|---|---|
| `WDialog` | `Dialog` |
| `WButton` | `Button` |
| `WInput` | `TextField` |
| `WLabel` | `Label` |
| `WListbox` | `ListView` |
| `WMenu` | `MenuBar` / `ContextMenu` |
| `WCheck` | `CheckBox` |
| `WRadio` | `RadioGroup` |
| `WGauge` | `ProgressBar` |
| `WFrame` | `FrameView` |
| `WHLine`/`WVLine` | `LineView` |
| `WButtonBar` | Custom bottom bar |
| `WPanel` | Custom two-panel layout |

### Phase 5 — File Manager Core (High Complexity)

| C Module | .NET Module | Notes |
|---|---|---|
| src/filemanager/panel.c | `PanelView` | Main panel widget |
| src/filemanager/dir.c | `DirectoryListing` | File list with sorting |
| src/filemanager/file.c | `FileOperations` | Copy/move/delete |
| src/filemanager/filegui.c | `FileProgressDialog` | Progress dialogs |
| src/filemanager/find.c | `FindDialog` | File finder |
| src/filemanager/cmd.c | `CommandDispatcher` | Command handling |
| src/filemanager/ext.c | `ExtensionRegistry` | MIME / open-with |
| src/filemanager/hotlist.c | `HotlistManager` | Bookmarks |
| src/filemanager/tree.c | `TreeView` | Directory tree |
| src/filemanager/layout.c | `LayoutManager` | Panel layout |
| src/filemanager/chmod.c | `ChmodDialog` | Permissions dialog |
| src/filemanager/chown.c | `ChownDialog` | Ownership dialog |
| src/filemanager/boxes.c | Various dialogs | Config dialogs |
| src/filemanager/mountlist.c | `MountManager` | Mount points |

### Phase 6 — Editor (High Complexity)

| C Module | .NET Module | Notes |
|---|---|---|
| src/editor/editbuffer.c | `TextBuffer` | Gap buffer or rope |
| src/editor/edit.c | `EditorController` | Core editing logic |
| src/editor/editcmd.c | `EditorCommands` | Command dispatch |
| src/editor/editdraw.c | `EditorRenderer` | Terminal rendering |
| src/editor/syntax.c | `SyntaxHighlighter` | Rule-based highlighting |
| src/editor/editsearch.c | `EditorSearch` | Find/replace |
| src/editor/editcomplete.c | `Autocomplete` | Word completion |
| src/editor/editmacros.c | `MacroRecorder` | Macro support |
| src/editor/bookmark.c | `BookmarkManager` | Editor bookmarks |

**Note:** For the editor, consider using `Terminal.Gui`'s built-in `TextView` as a base and extending it, rather than a full port.

### Phase 7 — Viewer and Diff (Medium Complexity)

| C Module | .NET Module | Notes |
|---|---|---|
| src/viewer/mcviewer.c | `ViewerController` | Main viewer logic |
| src/viewer/display.c | `ViewerRenderer` | Rendering |
| src/viewer/hex.c | `HexView` | Hex mode |
| src/viewer/ascii.c | `AsciiView` | ASCII mode |
| src/viewer/datasource.c | `IDataSource` | Streaming data source |
| src/viewer/growbuf.c | `GrowingBuffer` | Large file buffering |
| src/diffviewer/ydiff.c | `DiffViewer` | Side-by-side diff |
| src/diffviewer/search.c | `DiffSearch` | Search in diffs |

### Phase 8 — Support Features (Low–Medium)

| C Module | .NET Module | Notes |
|---|---|---|
| src/subshell/ | `SubshellManager` | `System.Diagnostics.Process` |
| src/execute.c | `Executor` | Process spawning |
| src/help.c | `HelpViewer` | Help system |
| src/learn.c | `KeyLearner` | Key binding wizard |
| src/keymap.c | `KeymapManager` | Key definitions |
| src/usermenu.c | `UserMenuManager` | User-defined menus |
| src/clipboard.c | `ClipboardManager` | Platform clipboard |
| src/background.c | `BackgroundOps` | `Task` / `async` |
| src/args.c | `CliArgs` | `System.CommandLine` |
| src/setup.c | `SetupDialogs` | Config UI |

---

## 6. Recommended .NET Project Structure

```
MidnightCommander.sln
├── src/
│   ├── Mc.Core/                    # Core business logic, VFS interface
│   │   ├── Vfs/                    # IVfsProvider, VfsPath, VfsRegistry
│   │   ├── Search/                 # ISearchProvider implementations
│   │   ├── Config/                 # Configuration system
│   │   ├── Skin/                   # Theme system
│   │   ├── FileHighlight/          # File type coloring
│   │   └── KeyBinding/             # Key binding management
│   ├── Mc.Vfs.Local/               # Local filesystem provider
│   ├── Mc.Vfs.Ftp/                 # FTP provider (System.Net)
│   ├── Mc.Vfs.Sftp/                # SFTP provider (SSH.NET)
│   ├── Mc.Vfs.Archives/            # Tar/CPIO/Zip providers
│   ├── Mc.Vfs.Shell/               # Shell VFS provider
│   ├── Mc.Ui/                      # Terminal.Gui wrapper layer
│   │   ├── Widgets/                # Custom widget implementations
│   │   ├── Panels/                 # Two-panel layout
│   │   └── Dialogs/                # All dialog windows
│   ├── Mc.FileManager/             # File manager logic
│   ├── Mc.Editor/                  # Built-in text editor
│   ├── Mc.Viewer/                  # File viewer
│   ├── Mc.DiffViewer/              # Diff viewer
│   └── Mc.App/                     # Entry point, DI setup, main loop
└── tests/
    ├── Mc.Core.Tests/
    ├── Mc.Vfs.Tests/
    ├── Mc.FileManager.Tests/
    └── Mc.Editor.Tests/
```

---

## 7. .NET Target: 8 vs 10

| Feature | .NET 8 | .NET 10 |
|---|---|---|
| LTS | Yes (until 2026-11) | Yes (until 2028-11, preview) |
| `System.IO.Compression` tar support | Built-in (`TarEntry`) | Same |
| `System.Console` ANSI | Improved | Same |
| `System.CommandLine` | Stable | Same |
| `Span<T>` / `Memory<T>` | Full | Full |
| Native AOT | Supported | Improved |
| Performance | Excellent | Better |

**Recommendation: Target .NET 8** (current LTS, stable tooling). Use `TarFile` API introduced in .NET 7+. Upgrade to .NET 10 when it reaches GA (late 2025).

---

## 8. Key Technical Decisions

### 8.1 TUI Framework

**Recommendation: `Terminal.Gui` (v2)**
- MIT license, cross-platform (Windows, Linux, macOS)
- Provides: dialogs, menus, text fields, listviews, checkboxes, progress bars, mouse support
- Message-loop model maps cleanly to mc's widget architecture
- Supports 24-bit color, Unicode

**Alternative:** `Spectre.Console` — better rendering but lacks interactive widget system needed for mc's panels.

### 8.2 Text Buffer for Editor

**Recommendation: Rope or Gap Buffer**
- Port `editbuffer.c` logic as a `RopeBuffer` or `GapBuffer` class
- `System.Text.StringBuilder` is not suitable for large interactive editing
- Consider: `Microsoft.CodeAnalysis.Text.SourceText` from Roslyn (read-only optimized)

### 8.3 VFS Path Representation

```csharp
public sealed record VfsPath(
    string Scheme,           // "local", "ftp", "sftp", "tar", "cpio"
    string? Host,
    string? User,
    int? Port,
    string LocalPath,        // physical path component
    string? VirtualPath,     // path inside archive/remote
    Encoding? Encoding       // #enc: support
);
```

### 8.4 Platform Support

- **Linux/macOS:** Full feature support via `System.IO` + P/Invoke for POSIX-specific ops
- **Windows:** Reduced feature set (no subshell, limited permissions)
- Unix-specific: `chmod`, `chown`, `symlink` → P/Invoke to `libc`

### 8.5 Localization

Replace gettext with .NET resource files:
- `.resx` files per language
- `ResourceManager` for lookup
- Plural forms via `PluralResourceManager` custom helper

---

## 9. Features NOT to Port (Out of Scope)

| Feature | Reason |
|---|---|
| Console saver (cons.saver.c) | Linux-specific, obsolete on modern systems |
| GPM mouse | Linux-only, superseded by xterm mouse protocol |
| Ext2/3/4 attribute support | Very niche, P/Invoke if needed later |
| man2hlp tool | Build tool, not runtime feature |

---

## 10. Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Terminal.Gui limitations | Medium | Evaluate v2 capabilities early; fallback to custom rendering |
| POSIX permission/ownership ops | Low | P/Invoke to libc; well-understood |
| Large file handling in editor | Medium | Implement gap buffer / rope before full editor port |
| SFTP edge cases | Low | SSH.NET is mature and well-tested |
| VFS path encoding edge cases | Medium | Comprehensive unit tests for VfsPath |
| Cross-platform consistency | Medium | CI on Linux + macOS + Windows from day 1 |

---

## 11. Effort Estimate

| Phase | Modules | Estimated Effort |
|---|---|---|
| 1 — Foundation | strutil, config, event, utils | 2–3 weeks |
| 2 — Core Infrastructure | VFS core, search, skin, keybind | 3–4 weeks |
| 3 — VFS Backends | local, tar, ftp, sftp, shell | 4–6 weeks |
| 4 — TUI Layer | Terminal.Gui integration, all widgets | 4–5 weeks |
| 5 — File Manager | Panels, file ops, dialogs, find | 6–8 weeks |
| 6 — Editor | Buffer, rendering, syntax, search | 6–8 weeks |
| 7 — Viewer + Diff | Viewer, hex, diff | 3–4 weeks |
| 8 — Support Features | Subshell, help, menus, clipboard | 2–3 weeks |
| **Total** | | **~30–41 weeks (1 developer)** |

---

## 12. NuGet Package Summary

```xml
<!-- Mc.App / Mc.Ui -->
<PackageReference Include="Terminal.Gui" Version="2.*" />

<!-- Mc.Vfs.Sftp -->
<PackageReference Include="SSH.NET" Version="*" />

<!-- Mc.App (CLI argument parsing) -->
<!-- Use System.CommandLine (in-box in .NET 8+) -->

<!-- Testing -->
<PackageReference Include="xunit" Version="*" />
<PackageReference Include="xunit.runner.visualstudio" Version="*" />
<PackageReference Include="Moq" Version="*" />
```

**All other functionality uses .NET BCL only.**

---

*Generated: 2026-02-25*
*Source: Midnight Commander git repository (248 .c files, 138 .h files, ~154,000 LOC)*
