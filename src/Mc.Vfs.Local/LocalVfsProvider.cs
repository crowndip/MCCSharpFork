using Mc.Core.Vfs;

namespace Mc.Vfs.Local;

/// <summary>
/// VFS provider for the local (native) filesystem.
/// Equivalent to src/vfs/local/ in the original C codebase.
/// Uses System.IO exclusively — no external packages needed.
/// </summary>
public sealed class LocalVfsProvider : IVfsProvider
{
    public IReadOnlyList<string> Schemes => ["local", "file"];
    public string Name => "Local Filesystem";

    public bool CanHandle(VfsPath path) => path.IsLocal;

    public void Initialize() { }
    public void Dispose() { }

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
    {
        var dir = path.Path;
        var entries = new List<VfsDirEntry>();

        // Add parent directory entry (unless at root)
        if (!string.IsNullOrEmpty(dir) && dir != "/")
        {
            var parentPath = path.Parent();
            entries.Add(new VfsDirEntry
            {
                Name = "..",
                FullPath = parentPath,
                IsDirectory = true,
                ModificationTime = DateTime.MinValue,
            });
        }

        var dirInfo = new DirectoryInfo(dir);
        if (!dirInfo.Exists) throw new DirectoryNotFoundException($"Directory not found: {dir}");

        // Directories first
        foreach (var d in dirInfo.GetDirectories())
        {
            try { entries.Add(FromDirectoryInfo(d, path)); }
            catch { /* skip inaccessible entries */ }
        }

        foreach (var f in dirInfo.GetFiles())
        {
            try { entries.Add(FromFileInfo(f, path)); }
            catch { }
        }

        return entries;
    }

    public bool DirectoryExists(VfsPath path) => Directory.Exists(path.Path);
    public bool FileExists(VfsPath path) => File.Exists(path.Path);

    public void CreateDirectory(VfsPath path) => Directory.CreateDirectory(path.Path);

    public void DeleteDirectory(VfsPath path, bool recursive) => Directory.Delete(path.Path, recursive);

    public Stream OpenRead(VfsPath path) => File.OpenRead(path.Path);

    public Stream OpenWrite(VfsPath path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path.Path)!);
        return File.OpenWrite(path.Path);
    }

    public Stream OpenAppend(VfsPath path) => File.Open(path.Path, FileMode.Append, FileAccess.Write);

    public void DeleteFile(VfsPath path) => File.Delete(path.Path);

    public void CopyFile(VfsPath source, VfsPath destination)
        => File.Copy(source.Path, destination.Path, overwrite: true);

    public void MoveFile(VfsPath source, VfsPath destination)
        => File.Move(source.Path, destination.Path, overwrite: true);

    public void CreateSymlink(VfsPath target, VfsPath link)
        => File.CreateSymbolicLink(link.Path, target.Path);

    public VfsDirEntry Stat(VfsPath path)
    {
        var fullPath = path.Path;
        if (Directory.Exists(fullPath))
        {
            var d = new DirectoryInfo(fullPath);
            return FromDirectoryInfo(d, path.Parent());
        }
        var f = new FileInfo(fullPath);
        return FromFileInfo(f, path.Parent());
    }

    public void SetPermissions(VfsPath path, UnixFileMode mode)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(path.Path, mode);
    }

    public void SetOwner(VfsPath path, int uid, int gid)
    {
        // On Linux, use P/Invoke to lchown
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            NativeChown(path.Path, uid, gid);
    }

    public void SetModificationTime(VfsPath path, DateTime time)
        => File.SetLastWriteTime(path.Path, time);

    public VfsPath GetParent(VfsPath path)
    {
        var parent = System.IO.Path.GetDirectoryName(path.Path.TrimEnd('/'));
        return path.WithPath(parent ?? "/");
    }

    public VfsPath Combine(VfsPath directory, string name)
        => directory.Combine(name);

    public bool IsAbsolute(VfsPath path)
        => System.IO.Path.IsPathRooted(path.Path);

    // --- Private helpers ---

    private static VfsDirEntry FromDirectoryInfo(DirectoryInfo d, VfsPath parent)
    {
        bool isSymlink = d.LinkTarget != null;
        UnixFileMode mode = UnixFileMode.None;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try { mode = d.UnixFileMode; } catch { }
        }
        return new VfsDirEntry
        {
            Name = d.Name,
            FullPath = parent.Combine(d.Name),
            Size = 0,
            ModificationTime = d.LastWriteTime,
            AccessTime = d.LastAccessTime,
            CreationTime = d.CreationTime,
            IsDirectory = true,
            IsSymlink = isSymlink,
            SymlinkTarget = d.LinkTarget,
            IsHidden = d.Name.StartsWith('.') || (d.Attributes & FileAttributes.Hidden) != 0,
            Permissions = mode,
        };
    }

    private static VfsDirEntry FromFileInfo(FileInfo f, VfsPath parent)
    {
        bool isSymlink = f.LinkTarget != null;
        UnixFileMode mode = UnixFileMode.None;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try { mode = f.UnixFileMode; } catch { }
        }
        bool isExec = (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        return new VfsDirEntry
        {
            Name = f.Name,
            FullPath = parent.Combine(f.Name),
            Size = f.Length,
            ModificationTime = f.LastWriteTime,
            AccessTime = f.LastAccessTime,
            CreationTime = f.CreationTime,
            IsDirectory = false,
            IsSymlink = isSymlink,
            SymlinkTarget = f.LinkTarget,
            IsHidden = f.Name.StartsWith('.') || (f.Attributes & FileAttributes.Hidden) != 0,
            IsExecutable = isExec,
            Permissions = mode,
        };
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "lchown")]
    private static extern int lchown(string path, uint owner, uint group);

    private static void NativeChown(string path, int uid, int gid)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            lchown(path, (uint)uid, (uint)gid);
    }
}
