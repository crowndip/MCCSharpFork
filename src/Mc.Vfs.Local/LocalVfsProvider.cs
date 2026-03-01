using System.Runtime.InteropServices;
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

        // Add parent directory entry (unless at root
        var isRoot = dir is "/" or ""
            || (OperatingSystem.IsWindows() && System.IO.Path.GetPathRoot(dir) == dir);
        if (!string.IsNullOrEmpty(dir) && !isRoot)
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
        var native  = path.Path.Replace('/', System.IO.Path.DirectorySeparatorChar);
        var parent  = System.IO.Path.GetDirectoryName(native.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        var fallback = OperatingSystem.IsWindows()
            ? System.IO.Path.GetPathRoot(path.Path) ?? "C:\\"
            : "/";
        return path.WithPath(parent ?? fallback);
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

        // Detect special file types via lstat() (#27)
        bool isBlockDevice = false, isCharDevice = false, isFifo = false, isSocket = false;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var typeBits = GetFileTypeBits(f.FullName);
            isBlockDevice = (typeBits & S_IFMT) == S_IFBLK;
            isCharDevice  = (typeBits & S_IFMT) == S_IFCHR;
            isFifo        = (typeBits & S_IFMT) == S_IFIFO;
            isSocket      = (typeBits & S_IFMT) == S_IFSOCK;
        }

        // Detect if a symlink points to a directory (#37)
        bool isSymlinkToDir = false;
        if (isSymlink && f.LinkTarget != null)
        {
            try
            {
                var absTarget = Path.IsPathRooted(f.LinkTarget)
                    ? f.LinkTarget
                    : Path.GetFullPath(f.LinkTarget, Path.GetDirectoryName(f.FullName)!);
                isSymlinkToDir = Directory.Exists(absTarget);
            }
            catch { }
        }

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
            IsBlockDevice = isBlockDevice,
            IsCharDevice  = isCharDevice,
            IsFifo        = isFifo,
            IsSocket      = isSocket,
            IsSymlinkToDirectory = isSymlinkToDir,  // #37
            Permissions = mode,
        };
    }

    // --- Native P/Invoke helpers ---

    // DllImport is resolved lazily in .NET JIT; the runtime guard below prevents
    // the call on Windows, but we also wrap in try/catch for defence-in-depth.
    [DllImport("libc", EntryPoint = "lchown")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    private static extern int lchown(string path, uint owner, uint group);

    private static void NativeChown(string path, int uid, int gid)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
        try { lchown(path, (uint)uid, (uint)gid); }
        catch { /* silently ignore on unsupported platforms or missing libc */ }
    }

    // File type bits from st_mode (#27)
    private const uint S_IFMT  = 0xF000u;
    private const uint S_IFBLK = 0x6000u;  // block device
    private const uint S_IFCHR = 0x2000u;  // character device
    private const uint S_IFIFO = 0x1000u;  // FIFO / named pipe
    private const uint S_IFSOCK= 0xC000u;  // Unix domain socket

    [DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    private static extern int NativeLstat(string path, IntPtr buf);

    /// <summary>
    /// Returns st_mode from lstat() so we can identify special file types.
    /// On Linux x86_64: st_mode is at offset 24 in the stat struct.
    /// On macOS x86_64/arm64: st_mode is at offset 8.
    /// Returns 0 on any error.
    /// </summary>
    private static uint GetFileTypeBits(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return 0;
        var buf = Marshal.AllocHGlobal(256); // larger than any stat struct
        try
        {
            if (NativeLstat(path, buf) != 0) return 0;
            int modeOffset = OperatingSystem.IsMacOS() ? 8 : 24;
            return (uint)Marshal.ReadInt32(buf, modeOffset);
        }
        catch { return 0; }
        finally { Marshal.FreeHGlobal(buf); }
    }
}
