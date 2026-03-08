using System.Collections.Specialized;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Interactive ncdu-style folder size analyzer.
/// Ported from MCCompanion's FolderSizeCommand / FolderSizeDialog.
/// Adapted from Terminal.Gui v1 to v2.
///
/// Navigation:
///   Enter / Space  = expand / collapse a directory
///   → / ←          = expand / collapse, or jump to parent
///   Insert         = mark / unmark item
///   Delete         = delete marked items (or item under cursor if nothing marked)
///   Sort buttons   = re-order by Name / Size / Date
/// </summary>
public static class FolderAnalyzerDialog
{
    public static void Show(string[] paths)
    {
        var dlg = new FolderAnalyzerView(paths);
        Application.Run(dlg);
        dlg.Dispose();
    }
}

// ── Sort mode ──────────────────────────────────────────────────────────────────

internal enum FolderSortMode { Size, Name, Date }

// ── Tree node ──────────────────────────────────────────────────────────────────

internal sealed class FolderNode
{
    public const long SizePending     = -1;
    public const long SizeCalculating = -2;

    public FolderNode(string path, int depth)
    {
        Path  = path;
        Name  = System.IO.Path.GetFileName(path) is { Length: > 0 } n ? n : path;
        Depth = depth;

        bool isDir = Directory.Exists(path);
        IsFile = !isDir;
        try
        {
            LastWriteTime = isDir
                ? Directory.GetLastWriteTime(path)
                : File.GetLastWriteTime(path);
        }
        catch { LastWriteTime = DateTime.MinValue; }
    }

    public string   Path           { get; }
    public string   Name           { get; }
    public bool     IsFile         { get; }
    public int      Depth          { get; }
    public DateTime LastWriteTime  { get; set; }
    public long     SizeBytes      { get; set; } = SizePending;
    public int      FileCount      { get; set; } = -1;
    public bool     IsExpanded     { get; set; }
    public bool     ChildrenLoaded { get; set; }
    public List<FolderNode> Children { get; } = [];

    public bool IsReady       => SizeBytes >= 0;
    public bool IsCalculating => SizeBytes == SizeCalculating;
}

// ── Dialog ────────────────────────────────────────────────────────────────────

// Window is used instead of Dialog so that TG v2's Dialog auto-close-on-Accepting
// behavior doesn't fire when sort buttons, Reload, Delete, or the ListView are activated.
// Application.RequestStop(this) is called explicitly only from the Close button.
internal sealed class FolderAnalyzerView : Window
{
    // Session-level cache — survives re-open within same process run.
    private static readonly Dictionary<string, (long Bytes, int Files)> _cache = new();

    private readonly List<FolderNode>    _roots;
    private readonly List<FolderNode>    _flat;
    private readonly HashSet<FolderNode> _marked = new();
    private          FolderSortMode      _sortMode    = FolderSortMode.Size;
    private          ListView            _lv          = null!;
    private          Label               _lblStatus   = null!;
    private          Button              _btnSortSize = null!;
    private          Button              _btnSortName = null!;
    private          Button              _btnSortDate = null!;
    private          int                 _pending;

    // ── Column layout constants ───────────────────────────────────────────────
    // Row format:  " {size,10}  {files,6}  {indent}{icon}{name}"
    private const int SizeW   = 10;
    private const int FilesW  =  6;
    private const int PrefixW = 1 + SizeW + 2 + FilesW + 2; // = 21

    // Files at or above this size are virtual/special (e.g. /proc/kcore).
    private const long MaxSaneFileSize = 1L << 40; // 1 TiB

    // ── Constructor ───────────────────────────────────────────────────────────

