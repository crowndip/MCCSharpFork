using System.IO.Compression;
using Mc.Core.Vfs;

namespace Mc.Vfs.Archives;

/// <summary>
/// VFS provider for ZIP archives.
/// Uses System.IO.Compression (built into .NET) — no external packages.
/// </summary>
public sealed class ZipVfsProvider : IVfsProvider
{
    public IReadOnlyList<string> Schemes => ["zip"];
    public string Name => "ZIP Archive";

    public bool CanHandle(VfsPath path)
    {
        if (path.Scheme == "zip") return true;
        return path.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }

    public void Initialize() { }
    public void Dispose() { }

    private static (string archive, string inner) SplitPath(VfsPath path)
    {
        var p = path.Path;
        var sep = p.IndexOf('|');
        if (sep < 0) return (p, string.Empty);
        return (p[..sep], p[(sep + 1)..].TrimStart('/'));
    }

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var entries = new List<VfsDirEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!string.IsNullOrEmpty(inner) && !entryPath.StartsWith(inner + "/", StringComparison.Ordinal))
                continue;

            var rel = string.IsNullOrEmpty(inner) ? entryPath : entryPath[(inner.Length + 1)..];
            if (string.IsNullOrEmpty(rel)) continue;

            var slash = rel.IndexOf('/');
            var childName = slash >= 0 ? rel[..slash] : rel;
            if (string.IsNullOrEmpty(childName) || seen.Contains(childName)) continue;
            seen.Add(childName);

            bool isDir = slash >= 0 || entry.FullName.EndsWith('/');
            var childInner = string.IsNullOrEmpty(inner) ? childName : inner + "/" + childName;

            entries.Add(new VfsDirEntry
            {
                Name = childName,
                FullPath = new VfsPath("zip", null, null, null, null, archive + "|" + childInner),
                Size = isDir ? 0 : entry.Length,
                IsDirectory = isDir,
                ModificationTime = entry.LastWriteTime.LocalDateTime,
            });
        }

        return entries;
    }

    public bool DirectoryExists(VfsPath path) => File.Exists(SplitPath(path).archive);

    public bool FileExists(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        using var zip = ZipFile.OpenRead(archive);
        return zip.GetEntry(inner) != null;
    }

    public Stream OpenRead(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var zip = ZipFile.OpenRead(archive);
        var entry = zip.GetEntry(inner) ?? throw new FileNotFoundException($"Entry not found: {inner}");
        var ms = new MemoryStream((int)entry.Length);
        using var entryStream = entry.Open();
        entryStream.CopyTo(ms);
        zip.Dispose();
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    public VfsDirEntry Stat(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        using var zip = ZipFile.OpenRead(archive);
        var entry = zip.GetEntry(inner) ?? throw new FileNotFoundException($"Entry not found: {inner}");
        return new VfsDirEntry
        {
            Name = System.IO.Path.GetFileName(inner),
            FullPath = path,
            Size = entry.Length,
            ModificationTime = entry.LastWriteTime.LocalDateTime,
        };
    }

    // Write operations
    public Stream OpenWrite(VfsPath path) => throw new NotSupportedException("Use ZipArchive directly for writes");
    public Stream OpenAppend(VfsPath path) => throw new NotSupportedException();
    public void DeleteFile(VfsPath path) => throw new NotSupportedException();
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
}
