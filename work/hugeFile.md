# 10 GB File Support in mcedit — Implementation Plan

## Progress

| Step | Status | Notes |
|------|--------|-------|
| 1 — ITextBuffer long signatures | ✅ Done | GetLine(long), GetLineCount():long, OffsetToLineCol→(long,int), LineColToOffset(long,int) |
| 2 — TextBuffer long signatures  | ✅ Done | Thin casts; internal `int` state unchanged |
| 3 — LargeFileBuffer block index | ✅ Done | BLOCK_SIZE=4096, two long[] arrays, async-ready Open/OpenAsync, SeekToAbsChar, AbsoluteChar |
| 4 — EditorController long types | ✅ Done | SortedSet<long> bookmarks, GotoLine(long), long _lastAbsSearchOffset, ShiftBlock/FormatParagraph long loops |
| 5 — EditorView long _topLine    | ✅ Done | long _topLine, long _colBlockAnchorLine, long lineNo in draw loop, long in mouse handlers |
| 6 — Tests update                | ✅ Done | 10/10 LargeFileBuffer tests pass; Mc.Core 129/129; Mc.FileManager 55/55 |

## Overview

Phase 1 (current) uses a dense per-line byte-offset index and a 3 000-line sliding window.
For a 10 GB file with short lines (~10 chars) that produces ~1 billion lines and a 16 GB RAM
index — unusable.  Phase 2 replaces the dense index with a two-level block index (~4 MB for
1 B lines) and promotes all line-number and char-offset types from `int` to `long`.

---

## 1. Two-Level Block Index

```
BLOCK_SIZE = 4096   // lines per block
```

Instead of one array entry per line, store one entry per block:

```csharp
long[] _blockByteOffsets;   // byte offset of the first byte of block[i]
long[] _blockCharOffsets;   // cumulative char count before block[i]
long   _totalLines;         // total number of lines in the file
long   _totalChars;         // total number of chars in the file
```

Memory for 1 billion lines:
- `1 000 000 000 / 4096 ≈ 244 141` blocks
- `244 141 × 8 bytes × 2 arrays ≈ 3.9 MB`  ✓

### Index Build (background thread)

```csharp
public static LargeFileBuffer Open(string path)
{
    var buf = new LargeFileBuffer(path);
    buf._buildTask = Task.Run(buf.BuildIndex);
    return buf;
}

private void BuildIndex()
{
    // Scan file sequentially in 512 KB byte chunks.
    // Count newlines; every BLOCK_SIZE-th newline saves (byteOffset, charOffset).
    // At end: store _totalLines, _totalChars.
    // Set _isReady = true; fire IndexingComplete event.
}

public bool   IsReady           => _isReady;
public event  EventHandler?     IndexingComplete;
public void   EnsureReady()     => _buildTask?.Wait();
```

### Locating a Line (block binary-search + linear scan)

```csharp
private long FindByteOffsetOfLine(long lineNumber)
{
    int block = (int)(lineNumber / BLOCK_SIZE);
    long bytePos  = _blockByteOffsets[block];
    long lineStart = (long)block * BLOCK_SIZE;
    long linesToSkip = lineNumber - lineStart;
    // Seek to bytePos, scan forward skipping linesToSkip newlines.
    return bytePos;   // byte offset of desired line's first byte
}
```

Worst-case scan per access: 4 095 newlines — negligible.

---

## 2. ITextBuffer — Long Signatures

Update the interface so all line numbers and char counts are `long`:

```csharp
public interface ITextBuffer
{
    long   Length       { get; }          // chars (window size for large files)
    bool   IsModified   { get; }
    string LineEnding   { get; }
    char   this[int index] { get; }       // index within window

    string GetText();
    string GetLine(long lineNumber);
    long   GetLineCount();

    (long Line, int Column) OffsetToLineCol(int offset);
    int    LineColToOffset(long line, int col);   // shifts window; returns int in-window offset

    string Extract(int start, int length);
    void   Insert(int position, char ch);
    void   Insert(int position, string text);
    void   Delete(int position, int count = 1);
    void   Replace(int position, int length, string text);
    void   SetContent(string content);
    void   SaveFile(string path);
}
```

`TextBuffer` wraps its existing `int` internals with trivial casts to satisfy the interface.

---

## 3. LargeFileBuffer — Window-Relative Cursor

The **window** is still ~3 000 lines loaded into an internal `TextBuffer`.

Key invariant: `_cursorOffset` (`int`) is always an offset **within the window**, never an
absolute file offset.  Absolute position is `_windowFirstChar + _cursorOffset`.

### Core state

```csharp
private long _windowFirstLine;    // absolute line index of window[0]
private long _windowLastLine;
private long _windowFirstChar;    // cumulative char count before window start
private long _windowLastChar;
private TextBuffer _window = new();
```

### LineColToOffset

```csharp
public int LineColToOffset(long absoluteLine, int col)
{
    EnsureWindowContains(absoluteLine);
    long relLine = absoluteLine - _windowFirstLine;
    return _window.LineColToOffset((int)relLine, col);
}
```

This is the only place window shifts happen during cursor movement.

### OffsetToLineCol

