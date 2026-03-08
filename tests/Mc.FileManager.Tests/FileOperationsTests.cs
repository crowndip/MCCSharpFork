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

    // --- Copy: rename (destination name changed) ---

    /// <summary>
    /// User copies a file from the left panel to the right panel and changes the destination
    /// name in the dialog. With a single source and a non-directory destination path,
    /// FileOperations must use the supplied path as the exact target name.
    /// </summary>
    [Fact]
    public async Task CopyAsync_SingleFile_ToNonExistentPath_OpenWriteCalledWithExactDestPath()
    {
        var (registry, mock) = BuildRegistry();
        var capturedDestPath = string.Empty;

        var entry = MakeFileEntry("file.txt", 5);
        mock.Setup(p => p.Stat(It.IsAny<VfsPath>())).Returns(entry);
        mock.Setup(p => p.OpenRead(It.IsAny<VfsPath>())).Returns(new MemoryStream(new byte[5]));
        mock.Setup(p => p.OpenWrite(It.IsAny<VfsPath>()))
            .Callback<VfsPath>(p => capturedDestPath = p.Path)
            .Returns(new MemoryStream());

        var ops = new FileOperations(registry);
        // /dst/renamed.txt does not exist on disk → used as exact target
        await ops.CopyAsync([VfsPath.FromLocal("/src/file.txt")],
                            VfsPath.FromLocal("/dst/renamed.txt"));

        Assert.Equal("/dst/renamed.txt", capturedDestPath);
    }

    // --- Copy: preserve attributes ---

    /// <summary>
    /// User copies a file and ticks "Preserve attributes" in the dialog.
    /// PreserveAttrs adds exactly one extra Stat call for the source compared to
    /// copying without preserve-attributes.
    /// </summary>
    [Fact]
    public async Task CopyAsync_WithPreserveAttributes_StatCalledMoreTimesForSource()
    {
        var entry = new VfsDirEntry
        {
            Name = "file.txt", FullPath = VfsPath.FromLocal("/src/file.txt"),
            IsDirectory = false, Size = 10, ModificationTime = DateTime.Now,
        };

        // --- without preserve-attributes ---
        int srcStatCountWithout = 0;
        var m1 = new Mock<IVfsProvider>();
        m1.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        m1.Setup(p => p.Stat(It.Is<VfsPath>(v => v.Path == "/src/file.txt")))
          .Callback(() => srcStatCountWithout++)
          .Returns(entry);
        m1.Setup(p => p.Stat(It.Is<VfsPath>(v => v.Path != "/src/file.txt"))).Returns(entry);
        m1.Setup(p => p.OpenRead(It.IsAny<VfsPath>())).Returns(() => new MemoryStream(new byte[10]));
        m1.Setup(p => p.OpenWrite(It.IsAny<VfsPath>())).Returns(() => new MemoryStream());
        var reg1 = new VfsRegistry();
        reg1.Register(m1.Object);
        await new FileOperations(reg1).CopyAsync(
            [VfsPath.FromLocal("/src/file.txt")], VfsPath.FromLocal("/dst"),
            preserveAttributes: false);

        // --- with preserve-attributes ---
        int srcStatCountWith = 0;
        var m2 = new Mock<IVfsProvider>();
        m2.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        m2.Setup(p => p.Stat(It.Is<VfsPath>(v => v.Path == "/src/file.txt")))
          .Callback(() => srcStatCountWith++)
          .Returns(entry);
        m2.Setup(p => p.Stat(It.Is<VfsPath>(v => v.Path != "/src/file.txt"))).Returns(entry);
        m2.Setup(p => p.OpenRead(It.IsAny<VfsPath>())).Returns(() => new MemoryStream(new byte[10]));
        m2.Setup(p => p.OpenWrite(It.IsAny<VfsPath>())).Returns(() => new MemoryStream());
        var reg2 = new VfsRegistry();
        reg2.Register(m2.Object);
        await new FileOperations(reg2).CopyAsync(
            [VfsPath.FromLocal("/src/file.txt")], VfsPath.FromLocal("/dst"),
            preserveAttributes: true);

        // Exactly one additional Stat call on the source when preserve-attributes is enabled
        Assert.Equal(srcStatCountWithout + 1, srcStatCountWith);
    }

    // --- Copy: directory with sub-folders ---

    /// <summary>
    /// User copies a directory (with children) from left to right panel.
    /// CopyAsync must create the destination directory and copy every child file.
    /// </summary>
    [Fact]
    public async Task CopyAsync_DirectorySource_CreatesDestDirAndCopiesAllChildren()
    {
        var (registry, mock) = BuildRegistry();

        var srcDir = MakeDirEntry("photos");
        mock.Setup(p => p.Stat(It.Is<VfsPath>(v => v.Path == "/src/photos"))).Returns(srcDir);
        // Dest stat throws → CopyDirectoryAsync calls CreateDirectory
        mock.Setup(p => p.Stat(It.Is<VfsPath>(v => v.Path != "/src/photos")))
            .Throws<IOException>();
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([
            MakeFileEntry("img1.jpg", 100),
            MakeFileEntry("img2.jpg", 200),
        ]);
        mock.Setup(p => p.OpenRead(It.IsAny<VfsPath>())).Returns(() => new MemoryStream(new byte[5]));
        mock.Setup(p => p.OpenWrite(It.IsAny<VfsPath>())).Returns(() => new MemoryStream());
        mock.Setup(p => p.CreateDirectory(It.IsAny<VfsPath>()));

        var ops = new FileOperations(registry);
        var result = await ops.CopyAsync([VfsPath.FromLocal("/src/photos")],
                                         VfsPath.FromLocal("/dst/photos"));

        Assert.Equal(OperationResult.Success, result);
        mock.Verify(p => p.CreateDirectory(It.IsAny<VfsPath>()), Times.Once);
        // Two child files → two OpenWrite calls
        mock.Verify(p => p.OpenWrite(It.IsAny<VfsPath>()), Times.Exactly(2));
    }

    /// <summary>
    /// User copies a directory with "Preserve attributes" enabled.
    /// PreserveAttrs adds one extra Stat call for the source directory path compared
    /// to a copy without preserve-attributes.
    /// </summary>
    [Fact]
    public async Task CopyAsync_DirectorySource_WithPreserveAttributes_StatCalledMoreTimesForDir()
    {
        static (VfsRegistry reg, Mock<IVfsProvider> m) BuildDirectoryMock()
        {
            var m   = new Mock<IVfsProvider>();
            var dir = new VfsDirEntry
            {
                Name = "photos", FullPath = VfsPath.FromLocal("/src/photos"),
                IsDirectory = true, Size = 0, ModificationTime = DateTime.Now,
            };
            m.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
            m.Setup(p => p.Initialize());
            m.Setup(p => p.Dispose());
            m.Setup(p => p.Stat(It.Is<VfsPath>(v => v.Path == "/src/photos"))).Returns(dir);
            m.Setup(p => p.Stat(It.Is<VfsPath>(v => v.Path != "/src/photos")))
             .Throws<IOException>();
            m.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([]);
            m.Setup(p => p.CreateDirectory(It.IsAny<VfsPath>()));

            var reg = new VfsRegistry();
            reg.Register(m.Object);
            return (reg, m);
        }

        var (reg1, mock1) = BuildDirectoryMock();
        await new FileOperations(reg1).CopyAsync(
            [VfsPath.FromLocal("/src/photos")], VfsPath.FromLocal("/dst/photos"),
            preserveAttributes: false);
        int withoutAttrs = mock1.Invocations.Count(i => i.Method.Name == "Stat"
                           && i.Arguments[0] is VfsPath vp && vp.Path == "/src/photos");

        var (reg2, mock2) = BuildDirectoryMock();
        await new FileOperations(reg2).CopyAsync(
            [VfsPath.FromLocal("/src/photos")], VfsPath.FromLocal("/dst/photos"),
            preserveAttributes: true);
        int withAttrs = mock2.Invocations.Count(i => i.Method.Name == "Stat"
                        && i.Arguments[0] is VfsPath vp && vp.Path == "/src/photos");

        Assert.Equal(withoutAttrs + 1, withAttrs);
    }

    // --- Move: rename (changed destination name) ---

    /// <summary>
    /// User moves a file from right to left panel and changes the destination name.
    /// With a single source and non-directory destination, MoveFile must use
    /// the supplied path verbatim as the target.
    /// </summary>
    [Fact]
    public async Task MoveAsync_SingleFile_ToNewPath_MoveCalledWithExactDest()
    {
        var (registry, mock) = BuildRegistry();
        var entry = MakeFileEntry("file.txt");
        mock.Setup(p => p.Stat(It.IsAny<VfsPath>())).Returns(entry);
        mock.Setup(p => p.MoveFile(It.IsAny<VfsPath>(), It.IsAny<VfsPath>()));

        var ops = new FileOperations(registry);
        await ops.MoveAsync([VfsPath.FromLocal("/src/file.txt")],
                            VfsPath.FromLocal("/dst/renamed.txt"));

        mock.Verify(p => p.MoveFile(
            It.Is<VfsPath>(v => v.Path == "/src/file.txt"),
            It.Is<VfsPath>(v => v.Path == "/dst/renamed.txt")),
            Times.Once);
    }

    // --- Move: multiple files ---

    /// <summary>
    /// User selects several files and moves them to an existing directory.
    /// Each file must be placed inside the destination directory.
    /// </summary>
    [Fact]
    public async Task MoveAsync_MultipleFiles_EachPlacedInsideExistingDestDir()
    {
        var (registry, mock) = BuildRegistry();

        var entries = new[] { MakeFileEntry("a.txt"), MakeFileEntry("b.txt") };
        int idx = 0;
        mock.Setup(p => p.Stat(It.IsAny<VfsPath>()))
            .Returns(() => entries[idx++ % entries.Length]);
        mock.Setup(p => p.MoveFile(It.IsAny<VfsPath>(), It.IsAny<VfsPath>()));

        var ops = new FileOperations(registry);
        // Path.GetTempPath() is guaranteed to exist on any OS
        var destDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var result  = await ops.MoveAsync(
            [VfsPath.FromLocal("/src/a.txt"), VfsPath.FromLocal("/src/b.txt")],
            VfsPath.FromLocal(destDir));

        Assert.Equal(OperationResult.Success, result);
        mock.Verify(p => p.MoveFile(It.IsAny<VfsPath>(), It.IsAny<VfsPath>()),
                    Times.Exactly(2));
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
