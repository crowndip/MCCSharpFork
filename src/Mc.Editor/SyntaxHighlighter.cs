using System.Text.RegularExpressions;

namespace Mc.Editor;

public enum TokenType
{
    Default,
    Keyword,
    Comment,
    String,
    Number,
    Preprocessor,
    Operator,
    Type,
    Identifier,
}

public sealed record SyntaxToken(int Start, int Length, TokenType Type);

/// <summary>
/// Rule-based syntax highlighter.
/// Equivalent to src/editor/syntax.c in the original C codebase.
/// Uses System.Text.RegularExpressions — no external packages.
/// </summary>
public sealed class SyntaxHighlighter
{
    private readonly SyntaxRuleSet _rules;

    public SyntaxHighlighter(SyntaxRuleSet rules) => _rules = rules;

    /// <summary>The display name of this syntax rule set (e.g. "C#", "Python").</summary>
    public string SyntaxName => _rules.Name;

    public static SyntaxHighlighter? ForFile(string fileName, string? firstLine = null)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var rules = SyntaxRuleSet.ForExtension(ext);
        if (rules != null) return new SyntaxHighlighter(rules);
        // Try shebang/first-line detection
        if (firstLine != null)
            rules = SyntaxRuleSet.ForFirstLine(firstLine);
        return rules != null ? new SyntaxHighlighter(rules) : null;
    }

    public IReadOnlyList<SyntaxToken> Tokenize(string line)
    {
        var tokens = new List<SyntaxToken>();
        int pos = 0;

        while (pos < line.Length)
        {
            bool matched = false;

            foreach (var rule in _rules.Rules)
            {
                var m = rule.Pattern.Match(line, pos);
                if (!m.Success || m.Index != pos) continue;

                tokens.Add(new SyntaxToken(pos, m.Length, rule.TokenType));
                pos += m.Length;
                matched = true;
                break;
            }

            if (!matched) pos++;
        }

        return tokens;
    }
}

public sealed record SyntaxRule(Regex Pattern, TokenType TokenType);

public sealed class SyntaxRuleSet
{
    public string Name { get; init; } = "default";
    public IReadOnlyList<SyntaxRule> Rules { get; init; } = [];

