# MCCSharpFork - Comprehensive Code Analysis

Full application review for visual and functional problems.
Performed: 2026-03-18

---

## CRITICAL SEVERITY

### C1. Brotli Used Instead of BZip2 for TAR.BZ2 Archives ✅ FIXED
- **File:** `src/Mc.Vfs.Archives/TarVfsProvider.cs:197`
- **Type:** Functional bug (data corruption)
- **Detail:** `.bz2` / `.tbz2` files are decompressed with `BrotliStream` instead of a BZip2 implementation. Brotli and BZip2 are completely different algorithms. Browsing any `.tar.bz2` file will produce garbage or crash.
- **Fix:** Replaced with `throw new NotSupportedException(...)` — now shows a clear user-facing error instead of silently corrupting data.

### C2. OpenWithDefaultApp Fails for Files Inside Archives ✅ FIXED
- **File:** `src/Mc.Ui/McApplication.cs:2546-2560`
- **Type:** Functional bug
- **Detail:** `OpenWithDefaultApp()` passes the raw VFS path (e.g. `archive.zip|file.txt`) directly to `xdg-open` / `Process.Start`. The OS cannot open these paths. Must call `ResolveArchiveEntryToLocalPath()` first, extract to temp, then open.
- **Fix:** Extract to temp dir before launching, clean up after (same pattern as `ViewCurrent`/`EditCurrent`).

### C3. Silent File Overwrite on Copy Without Callback ✅ FIXED
- **File:** `src/Mc.FileManager/FileOperations.cs:127-128`
- **Type:** Data loss risk
- **Detail:** When `conflictCallback` is null, the default action is `OverwriteAction.Overwrite` -- silently overwrites destination files without any confirmation.
- **Fix:** Default to `OverwriteAction.Skip` or require a non-null callback.

### C4. Shell Injection Risk in ExtfsVfsProvider ✅ FIXED
- **File:** `src/Mc.Vfs.Archives/ExtfsVfsProvider.cs:165-169`
- **Type:** Security
- **Detail:** Archive paths are shell-quoted and passed via `/bin/sh -c`. The `ShellQuote()` function is a defence but the pattern is fragile. If archive filenames contain crafted sequences, quoting may be bypassed.
- **Fix:** Use `ProcessStartInfo.ArgumentList` instead of shell command string concatenation.

---

## HIGH SEVERITY

### H1. ReplaceChar Records Two Separate Undo Operations ✅ FIXED
- **File:** `src/Mc.Editor/EditorController.cs:139-155`
- **Type:** Functional bug
- **Detail:** `ReplaceChar()` records a `DeleteOp` then an `InsertOp` as two independent undo entries. Pressing Undo only reverses the insert, leaving the delete un-reversed. The user sees a character disappear with no way to get it back in a single Undo.
- **Fix:** Combine into a single composite undo operation, or reverse them in paired fashion.

### H2. Column Block Paste Has Dead / Duplicate Code ✅ FIXED
- **File:** `src/Mc.Editor/EditorController.cs:367-370`
- **Type:** Functional bug (dead code, potentially wrong offset)
- **Detail:** Lines 367-368 calculate `insOff` and `insertion`, then lines 369-370 immediately overwrite both variables with different values. The first calculation is dead code. The overwritten `insOff` discards the `+ (atCol - lineText.Length)` adjustment, which could cause paste to appear at the wrong column when padding is needed.
- **Fix:** Remove lines 367-368 (dead code). Verify that line 369 produces the correct offset.

### H3. Hex Mode Crashes on Empty File ✅ FIXED
- **File:** `src/Mc.Editor/EditorView.cs:2228-2273`
- **Type:** Crash / undefined behavior
- **Detail:** Navigation in hex mode uses `Math.Min(_hexBytes.Length - 1, ...)`. For an empty file (`_hexBytes.Length == 0`), this evaluates to `Math.Min(-1, ...)`, setting `_hexCursorByte` to -1. Subsequent array access with -1 index will throw `IndexOutOfRangeException`.
- **Fix:** Guard all hex navigation with `if (_hexBytes.Length == 0) return true;` at the top of `HandleHexKey()`.

### H4. RepeatLastReplace Calls ReplaceAll Instead of Single Replace ✅ FIXED
- **File:** `src/Mc.Editor/EditorView.cs:1791`
- **Type:** Functional bug
- **Detail:** The "repeat last replace" action calls `ReplaceAll()`, which replaces every occurrence in the file. It should find and replace only the next single occurrence.
- **Fix:** Use `FindNext()` + single replace logic instead of `ReplaceAll()`.

