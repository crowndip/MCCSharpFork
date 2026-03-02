using Mc.Core.Vfs;
using System.Text;

namespace Mc.Vfs.Archives;

/// <summary>
/// VFS provider for external filesystem scripts (extfs).
/// Invokes scripts from /usr/lib/mc/extfs.d/ that expose arbitrary content
/// (RPM packages, Debian packages, audio CDs, etc.) as navigable VFS directories.
///
/// Each extfs script must respond to these commands:
///   list &lt;archive&gt;        — print ls-l style listing to stdout
///   copyout &lt;archive&gt; &lt;file&gt; &lt;dest&gt;  — extract one file
///   run &lt;archive&gt; &lt;file&gt;  — execute a file inside the archive
///
/// Equivalent to src/vfs/extfs/ in the original C codebase.
/// </summary>
public sealed class ExtfsVfsProvider : IVfsProvider
{
    private static readonly string[] ScriptDirs =
    [
        "/usr/lib/mc/extfs.d",
        "/usr/libexec/mc/extfs.d",
        "/usr/share/mc/extfs.d",
    ];

    // Map extension → script path, populated at Initialize()
    private readonly Dictionary<string, string> _scripts = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Schemes => ["extfs"];
    public string Name => "External Filesystem";

    public bool CanHandle(VfsPath path)
    {
        if (path.Scheme == "extfs") return true;
        var ext = path.Extension.TrimStart('.');
        return _scripts.ContainsKey(ext);
    }

