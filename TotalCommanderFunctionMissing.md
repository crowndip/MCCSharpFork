# Missing Total Commander Features in Midnight Commander .NET

## Overview
This document identifies features present in Total Commander that are currently missing in our Midnight Commander .NET implementation. Based on analysis of Total Commander's latest features.

**Last Updated**: 2026-04-07

## Summary of Current Status

### ✅ Features Where We Match or Exceed Total Commander
1. **Archive Support** - RAR, 7Z, CAB, ISO, ARJ, LHA, JAR with password support (via 7zip)
2. **Multi-Rename Tool** - SUPERIOR with Roman numerals, GUID, file size units, multi-level parents
3. **Folder Size Display** - Space on folder calculates size (matches TC exactly)
4. **Basic File Operations** - Copy, move, delete, rename with all options
5. **VFS Support** - Local, FTP, SFTP, multiple archive formats
6. **Built-in Editor (mcedit)** - SUPERIOR with multi-tab, split view, text/binary compare, git integration
7. **Built-in Viewer** - Hex/text modes with search
8. **Diff Viewer** - Side-by-side comparison
9. **Search Features** - Search in archives, duplicate finder, panelize results
10. **File Properties** - Touch (timestamps), Chmod, Chown, Chattr
11. **Panel Customization** - Configurable columns with reordering

### 🔄 Major Features Still Missing
1. **Tabbed Interface** - No tab support within panels
2. **Plugin System** - No extensibility architecture
3. **Cloud Storage** - No WebDAV, S3, Google Drive, Dropbox
4. **File Operations Queue** - No pause/resume/priority control
5. **Directory Synchronization** - No visual diff and sync tools

## Major Missing Features

### 1. **Tabbed Interface**
- ❌ **Tabbed panels** within each file panel
- ❌ **Tab groups** for organizing multiple directories
- ❌ **Tab operations** (move, duplicate, close all)
- ❌ **Session management** with saved tab sets

### 2. **Advanced Archive Support**
- ✅ **Built-in support for RAR, 7Z, CAB, ISO, ARJ, LHA, JAR** (via 7zip)
- ✅ **Password-protected archive** support (all 7zip formats including ZIP)
- ❌ **Multi-volume archive** handling
- ❌ **Self-extracting archive** creation
- ❌ **Archive conversion** between formats

### 3. **Plugin System**
- ❌ **WCX plugins** (packer plugins)
- ❌ **WDX plugins** (content plugins)
- ❌ **WLX plugins** (lister plugins)
- ❌ **WFX plugins** (file system plugins)
- ❌ **Plugin manager** with installation/configuration

### 4. **Advanced File Operations**
- ✅ **Multi-rename tool** with regex, variables, and extensive placeholders (SUPERIOR to TC)
- ✅ **Folder size calculation** on spacebar (matches TC behavior)
- ❌ **File splitting/joining** for large files
- ❌ **File operations queue** with pause/resume
- ❌ **Background operations** with priority control
- ❌ **File operations logging** with reports

### 5. **Cloud & Network Integration**
- ❌ **WebDAV** support (HTTP/HTTPS)
- ❌ **Amazon S3** and S3-compatible storage
- ❌ **Google Drive, Dropbox, OneDrive** integration
- ❌ **Network neighborhood** browsing
- ❌ **Mapped drives** integration (Windows)

### 6. **Advanced Search Features**
- ✅ **Search in archives** capability (all VFS-supported formats)
- ✅ **Duplicate file finder** with content comparison (SHA256)
- ✅ **Search results** in virtual folder (via Panelize)
- ❌ **Save/load search results** for later use
- ❌ **Plugins for content search** (Office files, PDF, etc.)

### 7. **File Comparison Tools**
- ✅ **Text compare** with syntax highlighting (IMPLEMENTED in mcedit)
- ✅ **Binary compare** with hex view (IMPLEMENTED in mcedit)
- ❌ **Directory comparison** with visual diff
- ❌ **Three-way merge** tool
- ❌ **Synchronize by content** (byte-by-byte)

### 8. **Viewing & Editing**
- ✅ **Built-in editor (mcedit)** with advanced features:
  - ✅ **Multi-tab viewer** for multiple files
  - ✅ **Split view** (horizontal/vertical)
  - ✅ **Text compare** with syntax highlighting
  - ✅ **Binary compare** with hex view
  - ✅ **Git integration** (blame, diff, stage)
  - ✅ **Syntax highlighting** for 20+ languages
  - ✅ **Macro recording/playback**
  - ✅ **Word completion**
  - ✅ **JSON/XML pretty print and validation**
- ❌ **Built-in viewers for 300+ file formats**
- ❌ **Image viewer** with thumbnail support
- ❌ **Quick view panel** (F3) enhancements
- ❌ **Office file preview** (Word, Excel, PDF)

