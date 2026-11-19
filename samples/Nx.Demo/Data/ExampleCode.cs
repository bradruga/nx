using System.Text.RegularExpressions;

namespace Nx.Demo.Data;

/// <summary>
/// The markup of each example, read from the pages' own .razor source (embedded in the assembly). A page marks the
/// markup to show with <c>@* code *@</c> … <c>@* /code *@</c>; the next <c>&lt;DocExampleCode Key="…" /&gt;</c>
/// shows every marked region since the previous one, with demo-only ids and <c>demo-*</c> classes trimmed.
/// </summary>
public static partial class ExampleCode
{
    const string Open = "@* code *@", Close = "@* /code *@";

    // route ("/button/examples") → key → code
    static readonly Lazy<Dictionary<string, Dictionary<string, string>>> Pages = new(Load);

    /// <summary>The code for <paramref name="key"/> on the page at <paramref name="route"/>, or null.</summary>
    public static string? Find(string route, string key) =>
        Pages.Value.TryGetValue(route, out var page) && page.TryGetValue(key, out var code) ? code : null;

    static Dictionary<string, Dictionary<string, string>> Load()
    {
        var pages = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(ExampleCode).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".razor", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd().Replace("\r\n", "\n");
            if (!source.Contains(Open, StringComparison.Ordinal))
                continue;
            var blocks = Extract(source);
            foreach (Match route in PageDirective().Matches(source))
                pages[route.Groups[1].Value] = blocks;
        }
        return pages;
    }

    /// <summary>Every example's code in a page's source, by its <c>DocExampleCode</c> key.</summary>
    public static Dictionary<string, string> Extract(string source)
    {
        var blocks = new Dictionary<string, string>(StringComparer.Ordinal);
        int from = 0;
        foreach (Match example in ExampleTag().Matches(source))
        {
            var regions = new List<string>();
            int at = from;
            while (true)
            {
                int open = source.IndexOf(Open, at, example.Index - at, StringComparison.Ordinal);
                if (open < 0)
                    break;
                int start = source.IndexOf('\n', open) + 1;
                int close = source.IndexOf(Close, start, example.Index - start, StringComparison.Ordinal);
                if (close < 0)
                    break;
                int end = source.LastIndexOf('\n', close) + 1;
                regions.Add(Trim(source[start..end]));
                at = close + Close.Length;
            }
            blocks[example.Groups[1].Value] = string.Join("\n\n", regions.Where(r => r.Length > 0));
            from = example.Index + example.Length;
        }
        return blocks;
    }

    // Drops what only the demo needs (ids for tests, demo-* layout classes and bare wrapper divs), then dedents.
    static string Trim(string region)
    {
        // An id stays when the example points at it (a label's for, a button's form, a #selector).
        var referenced = IdReference().Matches(region).Select(m => m.Groups[1].Value + m.Groups[2].Value).ToHashSet();
        region = IdAttribute().Replace(region, m => referenced.Contains(m.Groups[1].Value) ? m.Value : "");
        region = ClassAttribute().Replace(region, m =>
        {
            var kept = m.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(c => !c.StartsWith("demo-", StringComparison.Ordinal) && !c.StartsWith("doc-", StringComparison.Ordinal));
            var classes = string.Join(' ', kept);
            return classes.Length == 0 ? "" : $" {m.Groups[1].Value}=\"{classes}\"";
        });
        region = StyleAttribute().Replace(region, "");
        region = TestDataAttribute().Replace(region, "");
        var lines = region.Split('\n').Select(l => l.TrimEnd()).ToList();
        Unwrap(lines);
        for (int i = 0; i < lines.Count; i++)
            lines[i] = BareDivLine().Replace(lines[i], "$1$2");
        return Dedent(lines);
    }

    // Removes a <div> left with no attributes and its closing tag, when each is alone on its line.
    static void Unwrap(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim() != "<div>")
                continue;
            int depth = 0, close = -1;
            for (int j = i; j < lines.Count && close < 0; j++)
            {
                depth += DivOpen().Matches(lines[j]).Count - lines[j].Split("</div>").Length + 1;
                if (depth == 0)
                    close = j;
            }
            if (close < 0 || lines[close].Trim() != "</div>")
                continue;
            int outer = Indent(lines[i]);
            var inner = lines.Skip(i + 1).Take(close - i - 1).Where(l => l.Length > 0).Select(Indent).DefaultIfEmpty(outer).Min();
            int shift = Math.Max(0, inner - outer);
            for (int j = i + 1; j < close; j++)
                lines[j] = lines[j].Length >= shift ? lines[j][shift..] : lines[j].TrimStart();
            lines.RemoveAt(close);
            lines.RemoveAt(i);
            i--;
        }
    }

    static string Dedent(List<string> lines)
    {
        while (lines.Count > 0 && lines[0].Length == 0)
            lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        var indent = lines.Where(l => l.Length > 0).Select(Indent).DefaultIfEmpty(0).Min();
        return string.Join('\n', lines.Select(l => l.Length >= indent ? l[indent..] : l));
    }

    static int Indent(string line) => line.Length - line.TrimStart().Length;

    [GeneratedRegex("""^@page\s+"([^"]+)"$""", RegexOptions.Multiline)]
    private static partial Regex PageDirective();

    [GeneratedRegex("""<DocExampleCode\s+Key="([^"]+)"\s*/>""")]
    private static partial Regex ExampleTag();

    [GeneratedRegex(@"\s(?:for|form|aria-labelledby|aria-describedby|aria-controls)=""([^""@]+)""|#([A-Za-z][\w-]*)")]
    private static partial Regex IdReference();

    [GeneratedRegex(@"\s(?:id|Id)=""(@\((?:[^()]|\([^()]*\))*\)|[^""]*)""")]
    private static partial Regex IdAttribute();

    [GeneratedRegex(@"\s(class|Class)=""([^""@]*)""")]
    private static partial Regex ClassAttribute();

    // A plain element's demo layout; a component's Style parameter and styles that show a value stay.
    [GeneratedRegex(@"\sstyle=""[^""@]*""")]
    private static partial Regex StyleAttribute();

    // Test hooks such as data-size; data-nx-* is component API.
    [GeneratedRegex(@"\sdata-(?!nx-)[\w-]+=""[^""]*""")]
    private static partial Regex TestDataAttribute();

    [GeneratedRegex(@"^(\s*)<div>((?:(?!<div).)*)</div>$")]
    private static partial Regex BareDivLine();

    [GeneratedRegex("""<div[\s>]""")]
    private static partial Regex DivOpen();
}
