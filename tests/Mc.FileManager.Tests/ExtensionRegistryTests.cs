using Mc.FileManager;
using Xunit;

namespace Mc.FileManager.Tests;

public sealed class ExtensionRegistryTests : IDisposable
{
    private readonly ExtensionRegistry _registry = new();
    private readonly string _tempIni = Path.Combine(Path.GetTempPath(), $"mc_ext_{Guid.NewGuid()}.ini");

    public void Dispose()
    {
        if (File.Exists(_tempIni)) File.Delete(_tempIni);
    }

    // --- Find ---

    [Theory]
    [InlineData("photo.png")]
    [InlineData("photo.PNG")]   // case-insensitive
    [InlineData("photo.jpg")]
    [InlineData("photo.webp")]
    public void Find_ImageExtensions_ReturnsImageRule(string name)
    {
        var rule = _registry.Find(name);
        Assert.NotNull(rule);
        Assert.Equal("Image", rule.Description);
    }

    [Fact]
    public void Find_SvgFile_ReturnsSvgRule()
    {
        var rule = _registry.Find("icon.svg");
        Assert.NotNull(rule);
        Assert.Equal("SVG Image", rule.Description);
    }

    [Theory]
    [InlineData("main.cs")]
    [InlineData("main.CS")]   // case-insensitive
    [InlineData("module.vb")]
    [InlineData("lib.fs")]
    public void Find_CSharpSource_ReturnsDotNetRule(string name)
    {
        var rule = _registry.Find(name);
        Assert.NotNull(rule);
        Assert.Equal("C# / .NET source", rule.Description);
    }

    [Fact]
    public void Find_UnknownExtension_ReturnsNull()
        => Assert.Null(_registry.Find("binary.xyz123"));

    // --- GetOpenCommand ---

    [Fact]
    public void GetOpenCommand_ImageFile_ReturnsNonNull()
        => Assert.NotNull(_registry.GetOpenCommand("photo.png"));

    [Fact]
    public void GetOpenCommand_SourceFile_ReturnsNull()
        => Assert.Null(_registry.GetOpenCommand("program.cs"));   // no Open cmd for source files

    // --- ExpandCommand ---

    [Fact]
    public void ExpandCommand_PercentF_ReplacedWithQuotedFilePath()
    {
        var result = _registry.ExpandCommand("open %f", "/home/user/file.txt");
        Assert.Equal("open \"/home/user/file.txt\"", result);
    }

    [Fact]
    public void ExpandCommand_PercentD_ReplacedWithQuotedDirectory()
    {
        var result = _registry.ExpandCommand("cd %d", "/home/user/file.txt");
        var expectedDir = Path.GetDirectoryName("/home/user/file.txt")!;
        Assert.Equal($"cd \"{expectedDir}\"", result);
    }

    [Fact]
    public void ExpandCommand_Editor_Substituted()
    {
        var previous = Environment.GetEnvironmentVariable("EDITOR");
        try
        {
            Environment.SetEnvironmentVariable("EDITOR", "nano");
            var result = _registry.ExpandCommand("$EDITOR %f", "/tmp/file.txt");
            Assert.Equal("nano \"/tmp/file.txt\"", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EDITOR", previous);
        }
    }

    [Fact]
    public void ExpandCommand_EditorNotSet_FallsBackToVi()
    {
        var previous = Environment.GetEnvironmentVariable("EDITOR");
        try
        {
            Environment.SetEnvironmentVariable("EDITOR", null);
            var result = _registry.ExpandCommand("$EDITOR %f", "/tmp/file.txt");
            Assert.StartsWith("vi ", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EDITOR", previous);
        }
    }

    // --- LoadFromFile ---

    [Fact]
    public void LoadFromFile_AddsCustomRule()
    {
        File.WriteAllText(_tempIni, "[*.foobar]\nOpen=foobar-viewer %f\nDescription=FooBar Files\n");
        _registry.LoadFromFile(_tempIni);
        var rule = _registry.Find("document.foobar");
        Assert.NotNull(rule);
        Assert.Equal("FooBar Files", rule.Description);
        Assert.Equal("foobar-viewer %f", rule.OpenCommand);
    }

    [Fact]
    public void LoadFromFile_NonExistentFile_DoesNotThrow()
        => _registry.LoadFromFile("/tmp/does_not_exist_xyz.ini");   // should silently succeed
}
