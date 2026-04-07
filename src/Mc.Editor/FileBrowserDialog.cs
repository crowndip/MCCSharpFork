using Terminal.Gui;

namespace Mc.Editor;

/// <summary>
/// Simple file browser dialog for selecting files.
/// </summary>
public sealed class FileBrowserDialog : Dialog
{
    private readonly ListView _fileList;
    private readonly TextField _pathField;
    private string _currentPath;
    private readonly List<string> _files = [];
    private readonly List<string> _dirs = [];

    public string? SelectedFile { get; private set; }

    public FileBrowserDialog(string initialPath)
    {
        Title = "Select File";
        Width = 70;
        Height = 22;

        _currentPath = string.IsNullOrEmpty(initialPath) || !File.Exists(initialPath)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(initialPath) ?? Directory.GetCurrentDirectory();

        // Path field
        Add(new Label { X = 1, Y = 1, Text = "Path:" });
        _pathField = new TextField
        {
            X = 7, Y = 1,
            Width = Dim.Fill(1),
            Text = _currentPath
        };
        _pathField.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Enter)
            {
                NavigateToPath(_pathField.Text?.ToString() ?? "");
                e.Handled = true;
            }
        };
        Add(_pathField);

        // File list
        _fileList = new ListView
        {
            X = 1, Y = 3,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4)
        };
        _fileList.OpenSelectedItem += (_, _) => SelectItem();
        Add(_fileList);

        // Buttons
        var ok = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting += (_, _) => SelectItem();
        cancel.Accepting += (_, _) => Application.RequestStop(this);
        AddButton(ok);
        AddButton(cancel);

        LoadDirectory(_currentPath);
    }

    private void LoadDirectory(string path)
    {
        try
        {
            _currentPath = Path.GetFullPath(path);
            _pathField.Text = _currentPath;

            _dirs.Clear();
            _files.Clear();

            // Add parent directory
            if (Directory.GetParent(_currentPath) != null)
                _dirs.Add("..");

            // Add directories
            _dirs.AddRange(Directory.GetDirectories(_currentPath)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n));

            // Add files
            _files.AddRange(Directory.GetFiles(_currentPath)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n));

            var items = _dirs.Select(d => $"[{d}]")
                .Concat(_files)
                .ToList();

            _fileList.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(items));
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Cannot access directory: {ex.Message}", "OK");
        }
    }

    private void NavigateToPath(string path)
    {
        if (Directory.Exists(path))
            LoadDirectory(path);
    }

    private void SelectItem()
    {
        if (_fileList.SelectedItem < 0) return;

        if (_fileList.SelectedItem < _dirs.Count)
        {
            // Directory selected
            var dir = _dirs[_fileList.SelectedItem];
            var newPath = dir == ".."
                ? Path.GetDirectoryName(_currentPath) ?? _currentPath
                : Path.Combine(_currentPath, dir);
            LoadDirectory(newPath);
        }
        else
        {
            // File selected
            var fileIdx = _fileList.SelectedItem - _dirs.Count;
            if (fileIdx >= 0 && fileIdx < _files.Count)
            {
                SelectedFile = Path.Combine(_currentPath, _files[fileIdx]);
                Application.RequestStop(this);
            }
        }
    }
}