### 9. **Advanced Customization**
- ✅ **Panel column configuration** with reordering (IMPLEMENTED)
- ✅ **Column types**: Name, Size, ModifyTime, AccessTime, ChangeTime, Permissions, Owner, Group, Extension, Type
- ❌ **Toolbar customization** with custom commands
- ❌ **Button bar** with user-defined commands
- ❌ **Start menu** customization
- ❌ **Mouse gestures** support
- ❌ **Color schemes** and theme engine (partial - skins exist)

### 10. **Automation & Scripting**
- ❌ **Button bar** with custom commands
- ❌ **Command line** with advanced parameters
- ❌ **Batch file** integration with variables
- ❌ **Scripting support** (AutoHotkey/AutoIt compatible)
- ❌ **Macro recording** for repetitive tasks

### 11. **File System Features**
- ❌ **Registry browsing** (Windows-specific)
- ❌ **Recycle bin** integration with restore
- ❌ **File system monitoring** (real-time updates)
- ❌ **NTFS streams** support (alternate data streams)
- ❌ **File ownership/permissions** advanced management

### 12. **Security Features**
- ❌ **File encryption/decryption** (AES, Blowfish)
- ❌ **File wiping** with multiple passes
- ❌ **Secure delete** with DoD standards
- ❌ **Password manager** for FTP/cloud
- ❌ **Key file authentication** for SFTP

### 13. **Network Features**
- ❌ **FTP/FTPS** with SSL/TLS support
- ❌ **SFTP/SCP** with key authentication
- ❌ **Background transfers** with queue management
- ❌ **Connection manager** for multiple servers
- ❌ **Transfer speed limiting** and scheduling

### 14. **Advanced Tools**
- ✅ **File timestamp modification** with Touch dialog (IMPLEMENTED)
- ✅ **File permissions** management with Chmod (IMPLEMENTED)
- ✅ **File ownership** management with Chown/Advanced Chown (IMPLEMENTED)
- ✅ **File attributes** management with Chattr (IMPLEMENTED)
- ✅ **Checksum calculator** (MD5, SHA-1, SHA-256) (IMPLEMENTED)
- ❌ **File splitting/joining** with CRC verification
- ❌ **File comments** via NTFS streams
- ❌ **Extended attributes** management

### 15. **User Interface Enhancements**
- ❌ **Tabbed interface** with drag-and-drop
- ❌ **Customizable layout** with docking panels
- ❌ **Dual-pane** with multiple view modes
- ❌ **Quick search** with incremental highlighting
- ❌ **Folder history** with visual timeline

### 16. **File Operations Queue**
- ❌ **Queue management** with pause/resume
- ❌ **Priority control** for different operations
- ❌ **Error handling** with retry options
- ❌ **Logging** with detailed reports
- ❌ **Scheduled operations** for off-hours

### 17. **Multi-Rename Tool**
- ✅ **Regular expression** support (IMPLEMENTED)
- ✅ **Variables** for date, time, counter, file size, parent folders, etc. (IMPLEMENTED - SUPERIOR to TC)
- ✅ **Preview** before renaming (IMPLEMENTED)
- ✅ **Multiple counter formats** - numeric, alpha (upper/lower), Roman numerals (SUPERIOR to TC)
- ✅ **File size placeholders** with multiple units (SUPERIOR to TC)
- ✅ **Multi-level parent folder** access (SUPERIOR to TC)
- ✅ **GUID generation** for unique names (SUPERIOR to TC)
- ✅ **Title case conversion** (SUPERIOR to TC)
- ❌ **Undo/redo** for rename operations
- ❌ **Batch processing** with conditions

### 18. **Directory Synchronization**
- ❌ **Visual diff** with color coding
- ❌ **Synchronize by content** comparison
- ❌ **Preview changes** before synchronization
- ❌ **Multiple sync modes** (mirror, update, etc.)
- ❌ **Schedule synchronization** tasks

### 19. **Content Plugins**
- ❌ **Office file** content extraction
- ❌ **PDF file** text extraction
- ❌ **Image metadata** extraction
- ❌ **Audio/video** tag reading
- ❌ **Custom content** plugins via API

### 20. **Lister Plugins**
- ❌ **Office file** preview
- ❌ **PDF file** viewing
- ❌ **Image formats** with zoom/pan
- ❌ **Audio/video** playback
- ❌ **Custom file type** viewers

## Priority Classification

### High Priority (Core File Manager Features)
1. ~~Tabbed interface~~ (not yet implemented)
2. ~~Advanced archive support~~ ✅ **COMPLETED** (RAR, 7Z, CAB, ISO, ARJ, LHA, JAR with password support)
3. ~~Multi-rename tool~~ ✅ **COMPLETED** (exceeds Total Commander functionality)
4. File operations queue
5. Directory synchronization

