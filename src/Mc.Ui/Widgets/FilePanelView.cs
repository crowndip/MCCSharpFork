using Mc.Core.Models;
using Mc.Core.Utilities;
using Mc.FileManager;
using Terminal.Gui;

namespace Mc.Ui.Widgets;

/// <summary>
/// Panel listing display mode.
/// Equivalent to list_type in the original C codebase (src/filemanager/panel.h).
/// </summary>
public enum PanelListingMode
{
    /// <summary>Name + size + modification time (default).</summary>
    Full,
    /// <summary>Names only — more files visible at once.</summary>
    Brief,
}

/// <summary>
/// One file panel drawn in classic MC style:
///   ┌────── /path ──────┐
///   │ Name   Size Modify │  ← column header
///   │ ..                 │  ← entries
///   │ /Documents         │
///   │ file.c   1.2k Jan 1│
///   │ file.txt  1234 Jan 2│ ← selected (black on cyan)
///   │ info line          │  ← status
///   └────────────────────┘
/// Equivalent to WPanel in the original C codebase (lib/widget + src/filemanager/panel.c).
/// </summary>
public sealed class FilePanelView : View
{
    private readonly DirectoryListing _listing;
    private int _cursorIndex;
    private int _scrollOffset;
    private bool _isActive;
    private string _statusText = string.Empty;
    private PanelListingMode _listingMode = PanelListingMode.Full;

    // Quick search state (equivalent to panel->searching in original MC panel.c)
    private string _quickSearch = string.Empty;
    private bool _quickSearchActive;

