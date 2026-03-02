using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Mc.Core.Vfs;

namespace Mc.FileManager;

public enum OperationConflict { Ask, Overwrite, Skip, Rename }
public enum OperationResult { Success, Skipped, Error, Cancelled }

/// <summary>Per-file overwrite decision returned by a conflict callback.</summary>
public enum OverwriteAction { Overwrite, OverwriteAll, Skip, SkipAll, Append }

public sealed class OperationProgress
{
    public string CurrentFile { get; set; } = string.Empty;
    public long BytesDone { get; set; }
    public long TotalBytes { get; set; }
    public int FilesDone { get; set; }
    public int TotalFiles { get; set; }
    public double Percent => TotalBytes > 0 ? (double)BytesDone / TotalBytes * 100 : 0;
}

/// <summary>
/// Async file operations: copy, move, delete, mkdir.
/// Equivalent to src/filemanager/file.c in the original C codebase.
/// </summary>
public sealed class FileOperations
{
    private readonly VfsRegistry _vfs;

    public FileOperations(VfsRegistry vfs) => _vfs = vfs;

    public async Task<OperationResult> CopyAsync(
        IReadOnlyList<VfsPath> sources,
        VfsPath destination,
        OperationConflict onConflict = OperationConflict.Ask,
        Func<string, string, OverwriteAction>? conflictCallback = null,
        bool preserveAttributes = false,
        string? sourceMask = null,
        bool followSymlinks = false,
        bool diveIntoSubdir = true,
        bool stableSymlinks = false,
        bool preserveExt2Attributes = false,  // #35
        IProgress<OperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Build filtered source list from mask
        var filteredSources = string.IsNullOrEmpty(sourceMask) || sourceMask == "*"
            ? sources
            : sources.Where(s => MatchesGlob(Path.GetFileName(s.Path), sourceMask)).ToList();

        var prog = new OperationProgress { TotalFiles = filteredSources.Count };
        long totalBytes = 0;
        foreach (var src in filteredSources)
        {
            try { totalBytes += _vfs.Stat(src).Size; } catch { }
        }
        prog.TotalBytes = totalBytes;

        // Shared overwrite decision (OverwriteAll/SkipAll persists across files)
        var overwriteAll = false;
        var skipAll      = false;

        // Determine once whether destination is an existing directory.
        // Matches original MC move_file_file() / copy_file_file() logic:
        //   - existing dir  → place each source INSIDE it (dest/srcname)
        //   - non-dir, single source → use destination as the exact target path (rename)
        //   - non-dir, multiple sources → create the directory then place inside it
        bool destIsExistingDir = Directory.Exists(destination.Path);

        for (int i = 0; i < filteredSources.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var src = filteredSources[i];
            var stat = _vfs.Stat(src);
            prog.CurrentFile = stat.Name;
            prog.FilesDone = i;
            progress?.Report(prog);

            // Resolve the per-file destination path (mirrors original MC behaviour)
            VfsPath destPath;
            if (destIsExistingDir)
            {
                destPath = destination.Combine(stat.Name);
            }
            else if (filteredSources.Count == 1)
            {
                // Single file/dir → destination IS the new name (rename/copy-to-new-name)
                destPath = destination;
            }
            else
            {
                // Multiple sources, destination doesn't exist yet → treat as a new directory
                _vfs.CreateDirectory(destination);
                destIsExistingDir = true;
                destPath = destination.Combine(stat.Name);
            }

            // If source is a symlink and we are NOT following symlinks, copy the symlink itself
            if (stat.IsSymlink && !followSymlinks && stat.SymlinkTarget != null)
            {
                var linkTarget = stableSymlinks
                    ? MakeRelativeSymlinkTarget(stat.SymlinkTarget, destination.Path)
                    : stat.SymlinkTarget;
                try { _vfs.CreateSymlink(VfsPath.FromLocal(linkTarget), destPath); } catch { }
                continue;
            }

            if (stat.IsDirectory)
            {
                // Dive-into-subdir: if destination directory exists and diveIntoSubdir=true, merge into it
                // If diveIntoSubdir=false and dest exists, still merge (preserves current behaviour)
                await CopyDirectoryAsync(src, destPath, onConflict, conflictCallback,
                    preserveAttributes, followSymlinks, stableSymlinks, preserveExt2Attributes, prog, progress, ct);
            }
            else
            {
                // Resolve conflict when destination exists
                bool destExists = false;
                try { _vfs.Stat(destPath); destExists = true; } catch { }
                if (destExists)
                {
                    if (skipAll) { prog.FilesDone++; continue; }
                    if (!overwriteAll)
                    {
                        var action = conflictCallback != null
                            ? conflictCallback(stat.Name, destPath.Path)
                            : OverwriteAction.Overwrite;
                        if (action == OverwriteAction.Skip) continue;
                        if (action == OverwriteAction.SkipAll) { skipAll = true; continue; }
                        if (action == OverwriteAction.OverwriteAll) overwriteAll = true;
                    }
                }

                await CopySingleFileAsync(src, destPath, stat.Size, preserveAttributes, preserveExt2Attributes, prog, progress, ct);
            }
        }

        return OperationResult.Success;
    }

