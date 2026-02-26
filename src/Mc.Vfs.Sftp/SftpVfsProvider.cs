using Mc.Core.Vfs;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace Mc.Vfs.Sftp;

/// <summary>
/// VFS provider for SFTP (SSH File Transfer Protocol).
/// Uses SSH.NET (Renci.SshNet) package — the only non-built-in dependency.
/// Equivalent to src/vfs/sftpfs/ in the original C codebase.
/// </summary>
public sealed class SftpVfsProvider : IVfsProvider, IDisposable
{
    public IReadOnlyList<string> Schemes => ["sftp"];
    public string Name => "SFTP Filesystem";

    private readonly Dictionary<string, SftpClient> _connections = [];

    public bool CanHandle(VfsPath path) => path.Scheme == "sftp";
    public void Initialize() { }

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
    {
        var client = GetClient(path);
        var entries = new List<VfsDirEntry>();

        // Add parent directory entry
        if (path.Path != "/")
            entries.Add(new VfsDirEntry
            {
                Name = "..",
                FullPath = path.Parent(),
                IsDirectory = true,
                ModificationTime = DateTime.MinValue,
            });

        foreach (var file in client.ListDirectory(path.Path))
        {
            if (file.Name is "." or "..") continue;
            entries.Add(FromSftpFile(file, path));
        }

        return entries;
    }

    public bool DirectoryExists(VfsPath path)
        => GetClient(path).Exists(path.Path) &&
           GetClient(path).GetAttributes(path.Path).IsDirectory;

    public bool FileExists(VfsPath path)
        => GetClient(path).Exists(path.Path) &&
           !GetClient(path).GetAttributes(path.Path).IsDirectory;

    public void CreateDirectory(VfsPath path)
        => GetClient(path).CreateDirectory(path.Path);

    public void DeleteDirectory(VfsPath path, bool recursive)
    {
        if (recursive)
        {
            foreach (var f in ListDirectory(path))
            {
                if (f.Name == "..") continue;
                if (f.IsDirectory) DeleteDirectory(f.FullPath, true);
                else DeleteFile(f.FullPath);
            }
        }
        GetClient(path).DeleteDirectory(path.Path);
    }

    public Stream OpenRead(VfsPath path)
        => GetClient(path).OpenRead(path.Path);

    public Stream OpenWrite(VfsPath path)
        => GetClient(path).OpenWrite(path.Path);

    public Stream OpenAppend(VfsPath path)
        => GetClient(path).Open(path.Path, System.IO.FileMode.Append);

    public void DeleteFile(VfsPath path)
        => GetClient(path).Delete(path.Path);

    public void CopyFile(VfsPath source, VfsPath destination)
    {
        using var src = OpenRead(source);
        using var dst = OpenWrite(destination);
        src.CopyTo(dst);
    }

    public void MoveFile(VfsPath source, VfsPath destination)
    {
        GetClient(source).RenameFile(source.Path, destination.Path);
    }

    public void CreateSymlink(VfsPath target, VfsPath link)
        => GetClient(link).SymbolicLink(target.Path, link.Path);

    public VfsDirEntry Stat(VfsPath path)
    {
        var client = GetClient(path);
        var attrs = client.GetAttributes(path.Path);
        return new VfsDirEntry
        {
            Name = path.FileName,
            FullPath = path,
            Size = attrs.Size,
            IsDirectory = attrs.IsDirectory,
            IsSymlink = attrs.IsSymbolicLink,
            ModificationTime = attrs.LastWriteTime,
            AccessTime = attrs.LastAccessTime,
            Permissions = MapPermissions(attrs),
            OwnerUid = (int)attrs.UserId,
            OwnerGid = (int)attrs.GroupId,
        };
    }

    public void SetPermissions(VfsPath path, UnixFileMode mode)
    {
        var client = GetClient(path);
        var attrs = client.GetAttributes(path.Path);
        // Map UnixFileMode to SftpFileAttributes permissions
        attrs.SetPermissions((short)mode);
        client.SetAttributes(path.Path, attrs);
    }

