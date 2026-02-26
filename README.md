# Midnight Commander for .NET

A full C# / .NET 8 rewrite of [GNU Midnight Commander](https://midnight-commander.org/), built on [Terminal.Gui v2](https://github.com/gui-cs/Terminal.Gui).

[![Build](https://github.com/crowndip/MCCSharpFork/actions/workflows/ci.yml/badge.svg)](https://github.com/crowndip/MCCSharpFork/actions/workflows/ci.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)

## Features

- **Dual-panel file manager** — classic Norton Commander / mc layout
- **Virtual File System (VFS)** — local, FTP, SFTP, ZIP, TAR archives
- **Built-in viewer** — hex + text modes with search
- **Built-in editor** — syntax-aware text editor
- **Diff viewer** — side-by-side file comparison
- **Find files** — glob pattern + content search with regex support
- **Hotlist** — bookmarked directory shortcuts
- **Shell integration** — drop to shell (Ctrl+O), run commands from the command line
- **Tools menu** (MCCompanion features):
  - Copy path / name / directory to clipboard
  - Checksum calculator (MD5, SHA-1, SHA-256)
  - Directory size (async, live update)
  - Touch — edit file timestamps
  - Batch rename with placeholders `[N] [E] [C] [Y] [M] [D]`
  - Open terminal emulator in current directory (Ctrl+T)
  - Compare files with external diff tool (meld / kdiff3 / VS Code / vimdiff)

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Linux, macOS, or Windows
- A terminal with 256-colour support

## Quick Start

```bash
# Clone
git clone https://github.com/crowndip/MCCSharpFork.git
cd MCCSharpFork

# Build & run
dotnet run --project src/Mc.App

# Or build a self-contained binary
dotnet publish src/Mc.App -c Release -r linux-x64 --self-contained -o publish
./publish/mc
```

## Key Bindings

| Key | Action |
|-----|--------|
| F1 | Help |
| F3 | View file |
| F4 | Edit file |
| F5 | Copy |
| F6 | Move / Rename |
| F7 | Make directory |
| F8 | Delete |
| F9 | Menu |
| F10 | Quit |
| Tab | Switch panels |
| Insert | Mark / unmark file |
| Ctrl+R | Refresh panels |
| Ctrl+U | Swap panels |
| Ctrl+O | Drop to shell |
| Ctrl+T | Open terminal here |
| Ctrl+L | File info |

## Project Structure

```
src/
  Mc.App/           Entry point, DI bootstrap
  Mc.Core/          Domain models, VFS abstractions, search, config
  Mc.FileManager/   Business logic (copy, move, delete, rename)
  Mc.Ui/            Terminal.Gui application, all dialogs and widgets
  Mc.Viewer/        Built-in hex/text viewer
  Mc.Editor/        Built-in text editor
  Mc.DiffViewer/    Side-by-side diff view
  Mc.Vfs.Local/     Local filesystem VFS provider
  Mc.Vfs.Ftp/       FTP VFS provider
  Mc.Vfs.Sftp/      SFTP VFS provider
  Mc.Vfs.Archives/  ZIP and TAR VFS providers
tests/
  Mc.Core.Tests/
  Mc.FileManager.Tests/
```

## Configuration

Config files are stored in `~/.config/mc/`:

| File | Purpose |
|------|---------|
| `ini` | Main settings (panels, editor, viewer options) |
| `hotlist` | Directory bookmarks |
| `skins/` | UI colour themes |

## Building from Source

```bash
dotnet build
dotnet test
```

All 33 tests must pass before submitting a pull request.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

GNU General Public License v3 — see [LICENSE](LICENSE).

This project is a clean-room C# rewrite and is not derived from the GNU mc C source code.