    public event EventHandler<FileEntry?>? EntryActivated;
    public event EventHandler<int>? CursorChanged;
    public event EventHandler? BecameActive;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            SetNeedsDraw();
        }
    }

    public PanelListingMode ListingMode
    {
        get => _listingMode;
        set
        {
            _listingMode = value;
            SetNeedsDraw();
        }
    }

    public DirectoryListing Listing => _listing;

    public FileEntry? CurrentEntry =>
        _cursorIndex >= 0 && _cursorIndex < _listing.Entries.Count
            ? _listing.Entries[_cursorIndex]
            : null;

    public FilePanelView(DirectoryListing listing)
    {
        _listing = listing;
        CanFocus = true;
        ColorScheme = McTheme.Panel;

        _listing.Changed += OnListingChanged;
        MouseClick += OnMouseClick;

        UpdateStatus();
    }

    // --- Event handlers ---

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (!_isActive)
        {
            BecameActive?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Click inside the file-entry area (rows 2 .. h-3)
        int clickY = e.Position.Y;
        int h = Viewport.Height;
        int fileAreaStart = 2;
        int fileAreaEnd = h - 2; // exclusive (h-2 = status line, h-1 = bottom border)

        if (clickY >= fileAreaStart && clickY < fileAreaEnd)
        {
            int idx = _scrollOffset + (clickY - fileAreaStart);
            if (idx >= 0 && idx < _listing.Entries.Count)
            {
                _cursorIndex = idx;
                UpdateStatus();
                CursorChanged?.Invoke(this, _cursorIndex);
                SetNeedsDraw();
            }
        }
    }

    private void OnListingChanged(object? sender, EventArgs e)
    {
        Application.Invoke(() =>
        {
            _quickSearchActive = false;
            _quickSearch = string.Empty;
            if (_cursorIndex >= _listing.Entries.Count)
                _cursorIndex = Math.Max(0, _listing.Entries.Count - 1);
            EnsureCursorVisible();
            UpdateStatus();
            SetNeedsDraw();
        });
    }

    // --- Layout helpers ---

    // Number of file-entry rows visible between the column header and the status line.
    // Layout: row 0 = top border, row 1 = column header,
    //         rows 2 .. h-3 = entries,
    //         row h-2 = status, row h-1 = bottom border.
    private int ContentRows => Math.Max(0, Viewport.Height - 4);

    private void EnsureCursorVisible()
    {
        int contentRows = ContentRows;
        if (contentRows <= 0) return;
        if (_cursorIndex < _scrollOffset)
            _scrollOffset = _cursorIndex;
        else if (_cursorIndex >= _scrollOffset + contentRows)
            _scrollOffset = _cursorIndex - contentRows + 1;
    }

    private void UpdateStatus()
    {
        if (_quickSearchActive)
        {
            _statusText = $" Quick search: {_quickSearch}_";
            return;
        }
        var marked = _listing.MarkedCount;
        if (marked > 0)
        {
            _statusText = $" {marked} files, {FileSizeFormatter.Format(_listing.TotalMarkedSize)} marked";
        }
        else
        {
            var entry = CurrentEntry;
            if (entry != null && !entry.IsParentDir)
                _statusText = $" {entry.Name}  {FileSizeFormatter.Format(entry.Size)}  {entry.ModificationTime:yyyy-MM-dd HH:mm}";
            else
                _statusText = $" {_listing.TotalFiles} files, {_listing.TotalDirectories} dirs";
        }
    }

    // --- Custom drawing ---

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        int w = Viewport.Width;
        int h = Viewport.Height;
        if (w < 4 || h < 4) return false;

        DrawBorderAndPath(w, h);
        DrawColumnHeader(w);
        DrawFileEntries(w, h);
        DrawStatusLine(w, h);
        return false;
    }

    private void DrawBorderAndPath(int w, int h)
    {
        var frameAttr = McTheme.PanelFrame;
        // Active panel: path shown in bright header color; inactive: frame color (dimmer)
        var pathAttr = _isActive ? McTheme.PanelHeader : McTheme.PanelFrame;

        // ── Top border: ┌─── /path ───┐ ──────────────────────────────
        var pathStr     = PathUtils.TildePath(_listing.CurrentPath.ToString());
        var displayPath = $" {pathStr} ";
        int available   = w - 2; // width between the two corner chars
        int dashTotal   = available - displayPath.Length;
        int dashLeft, dashRight;

        if (dashTotal < 0)
        {
            // Truncate path to fit
            int maxPathLen = available - 2; // leave at least " " + " "
            if (maxPathLen > 0)
                displayPath = " " + pathStr[..Math.Min(pathStr.Length, maxPathLen)] + " ";
            else
                displayPath = string.Empty;
            dashLeft  = 0;
            dashRight = available - displayPath.Length;
            if (dashRight < 0) { displayPath = displayPath[..available]; dashRight = 0; }
        }
        else
        {
            dashLeft  = dashTotal / 2;
            dashRight = dashTotal - dashLeft;
        }

        Move(0, 0);
        Driver.SetAttribute(frameAttr);
        Driver.AddStr("┌" + new string('─', dashLeft));
        Driver.SetAttribute(pathAttr);
        Driver.AddStr(displayPath);
        Driver.SetAttribute(frameAttr);
        Driver.AddStr(new string('─', dashRight) + "┐");

        // ── Left and right side bars ──────────────────────────────────
        for (int y = 1; y < h - 1; y++)
        {
            Driver.SetAttribute(frameAttr);
            Move(0,     y); Driver.AddStr("│");
            Move(w - 1, y); Driver.AddStr("│");
        }

        // ── Bottom border: └────────────┘ ────────────────────────────
        Move(0, h - 1);
        Driver.SetAttribute(frameAttr);
        Driver.AddStr("└" + new string('─', w - 2) + "┘");
    }

    private void DrawColumnHeader(int w)
    {
        int innerWidth = w - 2;
        Move(1, 1);
        Driver.SetAttribute(McTheme.PanelHeader);

        if (_listingMode == PanelListingMode.Brief)
        {
            Driver.AddStr(" Name".PadRight(innerWidth));
            return;
        }

        (int nameWidth, int sizeWidth, int dateWidth) = ColumnWidths(innerWidth);
        var header = " "
            + "Name".PadRight(nameWidth)
            + "Size".PadLeft(sizeWidth) + " "
            + "Modify time".PadRight(dateWidth);

        if (header.Length > innerWidth) header = header[..innerWidth];
        Driver.AddStr(header.PadRight(innerWidth));
    }

    private void DrawFileEntries(int w, int h)
    {
        int innerWidth  = w - 2;
        int contentRows = h - 4;
        if (contentRows <= 0) return;

        var entries    = _listing.Entries;
        var normalAttr = McTheme.PanelFile;

        for (int row = 0; row < contentRows; row++)
        {
            int entryIdx = _scrollOffset + row;
            int screenY  = row + 2; // 0=top border, 1=header, 2=first entry

            Move(1, screenY);

            if (entryIdx >= entries.Count)
            {
                Driver.SetAttribute(normalAttr);
                Driver.AddStr(new string(' ', innerWidth));
                continue;
            }

            var  entry    = entries[entryIdx];
            bool isCursor = _isActive && entryIdx == _cursorIndex;

            Terminal.Gui.Attribute attr;
            if      (isCursor && entry.IsMarked) attr = McTheme.PanelMarkedCursor;
            else if (isCursor)                   attr = McTheme.PanelCursor;
            else if (entry.IsMarked)             attr = McTheme.PanelMarked;
            else if (entry.IsDirectory || entry.IsParentDir) attr = McTheme.PanelDirectory;
            else if (entry.IsSymlink)            attr = McTheme.PanelSymlink;
            else if (entry.IsExecutable)         attr = McTheme.PanelExecutable;
            else                                 attr = normalAttr;

            Driver.SetAttribute(attr);
            var text = _listingMode == PanelListingMode.Brief
                ? FormatBriefEntry(entry, innerWidth)
                : FormatEntry(entry, innerWidth);
            Driver.AddStr(text);
            // FormatEntry/FormatBriefEntry fills innerWidth-1 chars; pad the last cell
            Driver.AddStr(" ");
        }
    }

    private void DrawStatusLine(int w, int h)
    {
        int innerWidth = w - 2;
        Move(1, h - 2);
        Driver.SetAttribute(McTheme.PanelStatus);
        var text = _statusText.Length <= innerWidth
            ? _statusText.PadRight(innerWidth)
            : _statusText[..innerWidth];
        Driver.AddStr(text);
    }

    // --- Entry formatting ---

    private static (int nameWidth, int sizeWidth, int dateWidth) ColumnWidths(int innerWidth)
    {
        const int sizeWidth = 8;
        const int dateWidth = 12;
        int nameWidth = Math.Max(12, innerWidth - sizeWidth - dateWidth - 3);
        return (nameWidth, sizeWidth, dateWidth);
    }

    private static string FormatEntry(FileEntry entry, int innerWidth)
    {
        var (nameWidth, sizeWidth, dateWidth) = ColumnWidths(innerWidth);

        var marker = entry.IsMarked ? "*" : " ";

        string name;
        if      (entry.IsParentDir)  name = "..";
        else if (entry.IsDirectory)  name = "/" + entry.Name;
        else if (entry.IsSymlink)    name = "~" + entry.Name;
        else                         name = entry.Name;

        if (name.Length > nameWidth) name = name[..(nameWidth - 1)] + "~";
        name = name.PadRight(nameWidth);

        var size = FileSizeFormatter.FormatPanelSize(entry.Size, entry.IsDirectory)
                                    .PadLeft(sizeWidth);
        var date = entry.ModificationTime == DateTime.MinValue
            ? string.Empty.PadRight(dateWidth)
            : entry.ModificationTime.ToString("MMM dd HH:mm").PadRight(dateWidth);

        // Total: 1 + nameWidth + sizeWidth + 1 + dateWidth = innerWidth - 1
        return $"{marker}{name}{size} {date}";
    }

    private static string FormatBriefEntry(FileEntry entry, int innerWidth)
    {
        var marker = entry.IsMarked ? "*" : " ";
        string name;
        if      (entry.IsParentDir)  name = "..";
        else if (entry.IsDirectory)  name = "/" + entry.Name;
        else if (entry.IsSymlink)    name = "~" + entry.Name;
        else                         name = entry.Name;

        int nameWidth = innerWidth - 1; // 1 for marker
        if (name.Length > nameWidth) name = name[..(nameWidth - 1)] + "~";
        // Total: 1 + nameWidth = innerWidth; DrawFileEntries pads one extra space → innerWidth+1
        // but that is fine because the right border is at column w-1
        return marker + name.PadRight(nameWidth);
    }

    // --- Quick search ---

    /// <summary>
    /// Searches panel entries for the first name starting with <see cref="_quickSearch"/>
    /// and moves the cursor there.  Equivalent to panel_quick_search() in panel.c.
    /// </summary>
    private void SearchInPanel()
    {
        var entries = _listing.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Name.StartsWith(_quickSearch, StringComparison.OrdinalIgnoreCase))
            {
                if (_cursorIndex != i)
                {
                    _cursorIndex = i;
                    EnsureCursorVisible();
                    CursorChanged?.Invoke(this, _cursorIndex);
                }
                break;
            }
        }
        UpdateStatus();
        SetNeedsDraw();
    }

    private void ExitQuickSearch()
    {
        _quickSearchActive = false;
        _quickSearch = string.Empty;
        UpdateStatus();
        SetNeedsDraw();
    }

    // --- Keyboard input ---

    protected override bool OnKeyDown(Key keyEvent)
    {
        // ── Quick search mode ──────────────────────────────────────────────
        if (_quickSearchActive)
        {
            if (keyEvent.KeyCode == KeyCode.Esc)
            {
                ExitQuickSearch();
                return true;
            }
            if (keyEvent.KeyCode == KeyCode.Enter)
            {
                ExitQuickSearch();
                EntryActivated?.Invoke(this, CurrentEntry);
                return true;
            }
            if (keyEvent.KeyCode == KeyCode.Backspace)
            {
                if (_quickSearch.Length > 0)
                    _quickSearch = _quickSearch[..^1];
                if (_quickSearch.Length == 0)
                    ExitQuickSearch();
                else
                    SearchInPanel();
                return true;
            }
            // Printable char: extend search buffer
            var rune = keyEvent.AsRune;
            if (rune.Value >= 32 && !keyEvent.IsCtrl && !keyEvent.IsAlt)
            {
                _quickSearch += (char)rune.Value;
                SearchInPanel();
                return true;
            }
            // Any other key (navigation etc.) exits search and falls through
            ExitQuickSearch();
        }

        switch (keyEvent.KeyCode)
        {
            case KeyCode.CursorUp:
                if (_cursorIndex > 0)
                {
                    _cursorIndex--;
                    EnsureCursorVisible();
                    UpdateStatus();
                    CursorChanged?.Invoke(this, _cursorIndex);
                    SetNeedsDraw();
                }
                return true;

            case KeyCode.CursorDown:
                if (_cursorIndex < _listing.Entries.Count - 1)
                {
                    _cursorIndex++;
                    EnsureCursorVisible();
                    UpdateStatus();
                    CursorChanged?.Invoke(this, _cursorIndex);
                    SetNeedsDraw();
                }
                return true;

            case KeyCode.PageUp:
            {
                int step = Math.Max(1, ContentRows);
                _cursorIndex = Math.Max(0, _cursorIndex - step);
                EnsureCursorVisible();
                UpdateStatus();
                CursorChanged?.Invoke(this, _cursorIndex);
                SetNeedsDraw();
                return true;
            }

            case KeyCode.PageDown:
            {
                int step = Math.Max(1, ContentRows);
                _cursorIndex = Math.Min(_listing.Entries.Count - 1, _cursorIndex + step);
                EnsureCursorVisible();
                UpdateStatus();
                CursorChanged?.Invoke(this, _cursorIndex);
                SetNeedsDraw();
                return true;
            }

            case KeyCode.Home:
                _cursorIndex  = 0;
                _scrollOffset = 0;
                UpdateStatus();
                CursorChanged?.Invoke(this, _cursorIndex);
                SetNeedsDraw();
                return true;

            case KeyCode.End:
                _cursorIndex = Math.Max(0, _listing.Entries.Count - 1);
                EnsureCursorVisible();
                UpdateStatus();
                CursorChanged?.Invoke(this, _cursorIndex);
                SetNeedsDraw();
                return true;

            case KeyCode.Insert:
            case KeyCode.Space:
                ToggleMark();
                return true;

            case KeyCode.Enter:
                EntryActivated?.Invoke(this, CurrentEntry);
                return true;

            case KeyCode.Backspace:
                EntryActivated?.Invoke(this, _listing.Entries.FirstOrDefault(e => e.IsParentDir));
                return true;

            default:
                // Activate quick search on any printable character (no Ctrl/Alt modifier)
                if (_isActive)
                {
                    var rune = keyEvent.AsRune;
                    if (rune.Value >= 32 && !keyEvent.IsCtrl && !keyEvent.IsAlt)
                    {
                        _quickSearch = ((char)rune.Value).ToString();
                        _quickSearchActive = true;
                        SearchInPanel();
                        return true;
                    }
                }
                if (!_isActive) BecameActive?.Invoke(this, EventArgs.Empty);
                return base.OnKeyDown(keyEvent);
        }
    }

    // --- Public API ---

    public void MoveCursorTo(int index)
    {
        if (index < 0 || index >= _listing.Entries.Count) return;
        _cursorIndex = index;
        EnsureCursorVisible();
        UpdateStatus();
        SetNeedsDraw();
    }

    public void ToggleMark()
    {
        _listing.MarkFile(_cursorIndex);
        if (_cursorIndex < _listing.Entries.Count - 1)
        {
            _cursorIndex++;
            EnsureCursorVisible();
        }
        UpdateStatus();
        SetNeedsDraw();
    }

    public void Refresh() => _listing.Reload();
}
