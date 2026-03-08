using System.Diagnostics;
using Mc.Core.Vfs;

namespace Mc.Vfs.Archives;

/// <summary>
/// VFS provider for 7-Zip (.7z) archives.
/// Uses the external 7z/7za CLI tool — no extra .NET packages required.
/// Degrades gracefully when 7z is not installed (CanHandle returns false).
/// </summary>
public sealed class SevenZipVfsProvider : IVfsProvider
{
    private static readonly string[] Candidates = ["7z", "7za", "7zz"];
    private string? _exe; // resolved at Initialize()

    public IReadOnlyList<string> Schemes => ["7z"];
    public string Name => "7-Zip Archive";

    public bool CanHandle(VfsPath path)
    {
        if (_exe == null) return false; // 7z not installed
        if (path.Scheme == "7z") return true;
        return path.Extension.Equals(".7z", StringComparison.OrdinalIgnoreCase);
    }

    public void Initialize()
    {
        foreach (var candidate in Candidates)
        {
            if (TryExec(candidate, "i", out _)) { _exe = candidate; break; }
        }
    }

    public void Dispose() { }

    // Path format: scheme="7z", path="/absolute/path/archive.7z|inner/file.txt"
    private static (string archive, string inner) SplitPath(VfsPath path)
    {
        var p   = path.Path;
        var sep = p.IndexOf('|');
        if (sep < 0) return (p, string.Empty);
        return (p[..sep], p[(sep + 1)..].TrimStart('/'));
    }

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        var entries = new List<VfsDirEntry>();
        var seen    = new HashSet<string>(StringComparer.Ordinal);

        // Always add ".." so the user can navigate out of the archive
        entries.Add(new VfsDirEntry
        {
            Name             = "..",
            FullPath         = ParentPath(archive, inner),
            IsDirectory      = true,
            ModificationTime = DateTime.MinValue,
        });

        foreach (var e in ReadEntries(archive))
        {
            // Compute path relative to the requested inner directory
            string rel;
            if (string.IsNullOrEmpty(inner))
            {
                rel = e.path;
            }
            else if (e.path.StartsWith(inner + "/", StringComparison.Ordinal))
            {
                rel = e.path[(inner.Length + 1)..];
            }
            else
            {
                continue;
            }

            var slash     = rel.IndexOf('/');
            var childName = slash >= 0 ? rel[..slash] : rel;
            if (string.IsNullOrEmpty(childName) || seen.Contains(childName)) continue;
            seen.Add(childName);

            bool isDir     = slash >= 0 || e.isDir;
            var  childInner = string.IsNullOrEmpty(inner) ? childName : inner + "/" + childName;

            entries.Add(new VfsDirEntry
            {
                Name             = childName,
                FullPath         = new VfsPath("7z", null, null, null, null, archive + "|" + childInner),
                Size             = isDir ? 0 : e.size,
                IsDirectory      = isDir,
                ModificationTime = e.modified,
            });
        }