### Medium Priority (Advanced Features)
1. Plugin system architecture
2. Cloud storage integration
3. Advanced search capabilities
4. File comparison tools
5. Automation scripting

### Low Priority (Nice-to-Have)
1. Registry browsing (Windows-specific)
2. Office file preview
3. Mouse gestures
4. Color scheme engine
5. File wiping/encryption

## Recent Implementations (2026-04-07)

### ✅ Completed Today
1. **Advanced Archive Support**
   - Extended 7zip provider to handle RAR, CAB, ISO, ARJ, LHA, JAR
   - Password-protected archive support for all formats
   - 7zip as primary ZIP handler with fallback to .NET built-in
   - Status: **COMPLETE**

2. **Enhanced Multi-Rename Tool**
   - Added 10+ new placeholders (file size, parent folders, file index, etc.)
   - Multiple counter formats (numeric, alpha upper/lower, Roman numerals)
   - File size with multiple units (bytes, KB, MB, GB)
   - Multi-level parent folder access
   - GUID generation
   - Title case conversion
   - Status: **SUPERIOR** to Total Commander

3. **Folder Size on Spacebar**
   - Space on folder calculates and displays size in listing
   - Background calculation (non-blocking)
   - Persistent display until refresh
   - Status: **MATCHES** Total Commander exactly

4. **Search Enhancements**
   - Search in archives (ZIP, RAR, 7Z, TAR, etc.)
   - Duplicate file finder with SHA256 content comparison
   - Search results virtual folder (Panelize)
   - Status: **COMPLETE**

5. **Advanced mcedit Features**
   - Multi-tab viewer for multiple files
   - Split view (horizontal/vertical panes)
   - Text compare with syntax highlighting
   - Binary compare with hex view
   - Git integration (blame, diff, stage, unstage, status)
   - Status: **SUPERIOR** to basic editors

6. **Panel Column Configuration**
   - Configurable columns with visual reordering
   - 10 column types available
   - Per-panel configuration (left/right independent)
   - Persistent settings
   - Status: **COMPLETE**

## Implementation Considerations

### Technical Challenges
1. **Plugin System**: Requires stable API and sandboxing
2. **Tabbed Interface**: Complex UI state management
3. **Archive Formats**: Licensing for proprietary formats (RAR, 7Z)
4. **Cloud Integration**: API dependencies and authentication
5. **Office File Support**: Complex file format parsing

### Resource Requirements
1. **Development Time**: 6-12 months for major features
2. **Testing Complexity**: Cross-platform compatibility
3. **Documentation**: Extensive user and developer docs
4. **Maintenance**: Ongoing plugin API support
5. **Licensing**: Third-party library considerations

## Next Steps
1. **Evaluate priorities** based on user needs
2. **Design architecture** for plugin system
3. **Prototype tabbed interface**
4. **Research archive format libraries**
5. **Plan incremental implementation**

This analysis provides a roadmap for feature development to reach parity with Total Commander's extensive functionality.
## Already Identified Missing Features (from project analysis)

### Based on `notImplemented.md` and `missingFunctionsChecklist.md`:

#### 1. **Shell Link (FISH Protocol)**
- ❌ **FISH protocol** for remote shell connections
- ❌ **SSH command execution** via shell
- ❌ **Remote directory tree** for FTP/SFTP paths
- ❌ **Network file operations** via shell commands

#### 2. **Editor Features**
- ❌ **Mail integration** from editor
- ❌ **Toggle fullscreen** in editor
- ❌ **Advanced mail configuration** (SMTP settings)
- ❌ **Email templates** and attachments

#### 3. **Directory Tree**
- ❌ **Remote directory tree** (FTP/SFTP)
- ❌ **Tree caching** for network paths
- ❌ **Lazy loading** for large remote directories
- ❌ **Tree synchronization** with remote changes

#### 4. **Recently Fixed (Tier 1-2)**
- ✅ **Copy/Move source mask pattern** - Fixed
- ✅ **Dive into subdirectory option** - Fixed  
- ✅ **Follow symlinks option** - Fixed
- ✅ **Command line tab completion** - Fixed
- ✅ **Viewer file navigation** - Fixed
- ✅ **Emacs-style editing keys** - Fixed
- ✅ **Find file date/time filters** - Fixed
- ✅ **Find file size filters** - Fixed
- ✅ **Viewer line number prompts** - Fixed
- ✅ **Editor open file dialog** - Fixed
- ✅ **Editor repeat find/replace** - Fixed

#### 5. **Remaining Issues (from checklist)**
- ❌ **Some medium/low priority items** (3 remaining)
- ❌ **Various edge cases** in file operations
- ❌ **Minor UI inconsistencies**
- ❌ **Performance optimizations** needed
