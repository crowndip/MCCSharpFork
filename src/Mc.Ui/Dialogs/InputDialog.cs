using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>Single-field text input dialog. Equivalent to input_dialog() in the C code.</summary>
public static class InputDialog
{
    public static string? Show(string title, string prompt, string defaultValue = "")
    {
        string? result = null;
        int width = Math.Max(50, prompt.Length + 10);

        var d = new Dialog
        {
            Title = title,
            Width = width,
            Height = 9,
            ColorScheme = McTheme.Dialog,
        };

        d.Add(new Label { X = 1, Y = 1, Text = prompt });

        var input = new TextField
        {
            X = 1, Y = 3,
            Width = width - 4,
            Height = 1,
            Text = defaultValue,
            ColorScheme = McTheme.Dialog,
        };
        input.CursorPosition = defaultValue.Length;
        d.Add(input);

        var ok = new Button { X = Pos.Center() - 8, Y = 5, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            result = input.Text?.ToString() ?? string.Empty;
            Application.RequestStop(d);
        };

        var cancel = new Button { X = Pos.Center() + 2, Y = 5, Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop(d);

        d.AddButton(ok);
        d.AddButton(cancel);
        input.SetFocus();
        Application.Run(d);
        return result;
    }
}
