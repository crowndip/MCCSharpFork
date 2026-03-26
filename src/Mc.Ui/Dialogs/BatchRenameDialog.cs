// Dialogs/BatchRenameDialog.cs
// Ported from MCCompanion's BatchRenameCommand.cs, adapted for Terminal.Gui v2.
//
// ┌─ Rename mask placeholders ─────────────────────────────────────────────┐
// │  [N]      entire original filename (no extension)                      │
// │  [N1-5]   characters 1–5 (1-indexed, inclusive)                        │
// │  [N-5]    last 5 characters of the name                                │
// │  [P]      immediate parent folder name                                  │
// │  [C]      counter  (uses global Start / Step / Digits settings)        │
// │  [C:3]    counter with inline digit-width (overrides global Digits)    │
// │  [C:A]    alphabetic counter  (A, B … Z, AA, AB …)                    │
// │  [Y][M][D] file modification year / month / day                        │
// │  [h][m][s] file modification hour / minute / second                    │
// │  [T]      current date+time at rename time  (yyyyMMdd_HHmmss)          │
// │  [G]      new GUID per file (32 hex chars, no dashes)                  │
// │  [E]      original extension without the leading dot                   │
// │  [U]      convert the preceding placeholder's result to UPPERCASE      │
// │  [c][L]   convert the preceding placeholder's result to lowercase      │
// └────────────────────────────────────────────────────────────────────────┘
//
// Search / Replace: use  |  to separate multiple pairs at once.
// RegEx mode: use capture groups ($1, $2 …) in the Replace field.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Mc.Ui.Helpers;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

public sealed class BatchRenameDialog : IDisposable
{
    private readonly List<string> _files;

    // Controls
    private readonly Dialog    _d;
    private readonly TextField _tfMask;
    private readonly TextField _tfExtMask;
    private readonly TextField _tfSearch;
    private readonly TextField _tfReplace;
    private readonly CheckBox  _chkRegex;
    private readonly CheckBox  _chkCase;
    private readonly RadioGroup _rgCase;
    private readonly TextField _tfStart;
    private readonly TextField _tfStep;
    private readonly TextField _tfDigits;
    private readonly ListView  _lvPreview;

    private readonly ObservableCollection<string> _previewItems = [];

    private int _renamedCount;

