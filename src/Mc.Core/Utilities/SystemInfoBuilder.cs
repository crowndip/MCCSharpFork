using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Mc.Core.Utilities;

/// <summary>
/// Gathers live system information using cross-platform .NET APIs.
/// </summary>
public static class SystemInfoBuilder
{
    public static string Build()
    {
        var sb = new StringBuilder();

        AppendOsSection(sb);
        sb.AppendLine();
        AppendCpuSection(sb);
        sb.AppendLine();
        AppendMemorySection(sb);
        sb.AppendLine();
        AppendDrivesSection(sb);

        return sb.ToString().TrimEnd();
    }

    // ── OS ───────────────────────────────────────────────────────────────────

    private static void AppendOsSection(StringBuilder sb)
    {
        sb.AppendLine("OPERATING SYSTEM");
        sb.AppendLine($"  Description  : {RuntimeInformation.OSDescription}");
        sb.AppendLine($"  Architecture : {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"  .NET runtime : {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"  Machine name : {Environment.MachineName}");
        sb.AppendLine($"  User name    : {Environment.UserName}");
    }

    // ── CPU ──────────────────────────────────────────────────────────────────

    private static void AppendCpuSection(StringBuilder sb)
    {
        sb.AppendLine("PROCESSOR");
        sb.AppendLine($"  Model        : {GetCpuModel()}");
        sb.AppendLine($"  Architecture : {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"  Logical cores: {Environment.ProcessorCount}");
    }

    private static string GetCpuModel()
    {
        if (OperatingSystem.IsLinux())
            return ReadLinuxCpuModel() ?? "unknown";

        if (OperatingSystem.IsWindows())
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown";

        if (OperatingSystem.IsMacOS())
            return RunCommand("sysctl", "-n machdep.cpu.brand_string") ?? "unknown";

        return "unknown";
    }

    private static string? ReadLinuxCpuModel()
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = line.IndexOf(':');
                    if (idx >= 0)
                        return line[(idx + 1)..].Trim();
                }
            }
        }
        catch { }
        return null;
    }

    private static string? RunCommand(string command, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(command, args)
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return null;
            var result = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch { }
        return null;
    }

    // ── Memory ───────────────────────────────────────────────────────────────

    private static void AppendMemorySection(StringBuilder sb)
    {
        var gcInfo = GC.GetGCMemoryInfo();
        long total = gcInfo.TotalAvailableMemoryBytes;

        long process = -1;
        try { process = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64; }
        catch { }

        sb.AppendLine("MEMORY");
        sb.AppendLine(total > 0
            ? $"  Total RAM    : {FormatBytes(total)}  ({total:N0} bytes)"
            : "  Total RAM    : unknown");
        sb.AppendLine(process >= 0
            ? $"  Process use  : {FormatBytes(process)}  ({process:N0} bytes)"
            : "  Process use  : unknown");
    }

    // ── Drives ───────────────────────────────────────────────────────────────

    private static void AppendDrivesSection(StringBuilder sb)
    {
        sb.AppendLine("LOCAL DRIVES");

        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch { sb.AppendLine("  (unable to enumerate drives)"); return; }

        bool any = false;
        foreach (var d in drives)
        {
            if (!d.IsReady) continue;
            any = true;
            try
            {
                // VolumeLabel can throw UnauthorizedAccessException on Linux when
                // querying block-device metadata without elevated privileges.
                string label = "(no label)";
                try { label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "(no label)" : d.VolumeLabel; }
                catch { }

                long total = d.TotalSize;
                long avail = d.AvailableFreeSpace;
                long used  = total - avail;
                int  pct   = total > 0 ? (int)(used * 100L / total) : 0;
                sb.AppendLine($"  {d.Name,-12} [{d.DriveType,-10}]  Label: {label}");
                sb.AppendLine($"    Total: {FormatBytes(total),8}   Used: {FormatBytes(used),8} ({pct,3}%)   Free: {FormatBytes(avail),8}");
            }
            catch
            {
                sb.AppendLine($"  {d.Name,-12} [{d.DriveType,-10}]  (access denied)");
            }
        }

        if (!any)
            sb.AppendLine("  (no ready drives found)");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{(long)v} {units[i]}" : $"{v:F1} {units[i]}";
    }
}
