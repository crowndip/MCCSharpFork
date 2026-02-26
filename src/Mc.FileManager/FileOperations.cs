using Mc.Core.Vfs;

namespace Mc.FileManager;

public enum OperationConflict { Ask, Overwrite, Skip, Rename }
public enum OperationResult { Success, Skipped, Error, Cancelled }

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
                await CopyDirectoryAsync(src, destPath, onConflict, prog, progress, ct);
            }
            else
            {
                await CopySingleFileAsync(src, destPath, stat.Size, onConflict, prog, progress, ct);
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
                await CopySingleFileAsync(src, destPath, stat.Size, onConflict, prog, progress, ct);
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
        OperationProgress prog,
        IProgress<OperationProgress>? progress,
        CancellationToken ct)
    {
        _vfs.CreateDirectory(dest);
        var entries = _vfs.ListDirectory(src);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.Name == "..") continue;
            var childSrc = entry.FullPath;
            var childDest = dest.Combine(entry.Name);
            if (entry.IsDirectory)
                await CopyDirectoryAsync(childSrc, childDest, onConflict, prog, progress, ct);
            else
                await CopySingleFileAsync(childSrc, childDest, entry.Size, onConflict, prog, progress, ct);
        }
    }

    private async Task CopySingleFileAsync(
        VfsPath src, VfsPath dest, long size,
        OperationConflict onConflict,
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
    }
}
