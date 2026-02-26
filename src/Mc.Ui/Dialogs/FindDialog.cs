using Terminal.Gui;

namespace Mc.Ui.Dialogs;

public sealed class FindOptions
{
    public string FilePattern { get; set; } = "*";
    public string ContentPattern { get; set; } = string.Empty;
    public bool SearchInSubdirs { get; set; } = true;
    public bool CaseSensitive { get; set; }
    public bool ContentRegex { get; set; }
    public bool Confirmed { get; set; }
}

/// <summary>Find file dialog. Equivalent to find.c in the original C codebase.</summary>
public static class FindDialog
{
    public static FindOptions? Show(string startDir)
    {
        FindOptions? result = null;

        var d = new Dialog
        {
            Title = "Find File",
            Width = 70,
            Height = 14,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = $"Start directory: {startDir}" });
        d.Add(new Label { X = 1, Y = 3, Text = "File name pattern:" });

        var filePatternInput = new TextField
        {
            X = 1, Y = 4, Width = 66, Height = 1,
            Text = "*", ColorScheme = McTheme.Dialog,
        };
        d.Add(filePatternInput);

        d.Add(new Label { X = 1, Y = 6, Text = "Content (leave blank to skip):" });

        var contentInput = new TextField
        {
            X = 1, Y = 7, Width = 66, Height = 1,
            Text = string.Empty, ColorScheme = McTheme.Dialog,
        };
        d.Add(contentInput);

        var subDirsCb = new CheckBox { X = 1, Y = 9, Text = "Search in subdirectories", CheckedState = CheckState.Checked, ColorScheme = McTheme.Dialog };
        var caseCb = new CheckBox { X = 1, Y = 10, Text = "Case sensitive", CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog };
        var regexCb = new CheckBox { X = 35, Y = 10, Text = "Use regex", CheckedState = CheckState.UnChecked, ColorScheme = McTheme.Dialog };
        d.Add(subDirsCb, caseCb, regexCb);

        var ok = new Button { X = Pos.Center() - 10, Y = 12, Text = "Find", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            result = new FindOptions
            {
                FilePattern = filePatternInput.Text?.ToString() ?? "*",
                ContentPattern = contentInput.Text?.ToString() ?? string.Empty,
                SearchInSubdirs = subDirsCb.CheckedState == CheckState.Checked,
                CaseSensitive = caseCb.CheckedState == CheckState.Checked,
                ContentRegex = regexCb.CheckedState == CheckState.Checked,
                Confirmed = true,
            };
            Application.RequestStop(d);
        };

        var cancel = new Button { X = Pos.Center() + 2, Y = 12, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(ok);
        d.AddButton(cancel);
        filePatternInput.SetFocus();
        Application.Run(d);
        return result;
    }
}
