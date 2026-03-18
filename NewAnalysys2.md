# MCCSharpFork - Comprehensive Code Analysis #2

Full application review for visual and functional problems.
Performed: 2026-03-18

---

## CRITICAL SEVERITY

### C1. CompositeOp Undo Moves Cursor to Position 0 (Regression)
- **File:** `src/Mc.Editor/EditorController.cs:917-924`
- **Type:** Functional bug (regression from v1.7.0 fix session)
- **Detail:** The `CompositeOp` class inherits `Offset` and `Text` from `EditOperation` but never sets them — they default to `0` and `""`. When `Undo()` is called (line 407), `_cursorOffset = op.Offset` sets cursor to 0. When `Redo()` is called (line 416), `_cursorOffset = op.Offset + op.Text.Length` also sets cursor to 0. This breaks undo for `ReplaceChar`, `ReplaceNext`, and `CheckTypewriterWrap` — all added in v1.7.0.
- **Fix:** Set `Offset` in `CompositeOp`'s constructor from the first sub-operation: `Offset = ops[0].Offset; Text = ops[0].Text;`

### C2. Sort Command Replaces Wrong Text (Selection Setup Reversed)
- **File:** `src/Mc.Editor/EditorView.cs:1321-1324`
- **Type:** Functional bug (data corruption)
- **Detail:** After running the sort subprocess, the code does:
  ```csharp
  _editor.StartSelection();       // sets _selectionStart = current cursor pos
  _editor.MoveCursor(selStart);   // moves cursor to start of original selection
  _editor.ExtendSelection();      // sets _selectionEnd = selStart
  _editor.InsertText(sorted);     // InsertText → DeleteSelection → but HasSelection is false!
  ```
  Since `_selectionStart` (cursor's previous position, near `selEnd`) > `_selectionEnd` (= `selStart`), `HasSelection` returns false. `DeleteSelection()` does nothing. The sorted text is inserted WITHOUT deleting the original — doubling the content.
- **Fix:** Correct the order:
  ```csharp
  _editor.MoveCursor(selStart);
  _editor.StartSelection();
  _editor.MoveCursor(selEnd);
  _editor.ExtendSelection();
  _editor.InsertText(sorted);
  ```

### C3. Backward Selection Copy Returns Empty String
- **File:** `src/Mc.Editor/EditorController.cs:29, 387-391`
- **Type:** Functional bug
- **Detail:** `HasSelection` checks `_selectionEnd > _selectionStart` (line 29). When the user selects right-to-left (backward), `_selectionStart > _selectionEnd`, so `HasSelection` returns false. `Copy()` (line 389) checks `HasSelection` and returns empty. `Cut()` also checks `HasSelection` and does nothing. `GetSelectionOffsets()` normalizes with `Math.Min`/`Math.Max` but `Copy()` doesn't use it.
- **Fix:** Change `HasSelection` to `_selectionStart >= 0 && _selectionEnd >= 0 && _selectionStart != _selectionEnd`. Change `Copy()` to use `GetSelectionOffsets()`.

### C4. DiffEngine Line Numbers Off By One
- **File:** `src/Mc.DiffViewer/DiffEngine.cs:70, 82-98`
- **Type:** Logic bug (data corruption in saved diffs)
- **Detail:** `leftLine` and `rightLine` are initialized to `1`, then pre-incremented (`++leftLine`) before use. The first line is numbered `2`. Every line number in the diff output is wrong. `SaveDiff` uses these for `@@ -start,count +start,count @@` hunk headers, producing invalid unified diffs.
- **Fix:** Initialize to `0` instead of `1`, so the first `++` makes them `1`.

### C5. FileOperations.CopyAsync Uses Local `Directory.Exists` Instead of VFS
- **File:** `src/Mc.FileManager/FileOperations.cs:70, 154`
- **Type:** Functional bug
- **Detail:** `bool destIsExistingDir = Directory.Exists(destination.Path)` uses `System.IO.Directory.Exists` directly. For FTP, SFTP, or archive destinations, this always returns `false`, causing copy/move to treat existing remote directories as non-existent — wrong path resolution (rename instead of copy-into-dir).
- **Fix:** Use `_vfs.DirectoryExists(destination)` with try/catch instead.

### C6. FTP OpenRead Leaks FtpWebResponse
- **File:** `src/Mc.Vfs.Ftp/FtpVfsProvider.cs:72-77`
- **Type:** Resource leak (connection exhaustion)
- **Detail:** `OpenRead` creates an `FtpWebResponse` but only returns the inner stream. The response is never disposed. When the caller closes the stream, the FTP connection may not be properly released, eventually exhausting the connection pool.
- **Fix:** Wrap the response stream so disposing it also disposes the `FtpWebResponse`, or copy to `MemoryStream` and dispose both.

---

## HIGH SEVERITY

### H1. TarVfsProvider Leaks File Stream on .bz2 Exception (Regression)
- **File:** `src/Mc.Vfs.Archives/TarVfsProvider.cs:199-208`
- **Type:** Resource leak (regression from v1.7.0)
- **Detail:** `var raw = File.OpenRead(archivePath)` opens a file handle, then the switch expression throws `NotSupportedException` for `.bz2`/`.tbz2`. The `raw` stream is never disposed in the exception path.
- **Fix:** Wrap in try/catch: `try { return ext switch { ... }; } catch { raw.Dispose(); throw; }`

### H2. IsReadOnly Not Checked in EditorController Mutating Methods
- **File:** `src/Mc.Editor/EditorController.cs:44, 119-399`
- **Type:** Functional bug
- **Detail:** `EditorController.IsReadOnly` (line 44) is never referenced in any editing method (`InsertChar`, `InsertText`, `ReplaceChar`, `Backspace`, `DeleteForward`, `DeleteLine`, `Paste`, `InsertFile`, `FormatParagraph`, `ReplaceAll`, `ReplaceNext`, `ShiftBlockRight`, `ShiftBlockLeft`, `Sort`). The read-only guard only exists in `EditorView.OnKeyDown`. Any code path that bypasses `OnKeyDown` (menu actions, macros, external formatter, programmatic calls) can modify a read-only buffer.
- **Fix:** Add `if (IsReadOnly) return;` at the top of all mutating methods.

### H3. Shell Injection in Sort, External Command, External Formatter, User Menu
- **Files:**
  - `src/Mc.Editor/EditorView.cs:1309` (Sort)
  - `src/Mc.Editor/EditorView.cs:1342` (External command)
  - `src/Mc.Editor/EditorView.cs:2012` (External formatter)
  - `src/Mc.Editor/EditorView.cs:1473` (User menu)
  - `src/Mc.Ui/McApplication.cs:2427` (Command line)
  - `src/Mc.Ui/McApplication.cs:4111` (User menu script)
- **Type:** Security / shell injection
- **Detail:** All these methods pass user input or paths into `/bin/sh -c "..."` using double-quote wrapping: `Arguments = $"-c \"{cmdStr}\""`. If the input contains double quotes, `$()`, backticks, or `!`, the framing breaks, allowing arbitrary command injection.
- **Fix:** Use `ProcessStartInfo.ArgumentList.Add("-c"); ArgumentList.Add(command);` or use single-quote escaping.

### H4. OpenWithDefaultApp Deletes Temp File Before External App Reads It (Regression)
- **File:** `src/Mc.Ui/McApplication.cs:2582-2585`
- **Type:** Functional bug (regression from v1.7.0)
- **Detail:** The `finally` block calls `Directory.Delete(tempDir, recursive: true)` immediately after launching the external app. `xdg-open` / `open` fork and exit immediately; `TryLaunchArgs` uses `using var proc` which disposes the process handle. The temp file is deleted before the actual application reads it.
- **Fix:** Remove the `finally` cleanup. Instead, clean temp dirs on next startup or after a delay.

### H5. CpioVfsProvider Child Path Missing Separator
- **File:** `src/Mc.Vfs.Archives/CpioVfsProvider.cs:58`
- **Type:** Logic bug
- **Detail:** `var childInner = (inner == "/" ? "/" : inner) + childName;` — when `inner` is `/subdir`, this produces `/subdirchildName` (no `/` between `inner` and `childName`). Navigation into subdirectories in CPIO/RPM archives breaks.
- **Fix:** Change to `(inner == "/" ? "/" : inner + "/") + childName`.

### H6. SftpVfsProvider Does Not Dispose Old Client on Reconnect
- **File:** `src/Mc.Vfs.Sftp/SftpVfsProvider.cs:159-188`
- **Type:** Resource leak
- **Detail:** In `GetClient`, when an existing client is found but `!existing.IsConnected`, the code creates a new client and overwrites `_connections[key]`. The old disconnected client is never disposed.
- **Fix:** `existing.Dispose()` before replacing.

### H7. CpioVfsProvider.ExtractRpmPayload Leaks Raw File Stream
- **File:** `src/Mc.Vfs.Archives/CpioVfsProvider.cs:206-222`
- **Type:** Resource leak
- **Detail:** When gzip magic is found in the RPM payload, the original `raw` file stream is abandoned. A `MemoryStream` + `GZipStream` is returned, but `raw` is never disposed.
- **Fix:** Dispose `raw` after copying the relevant bytes into the `MemoryStream`.

### H8. Backward Search Starts From End of File, Not Cursor
- **File:** `src/Mc.Editor/EditorController.cs:433-442`
- **Type:** Logic bug
- **Detail:** `_lastSearchOffset` is initialized to `0`. For backward search, `searchFrom = _lastSearchOffset > 0 ? _lastSearchOffset : text.Length`. On the first search, `_lastSearchOffset` is `0`, so it searches from the end of the file instead of from the current cursor position (`_cursorOffset`).
- **Fix:** Use `_cursorOffset` as the initial search offset.

### H9. ExtfsVfsProvider RunScript Orphans Process on Timeout
- **File:** `src/Mc.Vfs.Archives/ExtfsVfsProvider.cs:163-180`
- **Type:** Resource leak / zombie process
- **Detail:** `proc.WaitForExit(30_000)` has a 30-second timeout. If the extfs script doesn't exit in time, the process is abandoned but never killed. It becomes a zombie process.
- **Fix:** Check `WaitForExit` return value and call `proc.Kill()` if false.

---

## MEDIUM SEVERITY

### M1. Tab Rendering Off by TabWidth Columns in Editor
- **File:** `src/Mc.Editor/EditorView.cs:263-264, 312`
- **Type:** Visual bug
- **Detail:** When `_showTabTws` is true and a tab is encountered, the code draws a single `→` character. But tabs occupy `TabWidth` columns visually. The drawing loop advances `pos` by 1 per character regardless. All text after a tab is shifted left by `(TabWidth - 1)` columns relative to where it should be.
- **Fix:** When a tab is drawn, either draw `TabWidth` characters (the arrow + padding spaces) or account for the tab width in the column calculation.

### M2. InsertNewlineWithIndent Records Multiple Undo Steps
- **File:** `src/Mc.Editor/EditorController.cs:157-172`
- **Type:** Functional bug
- **Detail:** `InsertNewlineWithIndent()` calls `InsertChar('\n')` then `InsertText(whitespace)`. Each pushes a separate undo entry and clears `_redoStack`. Undoing requires pressing Ctrl+Z twice.
- **Fix:** Combine into a single `CompositeOp`.

### M3. FormatParagraph Records Two Separate Undo Ops
- **File:** `src/Mc.Editor/EditorController.cs:884-887`
- **Type:** Functional bug
- **Detail:** `FormatParagraph` pushes a `DeleteOp` then an `InsertOp` as separate undo entries. Undoing requires Ctrl+Z twice.
- **Fix:** Use `CompositeOp`.

### M4. ReplaceAll Count Uses Wrong Matching Rules
- **File:** `src/Mc.Editor/EditorController.cs:482-498`
- **Type:** Logic bug
- **Detail:** `ReplaceAll` counts occurrences using `CountOccurrences` (case-insensitive `IndexOf`), but the actual replacement uses the search provider (which respects regex, case sensitivity, whole words). The reported count can be wrong.
- **Fix:** Count by comparing before/after text, or have the provider return the count.

### M5. RegexSearchProvider Uses `Singleline` for `EntireLine` Option
- **File:** `src/Mc.Core/Search/RegexSearchProvider.cs:79`
- **Type:** Logic bug
- **Detail:** `RegexOptions.Singleline` makes `.` match `\n`; it has nothing to do with "entire line" matching. The `EntireLine` option does not actually constrain matches to whole lines.
- **Fix:** Use `RegexOptions.Multiline` and anchor pattern with `^(?:...)$`.

### M6. VfsPath.Parse Misidentifies Windows Drive Paths as URIs
- **File:** `src/Mc.Core/Vfs/VfsPath.cs:63-83`
- **Type:** Logic bug
- **Detail:** `Uri.TryCreate("C:\\Users\\foo", UriKind.Absolute, out uri)` succeeds with scheme `"c"`. The method then treats it as a generic URI with `Scheme = "c"`, which no VFS provider handles. Local Windows paths break.
- **Fix:** After parsing, check if scheme is a single letter (drive letter) and treat as local path.

### M7. ExtfsVfsProvider.DirectoryExists Always Returns True
- **File:** `src/Mc.Vfs.Archives/ExtfsVfsProvider.cs:87`
- **Type:** Logic bug
- **Detail:** `return ListDirectory(path).Count >= 0;` — `Count` is always `>= 0` for any list. This always returns true as long as `ListDirectory` doesn't throw.
- **Fix:** Change to `.Count > 0` or do a specific directory existence check.

### M8. ExtfsVfsProvider.FileExists Compares Wrong Path Level
- **File:** `src/Mc.Vfs.Archives/ExtfsVfsProvider.cs:96-103`
- **Type:** Logic bug
- **Detail:** `entry.Name` from `ParseLsLine` is the full relative path (e.g., `dir/file.txt`), but `wantName` is `inner.TrimStart('/')`. The comparison `entry.Name == wantName` fails for files in subdirectories because `entry.Name` may include parent dirs.
- **Fix:** Compare normalized paths consistently.

### M9. Unreachable Ctrl+Shift+Enter Branch in McApplication
- **File:** `src/Mc.Ui/McApplication.cs:397-400`
- **Type:** Logic bug (dead code)
- **Detail:** `case KeyCode.Enter when keyEvent.IsCtrl && keyEvent.IsShift:` is unreachable — the preceding `case KeyCode.Enter when keyEvent.IsCtrl:` matches first (it's true for both Ctrl+Enter and Ctrl+Shift+Enter).
- **Fix:** Swap the two cases so the more specific one comes first, or add `&& !keyEvent.IsShift` to the first.

### M10. ChecksumDialog Reads File Three Times + Unused Stream
- **File:** `src/Mc.Ui/Dialogs/ChecksumDialog.cs:85-89`
- **Type:** Resource leak + performance
- **Detail:** `using var stream = File.OpenRead(filePath)` on line 85 is never used. Then `ReadFully(filePath)` is called three times — allocating the entire file contents three times.
- **Fix:** Read the file once into a `byte[]`, hash it three times. Remove the unused stream.

### M11. ProgressDialog Dispose Ordering Can Crash
- **File:** `src/Mc.Ui/Dialogs/ProgressDialog.cs:86-90`
- **Type:** Crash (race condition)
- **Detail:** `Dispose()` first calls `_cts.Dispose()`, then `Close()`. If a `Report()` call is still in-flight on a background thread, it may access the disposed CTS or dialog.
- **Fix:** Call `Close()` before `_cts.Dispose()`.

### M12. HotlistManager Uses File-Scoped ConfigPaths Instead of Core Config
- **File:** `src/Mc.FileManager/HotlistManager.cs:239-244`
- **Type:** Logic bug
- **Detail:** A `file static class ConfigPaths` shadows `Mc.Core.Config.ConfigPaths`. The file-local version doesn't respect the `MC_CONFIG_DIR` environment variable. If `MC_CONFIG_DIR` is set, hotlist reads/writes go to the wrong location.
- **Fix:** Use `Mc.Core.Config.ConfigPaths.HotlistFile` instead.

### M13. SfsVfsProvider Double-Quotes Shell Command
- **File:** `src/Mc.Vfs.Archives/SfsVfsProvider.cs:105`
- **Type:** Functional bug
- **Detail:** `ShellQuote(cmd)` wraps the entire command string (which already has `ShellQuote`-escaped paths inside it) with another layer of quoting. The `cmd` passed to `/bin/sh -c` must be the raw command string, not a quoted version of it.
- **Fix:** Pass `cmd` directly as the `-c` argument without the outer `ShellQuote`.

---

## LOW SEVERITY

### L1. ReplaceChar Fires Changed Event Twice in Else Branch
- **File:** `src/Mc.Editor/EditorController.cs:139-154`
- **Type:** Functional bug
- **Detail:** When cursor is at end-of-line, the `else` branch calls `InsertChar(ch)` (which fires `Changed`), then execution continues to line 153 which fires `Changed` again.
- **Fix:** Add `return;` after the `if` block (line 148) to prevent the second `Changed`.

### L2. TextBuffer.GetLine() Is O(n) For Entire Buffer Per Call
- **File:** `src/Mc.Editor/TextBuffer.cs:52-57`
- **Type:** Performance
- **Detail:** `GetLine()` converts the entire buffer to a string and splits into all lines on every call. Called multiple times per draw (once per visible line, plus inside `GetLineCount()`, `OffsetToLineCol()`). For large files, causes O(n*m) performance.
- **Fix:** Cache the line index or use a rope/piece-table data structure.

### L3. Backspace Through Tabs Can Crash at Buffer Start
- **File:** `src/Mc.Editor/EditorController.cs:200-208`
- **Type:** Crash (edge case)
- **Detail:** With `BackspaceThruTabs` on, if cursor is at position < `TabWidth` and the preceding characters are all spaces, the deletion loop runs `TabWidth` times regardless. When `_cursorOffset` reaches 0, `_buffer[_cursorOffset - 1]` is `_buffer[-1]` → `ArgumentOutOfRangeException`.
- **Fix:** Limit deletion count to `Math.Min(TabWidth, _cursorOffset)`.

### L4. KeyBindingManager Default Bindings Silently Conflict
- **File:** `src/Mc.Core/KeyBinding/KeyBindingManager.cs:23,33,61,67`
- **Type:** Logic bug
- **Detail:** `F2` is bound to `UserMenu` (line 23) then overwritten with `Save` (line 61). `F4` is bound to `Edit` (line 35) then overwritten with `MacroPlay` (line 67). Panel-mode bindings are silently lost.
- **Fix:** Use context-based binding maps (separate for panel vs. editor).

### L5. PathUtils.GetDisplayPath Crashes on maxLength < 3
- **File:** `src/Mc.Core/Utilities/PathUtils.cs:8-13`
- **Type:** Crash (edge case)
- **Detail:** `path[^(maxLength - 3)..]` with `maxLength < 3` produces a negative index → `ArgumentOutOfRangeException`.
- **Fix:** Clamp `maxLength` to at least 4.

### L6. CommandLineView Uses Case-Insensitive Path Comparison on Linux
- **File:** `src/Mc.Ui/Widgets/CommandLineView.cs:417`
- **Type:** Logic bug
- **Detail:** `dir.StartsWith(home, StringComparison.OrdinalIgnoreCase)` — Linux paths are case-sensitive. Could produce incorrect `~` prefix.
- **Fix:** Use `StringComparison.Ordinal` on non-Windows platforms.

### L7. MatchShellPattern Returns True on Regex Error
- **File:** `src/Mc.Ui/McApplication.cs:4095`
- **Type:** Logic bug
- **Detail:** `catch { return true; }` — a malformed condition pattern causes all files to match, potentially showing user menu entries that should be hidden.
- **Fix:** Return `false` in the catch block.

### L8. BackgroundJobs Dialog Not Disposed
- **File:** `src/Mc.Ui/McApplication.cs:4531`
- **Type:** Resource leak
- **Detail:** `Application.Run(d)` is called but `d.Dispose()` is never called afterward, unlike all other dialogs.
- **Fix:** Add `d.Dispose()` after `Application.Run(d)`.

### L9. McTheme.ApplySkin Does Not Set PanelHeaderSorted
- **File:** `src/Mc.Ui/McTheme.cs` (ApplySkin method)
- **Type:** Visual bug
- **Detail:** Skin-based themes never set `PanelHeaderSorted`, retaining the value from the previous theme. Sorted column header color may be wrong.
- **Fix:** Add handling for the sort-column color in `ApplySkin`.

### L10. RegexSearchProvider WholeWords Doesn't Group Pattern
- **File:** `src/Mc.Core/Search/RegexSearchProvider.cs:81`
- **Type:** Logic bug
- **Detail:** `$@"\b{options.Pattern}\b"` — for patterns with alternation (`foo|bar`), this becomes `\bfoo|bar\b` which means `\bfoo` OR `bar\b`, not whole-word `foo|bar`.
- **Fix:** Wrap in group: `$@"\b(?:{options.Pattern})\b"`.

### L11. DiffEngine Uses O(n*m) Memory — Crashes on Large Files
- **File:** `src/Mc.DiffViewer/DiffEngine.cs:28-30`
- **Type:** Crash (OOM on large files)
- **Detail:** `var dp = new int[n + 1, m + 1]` — for two 100K-line files, this is 40 GB. Diff viewer crashes with `OutOfMemoryException` on moderately large files.
- **Fix:** Use Myers diff O(n+m) space, or limit file sizes.

### L12. FileOperations.DeleteAsync Is Not Actually Async
- **File:** `src/Mc.FileManager/FileOperations.cs:209-233`
- **Type:** Functional bug
- **Detail:** `DeleteAsync` does all work synchronously, then `await Task.CompletedTask`. The UI thread blocks during delete of large directory trees.
- **Fix:** Run delete loop in `Task.Run()` or use `await Task.Yield()` between iterations.

### L13. ViewerController.FindNext Skips First Character on Initial Search
- **File:** `src/Mc.Viewer/ViewerController.cs:156-185`
- **Type:** Logic bug
- **Detail:** `_lastSearchOffset` defaults to `0`. First search starts at `0 + 1 = 1`, skipping position 0.
- **Fix:** Initialize `_lastSearchOffset = -1`.

### L14. ButtonBarView Divide by Zero When Buttons Array Empty
- **File:** `src/Mc.Ui/Widgets/ButtonBarView.cs:82`
- **Type:** Crash (edge case)
- **Detail:** `int baseWidth = totalWidth / count;` throws `DivideByZeroException` if `_buttons.Length == 0`.
- **Fix:** Add `if (count == 0) return true;` guard.

### L15. FormatBriefCell Crash on Very Narrow Panel
- **File:** `src/Mc.Ui/Widgets/FilePanelView.cs:607-608`
- **Type:** Crash (edge case)
- **Detail:** `int nameWidth = width - 1;` can be 0 or negative. `name[..(nameWidth - 1)]` uses a negative index.
- **Fix:** Clamp `nameWidth` to at least 1.

---

## SUMMARY

| Severity | Count | Key Areas |
|----------|-------|-----------|
| Critical | 6     | CompositeOp undo cursor, sort text doubling, backward selection copy, diff line numbers, VFS copy path, FTP leak |
| High     | 9     | Stream leaks (tar/cpio/sftp), read-only bypass, shell injection (6 sites), temp file race, cpio paths, search offset, process orphans |
| Medium   | 13    | Tab rendering, multi-undo ops, search/regex bugs, VFS path parsing, dead code, checksum perf, dispose ordering, config paths, SFS quoting |
| Low      | 15    | Performance (TextBuffer, DiffEngine, DeleteAsync), edge-case crashes, key conflicts, visual theme, regex grouping |
| **Total**| **43**| |
