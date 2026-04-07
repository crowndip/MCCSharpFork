using Mc.Core.Config;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Dialog for configuring panel columns (order and visibility).
/// </summary>
public sealed class ColumnConfigDialog : Dialog
{
    private readonly PanelColumnConfig _config;
    private readonly ListView _availableList;
    private readonly ListView _selectedList;
    private readonly List<PanelColumn> _available;
    private readonly List<PanelColumn> _selected;

    public PanelColumnConfig Result => _config;

    public ColumnConfigDialog(PanelColumnConfig config)
    {
        Title = "Configure Panel Columns";
        Width = 70;
        Height = 22;

        _config = new PanelColumnConfig
        {
            Columns = new List<PanelColumn>(config.Columns),
            ColumnWidths = new Dictionary<PanelColumn, int>(config.ColumnWidths)
        };

        _selected = new List<PanelColumn>(_config.Columns);
        _available = Enum.GetValues<PanelColumn>().Except(_selected).ToList();

        // Available columns
        Add(new Label { X = 2, Y = 1, Text = "Available columns:" });
        _availableList = new ListView
        {
            X = 2, Y = 2,
            Width = 25,
            Height = 10
        };
        _availableList.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(
            _available.Select(FormatColumn)));
        Add(_availableList);

        // Buttons
        var addBtn = new Button { X = 29, Y = 4, Text = "Add →" };
        var removeBtn = new Button { X = 29, Y = 6, Text = "← Remove" };
        var upBtn = new Button { X = 29, Y = 8, Text = "↑ Up" };
        var downBtn = new Button { X = 29, Y = 10, Text = "↓ Down" };
        Add(addBtn, removeBtn, upBtn, downBtn);

        // Selected columns
        Add(new Label { X = 40, Y = 1, Text = "Selected columns (in order):" });
        _selectedList = new ListView
        {
            X = 40, Y = 2,
            Width = 25,
            Height = 10
        };
        _selectedList.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(
            _selected.Select(FormatColumn)));
        Add(_selectedList);

        // Actions
        addBtn.Accepting += (_, _) => AddColumn();
        removeBtn.Accepting += (_, _) => RemoveColumn();
        upBtn.Accepting += (_, _) => MoveUp();
        downBtn.Accepting += (_, _) => MoveDown();

        // OK/Cancel
        var ok = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting += (_, _) =>
        {
            _config.Columns = new List<PanelColumn>(_selected);
            Application.RequestStop(this);
        };
        cancel.Accepting += (_, _) => Application.RequestStop(this);
        AddButton(ok);
        AddButton(cancel);
    }

    private string FormatColumn(PanelColumn col) => col switch
    {
        PanelColumn.Name => "Name",
        PanelColumn.Size => "Size",
        PanelColumn.ModifyTime => "Modify Time",
        PanelColumn.AccessTime => "Access Time",
        PanelColumn.ChangeTime => "Change Time",
        PanelColumn.Permissions => "Permissions",
        PanelColumn.Owner => "Owner",
        PanelColumn.Group => "Group",
        PanelColumn.Extension => "Extension",
        PanelColumn.Type => "Type",
        _ => col.ToString()
    };

    private void AddColumn()
    {
        if (_availableList.SelectedItem < 0 || _availableList.SelectedItem >= _available.Count)
            return;

        var col = _available[_availableList.SelectedItem];
        _available.RemoveAt(_availableList.SelectedItem);
        _selected.Add(col);
        RefreshLists();
    }

    private void RemoveColumn()
    {
        if (_selectedList.SelectedItem < 0 || _selectedList.SelectedItem >= _selected.Count)
            return;

        var col = _selected[_selectedList.SelectedItem];
        _selected.RemoveAt(_selectedList.SelectedItem);
        _available.Add(col);
        RefreshLists();
    }

    private void MoveUp()
    {
        int idx = _selectedList.SelectedItem;
        if (idx <= 0 || idx >= _selected.Count) return;

        (_selected[idx], _selected[idx - 1]) = (_selected[idx - 1], _selected[idx]);
        RefreshLists();
        _selectedList.SelectedItem = idx - 1;
    }

    private void MoveDown()
    {
        int idx = _selectedList.SelectedItem;
        if (idx < 0 || idx >= _selected.Count - 1) return;

        (_selected[idx], _selected[idx + 1]) = (_selected[idx + 1], _selected[idx]);
        RefreshLists();
        _selectedList.SelectedItem = idx + 1;
    }

    private void RefreshLists()
    {
        _availableList.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(
            _available.Select(FormatColumn)));
        _selectedList.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(
            _selected.Select(FormatColumn)));
    }
}
