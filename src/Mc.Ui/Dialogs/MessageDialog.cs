using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>Generic message / confirmation dialog. Equivalent to query_dialog() in the C code.</summary>
public static class MessageDialog
{
    public static void Show(string title, string message)
    {
        var d = new Dialog
        {
            Title = title,
            Width = Math.Min(70, Math.Max(40, message.Length + 6)),
            Height = 7,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(new Label { X = 1, Y = 1, Text = message });
        var ok = new Button { X = Pos.Center(), Y = 3, Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) => Application.RequestStop(d);
        d.AddButton(ok);
        Application.Run(d);
    }

    public static bool Confirm(string title, string message, string yesText = "Yes", string noText = "No")
    {
        bool result = false;
        var d = new Dialog
        {
            Title = title,
            Width = Math.Min(70, Math.Max(40, message.Length + 6)),
            Height = 7,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(new Label { X = 1, Y = 1, Text = message });

        var yes = new Button { X = Pos.Center() - 8, Y = 3, Text = yesText, IsDefault = true };
        yes.Accepting += (_, _) => { result = true; Application.RequestStop(d); };

        var no = new Button { X = Pos.Center() + 2, Y = 3, Text = noText };
        no.Accepting += (_, _) => { result = false; Application.RequestStop(d); };

        d.AddButton(yes);
        d.AddButton(no);
        Application.Run(d);
        return result;
    }

    public static void Error(string message) => Show("Error", message);
}
