using Mc.Core.Vfs;
using System.Text;

namespace Mc.Vfs.Archives;

/// <summary>
/// VFS provider for CPIO archives (newc / SVR4 format, as used in RPM packages).
/// Parses the "070701" (no CRC) and "070702" (with CRC) newc CPIO stream format.
/// No external packages required — pure managed implementation.
/// Equivalent to src/vfs/cpio/ in the original C codebase.
/// </summary>
public sealed class CpioVfsProvider : IVfsProvider
{
    public IReadOnlyList<string> Schemes => ["cpio"];
    public string Name => "CPIO Archive";

    public bool CanHandle(VfsPath path)
    {
        if (path.Scheme == "cpio") return true;
        var ext = path.Extension.ToLowerInvariant();
        return ext is ".cpio" or ".rpm";
    }

    public void Initialize() { }
    public void Dispose() { }

    private static (string archive, string inner) SplitPath(VfsPath path)
    {
        var p   = path.Path;
        var sep = p.IndexOf('|');
        if (sep < 0) return (p, "/");
        return (p[..sep], p[(sep + 1)..]);
    }

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        inner = "/" + inner.Trim('/');

        var entries = new List<VfsDirEntry>();
        var seen    = new HashSet<string>(StringComparer.Ordinal);

        foreach (var e in ReadEntries(archive))
        {
            var entryPath = "/" + e.Name.TrimStart('/');
            if (!entryPath.StartsWith(inner == "/" ? "/" : inner + "/", StringComparison.Ordinal))
                continue;

            var rel = inner == "/" ? entryPath[1..] : entryPath[(inner.Length + 1)..];
            if (string.IsNullOrEmpty(rel)) continue;

            var slash     = rel.IndexOf('/');
            var childName = slash >= 0 ? rel[..slash] : rel;
            if (string.IsNullOrEmpty(childName) || seen.Contains(childName)) continue;
            seen.Add(childName);

            bool isDir      = slash >= 0 || e.IsDirectory;
            var  childInner = (inner == "/" ? "/" : inner) + childName;

            entries.Add(new VfsDirEntry
            {
                Name             = childName,
                FullPath         = new VfsPath("cpio", null, null, null, null, archive + "|" + childInner),
                Size             = isDir ? 0 : e.Size,
                IsDirectory      = isDir,
                ModificationTime = e.ModTime,
                Permissions      = e.Mode,
                OwnerUid         = e.Uid,
                OwnerGid         = e.Gid,
                Inode            = e.Ino,
            });
        }
        return entries;
    }

    public bool DirectoryExists(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        if (inner is "/" or "") return File.Exists(archive);
        var want = "/" + inner.Trim('/') + "/";
        foreach (var e in ReadEntries(archive))
            if (("/" + e.Name.TrimStart('/')).StartsWith(want, StringComparison.Ordinal))
                return true;
        return false;
    }

    public bool FileExists(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var want = "/" + inner.Trim('/');
        foreach (var e in ReadEntries(archive))
            if ("/" + e.Name.TrimStart('/') == want) return true;
        return false;
    }

    public Stream OpenRead(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var want = "/" + inner.Trim('/');
        using var stream = OpenPayload(archive);
        using var reader = new CpioReader(stream);
        while (reader.NextEntry() is { } e)
        {
            if ("/" + e.Name.TrimStart('/') == want)
            {
                var ms = new MemoryStream((int)e.Size);
                reader.CopyDataTo(ms, e.Size);
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
            reader.SkipData(e.Size);
        }
        throw new FileNotFoundException($"Entry not found in CPIO archive: {inner}");
    }

    public VfsDirEntry Stat(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var want = "/" + inner.Trim('/');
        foreach (var e in ReadEntries(archive))
        {
            if ("/" + e.Name.TrimStart('/') == want)
                return new VfsDirEntry
                {
                    Name             = Path.GetFileName(inner),
                    FullPath         = path,
                    Size             = e.Size,
                    IsDirectory      = e.IsDirectory,
                    ModificationTime = e.ModTime,
                    Permissions      = e.Mode,
                    OwnerUid         = e.Uid,
                    OwnerGid         = e.Gid,
                    Inode            = e.Ino,
                };
        }
        throw new FileNotFoundException($"Entry not found in CPIO archive: {inner}");
    }

    // Write operations — CPIO archives are read-only via VFS
    public Stream OpenWrite(VfsPath path)     => throw new NotSupportedException("CPIO archives are read-only via VFS");
    public Stream OpenAppend(VfsPath path)    => throw new NotSupportedException();
    public void DeleteFile(VfsPath path)      => throw new NotSupportedException();
    public void CopyFile(VfsPath s, VfsPath d) => throw new NotSupportedException();
    public void MoveFile(VfsPath s, VfsPath d) => throw new NotSupportedException();
    public void CreateDirectory(VfsPath p)    => throw new NotSupportedException();
    public void DeleteDirectory(VfsPath p, bool r) => throw new NotSupportedException();
    public void CreateSymlink(VfsPath t, VfsPath l) => throw new NotSupportedException();
    public void SetPermissions(VfsPath p, UnixFileMode m) => throw new NotSupportedException();
    public void SetOwner(VfsPath p, int u, int g) => throw new NotSupportedException();
    public void SetModificationTime(VfsPath p, DateTime t) => throw new NotSupportedException();

    public VfsPath GetParent(VfsPath path) => path.Parent();
    public VfsPath Combine(VfsPath directory, string name) => directory.Combine(name);
    public bool IsAbsolute(VfsPath path) => true;

    // -----------------------------------------------------------------------
    // CPIO header / entry model
    // -----------------------------------------------------------------------
    private sealed class CpioEntry
    {
        public string       Name        = string.Empty;
        public long         Size;
        public UnixFileMode Mode;
        public int          Uid;
        public int          Gid;
        public long         Ino;
        public DateTime     ModTime;
        public bool         IsDirectory => (((int)Mode >> 12) & 0xF) == 4; // S_ISDIR
    }

    // -----------------------------------------------------------------------
    // Enumerate all entries (reads into memory for simplicity)
    // -----------------------------------------------------------------------
    private static IEnumerable<CpioEntry> ReadEntries(string archivePath)
    {
        using var stream = OpenPayload(archivePath);
        using var reader = new CpioReader(stream);
        CpioEntry? entry;
        while ((entry = reader.NextEntry()) != null)
        {
            if (entry.Name == "TRAILER!!!") yield break;
            var ms = new MemoryStream((int)Math.Min(entry.Size, 0));
            reader.SkipData(entry.Size);
            yield return entry;
        }
    }

    /// <summary>
    /// For RPM files, skip the RPM header/lead and locate the CPIO payload.
    /// For plain .cpio files return the stream as-is.
    /// </summary>
    private static Stream OpenPayload(string archivePath)
    {
        var raw = File.OpenRead(archivePath);
        if (Path.GetExtension(archivePath).Equals(".rpm", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractRpmPayload(raw);
        }
        return raw;
    }

    /// <summary>
    /// RPM format: 4-byte magic + 96-byte lead + signature section + header section + gzip/bz2/xz payload.
    /// We skip to the payload by scanning for the gzip magic bytes 0x1F 0x8B.
    /// </summary>
    private static Stream ExtractRpmPayload(Stream raw)
    {
        // Scan for gzip (1F 8B) or xz (FD 37 7A 58 5A 00) magic in the first 512 KB
        var buf = new byte[512 * 1024];
        var n   = raw.Read(buf, 0, buf.Length);
        for (int i = 0; i < n - 2; i++)
        {
            if (buf[i] == 0x1F && buf[i + 1] == 0x8B) // gzip
            {
                var ms = new MemoryStream(buf, i, n - i);
                return new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
            }
        }
        // Fallback: treat whole file as CPIO
        raw.Seek(0, SeekOrigin.Begin);
        return raw;
    }

    // -----------------------------------------------------------------------
    // Low-level CPIO newc stream reader
    // -----------------------------------------------------------------------
    private sealed class CpioReader : IDisposable
    {
        private readonly Stream _s;
        private long _pos;

        public CpioReader(Stream s) { _s = s; }

        public CpioEntry? NextEntry()
        {
            // Align to 4-byte boundary
            Align(4);

            var magic = ReadBytes(6);
            if (magic == null) return null;
            var magicStr = Encoding.ASCII.GetString(magic);
            if (magicStr is not ("070701" or "070702"))
                return null; // not a newc CPIO entry

            // 13 × 8-char hex fields
            var ino      = ReadHex8();
            var mode     = ReadHex8();
            var uid      = ReadHex8();
            var gid      = ReadHex8();
            var nlink    = ReadHex8();
            var mtime    = ReadHex8();
            var filesize = ReadHex8();
            var devmaj   = ReadHex8();
            var devmin   = ReadHex8();
            var rdevmaj  = ReadHex8();
            var rdevmin  = ReadHex8();
            var namesize = ReadHex8();
            var check    = ReadHex8();

            // Read filename (namesize includes the NUL terminator)
            var nameBytes = ReadBytes((int)namesize);
            if (nameBytes == null) return null;
            var name = Encoding.UTF8.GetString(nameBytes, 0, (int)namesize - 1); // strip NUL

            // Align after header + name (header is 110 bytes, pad to 4)
            AlignAfter(110 + (int)namesize, 4);

            return new CpioEntry
            {
                Name    = name,
                Size    = filesize,
                Mode    = (UnixFileMode)mode,
                Uid     = (int)uid,
                Gid     = (int)gid,
                Ino     = ino,
                ModTime = DateTimeOffset.FromUnixTimeSeconds(mtime).LocalDateTime,
            };
        }

        public void CopyDataTo(Stream dest, long size)
        {
            var buf = new byte[65536];
            long rem = size;
            while (rem > 0)
            {
                int toRead = (int)Math.Min(rem, buf.Length);
                int n      = _s.Read(buf, 0, toRead);
                if (n <= 0) break;
                dest.Write(buf, 0, n);
                _pos += n;
                rem  -= n;
            }
            AlignAfter((int)size, 4);
        }

        public void SkipData(long size)
        {
            if (size <= 0) return;
            if (_s.CanSeek)
            {
                _s.Seek(size, SeekOrigin.Current);
                _pos += size;
            }
            else
            {
                var buf = new byte[65536];
                long rem = size;
                while (rem > 0)
                {
                    int n = _s.Read(buf, 0, (int)Math.Min(rem, buf.Length));
                    if (n <= 0) break;
                    _pos += n;
                    rem  -= n;
                }
            }
            AlignAfter((int)size, 4);
        }

        private void Align(int boundary)
        {
            long rem = _pos % boundary;
            if (rem == 0) return;
            var skip = boundary - (int)rem;
            if (_s.CanSeek) { _s.Seek(skip, SeekOrigin.Current); _pos += skip; }
            else { var buf = new byte[skip]; _s.Read(buf, 0, skip); _pos += skip; }
        }

        private void AlignAfter(long dataLen, int boundary)
        {
            long rem = dataLen % boundary;
            if (rem == 0) return;
            var skip = boundary - (int)rem;
            if (_s.CanSeek) { _s.Seek(skip, SeekOrigin.Current); _pos += skip; }
            else { var buf = new byte[skip]; _s.Read(buf, 0, skip); _pos += skip; }
        }

        private byte[]? ReadBytes(int count)
        {
            var buf  = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = _s.Read(buf, read, count - read);
                if (n <= 0) return null;
                read += n;
            }
            _pos += count;
            return buf;
        }

        private long ReadHex8()
        {
            var buf = ReadBytes(8);
            if (buf == null) return 0;
            return Convert.ToInt64(Encoding.ASCII.GetString(buf), 16);
        }

        public void Dispose() { }
    }
}
