namespace Mc.DiffViewer;

public enum DiffLineType { Context, Added, Removed, Changed }

public sealed record DiffLine(DiffLineType Type, string? LeftText, string? RightText, int LeftLineNo, int RightLineNo);

/// <summary>
/// Myers diff algorithm implementation.
/// Produces a list of DiffLine entries for side-by-side display.
/// Equivalent to src/diffviewer/ydiff.c in the original C codebase.
/// Uses no external packages — pure .NET.
/// </summary>
public static class DiffEngine
{
    public static IReadOnlyList<DiffLine> ComputeDiff(string leftText, string rightText)
    {
        var left = leftText.Split('\n');
        var right = rightText.Split('\n');
        var ops = ComputeEditScript(left, right);
        return BuildDiffLines(left, right, ops);
    }

    private enum EditOp { Equal, Insert, Delete, Replace }
    private record Edit(EditOp Op, int LeftIdx, int RightIdx);

    private static List<Edit> ComputeEditScript(string[] left, string[] right)
    {
        // Standard LCS-based diff using dynamic programming
        int n = left.Length, m = right.Length;
        var dp = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) dp[i, 0] = i;
        for (int j = 0; j <= m; j++) dp[0, j] = j;

        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
                dp[i, j] = left[i - 1] == right[j - 1]
                    ? dp[i - 1, j - 1]
                    : 1 + Math.Min(dp[i - 1, j], Math.Min(dp[i, j - 1], dp[i - 1, j - 1]));

        // Backtrack to get edit script
        var edits = new List<Edit>();
        int li = n, ri = m;
        while (li > 0 || ri > 0)
        {
            if (li > 0 && ri > 0 && left[li - 1] == right[ri - 1])
            {
                edits.Add(new Edit(EditOp.Equal, li - 1, ri - 1));
                li--; ri--;
            }
            else if (ri > 0 && (li == 0 || dp[li, ri - 1] <= dp[li - 1, ri]))
            {
                edits.Add(new Edit(EditOp.Insert, li, ri - 1));
                ri--;
            }
            else
            {
                edits.Add(new Edit(EditOp.Delete, li - 1, ri));
                li--;
            }
        }

        edits.Reverse();
        return edits;
    }

    private static IReadOnlyList<DiffLine> BuildDiffLines(string[] left, string[] right, List<Edit> edits)
    {
        var result = new List<DiffLine>(edits.Count);
        int leftLine = 1, rightLine = 1;

        // Merge adjacent deletes/inserts into replace
        var merged = MergeEdits(edits);

        foreach (var edit in merged)
        {
            switch (edit.Op)
            {
                case EditOp.Equal:
                    result.Add(new DiffLine(DiffLineType.Context,
                        left[edit.LeftIdx], right[edit.RightIdx],
                        ++leftLine, ++rightLine));
                    break;
                case EditOp.Delete:
                    result.Add(new DiffLine(DiffLineType.Removed,
                        left[edit.LeftIdx], null,
                        ++leftLine, rightLine));
                    break;
                case EditOp.Insert:
                    result.Add(new DiffLine(DiffLineType.Added,
                        null, right[edit.RightIdx],
                        leftLine, ++rightLine));
                    break;
                case EditOp.Replace:
                    result.Add(new DiffLine(DiffLineType.Changed,
                        edit.LeftIdx < left.Length ? left[edit.LeftIdx] : null,
                        edit.RightIdx < right.Length ? right[edit.RightIdx] : null,
                        ++leftLine, ++rightLine));
                    break;
            }
        }

        return result;
    }

    private static List<Edit> MergeEdits(List<Edit> edits)
    {
        var result = new List<Edit>(edits.Count);
        int i = 0;
        while (i < edits.Count)
        {
            if (i + 1 < edits.Count &&
                edits[i].Op == EditOp.Delete &&
                edits[i + 1].Op == EditOp.Insert)
            {
                result.Add(new Edit(EditOp.Replace, edits[i].LeftIdx, edits[i + 1].RightIdx));
                i += 2;
            }
            else
            {
                result.Add(edits[i]);
                i++;
            }
        }
        return result;
    }
}
