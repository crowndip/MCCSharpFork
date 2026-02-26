using Mc.Core.Config;

namespace Mc.FileManager;

/// <summary>
/// Maps file extensions to open/view/edit actions.
/// Equivalent to src/filemanager/ext.c and the mc.ext.ini file.
/// </summary>
public sealed class ExtensionRegistry
{
    private readonly List<ExtensionRule> _rules = [];

    public record ExtensionRule(
        string Pattern,       // glob or regex
        string? OpenCommand,
        string? ViewCommand,
        string? EditCommand,
        string Description
    );

    public ExtensionRegistry()
    {
        LoadDefaults();
    }

    private void LoadDefaults()
    {
        // Platform-specific default file opener
        var opener = OperatingSystem.IsWindows() ? "start \"\" %f"
                   : OperatingSystem.IsMacOS()   ? "open %f"
                   :                               "xdg-open %f";

        // Images
        Add("*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tiff;*.webp", opener, null, null, "Image");
        Add("*.svg", opener, null, null, "SVG Image");

        // Documents
        Add("*.pdf", opener, null, null, "PDF Document");
        Add("*.docx;*.doc;*.odt", opener, null, null, "Word Document");

        // Archives
        Add("*.tar.gz;*.tgz", null, null, null, "GZipped tar archive");
        Add("*.tar.bz2;*.tbz2", null, null, null, "BZipped tar archive");
        Add("*.tar.xz;*.txz", null, null, null, "XZipped tar archive");
        Add("*.zip", null, null, null, "ZIP archive");

        // Source code — open in editor
        Add("*.cs;*.vb;*.fs", null, null, "$EDITOR %f", "C# / .NET source");
        Add("*.c;*.h;*.cpp;*.cxx;*.cc", null, null, "$EDITOR %f", "C/C++ source");
        Add("*.py", null, null, "$EDITOR %f", "Python source");
        Add("*.js;*.ts;*.jsx;*.tsx", null, null, "$EDITOR %f", "JavaScript/TypeScript");
        Add("*.go", null, null, "$EDITOR %f", "Go source");
        Add("*.rs", null, null, "$EDITOR %f", "Rust source");
        Add("*.java", null, null, "$EDITOR %f", "Java source");
        Add("*.sh;*.bash;*.zsh;*.fish", null, null, "$EDITOR %f", "Shell script");
        Add("*.md;*.rst;*.txt", null, null, "$EDITOR %f", "Text/Markdown");
        Add("*.json;*.yaml;*.yml;*.toml;*.xml", null, null, "$EDITOR %f", "Config/data");

        // Executables
        Add("*.so;*.dylib;*.dll", null, null, null, "Shared library");
    }

    public void LoadFromFile(string iniPath)
    {
        if (!File.Exists(iniPath)) return;
        var cfg = McConfig.Load(iniPath);
        foreach (var section in cfg.GetSections())
        {
            if (section == "Default") continue;
            var open = cfg.GetString(section, "Open");
            var view = cfg.GetString(section, "View");
            var edit = cfg.GetString(section, "Edit");
            var desc = cfg.GetString(section, "Description", section);
            _rules.Add(new ExtensionRule(
                section,
                string.IsNullOrEmpty(open) ? null : open,
                string.IsNullOrEmpty(view) ? null : view,
                string.IsNullOrEmpty(edit) ? null : edit,
                desc
            ));
        }
    }

    private void Add(string patterns, string? open, string? view, string? edit, string desc)
    {
        foreach (var p in patterns.Split(';'))
            _rules.Add(new ExtensionRule(p.Trim(), open, view, edit, desc));
    }

    public ExtensionRule? Find(string fileName)
    {
        foreach (var rule in _rules)
        {
            if (MatchGlob(fileName, rule.Pattern))
                return rule;
        }
        return null;
    }

    public string? GetOpenCommand(string fileName)
    {
        var rule = Find(fileName);
        return rule?.OpenCommand;
    }

    public string ExpandCommand(string command, string filePath)
        => command
            .Replace("%f", $"\"{filePath}\"")
            .Replace("%d", $"\"{Path.GetDirectoryName(filePath)}\"")
            .Replace("$EDITOR", Environment.GetEnvironmentVariable("EDITOR") ?? "vi");

    private static bool MatchGlob(string name, string pattern)
    {
        // Simple glob: * matches anything, ? matches one char
        return MatchCore(name.AsSpan(), pattern.AsSpan());
    }

    private static bool MatchCore(ReadOnlySpan<char> name, ReadOnlySpan<char> pattern)
    {
        while (true)
        {
            if (pattern.IsEmpty) return name.IsEmpty;
            if (pattern[0] == '*')
            {
                pattern = pattern[1..];
                if (pattern.IsEmpty) return true;
                for (int i = 0; i <= name.Length; i++)
                    if (MatchCore(name[i..], pattern)) return true;
                return false;
            }
            if (name.IsEmpty) return false;
            if (pattern[0] != '?' && char.ToLowerInvariant(name[0]) != char.ToLowerInvariant(pattern[0]))
                return false;
            name = name[1..]; pattern = pattern[1..];
        }
    }
}