### H5. No Fallback When 7z Is Not Installed (Archive Extraction) ✅ FIXED
- **File:** `src/Mc.Ui/McApplication.cs:814-826`
- **Type:** Functional bug
- **Detail:** `ResolveArchiveEntryToLocalPath()` launches `7z` for non-ZIP archives. If 7z is not installed, `Process.Start()` throws, caught by generic catch with a vague "Error extracting from archive" message. The 7z exit code is also never checked -- a failed extraction produces no error if the file simply doesn't appear.
- **Fix:** Check if `7z` is on PATH first. Check exit code. Show specific error: "7z not found -- install p7zip-full".

### H6. MoveFile Cross-Device Race Condition ✅ FIXED
- **File:** `src/Mc.FileManager/FileOperations.cs:182-199`
- **Type:** Data loss risk
- **Detail:** Cross-device move does copy-then-delete. If copy succeeds but delete fails (permission denied), file exists in both places (acceptable). But if copy is partial and interrupted, source may already be partially consumed with no rollback.
- **Fix:** Verify copy integrity before deleting source. Add try-catch around delete with user notification on failure.

### H7. HotlistManager Write Is Not Atomic ✅ FIXED
- **File:** `src/Mc.FileManager/HotlistManager.cs:128-133`
- **Type:** Data loss risk
- **Detail:** `Save()` writes directly to the hotlist file. If the process crashes mid-write, the file is corrupted and bookmarks are lost.
- **Fix:** Write to a temp file, then `File.Move(tmp, target, overwrite: true)`.

### H8. No Error Handling for Corrupted TAR Files ✅ FIXED
- **File:** `src/Mc.Vfs.Archives/TarVfsProvider.cs:56-90`
- **Type:** Functional bug
- **Detail:** `TarReader.GetNextEntry()` is not wrapped in try-catch. A corrupted TAR entry aborts the entire directory listing with an unhandled exception. User sees a blank error instead of a partial listing.
- **Fix:** Wrap in try-catch per entry, collect partial results, and show a warning.

---

## MEDIUM SEVERITY

### M1. Process Handle Leaks Throughout Application ✅ FIXED
- **Files:**
  - `src/Mc.Ui/Helpers/ProcessHelper.cs:19` (`RunDetached`)
  - `src/Mc.Ui/Helpers/ProcessHelper.cs:35` (`TryLaunchArgs`)
  - `src/Mc.Ui/McApplication.cs:2530` (`ShowOpenWith`)
  - `src/Mc.Ui/McApplication.cs:2551` (`OpenWithDefaultApp`)
- **Type:** Resource leak
- **Detail:** `Process.Start()` returns a `Process` object that is never stored or disposed. Process handles accumulate over time. In a long session with many file opens, this leads to handle exhaustion.
- **Fix:** Either `using var proc = Process.Start(...)` and detach, or call `proc?.Dispose()` after launch.

### M2. Dialog Disposal Missing (5 Instances) ✅ FIXED
- **Files:**
  - `src/Mc.Ui/Dialogs/InputDialog.cs:48`
  - `src/Mc.Ui/Dialogs/MessageDialog.cs:21` and `:44`
  - `src/Mc.Ui/Dialogs/SortDialog.cs:84`
  - `src/Mc.Ui/Dialogs/HotlistDialog.cs:169`
  - `src/Mc.Ui/Dialogs/InfoDialog.cs:42`
- **Type:** Resource leak
- **Detail:** These dialogs are created and run via `Application.Run(d)` but never disposed. Terminal.Gui `Dialog` objects hold native resources.
- **Fix:** Add `d.Dispose()` after each `Application.Run(d)` call.

### M3. CancellationTokenSource Never Disposed (2 Instances) ✅ FIXED
- **Files:**
  - `src/Mc.Ui/McApplication.cs:1437` (FindDialog results)
  - `src/Mc.Ui/Dialogs/DirSizeDialog.cs:37`
- **Type:** Resource leak
- **Detail:** `CancellationTokenSource` implements `IDisposable` and holds OS timer handles. Only `Cancel()` is called, never `Dispose()`.
- **Fix:** Use `using var cts = new CancellationTokenSource();` or explicit dispose.

### M4. Window Not Disposed in ComparePanels ✅ FIXED
- **File:** `src/Mc.Ui/McApplication.cs:1725`
- **Type:** Resource leak
- **Detail:** The comparison window is created and run but never disposed.
- **Fix:** Add `win.Dispose()` after `Application.Run(win)`.

### M5. Event Subscriptions Never Unsubscribed ⏭ SKIPPED
- **Files:**
  - `src/Mc.Ui/Widgets/FilePanelView.cs:101-106` -- `_listing.Changed`, `_listing.Reloading`, `MouseClick`, `MouseWheel`
  - `src/Mc.Ui/Widgets/CommandLineView.cs:59` -- `_input.KeyDown`
  - `src/Mc.Ui/Widgets/CommandLineView.cs:296,366,383` -- popup handlers accumulate on each show
  - `src/Mc.Ui/Widgets/ButtonBarView.cs:20` -- `MouseClick`