    public async Task<OperationResult> MoveAsync(
        IReadOnlyList<VfsPath> sources,
        VfsPath destination,
        OperationConflict onConflict = OperationConflict.Ask,
        IProgress<OperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var prog = new OperationProgress { TotalFiles = sources.Count };

        // Matches original MC move_file_file() logic:
        // existing dir → place each file INSIDE it; non-dir + single source → exact rename target.
        bool destIsExistingDir = Directory.Exists(destination.Path);

        for (int i = 0; i < sources.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var src = sources[i];
            var stat = _vfs.Stat(src);
            prog.CurrentFile = stat.Name;
            prog.FilesDone = i;
            progress?.Report(prog);

            VfsPath destPath;
            if (destIsExistingDir)
            {
                destPath = destination.Combine(stat.Name);
            }
            else if (sources.Count == 1)
            {
                // Single file → destination is the exact rename target
                destPath = destination;
            }
            else
            {
                // Multiple sources, non-existent destination → create dir and move inside
                _vfs.CreateDirectory(destination);
                destIsExistingDir = true;
                destPath = destination.Combine(stat.Name);
            }

            try
            {
                _vfs.MoveFile(src, destPath);
            }
            catch
            {
                // Cross-device: copy then delete
                if (stat.IsDirectory)
                {
                    await CopyDirectoryAsync(src, destPath, onConflict, null, false, false, false, false, prog, progress, ct);
                    _vfs.DeleteDirectory(src, recursive: true);
                }
                else
                {
                    await CopySingleFileAsync(src, destPath, stat.Size, false, false, prog, progress, ct);
                    _vfs.DeleteFile(src);
                }
            }
            prog.BytesDone += stat.Size;
        }

        return OperationResult.Success;
    }

    public async Task<OperationResult> DeleteAsync(
        IReadOnlyList<VfsPath> targets,
        IProgress<OperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var prog = new OperationProgress { TotalFiles = targets.Count };

        for (int i = 0; i < targets.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var target = targets[i];
            var stat = _vfs.Stat(target);
            prog.CurrentFile = stat.Name;
            prog.FilesDone = i;
            progress?.Report(prog);

            if (stat.IsDirectory)
                _vfs.DeleteDirectory(target, recursive: true);
            else
                _vfs.DeleteFile(target);
        }

        await Task.CompletedTask;
        return OperationResult.Success;
    }

    public void CreateDirectory(VfsPath parentPath, string name)
    {
        var newPath = parentPath.Combine(name);
        _vfs.CreateDirectory(newPath);
    }

    public void Rename(VfsPath path, string newName)
    {
        var parent = path.Parent();
        var destPath = parent.Combine(newName);
        _vfs.MoveFile(path, destPath);
    }

