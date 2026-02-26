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

    public static SyntaxHighlighter? ForFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return SyntaxRuleSet.ForExtension(ext) is { } rules ? new SyntaxHighlighter(rules) : null;
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
