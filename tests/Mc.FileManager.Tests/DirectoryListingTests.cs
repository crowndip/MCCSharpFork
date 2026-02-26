using Mc.Core.Models;
using Mc.Core.Vfs;
using Mc.FileManager;
using Moq;
using System.IO;
using Xunit;

namespace Mc.FileManager.Tests;

public sealed class DirectoryListingTests
{
    private static VfsDirEntry MakeEntry(string name, bool isDir = false, long size = 0)
        => new()
        {
            Name = name,
            FullPath = VfsPath.FromLocal("/test/" + name),
            IsDirectory = isDir,
            Size = size,
            ModificationTime = DateTime.Now,
        };

    [Fact]
    public void Load_CorrectlySeparatesFilesAndDirs()
    {
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.Schemes).Returns(["local"]);
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([
            MakeEntry("..", isDir: true),
            MakeEntry("docs", isDir: true),
            MakeEntry("images", isDir: true),
            MakeEntry("readme.txt", size: 100),
            MakeEntry("main.cs", size: 2048),
        ]);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);

        var listing = new DirectoryListing(registry);
        listing.Load(VfsPath.FromLocal("/test"));

        // ".." + 2 dirs + 2 files = 5 entries
        Assert.Equal(5, listing.Entries.Count);
        Assert.Equal(2, listing.TotalFiles);
        Assert.Equal(2, listing.TotalDirectories);
    }

    [Fact]
    public void Sort_ByName_SortsAlphabetically()
    {
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([
            MakeEntry("zebra.txt"),
            MakeEntry("apple.txt"),
            MakeEntry("mango.txt"),
        ]);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);

        var listing = new DirectoryListing(registry);
        listing.Sort.Field = SortField.Name;
        listing.Sort.CaseSensitive = false;
        listing.Load(VfsPath.FromLocal("/test"));

        var names = listing.Entries.Select(e => e.Name).ToList();
        Assert.Equal("apple.txt", names[0]);
        Assert.Equal("mango.txt", names[1]);
        Assert.Equal("zebra.txt", names[2]);
    }

    [Fact]
    public void Sort_BySize_SortsSmallestFirst()
    {
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([
            MakeEntry("big.bin", size: 1000000),
            MakeEntry("tiny.txt", size: 10),
            MakeEntry("medium.dat", size: 5000),
        ]);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);

        var listing = new DirectoryListing(registry);
        listing.Sort.Field = SortField.Size;
        listing.Sort.DirectoriesFirst = false;
        listing.Load(VfsPath.FromLocal("/test"));

        Assert.Equal("tiny.txt", listing.Entries[0].Name);
        Assert.Equal("big.bin", listing.Entries[2].Name);
    }

    [Fact]
    public void Filter_HiddenFiles_ExcludesHidden()
    {
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([
            MakeEntry(".hidden"),
            MakeEntry("visible.txt"),
        ]);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);

        var listing = new DirectoryListing(registry);
        listing.Filter.ShowHidden = false;
        listing.Load(VfsPath.FromLocal("/test"));

        Assert.Single(listing.Entries);
        Assert.Equal("visible.txt", listing.Entries[0].Name);
    }

    [Fact]
    public void MarkFile_TogglesMarkState()
    {
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([
            MakeEntry("a.txt"),
            MakeEntry("b.txt"),
        ]);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);

        var listing = new DirectoryListing(registry);
        listing.Load(VfsPath.FromLocal("/test"));

        listing.MarkFile(0);
        Assert.True(listing.Entries[0].IsMarked);
        Assert.Equal(1, listing.MarkedCount);

        listing.MarkFile(0);
        Assert.False(listing.Entries[0].IsMarked);
        Assert.Equal(0, listing.MarkedCount);
    }

    [Fact]
    public void MarkAll_MarksEveryNonParentEntry()
    {
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([
            MakeEntry("..", isDir: true),
            MakeEntry("a.txt"),
            MakeEntry("b.txt"),
        ]);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);

        var listing = new DirectoryListing(registry);
        listing.Load(VfsPath.FromLocal("/test"));
        listing.MarkAll(true);

        Assert.Equal(2, listing.MarkedCount); // .. is excluded
    }
}
