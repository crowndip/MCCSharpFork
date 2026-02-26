using Mc.Core.Vfs;
using Mc.FileManager;
using Moq;
using Xunit;

namespace Mc.FileManager.Tests;

public sealed class FileOperationsTests
{
    private static VfsDirEntry MakeFileEntry(string name, long size = 100)
        => new()
        {
            Name = name,
            FullPath = VfsPath.FromLocal("/src/" + name),
            IsDirectory = false,
            Size = size,
            ModificationTime = DateTime.Now,
        };

    private static VfsDirEntry MakeDirEntry(string name)
        => new()
        {
            Name = name,
            FullPath = VfsPath.FromLocal("/src/" + name),
            IsDirectory = true,
            Size = 0,
            ModificationTime = DateTime.Now,
        };

    private static (VfsRegistry registry, Mock<IVfsProvider> mock) BuildRegistry()
    {
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);
        return (registry, mock);
    }

    // --- CopyAsync ---

    [Fact]
    public async Task CopyAsync_SingleFile_StreamsCopied()
    {
        var (registry, mock) = BuildRegistry();
        var srcContent = "Hello, World!"u8.ToArray();
        var destStream = new MemoryStream();

        var entry = MakeFileEntry("file.txt", srcContent.Length);
        mock.Setup(p => p.Stat(It.IsAny<VfsPath>())).Returns(entry);
        mock.Setup(p => p.OpenRead(It.IsAny<VfsPath>())).Returns(new MemoryStream(srcContent));
        mock.Setup(p => p.OpenWrite(It.IsAny<VfsPath>())).Returns(destStream);

        var ops = new FileOperations(registry);
        var sources = new[] { VfsPath.FromLocal("/src/file.txt") };
        var dest = VfsPath.FromLocal("/dst");

        var result = await ops.CopyAsync(sources, dest);

        Assert.Equal(OperationResult.Success, result);
        Assert.Equal(srcContent, destStream.ToArray());
    }

    [Fact]
    public async Task CopyAsync_MultipleFiles_AllCopied()
    {
        var (registry, mock) = BuildRegistry();

        var entries = new[]
        {
            MakeFileEntry("a.txt", 5),
            MakeFileEntry("b.txt", 5),
        };
        int statCall = 0;
        mock.Setup(p => p.Stat(It.IsAny<VfsPath>()))
            .Returns(() => entries[statCall < entries.Length ? statCall++ : statCall - 1]);
        mock.Setup(p => p.OpenRead(It.IsAny<VfsPath>())).Returns(() => new MemoryStream(new byte[5]));
        mock.Setup(p => p.OpenWrite(It.IsAny<VfsPath>())).Returns(() => new MemoryStream());

        var ops = new FileOperations(registry);
        var sources = entries.Select(e => e.FullPath).ToList();
        var result = await ops.CopyAsync(sources, VfsPath.FromLocal("/dst"));

        Assert.Equal(OperationResult.Success, result);
    }

    [Fact]
    public async Task CopyAsync_Cancelled_ThrowsOperationCancelled()
    {
        var (registry, mock) = BuildRegistry();
        var entry = MakeFileEntry("file.txt");
        mock.Setup(p => p.Stat(It.IsAny<VfsPath>())).Returns(entry);
        mock.Setup(p => p.OpenRead(It.IsAny<VfsPath>())).Returns(new MemoryStream(new byte[100]));
        mock.Setup(p => p.OpenWrite(It.IsAny<VfsPath>())).Returns(new MemoryStream());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var ops = new FileOperations(registry);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ops.CopyAsync([VfsPath.FromLocal("/src/file.txt")], VfsPath.FromLocal("/dst"), ct: cts.Token));
    }

    // --- MoveAsync ---

    [Fact]
    public async Task MoveAsync_CallsMoveFile()
    {
        var (registry, mock) = BuildRegistry();
        var entry = MakeFileEntry("file.txt");
        mock.Setup(p => p.Stat(It.IsAny<VfsPath>())).Returns(entry);
        mock.Setup(p => p.MoveFile(It.IsAny<VfsPath>(), It.IsAny<VfsPath>()));

        var ops = new FileOperations(registry);
        var result = await ops.MoveAsync([VfsPath.FromLocal("/src/file.txt")], VfsPath.FromLocal("/dst"));

        Assert.Equal(OperationResult.Success, result);
        mock.Verify(p => p.MoveFile(It.IsAny<VfsPath>(), It.IsAny<VfsPath>()), Times.Once);
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_File_CallsDeleteFile()
    {
        var (registry, mock) = BuildRegistry();
        var entry = MakeFileEntry("trash.txt");
        mock.Setup(p => p.Stat(It.IsAny<VfsPath>())).Returns(entry);
        mock.Setup(p => p.DeleteFile(It.IsAny<VfsPath>()));

        var ops = new FileOperations(registry);
        var result = await ops.DeleteAsync([VfsPath.FromLocal("/src/trash.txt")]);

        Assert.Equal(OperationResult.Success, result);
        mock.Verify(p => p.DeleteFile(It.IsAny<VfsPath>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Directory_CallsDeleteDirectory()
    {
        var (registry, mock) = BuildRegistry();
        var entry = MakeDirEntry("olddir");
        mock.Setup(p => p.Stat(It.IsAny<VfsPath>())).Returns(entry);
        mock.Setup(p => p.DeleteDirectory(It.IsAny<VfsPath>(), true));

        var ops = new FileOperations(registry);
        var result = await ops.DeleteAsync([VfsPath.FromLocal("/src/olddir")]);

        Assert.Equal(OperationResult.Success, result);
        mock.Verify(p => p.DeleteDirectory(It.IsAny<VfsPath>(), true), Times.Once);
    }

    // --- CreateDirectory ---

    [Fact]
    public void CreateDirectory_CallsVfsCreateDirectory()
    {
        var (registry, mock) = BuildRegistry();
        mock.Setup(p => p.CreateDirectory(It.IsAny<VfsPath>()));

        var ops = new FileOperations(registry);
        ops.CreateDirectory(VfsPath.FromLocal("/base"), "newdir");

        mock.Verify(p => p.CreateDirectory(It.Is<VfsPath>(v => v.Path.EndsWith("newdir"))), Times.Once);
    }

    // --- Rename ---

    [Fact]
    public void Rename_CallsMoveFileWithNewName()
    {
        var (registry, mock) = BuildRegistry();
        mock.Setup(p => p.MoveFile(It.IsAny<VfsPath>(), It.IsAny<VfsPath>()));

        var ops = new FileOperations(registry);
        ops.Rename(VfsPath.FromLocal("/base/old.txt"), "new.txt");

        mock.Verify(p => p.MoveFile(
            It.Is<VfsPath>(v => v.Path.EndsWith("old.txt")),
            It.Is<VfsPath>(v => v.Path.EndsWith("new.txt"))),
            Times.Once);
    }

    // --- OperationProgress ---

    [Fact]
    public void OperationProgress_Percent_ZeroWhenNoBytesTotal()
    {
        var prog = new OperationProgress { TotalBytes = 0, BytesDone = 0 };
        Assert.Equal(0, prog.Percent);
    }

    [Fact]
    public void OperationProgress_Percent_CorrectCalculation()
    {
        var prog = new OperationProgress { TotalBytes = 200, BytesDone = 100 };
        Assert.Equal(50, prog.Percent);
    }
}
