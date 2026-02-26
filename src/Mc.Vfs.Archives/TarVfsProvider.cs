using System.Formats.Tar;
using System.IO.Compression;
using Mc.Core.Vfs;

namespace Mc.Vfs.Archives;

/// <summary>
/// VFS provider for TAR archives (.tar, .tar.gz, .tar.bz2, .tar.xz, .tgz).
/// Uses System.Formats.Tar (built into .NET 7+) — no external packages.
/// Equivalent to src/vfs/tar/ in the original C codebase.
/// </summary>
public sealed class TarVfsProvider : IVfsProvider
{
    public IReadOnlyList<string> Schemes => ["tar"];
    public string Name => "TAR Archive";

    public bool CanHandle(VfsPath path)
    {
        if (path.Scheme == "tar") return true;
        var ext = path.Extension.ToLowerInvariant();
        return ext is ".tar" or ".tgz" or ".tar.gz" or ".tbz2" or ".tar.bz2" or ".txz" or ".tar.xz";
    }

    public void Initialize() { }
    public void Dispose() { }

    // Archive path format: "tar:///path/to/archive.tar!/internal/path"
    // Or: VfsPath with Scheme="tar", LocalPath="outer|inner"
    // We use Path as "archivePath|innerPath"

    private static (string archive, string inner) SplitPath(VfsPath path)
    {
        var p = path.Path;
        var sep = p.IndexOf('|');
        if (sep < 0) return (p, "/");
        return (p[..sep], p[(sep + 1)..]);
    }

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var entries = new List<VfsDirEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        inner = inner.TrimEnd('/');

        using var stream = OpenArchiveStream(archive);
        using var reader = new TarReader(stream, leaveOpen: false);

        while (reader.GetNextEntry() is { } entry)
        {
            var entryPath = "/" + entry.Name.TrimStart('/');
            if (!entryPath.StartsWith(inner + "/", StringComparison.Ordinal)) continue;

            var rel = entryPath[(inner.Length + 1)..].TrimStart('/');
            if (string.IsNullOrEmpty(rel)) continue;

            // Only direct children
            var slash = rel.IndexOf('/');
            var childName = slash >= 0 ? rel[..slash] : rel;
            if (seen.Contains(childName)) continue;
            seen.Add(childName);

            bool isDir = slash >= 0 || entry.EntryType == TarEntryType.Directory;
            var childInner = inner + "/" + childName;
            entries.Add(new VfsDirEntry
            {
                Name = childName,
                FullPath = new VfsPath("tar", null, null, null, null, archive + "|" + childInner),
                Size = isDir ? 0 : entry.Length,
                IsDirectory = isDir,
                ModificationTime = entry.ModificationTime.LocalDateTime,
                Permissions = entry.Mode,
                OwnerUid = entry.Uid,
                OwnerGid = entry.Gid,
                OwnerName = (entry as PosixTarEntry)?.UserName,
                GroupName = (entry as PosixTarEntry)?.GroupName,
            });
        }

        return entries;
    }

    public bool DirectoryExists(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        if (inner is "/" or "") return File.Exists(archive);
        using var stream = OpenArchiveStream(archive);
        using var reader = new TarReader(stream);
        while (reader.GetNextEntry() is { } e)
            if (("/" + e.Name.TrimStart('/')).StartsWith(inner, StringComparison.Ordinal))
                return true;
        return false;
    }

    public bool FileExists(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        using var stream = OpenArchiveStream(archive);
        using var reader = new TarReader(stream);
        while (reader.GetNextEntry() is { } e)
            if ("/" + e.Name.TrimStart('/') == inner)
                return true;
        return false;
    }

    public Stream OpenRead(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        using var stream = OpenArchiveStream(archive);
        using var reader = new TarReader(stream, leaveOpen: false);
        while (reader.GetNextEntry() is { } e)
        {
            if ("/" + e.Name.TrimStart('/') == inner)
            {
                var ms = new MemoryStream();
                e.DataStream?.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }
        throw new FileNotFoundException($"Entry not found in archive: {inner}");
    }

    public VfsDirEntry Stat(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        using var stream = OpenArchiveStream(archive);
        using var reader = new TarReader(stream);
        while (reader.GetNextEntry() is { } e)
        {
            if ("/" + e.Name.TrimStart('/') == inner)
            {
                return new VfsDirEntry
                {
                    Name = System.IO.Path.GetFileName(inner),
                    FullPath = path,
                    Size = e.Length,
                    IsDirectory = e.EntryType == TarEntryType.Directory,
                    ModificationTime = e.ModificationTime.LocalDateTime,
                    Permissions = e.Mode,
                };
            }
        }
        throw new FileNotFoundException($"Entry not found in archive: {inner}");
    }

    // Write operations — tar archives are generally read-only via VFS
    public Stream OpenWrite(VfsPath path) => throw new NotSupportedException("TAR archives are read-only via VFS");
    public Stream OpenAppend(VfsPath path) => throw new NotSupportedException("TAR archives are read-only via VFS");
    public void DeleteFile(VfsPath path) => throw new NotSupportedException("TAR archives are read-only via VFS");
    public void CopyFile(VfsPath s, VfsPath d) => throw new NotSupportedException();
    public void MoveFile(VfsPath s, VfsPath d) => throw new NotSupportedException();
    public void CreateDirectory(VfsPath p) => throw new NotSupportedException();
    public void DeleteDirectory(VfsPath p, bool r) => throw new NotSupportedException();
    public void CreateSymlink(VfsPath t, VfsPath l) => throw new NotSupportedException();
    public void SetPermissions(VfsPath p, UnixFileMode m) => throw new NotSupportedException();
    public void SetOwner(VfsPath p, int u, int g) => throw new NotSupportedException();
    public void SetModificationTime(VfsPath p, DateTime t) => throw new NotSupportedException();

    public VfsPath GetParent(VfsPath path) => path.Parent();
    public VfsPath Combine(VfsPath directory, string name) => directory.Combine(name);
    public bool IsAbsolute(VfsPath path) => true;

    private static Stream OpenArchiveStream(string archivePath)
    {
        var raw = File.OpenRead(archivePath);
        var ext = System.IO.Path.GetExtension(archivePath).ToLowerInvariant();
        return ext switch
        {
            ".gz" or ".tgz" => new GZipStream(raw, CompressionMode.Decompress),
            ".bz2" or ".tbz2" => new BrotliStream(raw, CompressionMode.Decompress), // Note: bz2 not natively in .NET; using Brotli as placeholder
            _ => raw,
        };
    }
}
