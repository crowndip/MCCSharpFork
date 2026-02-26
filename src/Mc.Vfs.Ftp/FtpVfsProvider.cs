using System.Net;
using System.Text;
using Mc.Core.Vfs;

namespace Mc.Vfs.Ftp;

/// <summary>
/// VFS provider for FTP filesystems.
/// Uses System.Net.FtpWebRequest (built into .NET, no external packages).
/// Equivalent to src/vfs/ftpfs/ in the original C codebase.
/// </summary>
public sealed class FtpVfsProvider : IVfsProvider
{
    public IReadOnlyList<string> Schemes => ["ftp"];
    public string Name => "FTP Filesystem";

    public bool CanHandle(VfsPath path) => path.Scheme == "ftp";
    public void Initialize() { }
    public void Dispose() { }

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.ListDirectoryDetails);
        using var response = (FtpWebResponse)request.GetResponse();
        using var stream = response.GetResponseStream();
        using var reader = new StreamReader(stream);

        var entries = new List<VfsDirEntry>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var entry = ParseFtpListLine(line, path);
            if (entry != null) entries.Add(entry);
        }
        return entries;
    }

    public bool DirectoryExists(VfsPath path)
    {
        try { ListDirectory(path); return true; }
        catch { return false; }
    }

    public bool FileExists(VfsPath path)
    {
        try { Stat(path); return true; }
        catch { return false; }
    }

    public void CreateDirectory(VfsPath path)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.MakeDirectory);
        using var response = (FtpWebResponse)request.GetResponse();
    }

    public void DeleteDirectory(VfsPath path, bool recursive)
    {
        if (recursive)
        {
            foreach (var entry in ListDirectory(path))
            {
                if (entry.IsDirectory)
                    DeleteDirectory(entry.FullPath, true);
                else
                    DeleteFile(entry.FullPath);
            }
        }
        var request = CreateRequest(path, WebRequestMethods.Ftp.RemoveDirectory);
        using var response = (FtpWebResponse)request.GetResponse();
    }

    public Stream OpenRead(VfsPath path)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.DownloadFile);
        var response = (FtpWebResponse)request.GetResponse();
        return response.GetResponseStream();
    }

    public Stream OpenWrite(VfsPath path)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.UploadFile);
        return request.GetRequestStream();
    }

    public Stream OpenAppend(VfsPath path)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.AppendFile);
        return request.GetRequestStream();
    }

    public void DeleteFile(VfsPath path)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.DeleteFile);
        using var response = (FtpWebResponse)request.GetResponse();
    }

    public void CopyFile(VfsPath source, VfsPath destination)
    {
        using var src = OpenRead(source);
        using var dst = OpenWrite(destination);
        src.CopyTo(dst);
    }

    public void MoveFile(VfsPath source, VfsPath destination)
    {
        var request = CreateRequest(source, WebRequestMethods.Ftp.Rename);
        request.RenameTo = destination.Path;
        using var response = (FtpWebResponse)request.GetResponse();
    }

    public void CreateSymlink(VfsPath target, VfsPath link)
        => throw new NotSupportedException("FTP does not support symbolic links");

    public VfsDirEntry Stat(VfsPath path)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.GetFileSize);
        using var response = (FtpWebResponse)request.GetResponse();
        return new VfsDirEntry
        {
            Name = path.FileName,
            FullPath = path,
            Size = response.ContentLength,
            ModificationTime = response.LastModified,
        };
    }

    public void SetPermissions(VfsPath path, UnixFileMode mode)
    {
        // FTP SITE CHMOD command (not universally supported)
        var request = CreateRequest(path, WebRequestMethods.Ftp.PrintWorkingDirectory);
        request.Method = "SITE CHMOD " + Convert.ToString((int)mode, 8) + " " + path.Path;
        try { using var response = (FtpWebResponse)request.GetResponse(); }
        catch { }
    }

    public void SetOwner(VfsPath path, int uid, int gid)
        => throw new NotSupportedException("FTP does not support chown");

    public void SetModificationTime(VfsPath path, DateTime time)
    {
        // FTP MFMT command (not universally supported)
        var request = CreateRequest(path, WebRequestMethods.Ftp.PrintWorkingDirectory);
        request.Method = $"MFMT {time:yyyyMMddHHmmss} {path.Path}";
        try { using var response = (FtpWebResponse)request.GetResponse(); }
        catch { }
    }

    public VfsPath GetParent(VfsPath path) => path.Parent();
    public VfsPath Combine(VfsPath directory, string name) => directory.Combine(name);
    public bool IsAbsolute(VfsPath path) => path.Path.StartsWith('/');

    // --- Private ---

    private static FtpWebRequest CreateRequest(VfsPath path, string method)
    {
        var uri = path.ToString();
        var request = (FtpWebRequest)WebRequest.Create(uri);
        request.Method = method;
        if (path.User != null)
            request.Credentials = new NetworkCredential(path.User, path.Password);
        request.UsePassive = true;
        request.UseBinary = true;
        request.KeepAlive = true;
        return request;
    }

    private static VfsDirEntry? ParseFtpListLine(string line, VfsPath parent)
    {
        // Parse Unix-style ls -l output from FTP LIST command
        // Example: drwxr-xr-x    2 user     group        4096 Jan 01 12:00 directory
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 9) return null;

        var permissions = parts[0];
        if (!int.TryParse(parts[1], out _)) return null;

        bool isDir = permissions.StartsWith('d');
        bool isLink = permissions.StartsWith('l');

        if (!long.TryParse(parts[4], out var size)) size = 0;

        var namePart = string.Join(' ', parts[8..]);
        string name = namePart;
        string? linkTarget = null;

        if (isLink && namePart.Contains(" -> "))
        {
            var arrow = namePart.IndexOf(" -> ", StringComparison.Ordinal);
            name = namePart[..arrow];
            linkTarget = namePart[(arrow + 4)..];
        }

        if (name is "." or "..") return null;

        return new VfsDirEntry
        {
            Name = name,
            FullPath = parent.Combine(name),
            Size = size,
            IsDirectory = isDir,
            IsSymlink = isLink,
            SymlinkTarget = linkTarget,
            ModificationTime = DateTime.Now, // Simplified; full parse would use parts[5..7]
        };
    }
}