```csharp
public (long Line, int Column) OffsetToLineCol(int windowOffset)
{
    var (relLine, col) = _window.OffsetToLineCol(windowOffset);
    return (_windowFirstLine + relLine, col);
}
```

### Length (returns window size)

```csharp
public long Length => _window.Length;
```

---

## 4. EditorView Changes

```csharp
private long _topLine;            // was int
private long _colBlockAnchorLine; // was int
```

Drawing loop:

```csharp
for (long lineNo = _topLine; lineNo < _topLine + viewHeight; lineNo++)
{
    if (lineNo >= Buffer.GetLineCount()) break;
    string lineText = Buffer.GetLine(lineNo);
    // render ...
}
```

### Progress Overlay

When `Buffer` is a `LargeFileBuffer` and `!lfb.IsReady`, draw a centred overlay:

```csharp
protected override bool OnDrawingContent(DrawContext? ctx)
{
    base.OnDrawingContent(ctx);
    if (Buffer is LargeFileBuffer lfb && !lfb.IsReady)
    {
        DrawIndexingOverlay("Indexing file…");
        return false;
    }
    // normal rendering
}
```

Subscribe to `lfb.IndexingComplete` in `EditorController` to trigger a `SetNeedsDisplay()`.

---

## 5. EditorController Changes

```csharp
private SortedSet<long> _bookmarks = new();   // was SortedSet<int>
```

`GotoLine(long line)`:

```csharp
public void GotoLine(long line)
{
    line = Math.Clamp(line, 0, Buffer.GetLineCount() - 1);
    _cursorOffset = Buffer.LineColToOffset(line, 0);
    // fire CursorMoved
}
```

`MoveToStart` / `MoveToEnd`:

```csharp
public void MoveToStart() => _cursorOffset = Buffer.LineColToOffset(0, 0);
public void MoveToEnd()   => _cursorOffset = Buffer.LineColToOffset(Buffer.GetLineCount() - 1, 0);
```

Search result placement — `SeekToAbsChar(long absChar)`:

```csharp
private void SeekToAbsChar(long absChar)
{
    if (Buffer is LargeFileBuffer lfb)
        _cursorOffset = lfb.SeekToAbsChar(absChar);   // shifts window, returns int
    else
        _cursorOffset = (int)absChar;
}
```

---

## 6. Save (3-Part Write)

When saving a large file that has been partially edited through the window:

1. Copy bytes `[0, _origWindowStartByte)` from the original file path unchanged.
2. Write the current window content (`_window.GetText()`) re-encoded as UTF-8 (± BOM).
3. Copy bytes `[_origWindowEndByte, fileEnd)` from the original file path unchanged.

`_origWindowStartByte` / `_origWindowEndByte` are stored when `LoadWindow()` is called and
updated on every window shift.  They are computed precisely by the byte-level scan in
`FindByteOffsetOfLine`.

---

## 7. Implementation Sequence

| Step | Task | Estimated effort |
|------|------|-----------------|
| 1 | Update `ITextBuffer` signatures (`int` → `long` for line/char params) | 1–2 h |
| 2 | Update `TextBuffer` to implement updated `ITextBuffer` (thin casts) | 30 min |
| 3 | Rewrite `LargeFileBuffer`: two-level block index, async build, `IsReady`/`EnsureReady`, window-relative API | 4–6 h |
| 4 | Update `EditorController`: `long _bookmarks`, `GotoLine(long)`, `SeekToAbsChar`, search integration | 2 h |
| 5 | Update `EditorView`: `long _topLine`, drawing loop, progress overlay | 2 h |
| 6 | Update tests: mock long-line files, verify no overflow, progress wait | 1–2 h |
| 7 | Manual smoke-test with real 10 GB file | — |

---

## 8. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Block size 4 096 | Matches common filesystem page size; worst-case per-access scan is ~4 K newline checks |
| Window size 3 000 lines | Keeps window RAM under ~3 MB for typical 80-char lines |
| `int` cursor offsets within window | Avoids rewriting all TextBuffer internals; max window is ~240 KB chars, well within `int` |
| Async index build | Avoids blocking UI for 10–20 s on a cold 10 GB file |
| Window-relative `Length` | Existing cursor-motion code uses `Length` as an upper bound for clamp; within the window this is always correct |
| Long `GetLineCount()` | Required to avoid overflow at 2.1 B lines |

---

## 9. Memory Budget for a 10 GB File (100-char average line)

| Component | Size |
|-----------|------|
| Block index (2 × `long[]`, ~24 M blocks) | ~390 MB — wait, recalc: 10 GB / 100 chars = 100 M lines → 100 M / 4 096 ≈ 24 414 blocks → 24 414 × 16 B = **390 KB** |
| Window (3 000 × 100 chars) | ~300 KB |
| Read buffer for index scan | 512 KB |
| **Total** | **< 2 MB** |

For pathological 1-char lines (10 B lines in 10 GB):

| Component | Size |
|-----------|------|
| Block index: 10 B / 4 096 ≈ 2.44 M blocks → 2.44 M × 16 B | **39 MB** |
| Window | 3 000 × 1 char = 3 KB |
| **Total** | **~39 MB** |

Both cases are well within available RAM on any modern system.
