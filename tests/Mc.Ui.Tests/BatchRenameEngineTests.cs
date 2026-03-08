using System.Reflection;
using Mc.Ui.Dialogs;
using Xunit;

namespace Mc.Ui.Tests;

/// <summary>
/// Unit tests for the placeholder engine inside BatchRenameDialog.
/// Because ApplyMask is a private static method (pure logic, no UI side-effects)
/// we invoke it via reflection so we can verify every placeholder without
/// running the full dialog.
///
/// User scenario covered: user wants to batch-rename files, adding a timestamp
/// to the file name while keeping the original extension intact.
/// </summary>
public sealed class BatchRenameEngineTests
{
    // A fixed point in time used across all tests for deterministic output.
    private static readonly DateTime T = new(2024, 6, 15, 9, 30, 45);

    /// <summary>Reflective shim that calls the private static ApplyMask method.</summary>
    private static string ApplyMask(
        string mask,
        string stem      = "file",
        string origExt   = ".txt",
        string parent    = "docs",
        int    counter   = 1,
        int    digits    = 3,
        DateTime? modified = null,
        DateTime? now      = null)
    {
        var method = typeof(BatchRenameDialog)
            .GetMethod("ApplyMask", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(BatchRenameDialog), "ApplyMask");

        return (string)method.Invoke(null, [
            mask, stem, origExt, parent, counter, digits,
            modified ?? T, now ?? T
        ])!;
    }

    // ── Basic placeholders ────────────────────────────────────────────────────

    [Fact]
    public void N_ReturnsFileStem()
        => Assert.Equal("report", ApplyMask("[N]", stem: "report"));

    [Fact]
    public void E_ReturnsExtensionWithoutDot()
        => Assert.Equal("txt", ApplyMask("[E]", origExt: ".txt"));

    [Fact]
    public void P_ReturnsParentFolderName()
        => Assert.Equal("vacation", ApplyMask("[P]", parent: "vacation"));

    [Fact]
    public void G_Returns32HexCharGuid()
    {
        var result = ApplyMask("[G]");
        Assert.Equal(32, result.Length);
        Assert.Matches("^[0-9a-f]{32}$", result);
    }

    // ── Date / time placeholders ──────────────────────────────────────────────

    [Fact]
    public void YMD_ExpandsModificationDate()
        => Assert.Equal("20240615", ApplyMask("[Y][M][D]"));

    [Fact]
    public void Hms_ExpandsModificationTime()
        => Assert.Equal("093045", ApplyMask("[h][m][s]"));

    [Fact]
    public void T_ExpandsCurrentTimestampAtRenameTime()
        => Assert.Equal("20240615_093045", ApplyMask("[T]", now: T));

    // ── Counter placeholders ──────────────────────────────────────────────────

    [Fact]
    public void C_PadsCounterWithGlobalDigitWidth()
        => Assert.Equal("005", ApplyMask("[C]", counter: 5, digits: 3));

    [Fact]
    public void C_InlineDigitWidth_OverridesGlobal()
        => Assert.Equal("00042", ApplyMask("[C:5]", counter: 42, digits: 3));

    [Theory]
    [InlineData(1,  "A")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(28, "AB")]
    [InlineData(52, "AZ")]
    [InlineData(53, "BA")]
    public void C_Alphabetic_ConvertsToExcelStyleAlpha(int value, string expected)
        => Assert.Equal(expected, ApplyMask("[C:A]", counter: value));

    // ── Name-slice placeholders ───────────────────────────────────────────────

    [Fact]
    public void NameSlice_Range_ReturnsSubstring()
        // [N1-3] of "report" → "rep"
        => Assert.Equal("rep", ApplyMask("[N1-3]", stem: "report"));

    [Fact]
    public void NameSlice_LastN_ReturnsTrailingChars()
        // [N-3] of "report" → "ort"
        => Assert.Equal("ort", ApplyMask("[N-3]", stem: "report"));

    [Fact]
    public void NameSlice_SinglePos_ReturnsSingleChar()
        // [N2] of "report" → "e" (1-indexed)
        => Assert.Equal("e", ApplyMask("[N2]", stem: "report"));

    // ── Inline case modifiers ─────────────────────────────────────────────────

    [Fact]
    public void InlineCaseModifier_U_UppercasesPrecedingPlaceholder()
        => Assert.Equal("REPORT", ApplyMask("[N][U]", stem: "report"));

    [Fact]
    public void InlineCaseModifier_c_LowercasesPrecedingPlaceholder()
        => Assert.Equal("report", ApplyMask("[N][c]", stem: "REPORT"));

    [Fact]
    public void InlineCaseModifier_L_LowercasesPrecedingPlaceholder()
        => Assert.Equal("report", ApplyMask("[N][L]", stem: "REPORT"));

    [Fact]
    public void InlineCaseModifier_U_OnParent_UppercasesParentName()
        => Assert.Equal("VACATION", ApplyMask("[P][U]", parent: "vacation"));

    // ── Combined templates ────────────────────────────────────────────────────

    [Fact]
    public void LiteralText_IsPassedThroughUnchanged()
        => Assert.Equal("prefix_file_suffix", ApplyMask("prefix_[N]_suffix"));

    [Fact]
    public void UnknownToken_IsPreservedAsBracketedText()
        => Assert.Equal("[XYZ]", ApplyMask("[XYZ]"));

    /// <summary>
    /// Core user scenario: add a date stamp before the file name.
    /// The mask "[N]_[Y][M][D]" applied to "photo" with date 2024-06-15
    /// must produce "photo_20240615" — the caller appends the extension separately.
    /// </summary>
    [Fact]
    public void Combined_AddDateTimestamp_StemOnly_ExtensionHandledSeparately()
    {
        var stem = ApplyMask("[N]_[Y][M][D]", stem: "photo", origExt: ".jpg");
        Assert.Equal("photo_20240615", stem);
        // The dialog combines stem + extension as: "photo_20240615.jpg"
    }

    [Fact]
    public void Combined_CounterWithAlpha_AndParentPrefix()
    {
        // [P][c]_[C:A] applied to parent="Vacation", counter=1 → "vacation_A"
        var result = ApplyMask("[P][c]_[C:A]", parent: "Vacation", counter: 1);
        Assert.Equal("vacation_A", result);
    }

    [Fact]
    public void Combined_ThreeCharPrefixAndAlphaCounter()
    {
        // [N1-3][c]_[C:A] applied to stem="Report", counter=1 → "rep_A"
        var result = ApplyMask("[N1-3][c]_[C:A]", stem: "Report", counter: 1);
        Assert.Equal("rep_A", result);
    }
}
