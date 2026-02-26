using Mc.Core.Vfs;

namespace Mc.Core.Models;

/// <summary>
/// UI-level file entry displayed in a panel.
/// Wraps VfsDirEntry with display state (marked, selected, color).
/// Equivalent to file_entry_t + panel display state.
/// </summary>
public sealed class FileEntry
{
    public required VfsDirEntry DirEntry { get; init; }

    // --- Display state ---
    public bool IsMarked { get; set; }
    public int DisplayColor { get; set; }
    public int DisplayNameLength { get; set; }

    // --- Delegates to underlying VfsDirEntry ---
    public string Name => DirEntry.Name;
    public long Size => DirEntry.Size;
    public DateTime ModificationTime => DirEntry.ModificationTime;
    public bool IsDirectory => DirEntry.IsDirectory;
    public bool IsSymlink => DirEntry.IsSymlink;
    public bool IsExecutable => DirEntry.IsExecutable;
    public bool IsHidden => DirEntry.IsHidden;
    public UnixFileMode Permissions => DirEntry.Permissions;
    public string? OwnerName => DirEntry.OwnerName;
    public string? GroupName => DirEntry.GroupName;
    public string Extension => DirEntry.Extension;
    public VfsPath FullPath => DirEntry.FullPath;
    public bool IsParentDir => DirEntry.IsParentDir;
}
