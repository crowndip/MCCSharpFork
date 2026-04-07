using System.Diagnostics;
using System.Text;

namespace Mc.Editor;

/// <summary>
/// Minimal git integration for mcedit.
/// </summary>
internal static class GitHelper
{
    public static bool IsGitRepository(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var dir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        return RunGit(dir, "rev-parse --git-dir", out _) == 0;
    }

    public static string[] GetBlame(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileName(filePath);
        
        if (RunGit(dir, $"blame --line-porcelain \"{fileName}\"", out var output) != 0)
            return [];

        return ParseBlame(output);
    }

    public static string GetDiff(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileName(filePath);
        
        RunGit(dir, $"diff HEAD \"{fileName}\"", out var output);
        return output;
    }

    public static bool StageFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileName(filePath);
        
        return RunGit(dir, $"add \"{fileName}\"", out _) == 0;
    }

    public static bool UnstageFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileName(filePath);
        
        return RunGit(dir, $"reset HEAD \"{fileName}\"", out _) == 0;
    }

    public static string GetStatus(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileName(filePath);
        
        if (RunGit(dir, $"status --porcelain \"{fileName}\"", out var output) != 0)
            return "";

        if (string.IsNullOrWhiteSpace(output)) return "unmodified";
        
        var status = output.Trim();
        if (status.StartsWith("M ")) return "modified (staged)";
        if (status.StartsWith(" M")) return "modified";
        if (status.StartsWith("A ")) return "added (staged)";
        if (status.StartsWith("??")) return "untracked";
        if (status.StartsWith("D ")) return "deleted (staged)";
        if (status.StartsWith(" D")) return "deleted";
        
        return status;
    }

    private static string[] ParseBlame(string output)
    {
        var lines = output.Split('\n');
        var result = new List<string>();
        string? author = null;
        string? date = null;
        string? commit = null;

        foreach (var line in lines)
        {
            if (line.Length == 0) continue;

            if (line.Length >= 40 && !line.StartsWith('\t'))
            {
                commit = line[..8];
                author = null;
                date = null;
            }
            else if (line.StartsWith("author "))
            {
                author = line[7..];
            }
            else if (line.StartsWith("author-time "))
            {
                if (long.TryParse(line[12..], out var timestamp))
                {
                    var dt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                    date = dt.ToString("yyyy-MM-dd");
                }
            }
            else if (line.StartsWith('\t'))
            {
                var text = line[1..];
                var blame = $"{commit ?? "????????"} {date ?? "????-??-??"} {author ?? "Unknown"}";
                result.Add($"{blame,-50} {text}");
            }
        }

        return result.ToArray();
    }

    private static int RunGit(string workingDir, string args, out string output)
    {
        try
        {
            var psi = new ProcessStartInfo("git")
            {
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                output = "";
                return -1;
            }

            var sb = new StringBuilder();
            while (!proc.StandardOutput.EndOfStream)
                sb.AppendLine(proc.StandardOutput.ReadLine());

            proc.WaitForExit(5000);
            output = sb.ToString();
            return proc.ExitCode;
        }
        catch
        {
            output = "";
            return -1;
        }
    }
}
