using Mc.Core.Models;
using Mc.Core.Vfs;

namespace Mc.FileManager;

/// <summary>
/// Business logic controller for the file manager.
/// Glues together DirectoryListing, FileOperations, and ExtensionRegistry.
/// Equivalent to src/filemanager/cmd.c in the original C codebase.
/// </summary>
public sealed class FileManagerController
{
    public DirectoryListing LeftPanel { get; }
    public DirectoryListing RightPanel { get; }
    public FileOperations Operations { get; }
    public ExtensionRegistry Extensions { get; }
    public HotlistManager Hotlist { get; }

    private DirectoryListing _activePanel;

    public DirectoryListing ActivePanel => _activePanel;
    public DirectoryListing InactivePanel => _activePanel == LeftPanel ? RightPanel : LeftPanel;

    public event EventHandler<string>? StatusMessage;
    public event EventHandler<Exception>? OperationError;

    public FileManagerController(VfsRegistry vfs)
    {
        LeftPanel = new DirectoryListing(vfs);
        RightPanel = new DirectoryListing(vfs);
        Operations = new FileOperations(vfs);
        Extensions = new ExtensionRegistry();
        Hotlist = new HotlistManager();
        _activePanel = LeftPanel;
    }

    public void Initialize(string leftPath, string rightPath)
    {
        LeftPanel.Load(VfsPath.FromLocal(leftPath));
        RightPanel.Load(VfsPath.FromLocal(rightPath));
    }

    public void SwitchPanel() => _activePanel = InactivePanel;

    public void SwapPanels()
    {
        var leftPath = LeftPanel.CurrentPath;
        var rightPath = RightPanel.CurrentPath;
        LeftPanel.Load(rightPath);
        RightPanel.Load(leftPath);
    }

    public void NavigateTo(VfsPath path) => ActivePanel.Load(path);

    public void NavigateUp()
    {
        var parent = ActivePanel.CurrentPath.Parent();
        if (parent != ActivePanel.CurrentPath)
            ActivePanel.Load(parent);
    }

    public void OpenEntry(FileEntry entry)
    {
        if (entry.IsDirectory || entry.IsParentDir)
        {
            ActivePanel.Load(entry.FullPath);
            return;
        }

        // Check extension registry
        var cmd = Extensions.GetOpenCommand(entry.Name);
        if (cmd != null)
        {
            var expanded = Extensions.ExpandCommand(cmd, entry.FullPath.Path);
            ExecuteShellCommand(expanded);
        }
        else
        {
            // Default: let caller decide (viewer or editor)
            StatusMessage?.Invoke(this, $"No handler for {entry.Name}");
        }
    }

    public async Task CopyMarkedAsync(IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
    {
        var sources = GetSourceEntries().Select(e => e.FullPath).ToList();
        if (sources.Count == 0) return;
        await Operations.CopyAsync(sources, InactivePanel.CurrentPath, progress: progress, ct: ct);
        InactivePanel.Reload();
    }

    public async Task MoveMarkedAsync(IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
    {
        var sources = GetSourceEntries().Select(e => e.FullPath).ToList();
        if (sources.Count == 0) return;
        await Operations.MoveAsync(sources, InactivePanel.CurrentPath, progress: progress, ct: ct);
        ActivePanel.Reload();
        InactivePanel.Reload();
    }

    public async Task DeleteMarkedAsync(IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
    {
        var sources = GetSourceEntries().Select(e => e.FullPath).ToList();
        if (sources.Count == 0) return;
        await Operations.DeleteAsync(sources, progress: progress, ct: ct);
        ActivePanel.Reload();
    }

    public void CreateDirectory(string name)
    {
        try
        {
            Operations.CreateDirectory(ActivePanel.CurrentPath, name);
            ActivePanel.Reload();
            StatusMessage?.Invoke(this, $"Created directory: {name}");
        }
        catch (Exception ex)
        {
            OperationError?.Invoke(this, ex);
        }
    }

    public void Rename(FileEntry entry, string newName)
    {
        try
        {
            Operations.Rename(entry.FullPath, newName);
            ActivePanel.Reload();
        }
        catch (Exception ex)
        {
            OperationError?.Invoke(this, ex);
        }
    }

    public void ToggleMark(int index) => ActivePanel.MarkFile(index);
    public void MarkAll() => ActivePanel.MarkAll(true);
    public void UnmarkAll() => ActivePanel.MarkAll(false);
    public void InvertMarking() => ActivePanel.InvertMarking();
    public void MarkByPattern(string pattern) => ActivePanel.MarkByPattern(pattern);
    public void Refresh() { ActivePanel.Reload(); InactivePanel.Reload(); }

    private IReadOnlyList<FileEntry> GetSourceEntries()
    {
        var marked = ActivePanel.GetMarkedEntries();
        if (marked.Count > 0) return marked;

        // If nothing marked, use current cursor item (caller's responsibility to provide)
        return [];
    }

    private static void ExecuteShellCommand(string command)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"",
            UseShellExecute = false,
        };
        System.Diagnostics.Process.Start(psi)?.WaitForExit();
    }
}