    private BatchRenameDialog(IEnumerable<string> filePaths)
    {
        _files = filePaths.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        _d = new Dialog
        {
            Title       = "Batch Rename",
            Width       = Dim.Fill(4),
            Height      = Dim.Fill(2),
            ColorScheme = McTheme.Dialog,
        };

        int y = 1;

        // ── Masks ─────────────────────────────────────────────────────────────
        _d.Add(new Label { X = 1, Y = y, Text = "Rename mask:" });
        _tfMask = new TextField { X = 14, Y = y, Width = Dim.Fill(2), Text = "[N]", ColorScheme = McTheme.Panel };
        _d.Add(_tfMask);
        y++;

        _d.Add(new Label { X = 1, Y = y, Text = "Extension:" });
        _tfExtMask = new TextField { X = 14, Y = y, Width = Dim.Percent(30), Text = "[E]", ColorScheme = McTheme.Panel };
        _d.Add(_tfExtMask);
        _d.Add(new Label { X = Pos.Right(_tfExtMask) + 1, Y = y, Text = "(empty = keep original)" });
        y++;

        // ── Placeholder quick-ref ─────────────────────────────────────────────
        y++;
        _d.Add(new Label { X = 1, Y = y, Text = "Name: [N]  [N1-5]  [N-5]  [P]     Counter: [C]  [C:3]  [C:A]     Ext: [E]" });
        y++;
        _d.Add(new Label { X = 1, Y = y, Text = "Date: [Y][M][D]  [h][m][s]    Now: [T]    GUID: [G]    Case: [U] [c][L]" });
        y++;

        // ── Search / Replace ──────────────────────────────────────────────────
        y++;
        _d.Add(new Label { X = 1, Y = y, Text = "Search:" });
        _tfSearch = new TextField { X = 10, Y = y, Width = Dim.Percent(30), ColorScheme = McTheme.Panel };
        _d.Add(_tfSearch);
        _d.Add(new Label { X = Pos.Right(_tfSearch) + 1, Y = y, Text = "Replace:" });
        _tfReplace = new TextField { X = Pos.Right(_tfSearch) + 10, Y = y, Width = Dim.Fill(2), ColorScheme = McTheme.Panel };
        _d.Add(_tfReplace);
        y++;

        _chkRegex = new CheckBox { X = 10, Y = y, Text = "RegEx" };
        _chkCase  = new CheckBox { X = 25, Y = y, Text = "Case-sensitive" };
        _d.Add(_chkRegex, _chkCase);
        _d.Add(new Label { X = Pos.Right(_chkCase) + 2, Y = y, Text = "(use | to separate pairs)" });
        y++;

        // ── Case conversion ───────────────────────────────────────────────────
        y++;
        _d.Add(new Label { X = 1, Y = y, Text = "Case:" });
        _rgCase = new RadioGroup
        {
            X = 8, Y = y,
            RadioLabels = ["No change", "UPPER", "lower", "Title"],
        };
        _d.Add(_rgCase);
        y++;

        // ── Counter ───────────────────────────────────────────────────────────
        y++;
        _d.Add(new Label { X = 1,  Y = y, Text = "Counter — Start:" });
        _tfStart = new TextField { X = 18, Y = y, Width = 5, Text = "1", ColorScheme = McTheme.Panel };
        _d.Add(_tfStart);
        _d.Add(new Label { X = 25, Y = y, Text = "Step:" });
        _tfStep = new TextField { X = 31, Y = y, Width = 5, Text = "1", ColorScheme = McTheme.Panel };
        _d.Add(_tfStep);
        _d.Add(new Label { X = 38, Y = y, Text = "Digits:" });
        _tfDigits = new TextField { X = 46, Y = y, Width = 4, Text = "3", ColorScheme = McTheme.Panel };
        _d.Add(_tfDigits);
        y++;

        // ── Preview ───────────────────────────────────────────────────────────
        y++;
        _d.Add(new Label { X = 1, Y = y, Text = "Preview  (original → new):" });
        y++;
        _lvPreview = new ListView
        {
            X = 1, Y = y,
            Width = Dim.Fill(1), Height = Dim.Fill(3),
            ColorScheme = McTheme.Panel,
        };
        _lvPreview.SetSource(_previewItems);
        _lvPreview.EnableWheelScroll();
        _d.Add(_lvPreview);

        // ── Buttons ───────────────────────────────────────────────────────────
        var btnHelp   = new Button { Text = "_Help" };
        var btnCancel = new Button { Text = "_Cancel" };
        var btnRename = new Button { Text = "_Rename", IsDefault = true };

        btnHelp.Accepting   += (_, e) => { e.Cancel = true; ShowHelp(); };
        btnCancel.Accepting += (_, _) => Application.RequestStop(_d);
        btnRename.Accepting += (_, e) => { if (!DoRename()) e.Cancel = true; };

        _d.AddButton(btnHelp);
        _d.AddButton(btnCancel);
        _d.AddButton(btnRename);

        // ── Live preview ──────────────────────────────────────────────────────
        _tfMask.TextChanged    += (_, _) => UpdatePreview();
        _tfExtMask.TextChanged += (_, _) => UpdatePreview();
        _tfSearch.TextChanged  += (_, _) => UpdatePreview();
        _tfReplace.TextChanged += (_, _) => UpdatePreview();
        _tfStart.TextChanged   += (_, _) => UpdatePreview();
        _tfStep.TextChanged    += (_, _) => UpdatePreview();
        _tfDigits.TextChanged  += (_, _) => UpdatePreview();
        _chkRegex.CheckedStateChanged += (_, _) => UpdatePreview();
        _chkCase.CheckedStateChanged  += (_, _) => UpdatePreview();
        _rgCase.SelectedItemChanged   += (_, _) => UpdatePreview();

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

    // ── Preview ───────────────────────────────────────────────────────────────

    private void UpdatePreview()
    {
        int start  = int.TryParse(_tfStart.Text,  out int sv)  ? sv  : 1;
        int step   = int.TryParse(_tfStep.Text,   out int stv) ? stv : 1;
        int digits = int.TryParse(_tfDigits.Text, out int dv)  ? dv  : 3;
        var now    = DateTime.Now;

        _previewItems.Clear();
        for (int i = 0; i < _files.Count; i++)
        {
            string orig  = Path.GetFileName(_files[i]);
            int    cval  = start + i * step;
            string next  = ApplyRules(_files[i], cval, digits, now);
            string arrow = orig == next ? "(unchanged)" : $"→ {next}";
            _previewItems.Add($"{orig.PadRight(34)} {arrow}");
        }
    }

    // ── Core rename logic ─────────────────────────────────────────────────────

    private string ApplyRules(string fullPath, int counterVal, int digits, DateTime now)
    {
        string name       = Path.GetFileName(fullPath);
        string origExt    = Path.GetExtension(name);
        string stem       = Path.GetFileNameWithoutExtension(name);
        string parentName = Path.GetFileName(
            Path.GetDirectoryName(fullPath) ?? string.Empty) ?? string.Empty;

        DateTime modified;
        try   { modified = File.GetLastWriteTime(fullPath); }
        catch { modified = now; }

        string maskText = _tfMask.Text ?? string.Empty;
        if (string.IsNullOrEmpty(maskText)) maskText = "[N]";
        string newStem = ApplyMask(maskText, stem, origExt, parentName,
                                   counterVal, digits, modified, now);

        string extMaskText = _tfExtMask.Text ?? string.Empty;
        string newExt;
        if (string.IsNullOrEmpty(extMaskText))
        {
            newExt = origExt;
        }
        else
        {
            string resolved = ApplyMask(extMaskText, stem, origExt, parentName,
                                        counterVal, digits, modified, now);
            newExt = string.IsNullOrEmpty(resolved)
                ? string.Empty
                : resolved.StartsWith('.') ? resolved : '.' + resolved;
        }

        string searchText  = _tfSearch.Text  ?? string.Empty;
        string replaceText = _tfReplace.Text ?? string.Empty;

        if (!string.IsNullOrEmpty(searchText))
        {
            string[] searches = searchText.Split('|');
            string[] replaces = replaceText.Split('|');

            var opts = RegexOptions.CultureInvariant;
            if (_chkCase.CheckedState != CheckState.Checked) opts |= RegexOptions.IgnoreCase;

            for (int i = 0; i < searches.Length; i++)
            {
                string s = searches[i];
                if (string.IsNullOrEmpty(s)) continue;
                string r = i < replaces.Length ? replaces[i] : string.Empty;
                try
                {
                    string pattern = _chkRegex.CheckedState == CheckState.Checked
                        ? s : Regex.Escape(s);
                    newStem = Regex.Replace(newStem, pattern, r, opts);
                }
                catch { /* invalid pattern – skip pair */ }
            }
        }

        newStem = _rgCase.SelectedItem switch
        {
            1 => newStem.ToUpperInvariant(),
            2 => newStem.ToLowerInvariant(),
            3 => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(newStem.ToLowerInvariant()),
            _ => newStem
        };

        return newStem + newExt;
    }

    // ── Mask / placeholder engine ─────────────────────────────────────────────

    private static string ApplyMask(
        string mask, string stem, string origExt, string parentName,
        int counterVal, int digits, DateTime modified, DateTime now)
    {
        var result = new StringBuilder(mask.Length + 16);
        int lastPlaceholderStart = 0;
        int i = 0;

        while (i < mask.Length)
        {
            if (mask[i] == '[')
            {
                int close = mask.IndexOf(']', i + 1);
                if (close < 0) { result.Append(mask[i++]); continue; }

                string token = mask.Substring(i + 1, close - i - 1);

                if (token is "c" or "L")
                {
                    ConvertRange(result, lastPlaceholderStart, upper: false);
                    lastPlaceholderStart = result.Length;
                    i = close + 1;
                    continue;
                }
                if (token == "U")
                {
                    ConvertRange(result, lastPlaceholderStart, upper: true);
                    lastPlaceholderStart = result.Length;
                    i = close + 1;
                    continue;
                }

                lastPlaceholderStart = result.Length;
                result.Append(ResolveToken(token, stem, origExt, parentName,
                                           counterVal, digits, modified, now));
                i = close + 1;
            }
            else
            {
                result.Append(mask[i++]);
            }
        }

        return result.ToString();
    }

    private static void ConvertRange(StringBuilder sb, int from, bool upper)
    {
        for (int k = from; k < sb.Length; k++)
            sb[k] = upper ? char.ToUpperInvariant(sb[k]) : char.ToLowerInvariant(sb[k]);
    }

    private static string ResolveToken(
        string token, string stem, string origExt, string parentName,
        int counterVal, int digits, DateTime modified, DateTime now)
    {
        switch (token)
        {
            case "N": return stem;
            case "E": return origExt.TrimStart('.');
            case "P": return parentName;
            case "G": return Guid.NewGuid().ToString("N");
            case "T": return now.ToString("yyyyMMdd_HHmmss");
            case "Y": return modified.Year.ToString("D4");
            case "M": return modified.Month.ToString("D2");
            case "D": return modified.Day.ToString("D2");
            case "h": return modified.Hour.ToString("D2");
            case "m": return modified.Minute.ToString("D2");
            case "s": return modified.Second.ToString("D2");
        }

        // Counter: [C], [C:3], [C:A]
        if (token == "C" || token.StartsWith("C:"))
        {
            string spec = token.Length > 2 ? token.Substring(2) : string.Empty;
            if (string.Equals(spec, "A", StringComparison.OrdinalIgnoreCase))
                return ToAlphaCounter(counterVal);
            int effectiveDigits = int.TryParse(spec, out int d) ? d : digits;
            return counterVal.ToString().PadLeft(effectiveDigits, '0');
        }

        // Name slices: [N1-5], [N-5], [N3]
        if (token.Length > 1 && token[0] == 'N')
        {
            string range = token.Substring(1);

            // [N-5] → last 5 chars
            if (range.StartsWith("-") && range.Length > 1 &&
                int.TryParse(range.Substring(1), out int lastN) && lastN > 0)
            {
                int startIdx = Math.Max(0, stem.Length - lastN);
                return stem.Substring(startIdx);
            }

            // [N1-5] → chars 1..5 (1-indexed)
            int dash = range.IndexOf('-');
            if (dash > 0 &&
                int.TryParse(range.Substring(0, dash),  out int from) &&
                int.TryParse(range.Substring(dash + 1), out int to))
            {
                from = Math.Max(1, from) - 1;
                to   = Math.Min(to, stem.Length);
                return from < to ? stem.Substring(from, to - from) : string.Empty;
            }

            // [N3] → single char at position 3 (1-indexed)
            if (int.TryParse(range, out int pos))
            {
                pos -= 1;
                return pos >= 0 && pos < stem.Length ? stem[pos].ToString() : string.Empty;
            }
        }

        return $"[{token}]"; // unknown token → pass through
    }

    private static string ToAlphaCounter(int value)
    {
        if (value <= 0) value = 1;
        var stack = new Stack<char>();
        while (value > 0)
        {
            value--;
            stack.Push((char)('A' + value % 26));
            value /= 26;
        }
        return new string(stack.ToArray());
    }

    // ── Commit ────────────────────────────────────────────────────────────────

    /// <summary>Returns true if the dialog should be closed.</summary>
    private bool DoRename()
    {
        int start  = int.TryParse(_tfStart.Text,  out int sv)  ? sv  : 1;
        int step   = int.TryParse(_tfStep.Text,   out int stv) ? stv : 1;
        int digits = int.TryParse(_tfDigits.Text, out int dv)  ? dv  : 3;
        var now    = DateTime.Now;

        int changedCount = 0;
        for (int i = 0; i < _files.Count; i++)
        {
            int cv = start + i * step;
            if (ApplyRules(_files[i], cv, digits, now) != Path.GetFileName(_files[i]))
                changedCount++;
        }

        if (changedCount == 0)
        {
            MessageDialog.Show("Batch Rename", "No files would be changed with the current settings.");
            return false;
        }

        if (!MessageDialog.Confirm("Confirm Rename", $"Rename {changedCount} file(s)?"))
            return false;

        var errors = new List<string>();
        for (int i = 0; i < _files.Count; i++)
        {
            int    cv      = start + i * step;
            string orig    = Path.GetFileName(_files[i]);
            string newName = ApplyRules(_files[i], cv, digits, now);
            if (newName == orig) continue;

            string dir     = Path.GetDirectoryName(_files[i])!;
            string newPath = Path.Combine(dir, newName);
            try
            {
                File.Move(_files[i], newPath);
                _renamedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{orig}: {ex.Message}");
            }
        }

        Application.RequestStop(_d);

        if (errors.Count > 0)
            MessageDialog.Error("Batch Rename Errors:\n" + string.Join("\n", errors.Take(10)));

        return true;
    }

    // ── Help ──────────────────────────────────────────────────────────────────

    private static void ShowHelp()
    {
        var helpDlg = new Dialog
        {
            Title  = "Batch Rename – Help",
            Width  = Dim.Fill(4),
            Height = Dim.Fill(2),
        };

        var tv = new TextView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1), Height = Dim.Fill(2),
            ReadOnly = true,
            Text = HelpText,
        };