- **Type:** Memory leak
- **Detail:** Event handlers are attached in constructors but never detached. If views are recreated (layout changes, panel swaps), old handlers remain attached to shared objects, causing memory growth and duplicate event firing.
- **Fix:** Implement `IDisposable` or override `Dispose(bool)` to unsubscribe events.

### M6. Selection Rendering Off-by-One in Column Block Mode ✅ FIXED
- **File:** `src/Mc.Editor/EditorView.cs:278`
- **Type:** Visual bug
- **Detail:** `IsInSelection()` uses `col >= left && col < right` for column block. The right boundary column is excluded, so the rightmost selected column is never highlighted.
- **Fix:** Change to `col <= right` or `col < right + 1`.

### M7. Cursor Style Uses Invalid DECSCUSR Value on Focus Loss ✅ FIXED
- **Files:**
  - `src/Mc.Editor/EditorView.cs:100-102`
  - `src/Mc.Ui/Widgets/McTextField.cs:16`
- **Type:** Visual bug
- **Detail:** On focus loss, cursor style is set to `UserShape`. Some terminals do not support `DECSCUSR 0` (UserShape) and may display a missing/invisible cursor in other applications after mc exits.
- **Fix:** Use `SteadyBlock` or `BlinkingBlock` as fallback on blur; restore `UserShape` only on application exit.

### M8. Right Margin Column Boundary Check ✅ FIXED
- **File:** `src/Mc.Editor/EditorView.cs:212`
- **Type:** Visual bug
- **Detail:** Checks `marginScreenCol >= gutter` but should check `marginScreenCol >= 0`. When `gutter > 0` and the margin is at column 0 (unlikely but possible with horizontal scroll), the margin line is not drawn.
- **Fix:** Change `>= gutter` to `>= 0`.

### M9. Clipboard Timeout Too Short ✅ FIXED
- **File:** `src/Mc.Ui/Helpers/ClipboardHelper.cs:51`
- **Type:** Functional issue
- **Detail:** `proc.WaitForExit(2000)` -- 2-second timeout for clipboard tools may be insufficient on slow systems, WSL, or SSH sessions. Clipboard operations silently fail.
- **Fix:** Increase to 5000ms or make configurable.

### M10. TOCTOU in File Copy Existence Check ✅ FIXED (documented)
- **File:** `src/Mc.FileManager/FileOperations.cs:119-135`
- **Type:** Race condition
- **Detail:** Checks `_vfs.Stat(destPath)` then decides whether to overwrite. Between the check and the actual write, the file could be created/deleted by another process.
- **Fix:** Document the race or use atomic create-exclusive semantics where supported.

---

## LOW SEVERITY

### L1. Wide Character (CJK/Emoji) Display Corruption in File Panel ⏭ SKIPPED (complex)
- **Files:**
  - `src/Mc.Ui/Widgets/FilePanelView.cs:586,605,626,680,707` -- filename truncation
  - `src/Mc.Ui/Widgets/FilePanelView.cs:372` -- path truncation
  - `src/Mc.Ui/Widgets/FilePanelView.cs:436` -- column header truncation
- **Type:** Visual bug
- **Detail:** String truncation uses `.Length` (character count) not terminal display width. CJK characters are 2 columns wide but count as 1 character. Truncating at `nameWidth` characters leaves columns misaligned.
- **Fix:** Implement a `StringDisplayWidth()` helper using `Rune.ColumnWidth()` and use it for all truncation calculations.

### L2. Triple-Click Detection Is Non-Standard ⏭ SKIPPED
- **File:** `src/Mc.Editor/EditorView.cs:420`
- **Type:** UX inconsistency
- **Detail:** Triple-click requires two double-clicks on the same line within the timeout. Standard behavior is: third click within ~500ms of the first click on roughly the same position.
- **Fix:** Track click count (1, 2, 3) with a timer reset, rather than checking for `Button1DoubleClicked` twice.

### L3. Mouse Click Beyond Last Line Not Rejected ⏭ SKIPPED (already clamped)
- **File:** `src/Mc.Editor/EditorView.cs:410`
- **Type:** Edge case
- **Detail:** Clicking in the empty area below the last line of text passes the `targetLine < GetLineCount()` check if the last line is exactly at the boundary. Cursor may be placed at an invalid position.
- **Fix:** Clamp `targetCol` to the length of the target line (already partially done, but verify).

### L4. ViewerView / ViewerController Are Dead Code ⏭ SKIPPED (intentional, kept for reference)
- **Files:** `src/Mc.Viewer/ViewerView.cs`, `src/Mc.Viewer/ViewerController.cs`
- **Type:** Dead code
- **Detail:** `ViewFile()` now uses `EditorScreen(readOnly: true)`. The entire `Mc.Viewer` project is unreferenced at runtime. It still compiles and is included in the build.
- **Fix:** Either remove the Viewer project or add a setting to choose between viewer and editor for F3.

