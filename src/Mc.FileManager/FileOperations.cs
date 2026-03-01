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
        IProgress<OperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var prog = new OperationProgress { TotalFiles = sources.Count };
        long totalBytes = 0;
        foreach (var src in sources)
        {
            try { totalBytes += _vfs.Stat(src).Size; } catch { }
        }
        prog.TotalBytes = totalBytes;

        // Shared overwrite decision (OverwriteAll/SkipAll persists across files)
        var overwriteAll = false;
        var skipAll      = false;

        for (int i = 0; i < sources.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var src = sources[i];
            var stat = _vfs.Stat(src);
            prog.CurrentFile = stat.Name;
            prog.FilesDone = i;
            progress?.Report(prog);

            var destPath = destination.Combine(stat.Name);

            if (stat.IsDirectory)
            {
                await CopyDirectoryAsync(src, destPath, onConflict, conflictCallback,
                    preserveAttributes, prog, progress, ct);
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

                await CopySingleFileAsync(src, destPath, stat.Size, preserveAttributes, prog, progress, ct);
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

        for (int i = 0; i < sources.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var src = sources[i];
            var stat = _vfs.Stat(src);
            prog.CurrentFile = stat.Name;
            prog.FilesDone = i;
            progress?.Report(prog);

            var destPath = destination.Combine(stat.Name);

            try
            {
                _vfs.MoveFile(src, destPath);
            }
            catch
            {
                // Cross-device: copy then delete
                await CopySingleFileAsync(src, destPath, stat.Size, false, prog, progress, ct);
                _vfs.DeleteFile(src);
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
            if (entry.IsDirectory)
                await CopyDirectoryAsync(childSrc, childDest, onConflict, conflictCallback, preserveAttributes, prog, progress, ct);
            else
                await CopySingleFileAsync(childSrc, childDest, entry.Size, preserveAttributes, prog, progress, ct);
        }
        if (preserveAttributes) PreserveAttrs(src, dest);
    }

    private async Task CopySingleFileAsync(
        VfsPath src, VfsPath dest, long size,
        bool preserveAttributes,
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
}
