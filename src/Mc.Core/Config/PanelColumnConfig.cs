namespace Mc.Core.Config;

/// <summary>
/// Column types available for file panel display.
/// </summary>
public enum PanelColumn
{
    Name,
    Size,
    ModifyTime,
    AccessTime,
    ChangeTime,
    Permissions,
    Owner,
    Group,
    Extension,
    Type
}

/// <summary>
/// Configuration for panel column display.
/// </summary>
public sealed class PanelColumnConfig
{
    public List<PanelColumn> Columns { get; set; } = [
        PanelColumn.Name,
        PanelColumn.Size,
        PanelColumn.ModifyTime
    ];

    public Dictionary<PanelColumn, int> ColumnWidths { get; set; } = new()
    {
        [PanelColumn.Name] = 30,
        [PanelColumn.Size] = 8,
        [PanelColumn.ModifyTime] = 16,
        [PanelColumn.AccessTime] = 16,
        [PanelColumn.ChangeTime] = 16,
        [PanelColumn.Permissions] = 10,
        [PanelColumn.Owner] = 10,
        [PanelColumn.Group] = 10,
        [PanelColumn.Extension] = 5,
        [PanelColumn.Type] = 8
    };

    public string Serialize()
    {
        var parts = Columns.Select(c => $"{c}:{ColumnWidths.GetValueOrDefault(c, 10)}");
        return string.Join(",", parts);
    }

    public static PanelColumnConfig Deserialize(string value)
    {
        var config = new PanelColumnConfig { Columns = [] };
        
        if (string.IsNullOrWhiteSpace(value))
            return config;

        foreach (var part in value.Split(','))
        {
            var segments = part.Split(':');
            if (segments.Length == 0) continue;

            if (Enum.TryParse<PanelColumn>(segments[0], out var col))
            {
                config.Columns.Add(col);
                if (segments.Length > 1 && int.TryParse(segments[1], out var width))
                    config.ColumnWidths[col] = width;
            }
        }

        if (config.Columns.Count == 0)
        {
            config.Columns = [PanelColumn.Name, PanelColumn.Size, PanelColumn.ModifyTime];
        }

        return config;
    }
}
