using System.Collections.ObjectModel;
using Mc.Core.Models;
using Mc.Core.Utilities;
using Mc.FileManager;
using Terminal.Gui;

namespace Mc.Ui.Widgets;

/// <summary>
/// One file panel — shows path header, file list, and status footer.
/// Equivalent to WPanel in the original C codebase (lib/widget + src/filemanager/panel.c).
/// </summary>
public sealed class FilePanelView : View
{
    private readonly DirectoryListing _listing;
    private readonly ListView _listView;
    private readonly Label _pathLabel;
    private readonly Label _statusLabel;
    private int _cursorIndex;
    private bool _isActive;

    public event EventHandler<FileEntry?>? EntryActivated;
    public event EventHandler<int>? CursorChanged;
    public event EventHandler? BecameActive;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            ColorScheme = value ? McTheme.PanelSelected : McTheme.Panel;
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
        ColorScheme = McTheme.Panel;

        // Path header
        _pathLabel = new Label
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            ColorScheme = McTheme.StatusBar,
            TextAlignment = Alignment.Center,
        };

        // File list
        _listView = new ListView
        {
            X = 0, Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ColorScheme = McTheme.Panel,
            AllowsMarking = false,
        };

        // Status footer
        _statusLabel = new Label
        {
            X = 0, Y = Pos.Bottom(_listView),
            Width = Dim.Fill(),
            Height = 1,
            ColorScheme = McTheme.StatusBar,
        };

        Add(_pathLabel, _listView, _statusLabel);

        _listView.SelectedItemChanged += OnSelectedItemChanged;
        _listView.OpenSelectedItem += OnItemActivated;
        _listing.Changed += OnListingChanged;
        MouseClick += (_, _) => { if (!_isActive) BecameActive?.Invoke(this, EventArgs.Empty); };

        RefreshDisplay();
    }

    private void OnSelectedItemChanged(object? sender, ListViewItemEventArgs e)
    {
        _cursorIndex = e.Item;
        UpdateStatus();
        CursorChanged?.Invoke(this, _cursorIndex);
    }

    private void OnItemActivated(object? sender, ListViewItemEventArgs e)
    {
        EntryActivated?.Invoke(this, CurrentEntry);
    }

    private void OnListingChanged(object? sender, EventArgs e)
    {
        Application.Invoke(RefreshDisplay);
    }

    private void RefreshDisplay()
    {
        var path = PathUtils.TildePath(_listing.CurrentPath.ToString());
        _pathLabel.Text = $" {path} ";

        var items = new List<string>(_listing.Entries.Count);
        foreach (var entry in _listing.Entries)
            items.Add(FormatEntry(entry));

        _listView.SetSource(new ObservableCollection<string>(items));

        if (_cursorIndex >= items.Count)
            _cursorIndex = Math.Max(0, items.Count - 1);
        if (items.Count > 0)
            _listView.SelectedItem = _cursorIndex;

        UpdateStatus();
        SetNeedsDraw();
    }

    private string FormatEntry(FileEntry entry)
    {
        // Get available width (approximate)
        var available = Viewport.Width > 10 ? Viewport.Width - 2 : 40;
        int sizeWidth = 8;
        int dateWidth = 12;
        int nameWidth = Math.Max(12, available - sizeWidth - dateWidth - 3);

        var marker = entry.IsMarked ? "*" : " ";

        string name;
        if (entry.IsParentDir)
            name = "..";
        else if (entry.IsDirectory)
            name = "/" + entry.Name;
        else if (entry.IsSymlink)
            name = "~" + entry.Name;
        else
            name = entry.Name;

        if (name.Length > nameWidth) name = name[..(nameWidth - 1)] + "~";
        name = name.PadRight(nameWidth);

        var size = FileSizeFormatter.FormatPanelSize(entry.Size, entry.IsDirectory).PadLeft(sizeWidth);
        var date = entry.ModificationTime == DateTime.MinValue
            ? string.Empty.PadRight(dateWidth)
            : entry.ModificationTime.ToString("MMM dd HH:mm").PadRight(dateWidth);

        return $"{marker}{name}{size} {date}";
    }

    private void UpdateStatus()
    {
        var marked = _listing.MarkedCount;
        if (marked > 0)
        {
            _statusLabel.Text = $" {marked} files, {FileSizeFormatter.Format(_listing.TotalMarkedSize)} marked";
        }
        else
        {
            var entry = CurrentEntry;
            if (entry != null && !entry.IsParentDir)
                _statusLabel.Text = $" {entry.Name}  {FileSizeFormatter.Format(entry.Size)}  {entry.ModificationTime:yyyy-MM-dd HH:mm}";
            else
                _statusLabel.Text = $" {_listing.TotalFiles} files, {_listing.TotalDirectories} dirs";
        }
    }

    // --- Input handling ---

    protected override bool OnKeyDown(Key keyEvent)
    {
        switch (keyEvent.KeyCode)
        {
            case KeyCode.Insert:
            case KeyCode.Space:
                ToggleMark();
                return true;

            case KeyCode.Enter:
                EntryActivated?.Invoke(this, CurrentEntry);
                return true;

            case KeyCode.Backspace:
                // Navigate to parent
                EntryActivated?.Invoke(this, _listing.Entries.FirstOrDefault(e => e.IsParentDir));
                return true;

            default:
                if (!_isActive) { BecameActive?.Invoke(this, EventArgs.Empty); }
                return base.OnKeyDown(keyEvent);
        }
    }

    // --- Public API ---

    public void MoveCursorTo(int index)
    {
        if (index < 0 || index >= _listing.Entries.Count) return;
        _cursorIndex = index;
        _listView.SelectedItem = index;
        SetNeedsDraw();
    }

    public void ToggleMark()
    {
        _listing.MarkFile(_cursorIndex);
        // Move cursor down if MarkMovesCursor
        if (_cursorIndex < _listing.Entries.Count - 1)
        {
            _cursorIndex++;
            _listView.SelectedItem = _cursorIndex;
        }
        RefreshDisplay();
    }

    public void Refresh() => _listing.Reload();
}