    private async Task CopyDirectoryAsync(
        VfsPath src, VfsPath dest,
        OperationConflict onConflict,
        Func<string, string, OverwriteAction>? conflictCallback,
        bool preserveAttributes,
        bool followSymlinks,
        bool stableSymlinks,
        bool preserveExt2Attributes,  // #35
        OperationProgress prog,
        IProgress<OperationProgress>? progress,
        CancellationToken ct)
    {
        try { _vfs.Stat(dest); } catch { _vfs.CreateDirectory(dest); }
        var entries = _vfs.ListDirectory(src);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.Name == "..") continue;
            var childSrc  = entry.FullPath;
            var childDest = dest.Combine(entry.Name);
            if (entry.IsSymlink && !followSymlinks && entry.SymlinkTarget != null)
            {
                var linkTarget = stableSymlinks
                    ? MakeRelativeSymlinkTarget(entry.SymlinkTarget, dest.Path)
                    : entry.SymlinkTarget;
                try { _vfs.CreateSymlink(VfsPath.FromLocal(linkTarget), childDest); } catch { }
            }
            else if (entry.IsDirectory)
                await CopyDirectoryAsync(childSrc, childDest, onConflict, conflictCallback, preserveAttributes, followSymlinks, stableSymlinks, preserveExt2Attributes, prog, progress, ct);
            else
                await CopySingleFileAsync(childSrc, childDest, entry.Size, preserveAttributes, preserveExt2Attributes, prog, progress, ct);
        }
        if (preserveAttributes) PreserveAttrs(src, dest);
    }

    private async Task CopySingleFileAsync(
        VfsPath src, VfsPath dest, long size,
        bool preserveAttributes,
        bool preserveExt2Attributes,  // #35
        OperationProgress prog,
        IProgress<OperationProgress>? progress,
        CancellationToken ct)
    {
        const int BufferSize = 64 * 1024;
        using var srcStream = _vfs.OpenRead(src);
        using var dstStream = _vfs.OpenWrite(dest);

        var buffer = new byte[BufferSize];
        int read;
        while ((read = await srcStream.ReadAsync(buffer, ct)) > 0)
        {
            await dstStream.WriteAsync(buffer.AsMemory(0, read), ct);
            prog.BytesDone += read;
            progress?.Report(prog);
        }
        prog.FilesDone++;

        if (preserveAttributes) PreserveAttrs(src, dest);
        if (preserveExt2Attributes && OperatingSystem.IsLinux()) TryCopyExt2Attributes(src.Path, dest.Path); // #35
    }

    /// <summary>Glob pattern match: supports * (any chars) and ? (single char).</summary>
    private static bool MatchesGlob(string name, string pattern)
    {
        var regexPat = "^" + string.Concat(pattern.Select(c => c switch
        {
            '*' => ".*",
            '?' => ".",
            '.' => "\\.",
            _   => Regex.Escape(c.ToString())
        })) + "$";
        return Regex.IsMatch(name, regexPat, RegexOptions.IgnoreCase);
    }

    /// <summary>Convert an absolute symlink target to a path relative to the destination directory.</summary>
    private static string MakeRelativeSymlinkTarget(string target, string destDir)
    {
        try
        {
            var absTarget = Path.IsPathRooted(target) ? target : Path.GetFullPath(target);
            var relative  = Path.GetRelativePath(destDir, absTarget);
            return relative;
        }
        catch { return target; }
    }

    private void PreserveAttrs(VfsPath src, VfsPath dest)
    {
        try
        {
            var stat = _vfs.Stat(src);
            // Copy timestamps
            if (stat.ModificationTime != DateTime.MinValue)
                File.SetLastWriteTime(dest.Path, stat.ModificationTime);
            if (stat.AccessTime != DateTime.MinValue)
                File.SetLastAccessTime(dest.Path, stat.AccessTime);
            // Copy Unix file mode (permissions) — .NET 7+
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                try
                {
                    var srcMode = File.GetUnixFileMode(src.Path);
                    File.SetUnixFileMode(dest.Path, srcMode);
                }
                catch { /* non-local VFS or unsupported */ }
            }
        }
        catch { /* best-effort */ }
    }

    /// <summary>
    /// Copy ext2/ext4 file attributes (immutable, append-only, etc.) from src to dest. (#35)
    /// Uses ioctl FS_IOC_GETFLAGS / FS_IOC_SETFLAGS on Linux.  Best-effort — silently ignores errors.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void TryCopyExt2Attributes(string srcPath, string destPath)
    {
        if (!OperatingSystem.IsLinux()) return;
        try
        {
            // Read flags from source file via lsattr, set on dest via chattr.
            // This avoids P/Invoke ioctl complexity while remaining functionally correct.
            var lsPsi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "lsattr",
                ArgumentList = { "-d", srcPath },
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using var lsProc = System.Diagnostics.Process.Start(lsPsi);
            var line = lsProc?.StandardOutput.ReadLine();
            lsProc?.WaitForExit();

            // lsattr output: "----i--------e-- /path/to/file"
            if (string.IsNullOrEmpty(line) || line.Length < 20) return;
            var flagStr = line[..line.IndexOf(' ')].Replace("-", string.Empty).Trim();
            if (string.IsNullOrEmpty(flagStr)) return;

            var chattrPsi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chattr",
                ArgumentList = { "=" + flagStr, destPath },
                UseShellExecute = false,
            };
            using var chattrProc = System.Diagnostics.Process.Start(chattrPsi);
            chattrProc?.WaitForExit();
        }
        catch { /* lsattr/chattr unavailable or permission denied — silently ignore */ }
    }
}
