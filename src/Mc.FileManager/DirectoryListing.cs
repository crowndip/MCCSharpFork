using Mc.Core.Models;
using Mc.Core.Vfs;

namespace Mc.FileManager;

/// <summary>
/// Reads, sorts and filters a directory listing.
/// Equivalent to dir.c in the original C codebase.
/// </summary>
public sealed class DirectoryListing
{
    private readonly VfsRegistry _vfs;
    private List<FileEntry> _entries = [];
    private VfsPath _currentPath = VfsPath.FromLocal(Environment.CurrentDirectory);

    public VfsPath CurrentPath => _currentPath;
    public IReadOnlyList<FileEntry> Entries => _entries;
    public SortOptions Sort { get; } = new();
    public FilterOptions Filter { get; } = new();

    // Aggregates
    public int TotalFiles { get; private set; }
    public int TotalDirectories { get; private set; }
    public long TotalMarkedSize { get; private set; }
    public int MarkedCount { get; private set; }

    public event EventHandler? Changed;

    public DirectoryListing(VfsRegistry vfs)
    {
        _vfs = vfs;
    }

    public void Load(VfsPath path)
    {
        _currentPath = path;
        Reload();
    }

    public void Reload()
    {
        try
        {
            var raw = _vfs.ListDirectory(_currentPath);
            _entries = raw
                .Where(e => Filter.Matches(e.Name))
                .Select(e => new FileEntry { DirEntry = e })
                .ToList();

            SortEntries();
            UpdateCounts();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _entries = [new FileEntry
            {
                DirEntry = new VfsDirEntry
                {
                    Name = $"[Error: {ex.Message}]",
                    FullPath = _currentPath,
                    IsDirectory = false,
                    ModificationTime = DateTime.Now,
                }
            }];
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ChangeSortField(SortField field)
    {
        if (Sort.Field == field)
            Sort.Descending = !Sort.Descending;
        else
        {
            Sort.Field = field;
            Sort.Descending = false;
        }
        SortEntries();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkFile(int index)
    {
        if (index < 0 || index >= _entries.Count) return;
        var e = _entries[index];
        if (e.IsParentDir) return;
        e.IsMarked = !e.IsMarked;
        UpdateCounts();
    }

    public void MarkAll(bool marked)
    {
        foreach (var e in _entries.Where(x => !x.IsParentDir))
            e.IsMarked = marked;
        UpdateCounts();
    }

    public void MarkByPattern(string pattern, bool caseSensitive = false, bool mark = true)
    {
        var filter = new FilterOptions { Pattern = pattern, CaseSensitive = caseSensitive };
        foreach (var e in _entries.Where(x => !x.IsParentDir))
            if (filter.Matches(e.Name)) e.IsMarked = mark;
        UpdateCounts();
    }

    public void InvertMarking()
    {
        foreach (var e in _entries.Where(x => !x.IsParentDir))
            e.IsMarked = !e.IsMarked;
        UpdateCounts();
    }

    public IReadOnlyList<FileEntry> GetMarkedEntries()
        => _entries.Where(e => e.IsMarked).ToList();

    /// <summary>
    /// Recomputes the marked-file aggregates and fires Changed after external
    /// bulk-marking (e.g. compare-directories). Equivalent to recalculate_panel_summary().
    /// </summary>
    public void RefreshMarking()
    {
        UpdateCounts();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SortEntries()
    {
        // Always keep ".." at top
        var parent = _entries.FirstOrDefault(e => e.IsParentDir);
        var rest = _entries.Where(e => !e.IsParentDir).ToList();

        rest.Sort((a, b) =>
        {
            if (Sort.DirectoriesFirst)
            {
                if (a.IsDirectory && !b.IsDirectory) return -1;
                if (!a.IsDirectory && b.IsDirectory) return 1;
            }

            int cmp = Sort.Field switch
            {
                SortField.Name => CompareNames(a.Name, b.Name),
                SortField.Extension => string.Compare(a.Extension, b.Extension,
                    Sort.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase),
                SortField.Size => a.Size.CompareTo(b.Size),
                SortField.ModificationTime => a.ModificationTime.CompareTo(b.ModificationTime),
                _ => CompareNames(a.Name, b.Name),
            };

            return Sort.Descending ? -cmp : cmp;
        });

        _entries = parent != null ? [parent, .. rest] : rest;
    }

    private int CompareNames(string a, string b)
    {
        if (Sort.VersionSort)
            return FileVersionCompare(a, b);
        return string.Compare(a, b,
            Sort.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    // Natural version sort: file2 < file10
    private static int FileVersionCompare(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int ai = i, bi = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                var na = long.Parse(a[ai..i]);
                var nb = long.Parse(b[bi..j]);
                if (na != nb) return na.CompareTo(nb);
            }
            else
            {
                if (a[i] != b[j]) return a[i].CompareTo(b[j]);
                i++; j++;
            }
        }
        return a.Length.CompareTo(b.Length);
    }

    private void UpdateCounts()
    {
        TotalFiles = _entries.Count(e => !e.IsDirectory && !e.IsParentDir);
        TotalDirectories = _entries.Count(e => e.IsDirectory && !e.IsParentDir);
        TotalMarkedSize = _entries.Where(e => e.IsMarked && !e.IsDirectory).Sum(e => e.Size);
        MarkedCount = _entries.Count(e => e.IsMarked);
    }
}