        var btnClose = new Button { Text = "_Close", IsDefault = true };
        btnClose.Accepting += (_, _) => Application.RequestStop(helpDlg);
        helpDlg.AddButton(btnClose);
        helpDlg.Add(tv);

        Application.Run(helpDlg);
    }

    private const string HelpText =
        "MASK  (Rename mask & Extension field)\n" +
        "────────────────────────────────────────────────────────────────────\n" +
        " Type literal text freely; wrap dynamic parts in [ ] brackets.\n" +
        " Example:  Project_[C:3]_[Y][M][D]  →  Project_001_20260222\n" +
        "\n" +
        "NAME PLACEHOLDERS\n" +
        " [N]      Entire original filename (without extension)\n" +
        " [N1-5]   Characters 1–5 of the name  (1-indexed, inclusive)\n" +
        " [N-5]    Last 5 characters of the name\n" +
        " [P]      Immediate parent folder name\n" +
        "\n" +
        "COUNTER\n" +
        " [C]      Counter using global Start / Step / Digits values\n" +
        " [C:3]    Counter with inline digit width  →  001, 002, 003 …\n" +
        " [C:A]    Alphabetic counter  →  A, B … Z, AA, AB …\n" +
        " Start: first value   Step: increment   Digits: zero-pad width\n" +
        "\n" +
        "DATE & TIME  (uses file modification time)\n" +
        " [Y][M][D]   Year (4-digit), Month (2), Day (2)\n" +
        " [h][m][s]   Hour (2), Minute (2), Second (2)\n" +
        " [T]         Current timestamp when Rename is pressed\n" +
        "             (format: yyyyMMdd_HHmmss)\n" +
        "\n" +
        "OTHER\n" +
        " [E]   Original extension without the dot\n" +
        " [G]   Unique GUID per file (32 hex chars, no dashes)\n" +
        "\n" +
        "EXTENSION FIELD\n" +
        " Leave blank or use [E] to keep the original extension.\n" +
        " Type  txt  (no dot) to change all files to .txt.\n" +
        " The dot is added automatically — do not include it.\n" +
        "\n" +
        "INLINE CASE MODIFIERS\n" +
        " Place immediately after a placeholder to convert its output:\n" +
        " [U]     → UPPERCASE       [c] or [L]  → lowercase\n" +
        " Examples:\n" +
        "   [N][c]         →  lowercase name\n" +
        "   [P][U]_[N]     →  PARENTFOLDER_originalname\n" +
        "   [Y][M][D]_[N][c]  →  20260222_lowercase_name\n" +
        "\n" +
        "SEARCH & REPLACE\n" +
        " Applied to the expanded mask result (stem only, not extension).\n" +
        " Use  |  to define multiple substitutions in a single pass:\n" +
        "   Search:   ä|ö|ü       Replace:  ae|oe|ue\n" +
        " Enable RegEx for patterns and capture groups ($1, $2 …).\n" +
        "\n" +
        "CASE  (global dropdown)\n" +
        " Applied last, after the mask and search/replace.\n" +
        " For per-placeholder conversion use inline [U] / [c] instead.\n" +
        "\n" +
        "QUICK EXAMPLES\n" +
        " [Y]-[M]-[D]_[N]       →  2026-02-22_report\n" +
        " [P][c]_[C:3]_[N]      →  vacation_001_img\n" +
        " [N1-3][c]_[C:A]       →  rep_A  (3-char prefix + alpha counter)\n" +
        " [G]                    →  a3f8c1d2…  (collision-proof unique names)\n";
}
