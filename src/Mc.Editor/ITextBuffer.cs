namespace Mc.Editor;

/// <summary>
/// Common interface for in-memory and large-file virtual text buffers.
///
/// Offset semantics:
///   For TextBuffer (small files) every 'int offset' is an absolute character offset
///   into the full document.
///   For LargeFileBuffer (large files) every 'int offset' is a character offset
///   relative to the start of the currently-loaded window.  Absolute line numbers
///   are expressed as 'long' throughout to support files with more than 2 billion lines.
/// </summary>
public interface ITextBuffer
{
    /// <summary>
    /// Number of characters available through the indexer.
    /// For TextBuffer this is the full document length.
    /// For LargeFileBuffer this is the size of the loaded window.
    /// </summary>
    int Length { get; }

    bool IsModified { get; }
    string LineEnding { get; }

    /// <summary>Access a character by offset (window-relative for large files).</summary>
    char this[int index] { get; }

    /// <summary>Returns the full text. Throws NotSupportedException for very large files.</summary>
    string GetText();

    /// <summary>Returns the text of a single line (without the newline character).
    /// For large files this may shift the loaded window.</summary>
    string GetLine(long lineNumber);

    /// <summary>Returns the total number of lines in the document.</summary>
    long GetLineCount();

    /// <summary>
    /// Convert an offset (window-relative for large files) to (absolute line, column).
    /// </summary>
    (long Line, int Column) OffsetToLineCol(int offset);

    /// <summary>
    /// Convert (absolute line, column) to an offset (window-relative for large files).
    /// For large files this may shift the loaded window so the line is accessible.
    /// </summary>
    int LineColToOffset(long line, int col);

    /// <summary>Extract a substring by offset (window-relative for large files) and length.</summary>
    string Extract(int start, int length);

    // --- Mutation ---
    void Insert(int position, char ch);
    void Insert(int position, string text);
    void Delete(int position, int count = 1);
    void Replace(int position, int length, string text);

    // --- Persistence ---
    void SetContent(string content);
    void SaveFile(string path);
}