    public void Initialize()
    {
        foreach (var dir in ScriptDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var script in Directory.EnumerateFiles(dir))
            {
                // Script filename = the extension it handles (e.g. "rpm", "deb", "u7z")
                var ext = Path.GetFileName(script);
                if (!_scripts.ContainsKey(ext))
                    _scripts[ext] = script;
            }
        }
    }

    public void Dispose() { }

    // Path format: "extfs:///path/to/archive.rpm|inner/path"
    private static (string archive, string scriptExt, string inner) SplitPath(VfsPath path)
    {
        var p   = path.Path;
        var sep = p.IndexOf('|');
        string archive, inner;
        if (sep < 0) { archive = p; inner = "/"; }
        else         { archive = p[..sep]; inner = p[(sep + 1)..]; }
        var ext = Path.GetExtension(archive).TrimStart('.');
        return (archive, ext, inner);
    }

    private string? FindScript(string ext)
    {
        _scripts.TryGetValue(ext, out var s);
        return s;
    }

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
    {
        var (archive, ext, inner) = SplitPath(path);
        var script = FindScript(ext)
            ?? throw new NotSupportedException($"No extfs script for extension '.{ext}'");

        var listing = RunScript(script, "list", archive);
        return ParseListing(listing, archive, inner, path);
    }

    public bool DirectoryExists(VfsPath path)
    {
        try { return ListDirectory(path).Count >= 0; }
        catch { return false; }
    }

    public bool FileExists(VfsPath path)
    {
        var (archive, ext, inner) = SplitPath(path);
        var script = FindScript(ext);
        if (script == null) return false;
        var listing = RunScript(script, "list", archive);
        var wantName = inner.TrimStart('/');
        foreach (var line in listing.Split('\n'))
        {
            var (entry, ok) = ParseLsLine(line, archive, path);
            if (ok && entry.Name == wantName) return true;
        }
        return false;
    }

    public Stream OpenRead(VfsPath path)
    {
        var (archive, ext, inner) = SplitPath(path);
        var script = FindScript(ext)
            ?? throw new NotSupportedException($"No extfs script for extension '.{ext}'");

        var tmp = Path.GetTempFileName();
        try
        {
            RunScript(script, $"copyout {ShellQuote(archive)} {ShellQuote(inner.TrimStart('/'))} {ShellQuote(tmp)}");
            var data = File.ReadAllBytes(tmp);
            return new MemoryStream(data);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    public VfsDirEntry Stat(VfsPath path)
    {
        var (archive, ext, inner) = SplitPath(path);
        var script = FindScript(ext)
            ?? throw new NotSupportedException($"No extfs script for extension '.{ext}'");

        var wantName = inner.TrimStart('/');
        var listing  = RunScript(script, "list", archive);
        foreach (var line in listing.Split('\n'))
        {
            var (entry, ok) = ParseLsLine(line, archive, path);
            if (ok && entry.Name == wantName) return entry;
        }
        throw new FileNotFoundException($"Entry not found: {inner}");
    }

    // Write operations — extfs archives are read-only via VFS
    public Stream OpenWrite(VfsPath path)       => throw new NotSupportedException("extfs is read-only via VFS");
    public Stream OpenAppend(VfsPath path)      => throw new NotSupportedException();
    public void DeleteFile(VfsPath path)        => throw new NotSupportedException();
    public void CopyFile(VfsPath s, VfsPath d)  => throw new NotSupportedException();
    public void MoveFile(VfsPath s, VfsPath d)  => throw new NotSupportedException();
    public void CreateDirectory(VfsPath p)      => throw new NotSupportedException();
    public void DeleteDirectory(VfsPath p, bool r) => throw new NotSupportedException();
    public void CreateSymlink(VfsPath t, VfsPath l) => throw new NotSupportedException();
    public void SetPermissions(VfsPath p, UnixFileMode m) => throw new NotSupportedException();
    public void SetOwner(VfsPath p, int u, int g)  => throw new NotSupportedException();
    public void SetModificationTime(VfsPath p, DateTime t) => throw new NotSupportedException();

    public VfsPath GetParent(VfsPath path) => path.Parent();
    public VfsPath Combine(VfsPath directory, string name) => directory.Combine(name);
    public bool IsAbsolute(VfsPath path) => true;

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Run an extfs script command and return stdout as a string.</summary>
    private static string RunScript(string script, string command, string? archive = null)
    {
        var args = archive == null ? command : $"{command} {ShellQuote(archive)}";
        using var proc = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("/bin/sh", $"-c {ShellQuote(script)} {args}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = false,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            },
        };
        proc.Start();
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(30_000);
        return output;
    }

    private static string ShellQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    /// <summary>
    /// Parse the ls-l style listing produced by an extfs "list" command.
    /// Expected format (one entry per line):
    ///   -rwxr-xr-x   1 root root   12345 Jan  1  2024 filename
    /// Directories start with 'd', symlinks with 'l'.
    /// </summary>
    private static IReadOnlyList<VfsDirEntry> ParseListing(
        string listing, string archive, string inner, VfsPath basePath)
    {
        var entries = new List<VfsDirEntry>();
        inner = inner.TrimStart('/');
        foreach (var line in listing.Split('\n'))
        {
            var (entry, ok) = ParseLsLine(line, archive, basePath);
            if (!ok) continue;
            // Filter to direct children of 'inner'
            var name = entry.Name;
            string displayName;
            if (!string.IsNullOrEmpty(inner))
            {
                if (!name.StartsWith(inner + "/", StringComparison.Ordinal)) continue;
                var rel = name[(inner.Length + 1)..];
                if (rel.Contains('/')) continue; // deeper than one level
                displayName = rel;
            }
            else if (name.Contains('/'))
            {
                continue;
            }
            else
            {
                displayName = name;
            }
            entries.Add(new VfsDirEntry
            {
                Name          = displayName,
                FullPath      = entry.FullPath,
                Size          = entry.Size,
                IsDirectory   = entry.IsDirectory,
                IsSymlink     = entry.IsSymlink,
                SymlinkTarget = entry.SymlinkTarget,
            });
        }
        return entries;
    }

    private static (VfsDirEntry Entry, bool Ok) ParseLsLine(string line, string archive, VfsPath basePath)
    {
        // Expected: "drwxr-xr-x  2 root root   4096 Jan  1 12:00 dirname"
        if (string.IsNullOrWhiteSpace(line)) return (default!, false);
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 9) return (default!, false);

        var perms   = parts[0];
        // parts[1] = nlink, parts[2] = owner, parts[3] = group, parts[4] = size
        // parts[5..7] = date, parts[8..] = name (may have spaces for symlinks: "name -> target")
        if (!long.TryParse(parts[4], out var size)) return (default!, false);

        var name = string.Join(' ', parts[8..]);
        // Symlink: strip "-> target"
        bool isSymlink = perms[0] == 'l';
        string? symlinkTarget = null;
        if (isSymlink)
        {
            var arrow = name.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0) { symlinkTarget = name[(arrow + 4)..]; name = name[..arrow]; }
        }

        bool isDir = perms[0] == 'd';

        return (new VfsDirEntry
        {
            Name         = name,
            FullPath     = new VfsPath("extfs", null, null, null, null, archive + "|" + name),
            Size         = size,
            IsDirectory  = isDir,
            IsSymlink    = isSymlink,
            SymlinkTarget = symlinkTarget,
        }, true);
    }
}
