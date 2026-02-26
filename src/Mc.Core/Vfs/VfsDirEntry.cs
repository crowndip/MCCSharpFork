namespace Mc.Core.Vfs;

/// <summary>
/// A single entry returned by IVfsProvider.ListDirectory().
/// Equivalent to file_entry_t + struct stat in the original C codebase.
/// </summary>
public sealed class VfsDirEntry
{
    public required string Name { get; init; }
    public required VfsPath FullPath { get; init; }
    public long Size { get; init; }
    public DateTime ModificationTime { get; init; }
    public DateTime AccessTime { get; init; }
    public DateTime CreationTime { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsSymlink { get; init; }
    public string? SymlinkTarget { get; init; }
    public bool IsHidden { get; init; }
    public bool IsExecutable { get; init; }
    public UnixFileMode Permissions { get; init; }
    public int OwnerUid { get; init; }
    public int OwnerGid { get; init; }
    public string? OwnerName { get; init; }
    public string? GroupName { get; init; }
    public long Inode { get; init; }
    public int HardLinks { get; init; }

    public string Extension => System.IO.Path.GetExtension(Name);

    /// <summary>Whether the entry is the ".." parent directory placeholder.</summary>
    public bool IsParentDir => Name == "..";

    /// <summary>Whether the entry is the "." current directory placeholder.</summary>
    public bool IsCurrentDir => Name == ".";
}