    public FolderAnalyzerView(string[] paths)
    {
        Title       = "Folder Size Analysis";
        Width       = Dim.Fill();
        Height      = Dim.Fill();
        ColorScheme = McTheme.Panel;

        _roots = paths.Select(p => new FolderNode(p, 0)).ToList();
        _flat  = [.. _roots];

        Build();
        ExpandRootsAndStartCalculation();
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private void Build()
    {
        _lblStatus = new Label { X = 1, Y = 1, Width = Dim.Fill(1), ColorScheme = McTheme.Panel };
        Add(_lblStatus);

        Add(new Label { X = 1, Y = 2, Text = "Sort:", ColorScheme = McTheme.Panel });
        _btnSortSize = new Button { X = 8,                           Y = 2, Text = "Size ▼" };
        _btnSortName = new Button { X = Pos.Right(_btnSortSize) + 1, Y = 2, Text = "Name" };
        _btnSortDate = new Button { X = Pos.Right(_btnSortName) + 1, Y = 2, Text = "Date" };
        _btnSortSize.Accepting += (_, e) => { SetSort(FolderSortMode.Size); e.Cancel = true; };
        _btnSortName.Accepting += (_, e) => { SetSort(FolderSortMode.Name); e.Cancel = true; };
        _btnSortDate.Accepting += (_, e) => { SetSort(FolderSortMode.Date); e.Cancel = true; };
        Add(_btnSortSize, _btnSortName, _btnSortDate);

        Add(new Label { X = 1, Y = 3, Text = $" {"SIZE",SizeW}  {"FILES",FilesW}  NAME", ColorScheme = McTheme.Panel });

        _lv = new ListView
        {
            X             = 1,
            Y             = 4,
            Width         = Dim.Fill(1),
            Height        = Dim.Fill(3),
            AllowsMarking = false,
            ColorScheme   = McTheme.Panel,
        };
        _lv.Source              = new FolderSource(_flat, _marked);
        _lv.SelectedItemChanged += (_, _) => UpdateStatus();
        _lv.OpenSelectedItem    += (_, _) => ToggleExpand();
        _lv.KeyDown             += OnListKey;
        Add(_lv);

        // Bottom buttons — placed with AnchorEnd since we're in a Window, not a Dialog.
        var btnClose  = new Button { Y = Pos.AnchorEnd(1), X = Pos.AnchorEnd(11), Text = "_Close" };
        var btnDelete = new Button { Y = Pos.AnchorEnd(1), X = Pos.AnchorEnd(24), Text = "_Delete" };
        var btnReload = new Button { Y = Pos.AnchorEnd(1), X = Pos.AnchorEnd(37), Text = "_Reload" };
        btnClose.Accepting  += (_, _) => Application.RequestStop(this);
        btnDelete.Accepting += (_, e) => { OnDelete();  e.Cancel = true; };
        btnReload.Accepting += (_, e) => { OnReload();  e.Cancel = true; };
        Add(btnClose, btnDelete, btnReload);
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    private void OnListKey(object? sender, Key e)
    {
        switch (e.KeyCode)
        {
            case KeyCode.Enter:
            case KeyCode.Space:
                ToggleExpand();
                e.Handled = true;  // prevent Enter from activating the default (Close) button
                break;
            case KeyCode.Insert:
                ToggleMark();
                e.Handled = true;
                break;
            case KeyCode.CursorRight:
                ExpandSelected();
                e.Handled = true;
                break;
            case KeyCode.CursorLeft:
                CollapseSelected();
                e.Handled = true;
                break;
            case KeyCode.Delete:
                OnDelete();
                e.Handled = true;
                break;
        }
    }

    // ── Marking ───────────────────────────────────────────────────────────────

    private void ToggleMark()
    {
        var node = SelectedNode();
        if (node == null) return;
        if (!_marked.Remove(node)) _marked.Add(node);
        if (_lv.SelectedItem < _flat.Count - 1)
            _lv.SelectedItem++;
        RefreshList();
    }

    // ── Tree navigation ───────────────────────────────────────────────────────

    private FolderNode? SelectedNode()
    {
        int i = _lv.SelectedItem;
        return i >= 0 && i < _flat.Count ? _flat[i] : null;
    }

    private void ToggleExpand()
    {
        var n = SelectedNode();
        if (n == null || n.IsFile) return;
        if (n.IsExpanded) CollapseNode(n);
        else              ExpandNode(n);
    }

    private void ExpandSelected()
    {
        var n = SelectedNode();
        if (n is { IsFile: false, IsExpanded: false }) ExpandNode(n);
    }

    private void CollapseSelected()
    {
        var n = SelectedNode();
        if (n == null) return;
        if (!n.IsFile && n.IsExpanded)
            CollapseNode(n);
        else
        {
            int idx = _lv.SelectedItem;
            for (int i = idx - 1; i >= 0; i--)
                if (_flat[i].Depth < n.Depth) { _lv.SelectedItem = i; break; }
        }
    }

    private void ExpandNode(FolderNode node)
    {
        int idx = _flat.IndexOf(node);
        if (idx < 0) return;
        node.IsExpanded = true;
        if (!node.ChildrenLoaded) LoadImmediateChildren(node);
        var children = OrderedChildren(node).ToList();
        _flat.InsertRange(idx + 1, children);
        RefreshList();
        foreach (var child in children.Where(c => !c.IsFile && !c.IsReady))
            _ = BeginCalculateAsync(child);
    }

    private void CollapseNode(FolderNode node)
    {
        int idx = _flat.IndexOf(node);
        if (idx < 0) return;
        node.IsExpanded = false;
        int end = idx + 1;
        while (end < _flat.Count && _flat[end].Depth > node.Depth) end++;
        _flat.RemoveRange(idx + 1, end - idx - 1);
        if (_lv.SelectedItem > idx) _lv.SelectedItem = idx;
        RefreshList();
    }

    // Returns children ordered by current sort mode.
    // Directories always come before files (MC panel convention).
    private IEnumerable<FolderNode> OrderedChildren(FolderNode parent) => _sortMode switch
    {
        FolderSortMode.Name =>
            parent.Children.Where(c => !c.IsFile).OrderBy(c => c.Name)
            .Concat(parent.Children.Where(c => c.IsFile).OrderBy(c => c.Name)),

        FolderSortMode.Date =>
            parent.Children.Where(c => !c.IsFile).OrderByDescending(c => c.LastWriteTime)
            .Concat(parent.Children.Where(c => c.IsFile).OrderByDescending(c => c.LastWriteTime)),

        _ => // Size (default): largest first; pending/calculating (negative) go last
            parent.Children.Where(c => !c.IsFile)
                .OrderByDescending(c => c.SizeBytes).ThenBy(c => c.Name)
            .Concat(parent.Children.Where(c => c.IsFile)
                .OrderByDescending(c => c.SizeBytes)),
    };

    // ── Child enumeration ─────────────────────────────────────────────────────

    private void LoadImmediateChildren(FolderNode node)
    {
        node.ChildrenLoaded = true;
        try
        {
            var di = new DirectoryInfo(node.Path);
            foreach (var d in di.EnumerateDirectories().OrderBy(x => x.Name))
            {
                var child = new FolderNode(d.FullName, node.Depth + 1) { LastWriteTime = d.LastWriteTime };
                if (_cache.TryGetValue(d.FullName, out var c))
                    (child.SizeBytes, child.FileCount) = c;
                node.Children.Add(child);
            }
            foreach (var f in di.EnumerateFiles().OrderByDescending(x => x.Length))
            {
                var child = new FolderNode(f.FullName, node.Depth + 1)
                    { SizeBytes = f.Length, FileCount = 1, LastWriteTime = f.LastWriteTime };
                _cache[f.FullName] = (f.Length, 1);
                node.Children.Add(child);
            }
        }
        catch { /* access denied — leave children empty */ }
    }

    // ── Background size calculation ───────────────────────────────────────────

    private async Task BeginCalculateAsync(FolderNode node)
    {
        if (node.IsFile || node.IsReady) return;

        if (_cache.TryGetValue(node.Path, out var cached))
        {
            node.SizeBytes = cached.Bytes;
            node.FileCount = cached.Files;
            Application.Invoke(RefreshList);
            return;
        }

        _pending++;
        node.SizeBytes = FolderNode.SizeCalculating;

        long bytes = 0; int files = 0;
        try
        {
            (bytes, files) = await Task.Run(() =>
            {
                long b = 0; int f = 0;
                try
                {
                    foreach (var fi in new DirectoryInfo(node.Path)
                        .EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        try { long l = fi.Length; if (l > 0 && l < MaxSaneFileSize) { b += l; f++; } } catch { }
                    }
                }
                catch { }
                return (b, f);
            }).ConfigureAwait(false);
        }
        catch { }

        Application.Invoke(() =>
        {
            node.SizeBytes = bytes;
            node.FileCount = files;
            _cache[node.Path] = (bytes, files);
            _pending--;
            RefreshList();
        });
    }

    private void ExpandRootsAndStartCalculation()
    {
        foreach (var root in _roots)
        {
            if (!root.IsFile)
            {
                LoadImmediateChildren(root);
                root.IsExpanded = true;
                _flat.InsertRange(_flat.IndexOf(root) + 1, OrderedChildren(root).ToList());
            }
        }
        RefreshList();
        foreach (var node in _flat.ToList())
            if (!node.IsFile && !node.IsReady)
                _ = BeginCalculateAsync(node);
    }

    // ── Sort ──────────────────────────────────────────────────────────────────

    private void SetSort(FolderSortMode mode)
    {
        _sortMode = mode;
        UpdateSortButtons();
        ApplySort();
    }

    private void UpdateSortButtons()
    {
        _btnSortSize.Text = _sortMode == FolderSortMode.Size ? "Size ▼" : "Size";
        _btnSortName.Text = _sortMode == FolderSortMode.Name ? "Name ▲" : "Name";
        _btnSortDate.Text = _sortMode == FolderSortMode.Date ? "Date ▼" : "Date";
    }

    // Rebuild _flat from roots, re-expanding all previously expanded nodes
    // in the new sort order. Expansion state and marks are preserved.
    private void ApplySort()
    {
        _flat.Clear();
        _flat.AddRange(_roots);
        // Forward pass: expanded nodes get their children inserted after them.
        for (int i = 0; i < _flat.Count; i++)
        {
            var node = _flat[i];
            if (node.IsFile || !node.IsExpanded) continue;
            _flat.InsertRange(i + 1, OrderedChildren(node).ToList());
        }
        if (_lv.SelectedItem >= _flat.Count)
            _lv.SelectedItem = Math.Max(0, _flat.Count - 1);
        RefreshList();
    }

    // ── Reload ────────────────────────────────────────────────────────────────

    private void OnReload()
    {
        _cache.Clear();
        _marked.Clear();
        foreach (var root in _roots) ResetTree(root);
        _flat.Clear();
        _flat.AddRange(_roots);
        ExpandRootsAndStartCalculation();
    }

    private static void ResetTree(FolderNode node)
    {
        node.SizeBytes      = FolderNode.SizePending;
        node.FileCount      = -1;
        node.IsExpanded     = false;
        node.ChildrenLoaded = false;
        foreach (var c in node.Children) ResetTree(c);
        node.Children.Clear();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    private void OnDelete()
    {
        List<FolderNode> targets = _marked.Count > 0
            ? _flat.Where(n => _marked.Contains(n)).ToList()
            : SelectedNode() is { } sel ? [sel] : [];
        if (targets.Count == 0) return;

        // Skip items that are descendants of another target in the list.
        var rootTargets = targets.Count == 1 ? targets : targets
            .Where(n => !targets.Any(other =>
                other != n && n.Path.StartsWith(
                    other.Path + System.IO.Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)))
            .ToList();

        string title   = rootTargets.Count == 1
            ? $"Delete {(rootTargets[0].IsFile ? "file" : "directory")}?"
            : $"Delete {rootTargets.Count} items?";
        string message = BuildDeleteConfirmMessage(rootTargets);

        // "Cancel" is button 0 (default); "Delete permanently" is button 1.
        if (MessageBox.Query(title, message, "Cancel", "Delete permanently") != 1)
            return;

        var errors = new List<string>();
        foreach (var node in rootTargets)
        {
            try
            {
                if (node.IsFile) File.Delete(node.Path);
                else             Directory.Delete(node.Path, recursive: true);
            }
            catch (Exception ex) { errors.Add($"{node.Name}: {ex.Message}"); continue; }
            InvalidateAncestorSizes(node);
            _cache.Remove(node.Path);
            _marked.Remove(node);
            RemoveFromFlat(node);
            foreach (var root in _roots) RemoveChild(root, node);
        }

        if (errors.Count > 0)
            MessageBox.ErrorQuery("Deletion errors", string.Join("\n", errors), "OK");

        if (_lv.SelectedItem >= _flat.Count)
            _lv.SelectedItem = Math.Max(0, _flat.Count - 1);
        RefreshList();
    }

    private static string BuildDeleteConfirmMessage(List<FolderNode> targets)
    {
        if (targets.Count == 1)
        {
            var n = targets[0];
            string details = n.IsFile ? $"File:  {n.Path}" : $"Directory:  {n.Path}";
            if (!n.IsFile && n.FileCount > 0) details += $"\n\nContains ~{n.FileCount:N0} file(s)";
            if (!n.IsFile && n.SizeBytes > 0) details += $"  ({FormatSize(n.SizeBytes).Trim()})";
            return $"{details}\n\nThis CANNOT be undone.";
        }
        long total     = targets.Where(n => n.IsReady && n.SizeBytes > 0).Sum(n => n.SizeBytes);
        int  fileCount = targets.Where(n => n.FileCount > 0).Sum(n => n.FileCount);
        string summary = $"{targets.Count} selected items";
        if (total     > 0) summary += $"  ~{FormatSize(total).Trim()}";
        if (fileCount > 0) summary += $"  ({fileCount:N0} file(s))";
        return $"{summary}\n\nThis CANNOT be undone.";
    }

    private void InvalidateAncestorSizes(FolderNode deleted)
    {
        foreach (var ancestor in _flat)
        {
            if (ancestor.Depth < deleted.Depth &&
                deleted.Path.StartsWith(ancestor.Path + System.IO.Path.DirectorySeparatorChar,
                                        StringComparison.OrdinalIgnoreCase))
            {
                if (ancestor.IsReady && deleted.IsReady)
                    ancestor.SizeBytes = Math.Max(0, ancestor.SizeBytes - deleted.SizeBytes);
                if (ancestor.FileCount >= 0 && deleted.FileCount >= 0)
                    ancestor.FileCount = Math.Max(0, ancestor.FileCount - deleted.FileCount);
                _cache.Remove(ancestor.Path);
            }
        }
    }

    private void RemoveFromFlat(FolderNode node)
    {
        int idx = _flat.IndexOf(node);
        if (idx < 0) return;
        int end = idx + 1;
        while (end < _flat.Count && _flat[end].Depth > node.Depth) end++;
        _flat.RemoveRange(idx, end - idx);
    }

    private static void RemoveChild(FolderNode parent, FolderNode target)
    {
        if (parent.Children.Remove(target)) return;
        foreach (var c in parent.Children) RemoveChild(c, target);
    }

    // ── UI refresh ────────────────────────────────────────────────────────────

    private void RefreshList()
    {
        _lv.Source = new FolderSource(_flat, _marked);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var node  = SelectedNode();
        var parts = new List<string>();

        if (_marked.Count > 0)
        {
            long markedSize = _marked.Where(n => n.IsReady && n.SizeBytes > 0).Sum(n => n.SizeBytes);
            string sizeInfo = markedSize > 0 ? $"  {FormatSize(markedSize).Trim()}" : "";
            parts.Add($"[{_marked.Count} marked{sizeInfo}]");
        }
        if (_pending > 0)
            parts.Add($"[Calculating {_pending}…]");
        if (_sortMode == FolderSortMode.Date && node != null && node.LastWriteTime != DateTime.MinValue)
            parts.Add(node.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
        parts.Add(node?.Path ?? "");
        _lblStatus.Text = string.Join("  ", parts);
    }

    // ── Formatting helpers ────────────────────────────────────────────────────

    internal static string FormatSize(long bytes)
    {
        if (bytes == 0) return "0 B".PadLeft(SizeW);
        string[] u = ["B", "KB", "MB", "GB", "TB"];
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        string s = i == 0 ? $"{(int)v} B" : $"{v:F2} {u[i]}";
        return s.PadLeft(SizeW);
    }

    private static string FormatFiles(int count)
    {
        if (count < 0)          return new string(' ', FilesW);
        if (count >= 1_000_000) return $"{count / 1_000_000}M".PadLeft(FilesW);
        if (count >= 10_000)    return $"{count / 1_000}K".PadLeft(FilesW);
        return $"{count,6:N0}";
    }

    // ── Custom colored list source ────────────────────────────────────────────
    // Nested so it can share the private column constants and format helpers
    // of FolderAnalyzerView without any public surface area.

    private sealed class FolderSource : IListDataSource
    {
        private readonly List<FolderNode>    _flat;
        private readonly HashSet<FolderNode> _marked;

        private const long BytesPerGb = 1L   * 1024 * 1024 * 1024;
        private const long Bytes100Mb = 100L * 1024 * 1024;
        private const long Bytes10Mb  = 10L  * 1024 * 1024;

        public FolderSource(List<FolderNode> flat, HashSet<FolderNode> marked)
        {
            _flat   = flat;
            _marked = marked;
        }

        public int  Count                         => _flat.Count;
        public int  Length                        => 0;
        public bool SuspendCollectionChangedEvent { get; set; }

#pragma warning disable CS0067
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
#pragma warning restore CS0067

        public bool IsMarked(int item)            => false;
        public void SetMark(int item, bool value) { }
        public System.Collections.IList ToList()  => _flat;
        public void Dispose() { }

        // v2 signature: no ConsoleDriver param; use Application.Driver instead.
        public void Render(ListView container, bool marked, int item, int col, int line, int width, int start)
        {
            if (item < 0 || item >= _flat.Count) return;

            var  node     = _flat[item];
            var  norm     = container.ColorScheme?.Normal ?? Application.Driver!.GetAttribute();
            bool isCursor = item == container.SelectedItem;
            bool isMarked = _marked.Contains(node);

            // Size column (right-aligned, 10 chars)
            string sizeStr = node.IsCalculating ? "       … "
                           : !node.IsReady      ? new string(' ', SizeW)
                           :                      FormatSize(node.SizeBytes);

            // Files column (right-aligned, 6 chars)
            string fileStr = FormatFiles(node.FileCount);

            // Expand/collapse icon + depth indent
            string indent = new string(' ', node.Depth * 2);
            string icon   = node.IsFile     ? "  "
                          : node.IsExpanded ? "▼ "
                          :                   "▶ ";

            // Name truncated to available space
            int nameW = Math.Max(1, width - PrefixW - indent.Length - icon.Length);
            string name = node.Name.Length <= nameW ? node.Name : node.Name[..nameW];

            // Full row string
            string row = $" {sizeStr}  {fileStr}  {indent}{icon}{name}";
            if (row.Length > width) row = row[..width];
            if (row.Length < width) row = row.PadRight(width);

            // ── Colour priority (highest first) ──────────────────────────────
            // Cursor+marked  → inverted marked  (black on bright yellow)
            // Cursor         → MC cursor        (black on cyan)
            // Marked         → MC tagged        (bright yellow on black)
            // Calculating    → activity         (bright cyan on blue)
            // ≥ 1 GB         → red warning      (bright red on blue)
            // ≥ 100 MB       → caution          (bright yellow on blue)
            // Small file     → de-emphasised    (gray on blue)
            // Normal         → panel default
            var attr = isCursor && isMarked
                ? new Terminal.Gui.Attribute(Color.Black,        Color.BrightYellow)
                : isCursor
                ? new Terminal.Gui.Attribute(Color.Black,        Color.Cyan)
                : isMarked
                ? new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black)
                : node.IsCalculating
                ? new Terminal.Gui.Attribute(Color.BrightCyan,   Color.Blue)
                : node.SizeBytes >= BytesPerGb
                ? new Terminal.Gui.Attribute(Color.BrightRed,    Color.Blue)
                : node.SizeBytes >= Bytes100Mb
                ? new Terminal.Gui.Attribute(Color.BrightYellow, Color.Blue)
                : node.IsFile && node.SizeBytes < Bytes10Mb
                ? new Terminal.Gui.Attribute(Color.Gray,         Color.Blue)
                : norm;

            // Apply horizontal-scroll offset and draw the row
            int skip = Math.Min(col + start, row.Length);
            string vis = row[skip..];
            if (vis.Length > width) vis = vis[..width];
            int pad = width - vis.Length;

            var driver = Application.Driver!;
            driver.SetAttribute(attr);
            driver.AddStr(vis);
            // Fill the rest of the row so cursor/mark highlights the full width.
            if (pad > 0)
            {
                driver.SetAttribute(isCursor || isMarked ? attr : norm);
                driver.AddStr(new string(' ', pad));
            }
        }
    }
}
