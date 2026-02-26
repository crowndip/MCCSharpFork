using System.IO;

namespace Mc.Core.Vfs;

/// <summary>
/// Abstraction over a filesystem backend (local, FTP, SFTP, archive, etc.).
/// Mirrors the vfs_class function-pointer table from the original C codebase.
/// </summary>
public interface IVfsProvider
{
    /// <summary>URL schemes this provider handles, e.g. "ftp", "sftp", "local".</summary>
    IReadOnlyList<string> Schemes { get; }

    /// <summary>Human-readable name, e.g. "Local Filesystem".</summary>
    string Name { get; }

    bool CanHandle(VfsPath path);

    // --- Directory operations ---
    IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path);
    bool DirectoryExists(VfsPath path);
    void CreateDirectory(VfsPath path);
    void DeleteDirectory(VfsPath path, bool recursive);

    // --- File operations ---
    bool FileExists(VfsPath path);
    Stream OpenRead(VfsPath path);
    Stream OpenWrite(VfsPath path);
    Stream OpenAppend(VfsPath path);
    void DeleteFile(VfsPath path);
    void CopyFile(VfsPath source, VfsPath destination);
    void MoveFile(VfsPath source, VfsPath destination);
    void CreateSymlink(VfsPath target, VfsPath link);

    // --- Metadata ---
    VfsDirEntry Stat(VfsPath path);
    void SetPermissions(VfsPath path, UnixFileMode mode);
    void SetOwner(VfsPath path, int uid, int gid);
    void SetModificationTime(VfsPath path, DateTime time);

    // --- Path helpers ---
    VfsPath GetParent(VfsPath path);
    VfsPath Combine(VfsPath directory, string name);
    bool IsAbsolute(VfsPath path);

    // --- Provider lifecycle ---
    void Initialize();
    void Dispose();
}