        return entries;
    }

    public bool DirectoryExists(VfsPath path) => File.Exists(SplitPath(path).archive);

    public bool FileExists(VfsPath path)
    {
        var (archive, inner) = SplitPath(path);
        foreach (var e in ReadEntries(archive))
            if (e.path == inner) return true;
        return false;
    }

    public Stream OpenRead(VfsPath path)
    {
        EnsureInstalled();
        var (archive, inner) = SplitPath(path);
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        try
        {
            // 7z e extracts flat (no subdir structure); the file lands as Path.GetFileName(inner)
            Exec(_exe!, $"e {Q(archive)} {Q(inner)} -o{Q(tmpDir)} -y");

            var flat = Path.Combine(tmpDir, Path.GetFileName(inner));
            if (!File.Exists(flat))
            {
                var found = Directory.GetFiles(tmpDir);
                if (found.Length == 0)
                    throw new FileNotFoundException($"7z could not extract '{inner}' from '{archive}'");
                flat = found[0];
            }
            return new MemoryStream(File.ReadAllBytes(flat));
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    public VfsDirEntry Stat(VfsPath path)
    {
        EnsureInstalled();
        var (archive, inner) = SplitPath(path);
        foreach (var e in ReadEntries(archive))
        {
            if (e.path == inner)
            {
                return new VfsDirEntry
                {
                    Name             = Path.GetFileName(inner),
                    FullPath         = path,
                    Size             = e.size,
                    IsDirectory      = e.isDir,
                    ModificationTime = e.modified,
                };
            }
        }
        // Directory not listed explicitly — synthesise it
        return new VfsDirEntry
        {
            Name        = Path.GetFileName(inner.TrimEnd('/')),
            FullPath    = path,
            IsDirectory = true,
        };
    }

    // Write operations — .7z archives are read-only via VFS
    public Stream OpenWrite(VfsPath p)          => throw new NotSupportedException(".7z archives are read-only via VFS");
    public Stream OpenAppend(VfsPath p)         => throw new NotSupportedException();
    public void DeleteFile(VfsPath p)           => throw new NotSupportedException();
    public void CopyFile(VfsPath s, VfsPath d)  => throw new NotSupportedException();
    public void MoveFile(VfsPath s, VfsPath d)  => throw new NotSupportedException();
    public void CreateDirectory(VfsPath p)      => throw new NotSupportedException();
    public void DeleteDirectory(VfsPath p, bool r) => throw new NotSupportedException();
    public void CreateSymlink(VfsPath t, VfsPath l) => throw new NotSupportedException();
    public void SetPermissions(VfsPath p, UnixFileMode m) => throw new NotSupportedException();
    public void SetOwner(VfsPath p, int u, int g)   => throw new NotSupportedException();
    public void SetModificationTime(VfsPath p, DateTime t) => throw new NotSupportedException();

    public VfsPath GetParent(VfsPath path)                => path.Parent();
    public VfsPath Combine(VfsPath directory, string name) => directory.Combine(name);
    public bool IsAbsolute(VfsPath path)                   => true;

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void EnsureInstalled()
    {
        if (_exe == null)
            throw new InvalidOperationException(
                "7z/7za not found on PATH — install p7zip-full to browse .7z archives");
    }

    /// <summary>
    /// Run 7z with the given arguments, return stdout.
    /// Throws if the process cannot start.
    /// </summary>
    private static string Exec(string exe, string args)
    {
        using var proc = Process.Start(new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = false,
            UseShellExecute        = false,
        }) ?? throw new InvalidOperationException($"Failed to start '{exe}'");
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(60_000);
        return output;
    }

    private static bool TryExec(string exe, string args, out string output)
    {
        try { output = Exec(exe, args); return true; }
        catch { output = string.Empty; return false; }
    }

    /// <summary>
    /// Read all entries in the archive using "7z l -slt" (technical listing).
    /// Output format per entry (blocks separated by "----------"):
    ///   Path = ...
    ///   Size = ...
    ///   Attributes = D... (directory if starts with D)
    ///   Modified = YYYY-MM-DD HH:MM:SS
    /// </summary>
    private IEnumerable<(string path, long size, bool isDir, DateTime modified)> ReadEntries(string archive)
    {
        if (_exe == null) yield break;
        string output;
        if (!TryExec(_exe, $"l -slt {Q(archive)}", out output)) yield break;

        string? curPath  = null;
        long    curSize  = 0;
        bool    curIsDir = false;
        var     curMod   = DateTime.MinValue;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("----------", StringComparison.Ordinal))
            {
                if (curPath != null)
                    yield return (curPath, curSize, curIsDir, curMod);
                curPath = null; curSize = 0; curIsDir = false; curMod = DateTime.MinValue;
                continue;
            }

            var eq = line.IndexOf(" = ", StringComparison.Ordinal);
            if (eq < 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 3)..].Trim();

            switch (key)
            {
                case "Path":       curPath  = val; break;
                case "Size":       long.TryParse(val, out curSize); break;
                case "Attributes": curIsDir = val.Length > 0 && val[0] == 'D'; break;
                case "Modified":   DateTime.TryParse(val, out curMod); break;
            }
        }
        // Flush last entry (no trailing "----------" sometimes)
        if (curPath != null)
            yield return (curPath, curSize, curIsDir, curMod);
    }

    /// <summary>Shell-quote a path so it is safe to pass as a 7z argument.</summary>
    private static string Q(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

    /// <summary>
    /// Returns the VfsPath that ".." should navigate to from the given inner directory.
    /// At the archive root → exit to the local directory containing the archive file.
    /// Inside a subdirectory → go up one level within the same archive.
    /// </summary>
    private static VfsPath ParentPath(string archive, string inner)
    {
        if (string.IsNullOrEmpty(inner))
            return VfsPath.FromLocal(System.IO.Path.GetDirectoryName(archive) ?? "/");

        var lastSlash = inner.LastIndexOf('/');
        return lastSlash < 0
            ? new VfsPath("7z", null, null, null, null, archive + "|")
            : new VfsPath("7z", null, null, null, null, archive + "|" + inner[..lastSlash]);
    }
}