### L5. File Cycling (Ctrl+F / Ctrl+B) Lost in Viewer Replacement ⏭ SKIPPED (future work)
- **File:** `src/Mc.Ui/McApplication.cs:888-896`
- **Type:** Feature regression
- **Detail:** The old `ViewerView` supported `Ctrl+F` / `Ctrl+B` to cycle through panel files. The new `EditorScreen(readOnly:true)` replacement does not have this feature. The `_viewedFiles` list is still populated but never used for navigation.
- **Fix:** Add file-cycling keybindings to `EditorScreen` when in read-only mode, or re-add the feature in the McApplication layer.

### L6. Silent Exception Swallowing in File Operations ✅ FIXED (added ErrorCount tracking)
- **Files:**
  - `src/Mc.FileManager/FileOperations.cs:257` -- `CreateDirectory` failure silenced
  - `src/Mc.FileManager/FileOperations.cs:270` -- `CreateSymlink` failure silenced
- **Type:** Hidden failures
- **Detail:** `catch { }` blocks with no logging, no error count, no user notification. Users won't know some files weren't copied.
- **Fix:** At minimum increment an error counter and report at the end of the operation.

### L7. ZIP Unicode Filename Encoding Not Explicit ⏭ SKIPPED (edge case, VfsPath lacks Encoding)
- **File:** `src/Mc.Vfs.Archives/ZipVfsProvider.cs:48-52`
- **Type:** Edge case
- **Detail:** ZIP files created on Windows with non-UTF-8 filenames (e.g. Shift-JIS, GBK) may display garbled names. .NET's `ZipFile.OpenRead()` defaults to UTF-8 which is usually correct but not always.
- **Fix:** Allow specifying encoding via VfsPath's `Encoding` property when opening ZIP files.

### L8. Temp File Name Collision in ExtfsVfsProvider ✅ FIXED
- **File:** `src/Mc.Vfs.Archives/ExtfsVfsProvider.cs:106`
- **Type:** Edge case
- **Detail:** `Path.GetTempFileName()` can collide under heavy concurrent use (extremely unlikely but possible).
- **Fix:** Use `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())`.

### L9. Drag Selection Anchor Inconsistency ✅ FIXED
- **File:** `src/Mc.Editor/EditorView.cs:476`
- **Type:** Edge case
- **Detail:** When starting a mouse drag, `_selectionAnchor` is set to `_editor.CursorOffset` but this may differ from where `_editor.StartSelection()` anchored internally. The view-level anchor and controller-level anchor can diverge, causing visual selection to not match actual selection.
- **Fix:** Use a single source of truth for the selection anchor.

### L10. Typewriter Wrap Records Two Separate Undo Ops ✅ FIXED
- **File:** `src/Mc.Editor/EditorController.cs:755-758`
- **Type:** Minor undo inconsistency
- **Detail:** `CheckTypewriterWrap()` records a `DeleteOp` (remove space) and an `InsertOp` (insert newline) as separate undo entries. User must press Undo twice to reverse a single wrap.
- **Fix:** Combine into a single composite undo operation.

### L11. No Cancellation Support for Archive Directory Listing ⏭ SKIPPED (requires VFS interface change)
- **File:** `src/Mc.Vfs.Archives/TarVfsProvider.cs:55-90`
- **Type:** UX issue
- **Detail:** `ListDirectory()` for large archives blocks with no way to cancel. The UI freezes until the full archive is scanned.
- **Fix:** Accept `CancellationToken` parameter in the VFS interface.

### L12. Quick Search Status Can Overflow ✅ FIXED
- **File:** `src/Mc.Ui/Widgets/FilePanelView.cs:287-289`
- **Type:** Visual bug
- **Detail:** Quick search status text `" Quick search: {query}_"` is not width-capped. With a very long search string (e.g. pasted text), it overflows the status area.
- **Fix:** Truncate the display to available width.

---

## SUMMARY

| Severity | Count | Fixed | Skipped | Key Areas |
|----------|-------|-------|---------|-----------|
| Critical | 4     | 4     | 0       | BZip2 wrong algo, archive open fails, silent overwrite, shell injection |
| High     | 8     | 8     | 0       | Undo corruption, hex crash, replace-all bug, 7z missing, data loss risks |
| Medium   | 10    | 9     | 1       | Resource leaks (processes, dialogs, events, CTS), visual off-by-ones |
| Low      | 12    | 6     | 6       | Unicode display, dead code, feature regressions, edge cases |
| **Total**| **34**| **27**| **7**   | |

**Fix session completed: 2026-03-18**
