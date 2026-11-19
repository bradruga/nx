using System.Text;

namespace Nx.Demo.Components.Shared;

/// <summary>A highlighted run of code: <see cref="Kind"/> is its CSS class, or null for plain text.</summary>
public readonly record struct CodeToken(string? Kind, string Text);

/// <summary>
/// Splits a Razor or C# snippet into highlighted tokens for <see cref="DocCode"/>: tags, attribute names and values,
/// Razor transitions, C# keywords, strings, comments and numbers. A small tokenizer for short, well-formed samples,
/// not a parser; anything it does not recognize stays plain text.
/// </summary>
public static class CodeHighlighter
{
    public const string Tag = "tok-tag", Attr = "tok-attr", Str = "tok-str", Razor = "tok-razor",
                        Keyword = "tok-kw", Comment = "tok-com", Number = "tok-num";

    static readonly HashSet<string> Keywords =
    [
        "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch", "char", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "false", "finally", "float",
        "for", "foreach", "get", "if", "in", "init", "int", "interface", "internal", "is", "long", "namespace", "new",
        "not", "null", "object", "out", "override", "params", "private", "protected", "public", "readonly", "record",
        "ref", "return", "sealed", "set", "static", "string", "struct", "switch", "this", "throw", "true", "try",
        "typeof", "using", "var", "virtual", "void", "when", "while", "with", "yield", "and", "or", "nameof", "required",
    ];

    // Razor keywords after @ that are followed by C#, not by an expression.
    static readonly HashSet<string> RazorDirectives =
    [
        "code", "functions", "if", "else", "foreach", "for", "while", "switch", "using", "inject", "page", "implements",
        "inherits", "rendermode", "attribute", "typeparam", "layout", "namespace", "try", "catch", "finally", "do", "key", "ref",
    ];

    static readonly string[] CSharpLineStarts = ["await ", "var ", "if (", "else", "return ", "foreach (", "using ", "//", "builder.", "app."];

    /// <summary>Tokenizes <paramref name="code"/>; a snippet starting with markup or <c>@</c> is Razor, anything else C#.</summary>
    public static List<CodeToken> Tokenize(string code)
    {
        var tokens = new Tokens();
        var first = code.TrimStart();
        if (first.StartsWith('<') || first.StartsWith('@'))
            new Scanner(code, tokens).Razor(0, code.Length);
        else
            new Scanner(code, tokens).CSharpLines(0, code.Length);
        return tokens.List;
    }

    sealed class Tokens
    {
        public readonly List<CodeToken> List = [];
        readonly StringBuilder plain = new();

        public void Plain(string text) => plain.Append(text);
        public void Plain(char c) => plain.Append(c);

        public void Add(string kind, string text)
        {
            if (text.Length == 0)
                return;
            Flush();
            List.Add(new CodeToken(kind, text));
        }

        public void Flush()
        {
            if (plain.Length == 0)
                return;
            List.Add(new CodeToken(null, plain.ToString()));
            plain.Clear();
        }
    }

    sealed class Scanner(string s, Tokens t)
    {
        // ── Razor: markup with @ transitions ──────────────────────────────────────

        public void Razor(int i, int end)
        {
            bool lineStart = true;
            while (i < end)
            {
                if (lineStart && LooksLikeCSharpLine(i, end, out var lineEnd))
                {
                    CSharp(i, lineEnd);
                    i = lineEnd;
                    continue;
                }
                char c = s[i];
                lineStart = c == '\n' || (lineStart && (c == ' ' || c == '\t' || c == '\r'));
                if (Starts(i, "@*"))
                    i = Comment(i, "*@", end);
                else if (Starts(i, "<!--"))
                    i = Comment(i, "-->", end);
                else if (Starts(i, "@@"))
                {
                    t.Plain("@@");
                    i += 2;
                }
                else if (c == '@')
                    i = Transition(i, end);
                else if (c == '<' && i + 1 < end && (char.IsLetter(s[i + 1]) || s[i + 1] == '/'))
                    i = MarkupTag(i, end);
                else
                {
                    t.Plain(c);
                    i++;
                }
            }
            t.Flush();
        }

        // A line of statements between markup (a snippet mixing setup code and markup).
        bool LooksLikeCSharpLine(int i, int end, out int lineEnd)
        {
            int textStart = i;
            while (textStart < end && (s[textStart] == ' ' || s[textStart] == '\t'))
                textStart++;
            lineEnd = s.IndexOf('\n', textStart, end - textStart);
            if (lineEnd < 0)
                lineEnd = end;
            var line = s[textStart..lineEnd].TrimEnd();
            if (line.Length == 0 || line[0] == '<' || line[0] == '@')
                return false;
            return line.EndsWith(';') || CSharpLineStarts.Any(line.StartsWith);
        }