    public void SetOwner(VfsPath path, int uid, int gid)
    {
        var client = GetClient(path);
        var attrs = client.GetAttributes(path.Path);
        attrs.UserId = uid;
        attrs.GroupId = gid;
        client.SetAttributes(path.Path, attrs);
    }

    public void SetModificationTime(VfsPath path, DateTime time)
    {
        var client = GetClient(path);
        var attrs = client.GetAttributes(path.Path);
        attrs.LastWriteTime = time;
        client.SetAttributes(path.Path, attrs);
    }

    public VfsPath GetParent(VfsPath path) => path.Parent();
    public VfsPath Combine(VfsPath directory, string name) => directory.Combine(name);
    public bool IsAbsolute(VfsPath path) => path.Path.StartsWith('/');

    public void Dispose()
    {
        foreach (var c in _connections.Values)
        {
            c.Disconnect();
            c.Dispose();
        }
        _connections.Clear();
    }

    // --- Private helpers ---

    private SftpClient GetClient(VfsPath path)
    {
        var key = $"{path.User}@{path.Host}:{path.Port ?? 22}";
        if (_connections.TryGetValue(key, out var existing) && existing.IsConnected)
            return existing;

        ConnectionInfo connInfo;
        if (!string.IsNullOrEmpty(path.Password))
        {
            connInfo = new ConnectionInfo(path.Host!, path.Port ?? 22, path.User ?? "anonymous",
                new PasswordAuthenticationMethod(path.User ?? "anonymous", path.Password));
        }
        else
        {
            // Try key-based auth
            var keyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_rsa");
            if (File.Exists(keyFile))
                connInfo = new ConnectionInfo(path.Host!, path.Port ?? 22, path.User ?? Environment.UserName,
                    new PrivateKeyAuthenticationMethod(path.User ?? Environment.UserName,
                        new PrivateKeyFile(keyFile)));
            else
                connInfo = new ConnectionInfo(path.Host!, path.Port ?? 22, path.User ?? Environment.UserName,
                    new KeyboardInteractiveAuthenticationMethod(path.User ?? Environment.UserName));
        }

        var client = new SftpClient(connInfo);
        client.Connect();
        _connections[key] = client;
        return client;
    }

    private static VfsDirEntry FromSftpFile(ISftpFile file, VfsPath parent)
        => new()
        {
            Name = file.Name,
            FullPath = parent.Combine(file.Name),
            Size = file.Length,
            IsDirectory = file.IsDirectory,
            IsSymlink = file.IsSymbolicLink,
            ModificationTime = file.LastWriteTime,
            AccessTime = file.LastAccessTime,
            Permissions = MapPermissions(file.Attributes),
            OwnerUid = (int)file.Attributes.UserId,
            OwnerGid = (int)file.Attributes.GroupId,
            IsExecutable = (file.Attributes.OwnerCanExecute || file.Attributes.GroupCanExecute || file.Attributes.OthersCanExecute) && !file.IsDirectory,
        };

    private static UnixFileMode MapPermissions(SftpFileAttributes attrs)
    {
        var mode = UnixFileMode.None;
        if (attrs.OwnerCanRead) mode |= UnixFileMode.UserRead;
        if (attrs.OwnerCanWrite) mode |= UnixFileMode.UserWrite;
        if (attrs.OwnerCanExecute) mode |= UnixFileMode.UserExecute;
        if (attrs.GroupCanRead) mode |= UnixFileMode.GroupRead;
        if (attrs.GroupCanWrite) mode |= UnixFileMode.GroupWrite;
        if (attrs.GroupCanExecute) mode |= UnixFileMode.GroupExecute;
        if (attrs.OthersCanRead) mode |= UnixFileMode.OtherRead;
        if (attrs.OthersCanWrite) mode |= UnixFileMode.OtherWrite;
        if (attrs.OthersCanExecute) mode |= UnixFileMode.OtherExecute;
        return mode;
    }
}
