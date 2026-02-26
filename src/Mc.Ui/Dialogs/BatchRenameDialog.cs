using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Batch-rename dialog with placeholder engine.
/// Ported from MCCompanion's BatchRenameCommand.
///
/// Placeholders supported in the template:
///   [N]  – original file name (without extension)
///   [E]  – original extension (without dot)
///   [C]  – running counter (1-based)
///   [Y]  – year  (4 digit)  of file modification time
///   [M]  – month (2 digit)  of file modification time
///   [D]  – day   (2 digit)  of file modification time
///
/// Search/replace and case conversions are applied after placeholder expansion.
/// </summary>
public sealed class BatchRenameDialog : IDisposable
{
    private readonly List<string> _files;
    private readonly string _directory;

    // Controls
    private readonly Dialog  _d;
    private readonly TextField _tfTemplate;
    private readonly TextField _tfSearch;
    private readonly TextField _tfReplace;
    private readonly CheckBox  _chkRegex;
    private readonly RadioGroup _rgCase;
    private readonly ListView  _lvPreview;

    private readonly ObservableCollection<string> _previewItems = [];

    private BatchRenameDialog(IEnumerable<string> filePaths)
    {
        _files = filePaths.OrderBy(f => f).ToList();
        _directory = _files.Count > 0 ? Path.GetDirectoryName(_files[0])! : "";

        _d = new Dialog
        {
            Title  = "Batch Rename",
            Width  = Dim.Fill() - 4,
            Height = Dim.Fill() - 2,
            ColorScheme = McTheme.Dialog,
        };

        // Template row
        _d.Add(new Label { X = 1, Y = 1, Text = "Name template:" });
        _tfTemplate = new TextField
        {
            X = 16, Y = 1, Width = Dim.Fill(2),
            Text = "[N]",
            ColorScheme = McTheme.Panel,
        };
        _d.Add(_tfTemplate);

        // Search/Replace
        _d.Add(new Label { X = 1, Y = 3, Text = "Search:" });
        _tfSearch = new TextField
        {
            X = 10, Y = 3, Width = Dim.Percent(40),
            ColorScheme = McTheme.Panel,
        };
        _d.Add(_tfSearch);

        _d.Add(new Label { X = Pos.Right(_tfSearch) + 1, Y = 3, Text = "Replace:" });
        _tfReplace = new TextField
        {
            X = Pos.Right(_tfSearch) + 10, Y = 3, Width = Dim.Fill(2),
            ColorScheme = McTheme.Panel,
        };
        _d.Add(_tfReplace);

        _chkRegex = new CheckBox
        {
            X = 1, Y = 4, Text = "Regex search",
            CheckedState = CheckState.UnChecked,
        };
        _d.Add(_chkRegex);

        // Case
        _d.Add(new Label { X = 1, Y = 6, Text = "Case:" });
        _rgCase = new RadioGroup
        {
            X = 8, Y = 6,
            RadioLabels = ["As-is", "UPPER", "lower", "Title"],
        };
        _d.Add(_rgCase);

        // Preview header
        _d.Add(new Label { X = 1, Y = 8, Text = "Preview  (original → new):" });
        _lvPreview = new ListView
        {
            X = 1, Y = 9,
            Width = Dim.Fill(1), Height = Dim.Fill(3),
            ColorScheme = McTheme.Panel,
        };
        _lvPreview.SetSource(_previewItems);
        _d.Add(_lvPreview);

        // Buttons
        var btnRename = new Button { X = Pos.Center() - 12, Y = Pos.Bottom(_lvPreview), Text = "Rename", IsDefault = true };
        var btnCancel = new Button { X = Pos.Center() + 3,  Y = Pos.Bottom(_lvPreview), Text = "Cancel" };
        btnRename.Accepting += (_, _) => DoRename();
        btnCancel.Accepting += (_, _) => Application.RequestStop(_d);
        _d.AddButton(btnRename);
        _d.AddButton(btnCancel);

        // Live preview on any change
        _tfTemplate.TextChanged  += (_, _) => UpdatePreview();
        _tfSearch.TextChanged    += (_, _) => UpdatePreview();
        _tfReplace.TextChanged   += (_, _) => UpdatePreview();
        _chkRegex.CheckedStateChanged  += (_, _) => UpdatePreview();
        _rgCase.SelectedItemChanged    += (_, _) => UpdatePreview();

        UpdatePreview();
    }

    public void Dispose() => _d.Dispose();

    /// <summary>Show the dialog. Returns number of files successfully renamed.</summary>
    public static int Show(IEnumerable<string> filePaths)
    {
        using var dlg = new BatchRenameDialog(filePaths);
        Application.Run(dlg._d);
        return dlg._renamedCount;
    }

    private int _renamedCount;

    private void UpdatePreview()
    {
        _previewItems.Clear();
        for (int i = 0; i < _files.Count; i++)
        {
            var orig    = Path.GetFileName(_files[i]);
            var newName = BuildNewName(orig, i + 1);
            _previewItems.Add($"{orig}  →  {newName}");
        }
    }

    private string BuildNewName(string original, int counter)
    {
        var nameNoExt = Path.GetFileNameWithoutExtension(original);
        var ext       = Path.GetExtension(original).TrimStart('.');
        var mtime     = File.GetLastWriteTime(_files[counter - 1]);

        var template  = _tfTemplate.Text?.ToString() ?? "[N]";

        var result = template
            .Replace("[N]", nameNoExt)
            .Replace("[E]", ext)
            .Replace("[C]", counter.ToString())
            .Replace("[Y]", mtime.Year.ToString("D4"))
            .Replace("[M]", mtime.Month.ToString("D2"))
            .Replace("[D]", mtime.Day.ToString("D2"));

        // Search/replace
        var search  = _tfSearch.Text?.ToString() ?? "";
        var replace = _tfReplace.Text?.ToString() ?? "";
        if (!string.IsNullOrEmpty(search))
        {
            bool useRegex = _chkRegex.CheckedState == CheckState.Checked;
            result = useRegex
                ? Regex.Replace(result, search, replace)
                : result.Replace(search, replace);
        }

        // Case conversion
        result = _rgCase.SelectedItem switch
        {
            1 => result.ToUpperInvariant(),
            2 => result.ToLowerInvariant(),
            3 => ToTitleCase(result),
            _ => result,
        };

        // Re-attach extension if the template didn't include [E] and original had one
        if (!string.IsNullOrEmpty(ext) && !template.Contains("[E]") && !result.Contains('.'))
            result = result + "." + ext;

        return result;
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var chars = s.ToLowerInvariant().ToCharArray();
        bool capitalize = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]) || chars[i] == '_' || chars[i] == '-')
            {
                capitalize = true;
            }
            else if (capitalize)
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
                capitalize = false;
            }
        }
        return new string(chars);
    }

    private void DoRename()
    {
        var errors = new List<string>();
        for (int i = 0; i < _files.Count; i++)
        {
            var orig    = Path.GetFileName(_files[i]);
            var newName = BuildNewName(orig, i + 1);
            if (orig == newName) continue;
            var destPath = Path.Combine(_directory, newName);
            try
            {
                File.Move(_files[i], destPath);
                _renamedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{orig}: {ex.Message}");
            }
        }

        Application.RequestStop(_d);

        if (errors.Count > 0)
            MessageDialog.Error("Batch Rename Errors\n\n" + string.Join("\n", errors.Take(10)));
    }
}
