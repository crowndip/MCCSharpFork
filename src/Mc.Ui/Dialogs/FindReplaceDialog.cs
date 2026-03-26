using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Mc.Core.Search;
using Mc.Ui.Helpers;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Find &amp; Replace across multiple files dialog.
/// Operates on the file list produced by the Find results dialog.
/// Supports dry-run (simulation) mode that counts matches without writing.
/// </summary>
public static class FindReplaceDialog
{
    /// <summary>
    /// Shows the Find &amp; Replace dialog for <paramref name="filePaths"/>.
    /// <paramref name="searchOpts"/> supplies the already-applied search pattern
    /// and options so the user only has to provide the replacement text.
    /// </summary>
    public static void Show(IReadOnlyList<string> filePaths, FindOptions searchOpts)
    {
        if (filePaths.Count == 0) return;

        var d = new Dialog
        {
            Title = "Find & Replace in Files",
            Width = 72,
            Height = 26,
            ColorScheme = McTheme.Dialog,
        };

        // ── File list ─────────────────────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 0, Text = $"Files to search ({filePaths.Count}):" });
        var fileList = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = 7,
            ColorScheme = McTheme.Panel,
        };
        fileList.SetSource(new ObservableCollection<string>(filePaths.Select(Path.GetFileName).ToList()!));
        fileList.EnableWheelScroll();
        d.Add(fileList);

        // ── Search / Replace fields ───────────────────────────────────────────
        d.Add(new Label { X = 1, Y = 9,  Text = "Search for:" });
        d.Add(new Label
        {
            X = 14, Y = 9,
            Width = Dim.Fill(1),
            Text = searchOpts.ContentPattern,
            ColorScheme = McTheme.Panel,
        });

        d.Add(new Label { X = 1, Y = 11, Text = "Replace with:" });
        var replaceInput = new TextField
        {
            X = 15, Y = 11,
            Width = Dim.Fill(1), Height = 1,
            Text = string.Empty,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(replaceInput);

        // ── Options ───────────────────────────────────────────────────────────
        var dryRunCb = new CheckBox
        {
            X = 1, Y = 13,
            Text = "Dry run (count matches only — no files will be modified)",
            CheckedState = CheckState.Checked,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(dryRunCb);

        // ── Status ────────────────────────────────────────────────────────────
        var statusLabel = new Label
        {
            X = 1, Y = 15,
            Width = Dim.Fill(1),
            Text = "Ready.",
            ColorScheme = McTheme.Dialog,
        };
        d.Add(statusLabel);

        var progressLabel = new Label
        {
            X = 1, Y = 16,
            Width = Dim.Fill(1),
            Text = string.Empty,
            ColorScheme = McTheme.Dialog,
        };
        d.Add(progressLabel);

        // ── Buttons ───────────────────────────────────────────────────────────
        var cts = new System.Threading.CancellationTokenSource();
        bool running = false;

        var replaceBtn = new Button { Text = "Replace All", IsDefault = true };
        var cancelBtn  = new Button { Text = "Cancel" };

        replaceBtn.Accepting += (_, _) =>
        {
            if (running) return;

            bool isDryRun   = dryRunCb.CheckedState == CheckState.Checked;
            string replaceWith = replaceInput.Text?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(searchOpts.ContentPattern))
            {
                MessageDialog.Error("No search pattern — run Find first with a content pattern.");
                return;
            }

            running = true;
            replaceBtn.Enabled = false;
            dryRunCb.Enabled   = false;
            replaceInput.ReadOnly = true;

            ISearchProvider provider = searchOpts.ContentRegex
                ? new RegexSearchProvider()
                : new NormalSearchProvider();

            var opts = new SearchOptions
            {
                Pattern       = searchOpts.ContentPattern,
                Type          = searchOpts.ContentRegex ? SearchType.Regex : SearchType.Normal,
                CaseSensitive = searchOpts.CaseSensitive,
                Replacement   = replaceWith,
            };

            var token = cts.Token;
            Task.Run(() => RunReplace(filePaths, provider, opts, isDryRun, token,
                statusLabel, progressLabel, cancelBtn, d));
        };

        cancelBtn.Accepting += (_, _) =>
        {
            cts.Cancel();
            Application.RequestStop(d);
        };

        d.AddButton(replaceBtn);
        d.AddButton(cancelBtn);

        replaceInput.SetFocus();
        Application.Run(d);
        cts.Cancel();
        cts.Dispose();
        d.Dispose();
    }

    // ── Background replacement worker ─────────────────────────────────────────

    private static void RunReplace(
        IReadOnlyList<string> filePaths,
        ISearchProvider provider,
        SearchOptions opts,
        bool isDryRun,
        System.Threading.CancellationToken token,
        Label statusLabel,
        Label progressLabel,
        Button cancelBtn,
        Dialog d)
    {
        int filesModified = 0;
        int filesSkipped  = 0;    // no match
        int filesErrored  = 0;
        long totalMatches = 0;

        for (int i = 0; i < filePaths.Count; i++)
        {
            if (token.IsCancellationRequested) break;

            var path     = filePaths[i];
            var fileName = Path.GetFileName(path);
            int idx      = i;

            Application.Invoke(() =>
                progressLabel.Text = $"[{idx + 1}/{filePaths.Count}] {fileName}");

            try
            {
                var original = File.ReadAllText(path);

                long matches = CountMatches(original, opts, provider);
                if (matches == 0) { filesSkipped++; continue; }

                totalMatches += matches;
                filesModified++;

                if (!isDryRun)
                {
                    var replaced = provider.ReplaceAll(original, opts);
                    File.WriteAllText(path, replaced);
                }
            }
            catch (Exception ex)
            {
                filesErrored++;
                Application.Invoke(() =>
                    statusLabel.Text = $"Error in {fileName}: {ex.Message}");
            }
        }

        // Show summary
        Application.Invoke(() =>
        {
            progressLabel.Text = string.Empty;
            string prefix = isDryRun ? "DRY RUN — Would replace" : "Replaced";
            statusLabel.Text =
                $"{prefix} {totalMatches} occurrence(s) in {filesModified} file(s)" +
                (filesSkipped > 0  ? $", {filesSkipped} unchanged" : string.Empty) +
                (filesErrored > 0  ? $", {filesErrored} error(s)"  : string.Empty) +
                ".";

            cancelBtn.Text = "Close";
        });
    }

    // ── Match counting ────────────────────────────────────────────────────────

    private static long CountMatches(string content, SearchOptions opts, ISearchProvider provider)
    {
        // For regex use Regex.Matches for an accurate count; for normal use a
        // scan loop so we don't build a Regex object unnecessarily.
        if (opts.Type == SearchType.Regex)
        {
            var flags = opts.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            try { return Regex.Matches(content, opts.Pattern, flags).Count; }
            catch { return 0; }
        }

        // Normal: count non-overlapping occurrences
        var cmp = opts.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        long count = 0;
        int  pos   = 0;
        int  step  = Math.Max(opts.Pattern.Length, 1);
        while (pos <= content.Length - opts.Pattern.Length)
        {
            int found = content.IndexOf(opts.Pattern, pos, cmp);
            if (found < 0) break;
            count++;
            pos = found + step;
        }
        return count;
    }
}
