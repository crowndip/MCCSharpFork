using System.Text;

namespace Mc.Core.Vfs;

/// <summary>
/// Represents a path in the virtual filesystem.
/// Supports local paths and URLs like ftp://user@host/path, sftp://host/path,
/// and archive paths like /archive.tar#tar://inner/file.txt.
/// Mirrors vfs_path_t from the original C codebase.
/// </summary>
public sealed class VfsPath : IEquatable<VfsPath>
{
    public static readonly VfsPath Root = FromLocal("/");
    public static readonly VfsPath Empty = new(string.Empty, null, null, null, null, string.Empty, null);

    public string Scheme { get; }        // "local", "ftp", "sftp", "tar", "zip", "cpio", "shell"
    public string? Host { get; }
    public int? Port { get; }
    public string? User { get; }
    public string? Password { get; }
    public string Path { get; }          // The actual path component
    public string? Encoding { get; }     // e.g. "UTF-8" from #enc: prefix

    public bool IsLocal => Scheme is "local" or "";
    public bool IsRemote => !IsLocal;
    public bool IsRoot => Path is "/" or "";
    public string FileName => System.IO.Path.GetFileName(Path.TrimEnd('/'));
    public string Extension => System.IO.Path.GetExtension(FileName);

    public VfsPath(
        string scheme, string? host, int? port,
        string? user, string? password,
        string path, string? encoding = null)
    {
        Scheme = scheme;
        Host = host;
        Port = port;
        User = user;
        Password = password;
        Path = path;
        Encoding = encoding;
    }

    public static VfsPath FromLocal(string localPath)
        => new("local", null, null, null, null, localPath);

    public static VfsPath Parse(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return Empty;

        // Check for encoding hint: path#enc:UTF-8
        string? encoding = null;
        var encIdx = raw.LastIndexOf("#enc:", StringComparison.Ordinal);
        if (encIdx >= 0)
        {
            encoding = raw[(encIdx + 5)..];
            raw = raw[..encIdx];
        }

        // Try to parse as URI
        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            var scheme = uri.Scheme;
            if (scheme == "file")
                return new VfsPath("local", null, null, null, null, Uri.UnescapeDataString(uri.AbsolutePath), encoding);

            string? user = null, password = null;
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                user = Uri.UnescapeDataString(parts[0]);
                if (parts.Length > 1) password = Uri.UnescapeDataString(parts[1]);
            }

            int? port = uri.IsDefaultPort ? null : uri.Port;
            return new VfsPath(scheme, uri.Host, port, user, password, uri.AbsolutePath, encoding);
        }

        // Plain local path
        return new VfsPath("local", null, null, null, null, raw, encoding);
    }

    public VfsPath Parent()
    {
        var parent = System.IO.Path.GetDirectoryName(Path.TrimEnd('/'));
        if (string.IsNullOrEmpty(parent))
            parent = IsLocal ? "/" : string.Empty;
        return new VfsPath(Scheme, Host, Port, User, Password, parent, Encoding);
    }

    public VfsPath Combine(string name)
    {
        var sep = Path.EndsWith('/') ? string.Empty : "/";
        return new VfsPath(Scheme, Host, Port, User, Password, Path + sep + name, Encoding);
    }

    public VfsPath WithPath(string newPath)
        => new(Scheme, Host, Port, User, Password, newPath, Encoding);

    public override string ToString()
    {
        if (IsLocal)
            return Encoding != null ? $"{Path}#enc:{Encoding}" : Path;

        var sb = new StringBuilder();
        sb.Append(Scheme).Append("://");
        if (User != null)
        {
            sb.Append(Uri.EscapeDataString(User));
            if (Password != null) sb.Append(':').Append(Uri.EscapeDataString(Password));
            sb.Append('@');
        }
        sb.Append(Host);
        if (Port.HasValue) sb.Append(':').Append(Port.Value);
        sb.Append(Path);
        if (Encoding != null) sb.Append("#enc:").Append(Encoding);
        return sb.ToString();
    }

    public bool Equals(VfsPath? other)
        => other is not null && ToString() == other.ToString();

    public override bool Equals(object? obj) => Equals(obj as VfsPath);
    public override int GetHashCode() => ToString().GetHashCode();
    public static bool operator ==(VfsPath? a, VfsPath? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(VfsPath? a, VfsPath? b) => !(a == b);
}
