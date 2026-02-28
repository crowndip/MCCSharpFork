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
    /// <summary>Two-column names-only — more files visible at once.</summary>
    Brief,
    /// <summary>ls -l style: permissions + owner + group + size + date + name.</summary>
    Long,
}

/// <summary>
/// One file panel drawn in classic MC style.
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

    // Public option: show free disk space in status (from McSettings.ShowFreeSpace)
    public bool ShowFreeSpace { get; set; } = true;

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

        int clickY = e.Position.Y;
        int h = Viewport.Height;
        int fileAreaStart = 2;
        int fileAreaEnd = h - 2;

        if (clickY >= fileAreaStart && clickY < fileAreaEnd)
        {
            int idx = _scrollOffset + (clickY - fileAreaStart);
            if (_listingMode == PanelListingMode.Brief)
            {
                // Two columns: right-column entries start at _scrollOffset + ContentRows
                int contentRows = ContentRows;
                int innerWidth  = Viewport.Width - 2;
                int colWidth    = (innerWidth - 1) / 2;
                int col1StartX  = 1 + colWidth + 1; // panel-local X of right column start
                if (e.Position.X >= col1StartX)
                    idx = _scrollOffset + contentRows + (clickY - fileAreaStart);
            }
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

        if (_listingMode == PanelListingMode.Brief)
        {
            // Brief (two-column) mode: visible range = _scrollOffset .. _scrollOffset + 2*contentRows - 1
            int visible = 2 * contentRows;
            if (_cursorIndex < _scrollOffset)
                _scrollOffset = (_cursorIndex / contentRows) * contentRows;
            else if (_cursorIndex >= _scrollOffset + visible)
                _scrollOffset = ((_cursorIndex - visible + contentRows) / contentRows) * contentRows;
        }
        else
        {
            if (_cursorIndex < _scrollOffset)
                _scrollOffset = _cursorIndex;
            else if (_cursorIndex >= _scrollOffset + contentRows)
                _scrollOffset = _cursorIndex - contentRows + 1;
        }
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
            // Singular/plural (#34)
            string label = marked == 1 ? "file" : "files";
            _statusText = $" {marked} {label}, {FileSizeFormatter.Format(_listing.TotalMarkedSize)} tagged";
        }
        else
        {
            var entry = CurrentEntry;
            if (entry != null && !entry.IsParentDir)
            {
                // Symlink: show target after " -> " (#20)
                string extra = entry.IsSymlink && !string.IsNullOrEmpty(entry.SymlinkTarget)
                    ? $" -> {entry.SymlinkTarget}" : string.Empty;
                // Date in ls-style format (#12)
                string dateStr = FormatDate(entry.ModificationTime, 12).TrimEnd();
                _statusText = $" {entry.Name}{extra}  {FileSizeFormatter.Format(entry.Size)}  {dateStr}";
            }
            else
            {
                // No file selected: count + free space (#16, #25)
                string freeStr = string.Empty;
                if (ShowFreeSpace)
                {
                    try
                    {
                        var root = Path.GetPathRoot(_listing.CurrentPath.ToString()) ?? "/";
                        freeStr = $", {FileSizeFormatter.Format(new DriveInfo(root).AvailableFreeSpace)} free";
                    }
                    catch { }
                }
                _statusText = $" {_listing.TotalFiles} files, {_listing.TotalDirectories} dirs{freeStr}";
            }
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
        // Active panel frame is bright (PanelHeader); inactive is dimmed (PanelFrame).
        // Applies to the ENTIRE border — corners, dashes, side bars, bottom. (#6, #27)
        var frameAttr = _isActive ? McTheme.PanelHeader : McTheme.PanelFrame;

        // ── Top border: ┌─── ~/path/ ───┐  (trailing slash per #32) ──────
        var pathStr = PathUtils.TildePath(_listing.CurrentPath.ToString());
        if (!pathStr.EndsWith('/')) pathStr += '/';

        var displayPath = $" {pathStr} ";
        int available   = w - 2;
        int dashTotal   = available - displayPath.Length;
        int dashLeft, dashRight;

        if (dashTotal < 0)
        {
            int maxPathLen = available - 2;
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
            // Extra dash goes to LEFT so right is equal or shorter (#38)
            dashRight = dashTotal / 2;
            dashLeft  = dashTotal - dashRight;
        }

        Move(0, 0);
        Driver.SetAttribute(frameAttr);
        Driver.AddStr("┌" + new string('─', dashLeft) + displayPath + new string('─', dashRight) + "┐");

        // ── Side bars ────────────────────────────────────────────────────
        for (int y = 1; y < h - 1; y++)
        {
            Move(0,     y); Driver.AddStr("│");
            Move(w - 1, y); Driver.AddStr("│");
        }

        // ── Bottom border ─────────────────────────────────────────────────
        Move(0, h - 1);
        Driver.AddStr("└" + new string('─', w - 2) + "┘");
    }

    private void DrawColumnHeader(int w)
    {
        int innerWidth = w - 2;
        Move(1, 1);
        Driver.SetAttribute(McTheme.PanelHeader);

        if (_listingMode == PanelListingMode.Brief)
        {
            // Two-column brief header: " Name │ Name" (#18)
            int colWidth  = (innerWidth - 1) / 2;
            int col2Width = innerWidth - 1 - colWidth;
            var frameAttr = _isActive ? McTheme.PanelHeader : McTheme.PanelFrame;
            Driver.AddStr(" Name".PadRight(colWidth));
            Driver.SetAttribute(frameAttr);
            Driver.AddStr("│");
            Driver.SetAttribute(McTheme.PanelHeader);
            Driver.AddStr(" Name".PadRight(col2Width));
            return;
        }

        (int nameWidth, int sizeWidth, int dateWidth) = ColumnWidths(innerWidth);
        var sort = _listing.Sort;

        // Sort direction indicator on active sort column (#9, #31)
        string nameInd = sort.Field == SortField.Name             ? (sort.Descending ? "↓" : "↑") : string.Empty;
        string sizeInd = sort.Field == SortField.Size             ? (sort.Descending ? "↓" : "↑") : string.Empty;
        string dateInd = sort.Field == SortField.ModificationTime ? (sort.Descending ? "↓" : "↑") : string.Empty;

        var header = (" Name" + nameInd).PadRight(nameWidth)
                   + ("Size" + sizeInd).PadLeft(sizeWidth) + " "
                   + ("Modify time" + dateInd).PadRight(dateWidth);

        if (header.Length > innerWidth) header = header[..innerWidth];
        Driver.AddStr(header.PadRight(innerWidth));
    }

    private void DrawFileEntries(int w, int h)
    {
        int innerWidth  = w - 2;
        int contentRows = h - 4;
        if (contentRows <= 0) return;

        if (_listingMode == PanelListingMode.Brief)
        {
            DrawBriefEntries(innerWidth, contentRows);
            return;
        }

        var entries    = _listing.Entries;
        var normalAttr = McTheme.PanelFile;

        for (int row = 0; row < contentRows; row++)
        {
            int entryIdx = _scrollOffset + row;
            int screenY  = row + 2;

            Move(1, screenY);

            if (entryIdx >= entries.Count)
            {
                Driver.SetAttribute(normalAttr);
                Driver.AddStr(new string(' ', innerWidth));
                continue;
            }

            var entry = entries[entryIdx];
            Driver.SetAttribute(GetEntryAttr(entry, entryIdx));

            var text = _listingMode == PanelListingMode.Long
                ? FormatLongEntry(entry, innerWidth)
                : FormatEntry(entry, innerWidth);
            Driver.AddStr(text);
        }
    }

    // Brief mode: two columns with │ separator (#4)
    private void DrawBriefEntries(int innerWidth, int contentRows)
    {
        int colWidth  = (innerWidth - 1) / 2;
        int col2Width = innerWidth - 1 - colWidth;
        int sepX      = 1 + colWidth; // panel-local X of │ separator
        var frameAttr = _isActive ? McTheme.PanelHeader : McTheme.PanelFrame;
        var entries   = _listing.Entries;

        for (int row = 0; row < contentRows; row++)
        {
            int screenY = row + 2;
            DrawBriefCell(entries, _scrollOffset + row,               1,       screenY, colWidth);
            Move(sepX, screenY); Driver.SetAttribute(frameAttr); Driver.AddStr("│");
            DrawBriefCell(entries, _scrollOffset + contentRows + row, sepX + 1, screenY, col2Width);
        }
    }

    private void DrawBriefCell(
        IReadOnlyList<FileEntry> entries, int idx, int screenX, int screenY, int width)
    {
        Move(screenX, screenY);
        if (idx >= entries.Count)
        {
            Driver.SetAttribute(McTheme.PanelFile);
            Driver.AddStr(new string(' ', width));
            return;
        }
        var entry = entries[idx];
        Driver.SetAttribute(GetEntryAttr(entry, idx));
        Driver.AddStr(FormatBriefCell(entry, width));
    }

    private Terminal.Gui.Attribute GetEntryAttr(FileEntry entry, int entryIdx)
    {
        bool activeCursor   = _isActive  && entryIdx == _cursorIndex;
        bool inactiveCursor = !_isActive && entryIdx == _cursorIndex;

        if      (activeCursor && entry.IsMarked)            return McTheme.PanelMarkedCursor;
        else if (activeCursor)                              return McTheme.PanelCursor;
        else if (inactiveCursor)                            return McTheme.PanelInactiveCursor; // (#8, #19)
        else if (entry.IsMarked)                            return McTheme.PanelMarked;
        else if (entry.IsDirectory || entry.IsParentDir)    return McTheme.PanelDirectory;
        else if (entry.IsSymlink)                           return McTheme.PanelSymlink;
        else if (entry.IsExecutable)                        return McTheme.PanelExecutable;
        else                                                return McTheme.PanelFile;
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
        int nameWidth = Math.Max(12, innerWidth - sizeWidth - dateWidth - 2);
        return (nameWidth, sizeWidth, dateWidth);
    }

    /// <summary>
    /// ls -l style date: recent files show HH:MM; files older than 6 months show YYYY. (#10, #12)
    /// </summary>
    private static string FormatDate(DateTime dt, int width)
    {
        if (dt == DateTime.MinValue) return string.Empty.PadRight(width);
        var fmt = dt > DateTime.Now.AddMonths(-6) ? "MMM dd HH:mm" : "MMM dd  yyyy";
        return dt.ToString(fmt).PadRight(width);
    }

    private static string FormatEntry(FileEntry entry, int innerWidth)
    {
        var (nameWidth, sizeWidth, dateWidth) = ColumnWidths(innerWidth);

        var marker = entry.IsMarked ? "*" : " ";

        // No prefix chars for directories/symlinks — colour alone distinguishes them (#1, #2)
        string name = entry.IsParentDir ? ".." : entry.Name;
        if (name.Length > nameWidth) name = name[..(nameWidth - 1)] + "~";
        name = name.PadRight(nameWidth);

        // Parent dir → <UP-DIR>; regular dirs → <DIR> (#7)
        var size = (entry.IsParentDir ? "<UP-DIR>"
                    : FileSizeFormatter.FormatPanelSize(entry.Size, entry.IsDirectory))
                   .PadLeft(sizeWidth);

        var date = FormatDate(entry.ModificationTime, dateWidth); // age-aware (#10)

        return $"{marker}{name}{size} {date}";
    }

    private static string FormatBriefCell(FileEntry entry, int width)
    {
        var marker = entry.IsMarked ? "*" : " ";
        string name = entry.IsParentDir ? ".." : entry.Name; // no prefix chars (#1, #2)
        int nameWidth = width - 1;
        if (name.Length > nameWidth) name = name[..(nameWidth - 1)] + "~";
        return marker + name.PadRight(nameWidth);
    }

    /// <summary>ls -l style: [*]perms owner group size date name</summary>
    private static string FormatLongEntry(FileEntry entry, int innerWidth)
    {
        var marker = entry.IsMarked ? "*" : " ";
        var perms  = PermissionsFormatter.Format(entry.Permissions, entry.IsDirectory, entry.IsSymlink);
        var owner  = (entry.OwnerName ?? entry.DirEntry.OwnerUid.ToString()).PadRight(8)[..8];
        var group  = (entry.GroupName ?? entry.DirEntry.OwnerGid.ToString()).PadRight(8)[..8];
        // Parent dir → <UP-DIR> (#13)
        var size   = (entry.IsParentDir ? "<UP-DIR>"
                      : FileSizeFormatter.FormatPanelSize(entry.Size, entry.IsDirectory)).PadLeft(8);
        var date   = FormatDate(entry.ModificationTime, 12); // age-aware (#10)

        var prefix = $"{marker}{perms} {owner} {group} {size} {date} ";

        // No prefix chars for dirs/symlinks (#17)
        string name = entry.IsParentDir ? ".." : entry.Name;
        int nameWidth = Math.Max(1, innerWidth - prefix.Length);
        if (name.Length > nameWidth) name = name[..(nameWidth - 1)] + "~";

        return (prefix + name).PadRight(innerWidth);
    }

    // --- Quick search ---

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
            var rune = keyEvent.AsRune;
            if (rune.Value >= 32 && !keyEvent.IsCtrl && !keyEvent.IsAlt)
            {
                _quickSearch += (char)rune.Value;
                SearchInPanel();
                return true;
            }
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
