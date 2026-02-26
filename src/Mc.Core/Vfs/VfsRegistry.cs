namespace Mc.Core.Vfs;

/// <summary>
/// Registry of all registered IVfsProvider implementations.
/// Dispatches VfsPath instances to the correct provider.
/// Equivalent to the vfs registration system in the original C codebase.
/// </summary>
public sealed class VfsRegistry : IDisposable
{
    private readonly List<IVfsProvider> _providers = [];
    private bool _disposed;

    public void Register(IVfsProvider provider)
    {
        provider.Initialize();
        _providers.Add(provider);
    }

    public IVfsProvider Resolve(VfsPath path)
    {
        foreach (var p in _providers)
            if (p.CanHandle(path))
                return p;

        throw new NotSupportedException($"No VFS provider found for path: {path}");
    }

    // Convenience delegating methods

    public IReadOnlyList<VfsDirEntry> ListDirectory(VfsPath path)
        => Resolve(path).ListDirectory(path);

    public VfsDirEntry Stat(VfsPath path)
        => Resolve(path).Stat(path);

    public bool Exists(VfsPath path)
    {
        try
        {
            var provider = Resolve(path);
            return provider.FileExists(path) || provider.DirectoryExists(path);
        }
        catch
        {
            return false;
        }
    }

    public Stream OpenRead(VfsPath path)
        => Resolve(path).OpenRead(path);

    public Stream OpenWrite(VfsPath path)
        => Resolve(path).OpenWrite(path);

    public void CopyFile(VfsPath source, VfsPath destination)
    {
        var srcProvider = Resolve(source);
        var dstProvider = Resolve(destination);

        if (srcProvider == dstProvider)
        {
            srcProvider.CopyFile(source, destination);
            return;
        }

        // Cross-provider copy: stream through
        using var srcStream = srcProvider.OpenRead(source);
        using var dstStream = dstProvider.OpenWrite(destination);
        srcStream.CopyTo(dstStream);
    }

    public void MoveFile(VfsPath source, VfsPath destination)
    {
        var srcProvider = Resolve(source);
        var dstProvider = Resolve(destination);

        if (srcProvider == dstProvider)
        {
            srcProvider.MoveFile(source, destination);
            return;
        }

        CopyFile(source, destination);
        srcProvider.DeleteFile(source);
    }

    public void DeleteFile(VfsPath path)
        => Resolve(path).DeleteFile(path);

    public void CreateDirectory(VfsPath path)
        => Resolve(path).CreateDirectory(path);

    public void DeleteDirectory(VfsPath path, bool recursive)
        => Resolve(path).DeleteDirectory(path, recursive);

    public IReadOnlyList<IVfsProvider> GetAllProviders() => _providers.AsReadOnly();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var p in _providers)
            p.Dispose();
    }
}
