# Not Implemented UI Elements

Visual elements present in the UI that lack actual functionality.

---

## Main Panel Menu

### Shell Link (FISH Protocol)
- **Type**: Menu Item
- **Menu Path**: Panel → "Shell link..."
- **Location**: `src/Mc.Ui/McApplication.cs` (line ~99)
- **Description**: Intended to open a remote shell connection via the FISH (Files transferred over Shell) protocol, similar to the original Midnight Commander feature.
- **Current Behavior**: Displays a message: *"Shell link (FISH protocol) is not yet implemented in this .NET port."*
- **Complexity**: MEDIUM-HIGH (~300–400 LOC)
  - The VFS layer already supports FTP and SFTP via `IVfsProvider`; a new `ShellVfsProvider` would follow the same pattern.
  - SSH.NET is already a project dependency (used by SFTP), so no new libraries are needed.
  - The main difficulty is parsing shell command output (`ls -la`, etc.) which is fragile across different shells, locales, and encodings.
  - FISH is a legacy protocol — SFTP covers the same use case with a proper binary protocol.

### Directory Tree (Remote Paths)
- **Type**: Menu Item (partially implemented)
- **Menu Path**: Panel → "Tree"
- **Location**: `src/Mc.Ui/McApplication.cs` (line ~3469)
- **Description**: The directory tree dialog works for local paths but is not implemented for remote (FTP/SFTP) paths. When invoked on a remote panel, it shows an error instead of displaying the remote directory tree.
- **Current Behavior**: Displays a message: *"Directory tree (remote paths) is not yet implemented in this .NET port."*
- **Complexity**: MEDIUM (~100–150 LOC)
  - The tree UI and local-path logic already exist in `ShowTreeDialog()`.
  - The main work is replacing `Directory.GetDirectories()` calls with `vfsRegistry.ListDirectory()` and handling `VfsPath` instead of raw strings.
  - Performance is the key concern: each tree expansion triggers a network round-trip, so lazy loading and caching would be needed to keep the UI responsive.

---

## Editor (mcedit)

### Mail
- **Type**: Menu Item
- **Menu Path**: Command → "Mail..." (Alt+M)
- **Location**: `src/Mc.Editor/EditorView.cs` (line ~2283)
- **Description**: Intended to send the current file or selection as an email, mirroring the original mc editor's mail integration.
- **Current Behavior**: Displays a message: *"Mail functionality requires a mail program. This feature is not available in this implementation."*
- **Complexity**: LOW-MEDIUM (~150–200 LOC)
  - Core send logic is straightforward using `System.Net.Mail` or the MailKit package.
  - A small dialog is needed to collect recipient, subject, and optional body.
  - The bulk of the work is SMTP configuration — storing and retrieving server/port/credentials from settings. Without a configured mail server the feature is useless, so a settings UI is part of the cost.

### Toggle Fullscreen
- **Type**: Menu Item
- **Menu Path**: Window → "Toggle fullscreen"
- **Location**: `src/Mc.Editor/EditorScreen.cs` (line ~151)
- **Description**: Intended to toggle the editor between fullscreen and windowed mode. In this implementation the editor is always fullscreen, so the toggle has no effect.
- **Current Behavior**: No-op — clicking the menu item does nothing.
- **Complexity**: HIGH (~400–600 LOC)
  - The editor is launched as a `Toplevel` via `Application.Run()`, which replaces the entire terminal UI. Supporting a windowed mode means changing it to a `Dialog`/child view embedded inside `McApplication`.
  - This is an architectural change: launching, focus routing, keyboard dispatch, and the editor lifecycle all need reworking.
  - Terminal.Gui has limited support for overlapping, resizable windows, adding further risk.
  - High chance of subtle bugs around focus, z-order, and redraw.

---

## Editor — Syntax Highlighting

### Perl Syntax Highlighting
- **Type**: Feature gap (not a clickable element)
- **Location**: `src/Mc.Editor/SyntaxHighlighter.cs` (line ~92)
- **Description**: Perl files are detected but syntax highlighting is not supported for them; the highlighter returns `null` for Perl, so Perl source files are displayed without any highlighting.
- **Complexity**: LOW (~20–30 LOC)
  - The highlighter uses a regex-rule system (`SyntaxRuleSet`); adding Perl means writing a new static method with rules for comments (`#`), strings, keywords, variables (`$`, `@`, `%`), and numbers — similar to the existing Ruby rules.
  - Advanced Perl constructs (here-docs, `s///`, complex quoting) can be skipped for an MVP.
  - Zero risk to the rest of the codebase.

---

## Summary

| Feature | Complexity | Est. LOC | New Dependencies | Priority |
|---|---|---|---|---|
| Perl Syntax Highlighting | LOW | 20–30 | None | 1 — quick win |
| Mail | LOW-MEDIUM | 150–200 | MailKit (optional) | 2 |
| Remote Directory Tree | MEDIUM | 100–150 | None | 3 |
| Shell Link (FISH) | MEDIUM-HIGH | 300–400 | None (SSH.NET exists) | 4 — legacy protocol |
| Toggle Fullscreen | HIGH | 400–600 | None | 5 — architectural risk |