    private static readonly RegexOptions RO = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    public static SyntaxRuleSet? ForFirstLine(string firstLine)
    {
        if (string.IsNullOrEmpty(firstLine)) return null;
        var line = firstLine.TrimStart();
        if (line.StartsWith("#!"))
        {
            // Shebang line
            if (line.Contains("python")) return Python();
            if (line.Contains("ruby") || line.Contains("irb")) return Ruby();
            if (line.Contains("node") || line.Contains("js")) return JavaScript();
            if (line.Contains("lua")) return Lua();
            if (line.Contains("perl")) return Perl();
            if (line.Contains("php")) return Php();
            if (line.Contains("bash") || line.Contains("sh") || line.Contains("/env sh")) return Shell();
            if (line.Contains("r ") || line.EndsWith("/R") || line.EndsWith("/Rscript")) return R();
        }
        // -*- mode: xxx -*- emacs style
        var modeMatch = System.Text.RegularExpressions.Regex.Match(line,
            @"-\*-\s*(?:mode:\s*)?(\w+)\s*-\*-", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (modeMatch.Success)
        {
            var mode = modeMatch.Groups[1].Value.ToLowerInvariant();
            return mode switch
            {
                "python" => Python(),
                "ruby" => Ruby(),
                "javascript" or "js" => JavaScript(),
                "c" or "cpp" or "c++" => C(),
                "java" => Java(),
                "go" => Go(),
                "rust" => Rust(),
                "shell" or "sh" or "bash" => Shell(),
                "lua" => Lua(),
                "r" => R(),
                "swift" => Swift(),
                "kotlin" => Kotlin(),
                "yaml" => Yaml(),
                "toml" => Toml(),
                "css" => Css(),
                "perl" => Perl(),
                _ => null,
            };
        }
        return null;
    }

    public static SyntaxRuleSet? ForExtension(string ext) => ext switch
    {
        ".cs" => CSharp(),
        ".c" or ".h" or ".cpp" or ".cxx" or ".cc" => C(),
        ".py" => Python(),
        ".js" or ".ts" or ".jsx" or ".tsx" => JavaScript(),
        ".go" => Go(),
        ".rs" => Rust(),
        ".sh" or ".bash" => Shell(),
        ".json" => Json(),
        ".xml" or ".html" or ".htm" => Xml(),
        ".md" => Markdown(),
        ".rb" => Ruby(),
        ".php" => Php(),
        ".java" => Java(),
        ".css" => Css(),
        ".yaml" or ".yml" => Yaml(),
        ".toml" => Toml(),
        ".lua" => Lua(),
        ".r" or ".R" => R(),
        ".swift" => Swift(),
        ".kt" or ".kts" => Kotlin(),
        ".pl" or ".pm" or ".t" => Perl(),
        _ => null,
    };

    private static SyntaxRuleSet CSharp() => new()
    {
        Name = "C#",
        Rules = BuildRules(
            Comment(@"//.*$", @"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|@""(?:[^""]|"""")*""|'(?:\\.|[^'\\])'"),
            Keyword(@"\b(?:abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|record|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while|async|await|partial|yield|get|set|init|with|and|or|not|when)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?[fFdDmMlLuU]*\b|0x[0-9a-fA-F]+"),
            TypeName(@"\b[A-Z][a-zA-Z0-9]*\b"),
            Preprocessor(@"^\s*#.*$")
        )
    };

    private static SyntaxRuleSet C() => new()
    {
        Name = "C/C++",
        Rules = BuildRules(
            Comment(@"//.*$", @"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])'"),
            Keyword(@"\b(?:auto|break|case|char|const|continue|default|do|double|else|enum|extern|float|for|goto|if|inline|int|long|register|restrict|return|short|signed|sizeof|static|struct|switch|typedef|union|unsigned|void|volatile|while|class|namespace|new|delete|virtual|override|template|typename|public|private|protected|bool|true|false|nullptr|constexpr|auto)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?[fFlLuU]*\b|0x[0-9a-fA-F]+"),
            Preprocessor(@"^\s*#.*$")
        )
    };

    private static SyntaxRuleSet Python() => new()
    {
        Name = "Python",
        Rules = BuildRules(
            Comment(@"#.*$"),
            StringLiteral(""""(?:"""[\s\S]*?"""|'''[\s\S]*?'''|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*')""""),
            Keyword(@"\b(?:False|None|True|and|as|assert|async|await|break|class|continue|def|del|elif|else|except|finally|for|from|global|if|import|in|is|lambda|nonlocal|not|or|pass|raise|return|try|while|with|yield)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b")
        )
    };

    private static SyntaxRuleSet JavaScript() => new()
    {
        Name = "JavaScript/TypeScript",
        Rules = BuildRules(
            Comment(@"//.*$", @"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`"),
            Keyword(@"\b(?:break|case|catch|class|const|continue|debugger|default|delete|do|else|export|extends|finally|for|function|if|import|in|instanceof|let|new|null|return|static|super|switch|this|throw|true|false|try|typeof|undefined|var|void|while|with|yield|async|await|of|from|as|type|interface|enum|implements|declare)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b|0x[0-9a-fA-F]+")
        )
    };

    private static SyntaxRuleSet Go() => new()
    {
        Name = "Go",
        Rules = BuildRules(
            Comment(@"//.*$", @"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|`[^`]*`|'(?:\\.|[^'\\])'"),
            Keyword(@"\b(?:break|case|chan|const|continue|default|defer|else|fallthrough|for|func|go|goto|if|import|interface|map|package|range|return|select|struct|switch|type|var)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b")
        )
    };

    private static SyntaxRuleSet Rust() => new()
    {
        Name = "Rust",
        Rules = BuildRules(
            Comment(@"//.*$", @"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])'|r#*""[\s\S]*?""#*"),
            Keyword(@"\b(?:as|async|await|break|const|continue|crate|dyn|else|enum|extern|false|fn|for|if|impl|in|let|loop|match|mod|move|mut|pub|ref|return|self|Self|static|struct|super|trait|true|type|union|unsafe|use|where|while|abstract|become|box|do|final|macro|override|priv|try|typeof|unsized|virtual|yield)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?(?:u8|u16|u32|u64|u128|usize|i8|i16|i32|i64|i128|isize|f32|f64)?\b")
        )
    };

    private static SyntaxRuleSet Shell() => new()
    {
        Name = "Shell",
        Rules = BuildRules(
            Comment(@"#.*$"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'[^']*'"),
            Keyword(@"(?:^|\s)(?:if|then|else|elif|fi|for|while|do|done|case|esac|in|function|return|exit|echo|read|export|local|source|alias|unalias|set|unset|cd|ls|mkdir|rm|cp|mv|cat|grep|sed|awk|find|sort|cut|tr|wc|head|tail|chmod|chown)\b")
        )
    };

    private static SyntaxRuleSet Json() => new()
    {
        Name = "JSON",
        Rules = BuildRules(
            StringLiteral(@"""(?:\\.|[^""\\])*"""),
            Keyword(@"\b(?:true|false|null)\b"),
            Number(@"-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b")
        )
    };

    private static SyntaxRuleSet Xml() => new()
    {
        Name = "XML/HTML",
        Rules = BuildRules(
            Comment(@"<!--[\s\S]*?-->"),
            StringLiteral(@"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'"),
            Keyword(@"</?\w[\w.-]*"),
            Preprocessor(@"<\?[\s\S]*?\?>|<!DOCTYPE[^>]*>")
        )
    };

    private static SyntaxRuleSet Markdown() => new()
    {
        Name = "Markdown",
        Rules = BuildRules(
            Keyword(@"^#{1,6}\s.*$"),
            Comment(@"`[^`]+`|```[\s\S]*?```"),
            StringLiteral(@"\*\*[\s\S]*?\*\*|__[\s\S]*?__"),
            Preprocessor(@"^\s*[-*+]\s|^\s*\d+\.\s")
        )
    };

    private static SyntaxRuleSet Ruby() => new()
    {
        Name = "Ruby",
        Rules = BuildRules(
            Comment(@"#.*$", @"=begin[\s\S]*?=end"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:[^'\\]|\\.)*'|`(?:[^`\\]|\\.)*`"),
            Keyword(@"\b(?:def|end|do|class|module|if|else|elsif|unless|while|until|for|begin|rescue|ensure|return|nil|true|false|self|super|yield|raise|require|include|extend|attr_reader|attr_writer|attr_accessor|lambda|proc)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b")
        )
    };

    private static SyntaxRuleSet Php() => new()
    {
        Name = "PHP",
        Rules = BuildRules(
            Comment(@"//.*$", @"#.*$", @"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:[^'\\]|\\.)*'"),
            Keyword(@"\b(?:echo|print|class|function|if|else|elseif|while|for|foreach|do|switch|case|break|continue|return|new|extends|implements|interface|abstract|public|private|protected|static|final|const|namespace|use|require|include|null|true|false)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b")
        )
    };

    private static SyntaxRuleSet Java() => new()
    {
        Name = "Java",
        Rules = BuildRules(
            Comment(@"//.*$", @"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])'"),
            Keyword(@"\b(?:public|private|protected|class|interface|extends|implements|abstract|final|static|new|return|if|else|while|for|do|switch|case|break|continue|throw|throws|try|catch|finally|null|true|false|void|int|long|double|float|boolean|byte|char|short|import|package|this|super|instanceof)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?[lLfFdD]?\b"),
            TypeName(@"\b[A-Z][a-zA-Z0-9]*\b")
        )
    };

    private static SyntaxRuleSet Css() => new()
    {
        Name = "CSS",
        Rules = BuildRules(
            Comment(@"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:[^""\\]|\\.)*"""),
            Keyword(@"\b(?:color|background|font|margin|padding|border|width|height|display|position|top|left|right|bottom|flex|grid|align|justify|overflow|visibility|opacity|transition|transform|animation|content|float|clear|z-index|cursor)\b"),
            Number(@"-?\b\d+(?:\.\d+)?(?:px|em|rem|%|vh|vw|pt|pc|cm|mm|ex|ch|vmin|vmax|s|ms|deg|rad|turn)?\b")
        )
    };

    private static SyntaxRuleSet Yaml() => new()
    {
        Name = "YAML",
        Rules = BuildRules(
            Comment(@"#.*$"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:[^'\\]|\\.)*'"),
            Keyword(@"(?m)^\s*[\w\-]+(?=\s*:)|\b(?:true|false|null|yes|no|on|off)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b")
        )
    };

    private static SyntaxRuleSet Toml() => new()
    {
        Name = "TOML",
        Rules = BuildRules(
            Comment(@"#.*$"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'[^']*'"),
            Keyword(@"(?m)^\s*\[[\w\.\-]+\]|\b(?:true|false)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b")
        )
    };

    private static SyntaxRuleSet Lua() => new()
    {
        Name = "Lua",
        Rules = BuildRules(
            Comment(@"--(?:\[\[[\s\S]*?\]\]|.*$)"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|\[\[[\s\S]*?\]\]"),
            Keyword(@"\b(?:and|break|do|else|elseif|end|false|for|function|goto|if|in|local|nil|not|or|repeat|return|then|true|until|while)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b|0x[0-9a-fA-F]+")
        )
    };

    private static SyntaxRuleSet R() => new()
    {
        Name = "R",
        Rules = BuildRules(
            Comment(@"#.*$"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'"),
            Keyword(@"\b(?:function|return|if|else|for|while|repeat|break|next|NULL|TRUE|FALSE|NA|Inf|NaN|library|require)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?[iL]?\b")
        )
    };

    private static SyntaxRuleSet Swift() => new()
    {
        Name = "Swift",
        Rules = BuildRules(
            Comment(@"//.*$", @"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:\\.|[^""\\])*"""),
            Keyword(@"\b(?:func|var|let|class|struct|enum|protocol|extension|if|else|guard|switch|case|for|while|return|import|public|private|internal|override|init|self|super|true|false|nil|try|catch|throw|throws|async|await|actor|in|is|as|where)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b"),
            TypeName(@"\b[A-Z][a-zA-Z0-9]*\b")
        )
    };

    private static SyntaxRuleSet Kotlin() => new()
    {
        Name = "Kotlin",
        Rules = BuildRules(
            Comment(@"//.*$", @"/\*[\s\S]*?\*/"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])'"),
            Keyword(@"\b(?:fun|val|var|class|interface|object|if|else|when|for|while|do|return|import|package|public|private|protected|internal|override|data|sealed|abstract|companion|null|true|false|is|in|as|this|super|throw|try|catch|finally)\b"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?[lLfF]?\b"),
            TypeName(@"\b[A-Z][a-zA-Z0-9]*\b")
        )
    };

    private static SyntaxRuleSet Perl() => new()
    {
        Name = "Perl",
        Rules = BuildRules(
            Comment(@"#.*$"),
            StringLiteral(@"""(?:\\.|[^""\\])*""|'(?:[^'\\]|\\.)*'|`(?:[^`\\]|\\.)*`"),
            Keyword(@"\b(?:sub|my|our|local|if|else|elsif|unless|while|until|for|foreach|do|return|last|next|redo|die|warn|print|say|use|require|package|BEGIN|END|eval|chomp|chop|push|pop|shift|unshift|splice|join|split|sort|map|grep|keys|values|delete|exists|defined|undef|ref|bless|tie|untie|open|close|read|write|seek|tell|eof|binmode|length|substr|index|rindex|sprintf|printf|chdir|chmod|chown|mkdir|rmdir|rename|unlink|stat|lstat|glob|opendir|readdir|closedir|scalar|wantarray)\b"),
            Preprocessor(@"[$@%]\w+"),
            Number(@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b|0x[0-9a-fA-F]+|0b[01]+|0[0-7]+")
        )
    };

    private static IReadOnlyList<SyntaxRule> BuildRules(params SyntaxRule[] rules) => rules;
    private static SyntaxRule Comment(params string[] patterns) => MakeRule(TokenType.Comment, patterns);
    private static SyntaxRule StringLiteral(params string[] patterns) => MakeRule(TokenType.String, patterns);
    private static SyntaxRule Keyword(params string[] patterns) => MakeRule(TokenType.Keyword, patterns);
    private static SyntaxRule Number(params string[] patterns) => MakeRule(TokenType.Number, patterns);
    private static SyntaxRule TypeName(params string[] patterns) => MakeRule(TokenType.Type, patterns);
    private static SyntaxRule Preprocessor(params string[] patterns) => MakeRule(TokenType.Preprocessor, patterns);

    private static SyntaxRule MakeRule(TokenType t, string[] patterns)
    {
        var combined = string.Join("|", patterns.Select(p => $"(?:{p})"));
        return new SyntaxRule(new Regex(combined, RO | RegexOptions.Multiline), t);
    }
}
