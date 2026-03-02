using Mc.Core.Vfs;

namespace Mc.Vfs.Archives;

/// <summary>
/// VFS provider for SFS (single-file filesystem) — mounts single-file containers
/// such as ISO images through an external helper defined in mc.sfs.
///
/// The mc.sfs config file maps file extensions to mount commands:
///   iso    /usr/bin/isoinfo -d -i %1
///   iso    /usr/bin/isoinfo -l -i %1   (listing variant)
///
/// This implementation reads mc.sfs, locates matching helpers, mounts the file
/// to a temporary directory via the helper, and wraps the temporary directory
/// with the LocalVfsProvider.
///
/// Equivalent to src/vfs/sfs/ in the original C codebase.
/// </summary>
public sealed class SfsVfsProvider : IVfsProvider
{
    private static readonly string[] SfsConfigPaths =
    [
        "/usr/lib/mc/mc.sfs",
        "/usr/libexec/mc/mc.sfs",
        "/usr/share/mc/mc.sfs",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     ".config", "mc", "mc.sfs"),
    ];

    // Extension → (mountCmd, umountCmd) pairs
    private readonly Dictionary<string, (string MountCmd, string UmountCmd)> _handlers
        = new(StringComparer.OrdinalIgnoreCase);

    // Mounted temporary directories: archivePath → tmpDir
    private readonly Dictionary<string, string> _mountedDirs = new();

    public IReadOnlyList<string> Schemes => ["sfs"];
    public string Name => "Single-File Filesystem";

    public bool CanHandle(VfsPath path)
    {
        if (path.Scheme == "sfs") return true;
        var ext = path.Extension.TrimStart('.');
        return _handlers.ContainsKey(ext);
    }

    public void Initialize()
    {
        foreach (var cfgPath in SfsConfigPaths)
        {
            if (!File.Exists(cfgPath)) continue;
            foreach (var rawLine in File.ReadAllLines(cfgPath))
            {
                var line = rawLine.Trim();
                if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line)) continue;

                // Format: "ext mountcommand [umountcommand]"
                var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var ext   = parts[0].Trim();
                var mount = parts[1].Trim();
                var umount = parts.Length > 2 ? parts[2].Trim() : string.Empty;
                if (!_handlers.ContainsKey(ext))
                    _handlers[ext] = (mount, umount);
            }
        }
    }

    public void Dispose()
    {
        // Unmount all temp mounts
        foreach (var (archive, tmpDir) in _mountedDirs)
        {
            TryUmount(archive, tmpDir);
        }
        _mountedDirs.Clear();
    }

    private static (string archive, string inner) SplitPath(VfsPath path)
    {
        var p   = path.Path;
        var sep = p.IndexOf('|');
        if (sep < 0) return (p, "/");
        return (p[..sep], p[(sep + 1)..]);
    }

    private string EnsureMounted(string archivePath)
    {
        if (_mountedDirs.TryGetValue(archivePath, out var existing))
            return existing;

        var ext = Path.GetExtension(archivePath).TrimStart('.');
        if (!_handlers.TryGetValue(ext, out var handler))
            throw new NotSupportedException($"No SFS handler for extension '.{ext}'");

        var tmpDir = Path.Combine(Path.GetTempPath(), $"mc_sfs_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(tmpDir);

        var cmd = handler.MountCmd.Replace("%1", ShellQuote(archivePath))
                                   .Replace("%2", ShellQuote(tmpDir));
        try
        {
            using var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo("/bin/sh", $"-c {ShellQuote(cmd)}")
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                },
            };
            proc.Start();
            proc.WaitForExit(30_000);
        }
        catch (Exception ex)
        {
            Directory.Delete(tmpDir, recursive: true);
            throw new IOException($"SFS mount failed for '{archivePath}': {ex.Message}", ex);
        }

        _mountedDirs[archivePath] = tmpDir;
        return tmpDir;
    }

    private void TryUmount(string archivePath, string tmpDir)
    {
        var ext = Path.GetExtension(archivePath).TrimStart('.');
        if (_handlers.TryGetValue(ext, out var handler) && !string.IsNullOrEmpty(handler.UmountCmd))
        {
            var cmd = handler.UmountCmd.Replace("%1", ShellQuote(archivePath))
                                        .Replace("%2", ShellQuote(tmpDir));
            try
            {
                using var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo("/bin/sh", $"-c {ShellQuote(cmd)}")
                    {
                        UseShellExecute = false,
                        CreateNoWindow  = true,
                    },
                };
                proc.Start();
                proc.WaitForExit(10_000);
            }
            catch { /* best-effort */ }
        }
        try { Directory.Delete(tmpDir, recursive: true); } catch { }
    }

    // Delegate to local filesystem for the mounted temp dir
    private VfsDirEntry[] ListLocal(string dir, string archivePath, VfsPath basePath)
    {
        if (!Directory.Exists(dir)) return [];
        var entries = new List<VfsDirEntry>();
        foreach (var e in Directory.EnumerateFileSystemEntries(dir))
        {
            var info  = new FileInfo(e);
            var name  = Path.GetFileName(e);
            var inner = e[(dir.Length + 1)..].Replace('\\', '/');
            entries.Add(new VfsDirEntry
            {
                Name             = name,
                FullPath         = new VfsPath("sfs", null, null, null, null, archivePath + "|" + inner),
                Size             = info.Exists && (info.Attributes & FileAttributes.Directory) == 0 ? info.Length : 0,
                IsDirectory      = Directory.Exists(e),
                ModificationTime = info.LastWriteTime,
            });
        }
        return entries.ToArray();
    }

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var tmpDir = EnsureMounted(archive);
        var target = inner.TrimStart('/');
        var dir    = string.IsNullOrEmpty(target) ? tmpDir : Path.Combine(tmpDir, target);
        return ListLocal(dir, archive, path);
    }

    public bool DirectoryExists(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        try
        {
            var tmpDir = EnsureMounted(archive);
            var target = inner.TrimStart('/');
            return string.IsNullOrEmpty(target)
                ? true
                : Directory.Exists(Path.Combine(tmpDir, target));
        }
        catch { return false; }
    }

    public bool FileExists(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        try
        {
            var tmpDir = EnsureMounted(archive);
            return File.Exists(Path.Combine(tmpDir, inner.TrimStart('/')));
        }
        catch { return false; }
    }

    public Stream OpenRead(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var tmpDir = EnsureMounted(archive);
        var file   = Path.Combine(tmpDir, inner.TrimStart('/'));
        if (!File.Exists(file)) throw new FileNotFoundException($"Not found in SFS mount: {inner}");
        return File.OpenRead(file);
    }

    public VfsDirEntry Stat(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var tmpDir = EnsureMounted(archive);
        var target = Path.Combine(tmpDir, inner.TrimStart('/'));
        var isDir  = Directory.Exists(target);
        var isFile = File.Exists(target);
        if (!isDir && !isFile) throw new FileNotFoundException($"Not found in SFS mount: {inner}");
        var info = new FileInfo(target);
        return new VfsDirEntry
        {
            Name             = Path.GetFileName(inner),
            FullPath         = path,
            Size             = isFile ? info.Length : 0,
            IsDirectory      = isDir,
            ModificationTime = info.LastWriteTime,
        };
    }

    // Write operations — SFS mounts are read-only
    public Stream OpenWrite(VfsPath path)       => throw new NotSupportedException("SFS mounts are read-only");
    public Stream OpenAppend(VfsPath path)      => throw new NotSupportedException();
    public void DeleteFile(VfsPath path)        => throw new NotSupportedException();
    public void CopyFile(VfsPath s, VfsPath d)  => throw new NotSupportedException();
    public void MoveFile(VfsPath s, VfsPath d)  => throw new NotSupportedException();
    public void CreateDirectory(VfsPath p)      => throw new NotSupportedException();
    public void DeleteDirectory(VfsPath p, bool r) => throw new NotSupportedException();
    public void CreateSymlink(VfsPath t, VfsPath l) => throw new NotSupportedException();
    public void SetPermissions(VfsPath p, UnixFileMode m) => throw new NotSupportedException();
    public void SetOwner(VfsPath p, int u, int g)   => throw new NotSupportedException();
    public void SetModificationTime(VfsPath p, DateTime t) => throw new NotSupportedException();

    public VfsPath GetParent(VfsPath path) => path.Parent();
    public VfsPath Combine(VfsPath directory, string name) => directory.Combine(name);
    public bool IsAbsolute(VfsPath path) => true;

    private static string ShellQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";
}
