using Mc.Core.Models;
using Mc.Core.Vfs;
using Mc.FileManager;
using Moq;
using Xunit;

namespace Mc.FileManager.Tests;

public sealed class DirectoryListingExtendedTests
{
    private static VfsDirEntry MakeEntry(string name, bool isDir = false, long size = 0,
        DateTime? modified = null, string? ext = null)
        => new()
        {
            Name = name,
            FullPath = VfsPath.FromLocal("/test/" + name),
            IsDirectory = isDir,
            Size = size,
            ModificationTime = modified ?? DateTime.Now,
        };

    private static DirectoryListing BuildListing(IReadOnlyList<VfsDirEntry> entries)
    {
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns(entries);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);

        var listing = new DirectoryListing(registry);
        listing.Load(VfsPath.FromLocal("/test"));
        return listing;
    }

    // --- MarkByPattern ---

    [Fact]
    public void MarkByPattern_GlobPattern_MarksMatchingFiles()
    {
        var listing = BuildListing([
            MakeEntry("report.pdf"),
            MakeEntry("notes.txt"),
            MakeEntry("image.png"),
        ]);

        listing.MarkByPattern("*.pdf");

        Assert.Equal(1, listing.MarkedCount);
        Assert.Equal("report.pdf", listing.GetMarkedEntries()[0].Name);
    }

    [Fact]
    public void MarkByPattern_MatchesMultiple()
    {
        var listing = BuildListing([
            MakeEntry("file1.txt"),
            MakeEntry("file2.txt"),
            MakeEntry("file3.log"),
        ]);

        listing.MarkByPattern("*.txt");

        Assert.Equal(2, listing.MarkedCount);
    }

    // --- InvertMarking ---

    [Fact]
    public void InvertMarking_FlipsAllMarks()
    {
        var listing = BuildListing([
            MakeEntry("a.txt"),
            MakeEntry("b.txt"),
            MakeEntry("c.txt"),
        ]);

        listing.MarkFile(0);  // mark first
        Assert.Equal(1, listing.MarkedCount);

        listing.InvertMarking();
        Assert.Equal(2, listing.MarkedCount);   // other two are now marked
        Assert.False(listing.Entries[0].IsMarked);
    }

    // --- GetMarkedEntries ---

    [Fact]
    public void GetMarkedEntries_ReturnsOnlyMarked()
    {
        var listing = BuildListing([
            MakeEntry("a.txt"),
            MakeEntry("b.txt"),
            MakeEntry("c.txt"),
        ]);

        listing.MarkFile(1);
        var marked = listing.GetMarkedEntries();
        Assert.Single(marked);
        Assert.Equal("b.txt", marked[0].Name);
    }

    // --- ChangeSortField ---

    [Fact]
    public void ChangeSortField_SameField_TogglesDescending()
    {
        var listing = BuildListing([
            MakeEntry("apple.txt"),
            MakeEntry("zebra.txt"),
        ]);

        listing.Sort.Field = SortField.Name;
        listing.ChangeSortField(SortField.Name);   // toggle
        Assert.True(listing.Sort.Descending);
        Assert.Equal("zebra.txt", listing.Entries[0].Name);

        listing.ChangeSortField(SortField.Name);   // toggle back
        Assert.False(listing.Sort.Descending);
        Assert.Equal("apple.txt", listing.Entries[0].Name);
    }

    [Fact]
    public void ChangeSortField_DifferentField_SetsFieldAndClearsDescending()
    {
        var listing = BuildListing([
            MakeEntry("a.txt", size: 200),
            MakeEntry("b.log", size: 50),
        ]);

        listing.Sort.Descending = true;
        listing.ChangeSortField(SortField.Size);
        Assert.Equal(SortField.Size, listing.Sort.Field);
        Assert.False(listing.Sort.Descending);
    }

    // --- Sort by extension ---

    [Fact]
    public void Sort_ByExtension_SortsAlphabetically()
    {
        var listing = BuildListing([
            MakeEntry("x.txt"),
            MakeEntry("y.log"),
            MakeEntry("z.csv"),
        ]);

        listing.Sort.DirectoriesFirst = false;
        listing.ChangeSortField(SortField.Extension);
        var names = listing.Entries.Select(e => e.Name).ToList();
        Assert.Equal("z.csv", names[0]);
        Assert.Equal("y.log", names[1]);
        Assert.Equal("x.txt", names[2]);
    }

    // --- Sort by modification time ---

    [Fact]
    public void Sort_ByModificationTime_SortsChronologically()
    {
        var baseTime = new DateTime(2024, 1, 1);
        var listing = BuildListing([
            MakeEntry("new.txt",  modified: baseTime.AddDays(10)),
            MakeEntry("old.txt",  modified: baseTime),
            MakeEntry("mid.txt",  modified: baseTime.AddDays(5)),
        ]);

        listing.Sort.DirectoriesFirst = false;
        listing.ChangeSortField(SortField.ModificationTime);
        Assert.Equal("old.txt", listing.Entries[0].Name);
        Assert.Equal("mid.txt", listing.Entries[1].Name);
        Assert.Equal("new.txt", listing.Entries[2].Name);
    }

    // --- TotalMarkedSize excludes directories ---

    [Fact]
    public void TotalMarkedSize_OnlyCountsFiles()
    {
        var listing = BuildListing([
            MakeEntry("docs", isDir: true),
            MakeEntry("file.txt", size: 500),
        ]);

        listing.MarkAll(true);
        // directory should be excluded from size
        Assert.Equal(500, listing.TotalMarkedSize);
    }

    // --- Version sort ---

    [Fact]
    public void Sort_VersionSort_NaturalOrder()
    {
        // Set VersionSort BEFORE loading so it applies during the first sort
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([
            MakeEntry("file10.txt"),
            MakeEntry("file2.txt"),
            MakeEntry("file1.txt"),
        ]);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);

        var listing = new DirectoryListing(registry);
        listing.Sort.VersionSort = true;
        listing.Sort.DirectoriesFirst = false;
        listing.Load(VfsPath.FromLocal("/test"));

        var names = listing.Entries.Select(e => e.Name).ToList();
        Assert.Equal("file1.txt", names[0]);
        Assert.Equal("file2.txt", names[1]);
        Assert.Equal("file10.txt", names[2]);
    }

    // --- Changed event ---

    [Fact]
    public void Load_FiresChangedEvent()
    {
        var mock = new Mock<IVfsProvider>();
        mock.Setup(p => p.CanHandle(It.IsAny<VfsPath>())).Returns(true);
        mock.Setup(p => p.ListDirectory(It.IsAny<VfsPath>())).Returns([MakeEntry("file.txt")]);
        mock.Setup(p => p.Initialize());
        mock.Setup(p => p.Dispose());

        var registry = new VfsRegistry();
        registry.Register(mock.Object);

        var listing = new DirectoryListing(registry);
        bool eventFired = false;
        listing.Changed += (_, _) => eventFired = true;
        listing.Load(VfsPath.FromLocal("/test"));

        Assert.True(eventFired);
    }
}