        int Transition(int i, int end)
        {
            int start = i++;
            if (i < end && s[i] == '(')
            {
                i = Balanced(i, end, '(', ')');
                t.Add(CodeHighlighter.Razor, s[start..i]);
                return i;
            }
            if (i < end && s[i] == '{')
            {
                t.Add(CodeHighlighter.Razor, "@");
                return CSharpBlock(i, end);
            }
            int word = i;
            while (i < end && (char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                i++;
            var name = s[word..i];
            if (RazorDirectives.Contains(name))
            {
                t.Add(CodeHighlighter.Razor, s[start..i]);
                if (name is "code" or "functions")
                {
                    int brace = i;
                    while (brace < end && char.IsWhiteSpace(s[brace]))
                        brace++;
                    if (brace < end && s[brace] == '{')
                    {
                        t.Plain(s[i..brace]);
                        return CSharpBlock(brace, end);
                    }
                }
                else if (name is "if" or "foreach" or "for" or "while" or "switch" or "catch")
                {
                    int paren = i;
                    while (paren < end && s[paren] == ' ')
                        paren++;
                    if (paren < end && s[paren] == '(')
                    {
                        int close = Balanced(paren, end, '(', ')');
                        CSharp(i, close);
                        return close;
                    }
                }
                else if (name is not ("else" or "try" or "finally" or "do" or "key" or "ref"))
                {
                    // @inject, @using, @page…: the rest of the line is C#
                    int eol = s.IndexOf('\n', i, end - i);
                    eol = eol < 0 ? end : eol;
                    CSharp(i, eol);
                    return eol;
                }
                return i;
            }
            // An implicit expression: @name, @item.Name, @Format(x)
            while (i < end)
            {
                if (s[i] == '.' && i + 1 < end && char.IsLetter(s[i + 1]))
                    i++;
                else if (s[i] == '(' || s[i] == '[')
                    i = Balanced(i, end, s[i], s[i] == '(' ? ')' : ']');
                else if (char.IsLetterOrDigit(s[i]) || s[i] == '_')
                    i++;
                else
                    break;
            }
            t.Add(CodeHighlighter.Razor, s[start..i]);
            return i;
        }

        int CSharpBlock(int open, int end)
        {
            int close = Balanced(open, end, '{', '}');
            bool closed = close <= end && close > open && s[close - 1] == '}';
            t.Plain('{');
            CSharp(open + 1, closed ? close - 1 : close);
            if (closed)
                t.Plain('}');
            return close;
        }

        int MarkupTag(int i, int end)
        {
            int start = i++;
            if (s[i] == '/')
                i++;
            while (i < end && (char.IsLetterOrDigit(s[i]) || s[i] is ':' or '.' or '-' or '_'))
                i++;
            t.Add(Tag, s[start..i]);
            while (i < end)
            {
                char c = s[i];
                if (c == '>')
                {
                    t.Add(Tag, ">");
                    return i + 1;
                }
                if (c == '/' && i + 1 < end && s[i + 1] == '>')
                {
                    t.Add(Tag, "/>");
                    return i + 2;
                }
                if (char.IsWhiteSpace(c))
                {
                    t.Plain(c);
                    i++;
                    continue;
                }
                if (Starts(i, "@*"))
                {
                    i = Comment(i, "*@", end);
                    continue;
                }
                if (c == '=')
                {
                    t.Plain('=');
                    i = AttributeValue(i + 1, end);
                    continue;
                }
                int name = i;
                while (i < end && !char.IsWhiteSpace(s[i]) && s[i] != '=' && s[i] != '>' && !(s[i] == '/' && i + 1 < end && s[i + 1] == '>'))
                    i++;
                if (i == name)
                {
                    t.Plain(c);
                    i++;
                    continue;
                }
                t.Add(s[name] == '@' ? CodeHighlighter.Razor : Attr, s[name..i]);
            }
            return i;
        }

        int AttributeValue(int i, int end)
        {
            if (i >= end)
                return i;
            char quote = s[i];
            if (quote is not ('"' or '\''))
            {
                int start = i;
                while (i < end && !char.IsWhiteSpace(s[i]) && s[i] != '>' && !(s[i] == '/' && i + 1 < end && s[i + 1] == '>'))
                    i++;
                t.Add(Str, s[start..i]);
                return i;
            }
            // Quoted: skip balanced parentheses so @(() => Add("x")) stays one value.
            int from = i++;
            int depth = 0;
            while (i < end)
            {
                char c = s[i];
                if (depth > 0 && c == '"')
                    i = SkipString(i, end);
                else
                {
                    if (c == '(') depth++;
                    else if (c == ')' && depth > 0) depth--;
                    else if (c == quote && depth == 0)
                    {
                        i++;
                        break;
                    }
                    i++;
                }
            }
            t.Add(Str, s[from..i]);
            return i;
        }

        // ── C# ───────────────────────────────────────────────────────────────────

        // C# where whole lines of markup may appear (a snippet that starts with code).
        public void CSharpLines(int i, int end)
        {
            while (i < end)
            {
                int lineEnd = s.IndexOf('\n', i, end - i);
                lineEnd = lineEnd < 0 ? end : lineEnd + 1;
                var text = s[i..lineEnd].TrimStart();
                if (text.StartsWith('<') || text.StartsWith("@*") || (text.StartsWith('@') && text.Length > 1 && char.IsLetter(text[1])))
                    Razor(i, lineEnd);
                else
                    CSharp(i, lineEnd);
                i = lineEnd;
            }
            t.Flush();
        }

        void CSharp(int i, int end)
        {
            while (i < end)
            {
                char c = s[i];
                if (Starts(i, "//"))
                {
                    int eol = s.IndexOf('\n', i, end - i);
                    eol = eol < 0 ? end : eol;
                    t.Add(CodeHighlighter.Comment, s[i..eol]);
                    i = eol;
                }
                else if (Starts(i, "/*"))
                    i = Comment(i, "*/", end);
                else if (Starts(i, "@*"))
                    i = Comment(i, "*@", end);
                else if (Starts(i, "\"\"\""))
                {
                    int close = s.IndexOf("\"\"\"", i + 3, end - i - 3, StringComparison.Ordinal);
                    close = close < 0 ? end : close + 3;
                    t.Add(Str, s[i..close]);
                    i = close;
                }
                else if (c == '"' || ((c == '$' || c == '@') && i + 1 < end && s[i + 1] == '"')
                         || (c == '$' && Starts(i + 1, "@\"")) || (c == '@' && Starts(i + 1, "$\"")))
                {
                    int from = i;
                    bool verbatim = false;
                    while (s[i] != '"')
                    {
                        verbatim |= s[i] == '@';
                        i++;
                    }
                    i = verbatim ? SkipVerbatim(i, end) : SkipString(i, end);
                    t.Add(Str, s[from..i]);
                }
                else if (c == '\'' && i + 2 < end && (s[i + 2] == '\'' || (s[i + 1] == '\\' && i + 3 < end && s[i + 3] == '\'')))
                {
                    int close = s.IndexOf('\'', i + 2) + 1;
                    t.Add(Str, s[i..close]);
                    i = close;
                }
                else if (char.IsDigit(c) && (i == 0 || !IsIdent(s[i - 1])))
                {
                    int from = i;
                    while (i < end && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || (s[i] == '.' && i + 1 < end && char.IsDigit(s[i + 1]))))
                        i++;
                    t.Add(Number, s[from..i]);
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    int from = i;
                    while (i < end && IsIdent(s[i]))
                        i++;
                    var word = s[from..i];
                    if (Keywords.Contains(word))
                        t.Add(Keyword, word);
                    else
                        t.Plain(word);
                }
                else if (c == '@' && i + 1 < end && s[i + 1] == '<')
                {
                    // A Razor template inside C#: @<span>…</span>
                    t.Add(CodeHighlighter.Razor, "@");
                    i++;
                }
                else if (c == '<' && i > 0 && s[i - 1] == '@')
                    i = MarkupTag(i, end);
                else if (c == '<' && i + 1 < end && s[i + 1] == '/' && i + 2 < end && char.IsLetter(s[i + 2]))
                    i = MarkupTag(i, end);
                else
                {
                    t.Plain(c);
                    i++;
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c == '_';

        bool Starts(int i, string value) => string.CompareOrdinal(s, i, value, 0, value.Length) == 0;

        int Comment(int i, string close, int end)
        {
            int at = s.IndexOf(close, i + 2, StringComparison.Ordinal);
            int stop = at < 0 || at + close.Length > end ? end : at + close.Length;
            t.Add(CodeHighlighter.Comment, s[i..stop]);
            return stop;
        }

        // From an opening quote to just past the closing one, honoring backslash escapes.
        int SkipString(int i, int end)
        {
            i++;
            while (i < end && s[i] != '"' && s[i] != '\n')
                i += s[i] == '\\' ? 2 : 1;
            return Math.Min(i + 1, end);
        }

        int SkipVerbatim(int i, int end)
        {
            i++;
            while (i < end)
            {
                if (s[i] == '"')
                {
                    if (i + 1 < end && s[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }
                    return i + 1;
                }
                i++;
            }
            return end;
        }

        // From an opening bracket to just past its match, skipping strings.
        int Balanced(int i, int end, char open, char close)
        {
            int depth = 0;
            while (i < end)
            {
                char c = s[i];
                if (c == '"')
                {
                    i = SkipString(i, end);
                    continue;
                }
                if (c == open)
                    depth++;
                else if (c == close && --depth == 0)
                    return i + 1;
                i++;
            }
            return end;
        }
    }
}
