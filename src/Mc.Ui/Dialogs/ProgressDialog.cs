using Mc.FileManager;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// File operation progress dialog.
/// Equivalent to the progress dialogs in src/filemanager/filegui.c.
/// </summary>
public sealed class ProgressDialog : IProgress<OperationProgress>, IDisposable
{
    private readonly Dialog _dialog;
    private readonly Label _fileLabel;
    private readonly ProgressBar _progressBar;
    private readonly Label _countLabel;
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken CancellationToken => _cts.Token;

    public ProgressDialog(string title)
    {
        _dialog = new Dialog
        {
            Title = title,
            Width = 60,
            Height = 10,
            ColorScheme = McTheme.Dialog,
        };

        _dialog.Add(new Label { X = 1, Y = 1, Text = "File:" });

        _fileLabel = new Label
        {
            X = 7, Y = 1,
            Width = 50, Height = 1,
            Text = string.Empty,
        };
        _dialog.Add(_fileLabel);

        _progressBar = new ProgressBar
        {
            X = 1, Y = 3,
            Width = Dim.Fill(1), Height = 1,
            Fraction = 0f,
            ProgressBarStyle = ProgressBarStyle.Blocks,
            ColorScheme = McTheme.StatusBar,
        };
        _dialog.Add(_progressBar);

        _countLabel = new Label
        {
            X = 1, Y = 5,
            Width = Dim.Fill(1), Height = 1,
        };
        _dialog.Add(_countLabel);

        var cancel = new Button { X = Pos.Center(), Y = 7, Text = "Cancel" };
        cancel.Accepting += (_, _) => { _cts.Cancel(); Application.RequestStop(_dialog); };
        _dialog.AddButton(cancel);
    }

    /// <summary>
    /// Runs the dialog modally on the calling (main) thread.
    /// Start the background task BEFORE calling this; it will block until
    /// the task calls <see cref="Close"/>.
    /// </summary>
    public void Show() => Application.Run(_dialog);

    public void Report(OperationProgress value)
    {
        Application.Invoke(() =>
        {
            _fileLabel.Text = value.CurrentFile.Length > 50
                ? "..." + value.CurrentFile[^47..]
                : value.CurrentFile;
            _progressBar.Fraction = (float)(value.Percent / 100.0);
            _countLabel.Text = $"File {value.FilesDone}/{value.TotalFiles}  |  {value.BytesDone:N0} / {value.TotalBytes:N0} bytes";
        });
    }

    public void Close()
    {
        Application.Invoke(() => Application.RequestStop(_dialog));
    }

    public void Dispose()
    {
        _cts.Dispose();
        Close();
    }
}
