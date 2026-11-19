using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;
using Nx.Components;

namespace Nx.Demo.Data;

/// <summary>One place the site search can go: a component, an example section, a parameter or a guide.</summary>
public sealed record SearchEntry(string Group, string Title, string Url, string Where, string? Detail, IReadOnlyList<string> Aliases, string? Text, int Order);

/// <summary>Text with the parts that matched the query.</summary>
public sealed record MarkedText(string Text, IReadOnlyList<(int Start, int Length)> Marks)
{
    public static MarkedText Plain(string text) => new(text, []);
}

/// <summary>A search result: the entry, its group (Recent and Pinned too), and what matched: the title, an alias or the summary.</summary>
public sealed record SearchHit(SearchEntry Entry, string Group, int Score, MarkedText Title, MarkedText? Alias = null, MarkedText? Snippet = null);

/// <summary>
/// The site search index, built once from DemoCatalog, the .razor sources embedded in the assembly (example section
/// titles, guides) and reflection over Nx.Components (parameters). Names match ignoring case and the Nx/Telerik/Nep prefix.
/// </summary>
public static partial class SiteSearchIndex
{
    public const string Components = "Components", Examples = "Examples", Parameters = "Parameters", Guides = "Guides",
                        Pinned = "Pinned", Recent = "Recent";

    private static readonly string[] GroupOrder = [Pinned, Recent, Components, Examples, Parameters, Guides];
    private static readonly string[] Prefixes = ["telerik", "nep", "nx"];
    private static readonly Lazy<IReadOnlyList<SearchEntry>> entries = new(Build);

    /// <summary>Every entry, in group order.</summary>
    public static IReadOnlyList<SearchEntry> Entries => entries.Value;

    /// <summary>The id a heading gets on its page; the same rule as the page's anchor script.</summary>
    public static string Anchor(string headingText)
    {
        var text = WebUtility.HtmlDecode(headingText).ToLowerInvariant().Replace("&", "");
        return NonAlnum().Replace(text, "-").Trim('-');
    }

    /// <summary>A component as a Recent or Pinned result.</summary>
    public static SearchHit ForComponent(DemoComponent component, string group)
    {
        var entry = Entries.First(e => e.Group == Components && e.Title == component.Name);
        return new SearchHit(entry, group, 0, MarkedText.Plain(entry.Title));
    }

    /// <summary>Ranked results, at most <paramref name="perGroup"/> in each group, groups in a fixed order.</summary>
    public static IReadOnlyList<SearchHit> Search(string query, int perGroup = 8)
    {
        var q = Normalize(query).Text;
        if (q.Length == 0)
            return [];
        var stripped = StripPrefix(q);
        var words = NonAlnum().Split(query.ToLowerInvariant()).Where(w => w.Length > 0).ToArray();

        var hits = new List<SearchHit>();
        foreach (var entry in Entries)
        {
            if (Score(entry, q, stripped, words) is { } hit)
                hits.Add(hit);
        }
        if (hits.Count == 0 && stripped.Length >= 4)
            hits.AddRange(Typos(stripped));

        return hits.GroupBy(h => h.Group)
                   .OrderBy(g => Array.IndexOf(GroupOrder, g.Key))
                   .SelectMany(g => g.OrderByDescending(h => h.Score).ThenBy(h => h.Entry.Order).Take(perGroup))
                   .ToList();
    }

    // ── Matching ──────────────────────────────────────────────────────────────

    private static SearchHit? Score(SearchEntry entry, string q, string stripped, string[] words)
    {
        // Title: exact > prefix > start of a word > anywhere.
        if (MatchName(entry.Title, q, stripped) is { } title)
            return new SearchHit(entry, entry.Group, 600 + title.Tier * 100 - entry.Title.Length, Mark(entry.Title, title.Start, title.Length));

        // Aliases (Telerik / ERP names, Component.Parameter): below any title match.
        SearchHit? best = null;
        foreach (var alias in entry.Aliases)
        {
            // A parameter's "Component.Name" alias counts only when the query reaches into the parameter's name.
            if (MatchName(alias, q, stripped) is { } m && (best is null || 400 + m.Tier * 30 > best.Score)
                && (entry.Group != Parameters || m.Start + m.Length > alias.IndexOf('.') + 1))
                best = new SearchHit(entry, entry.Group, 400 + m.Tier * 30, MarkedText.Plain(entry.Title),
                    entry.Group == Parameters ? null : Mark(alias, m.Start, m.Length));
        }
        if (best is not null)
            return best;

        // Summary, features, descriptions: every word somewhere.
        if (entry.Text is null || q.Length < 2 || words.Length == 0)
            return null;
        var text = entry.Text.ToLowerInvariant();
        if (!words.All(w => text.Contains(w, StringComparison.Ordinal)))
            return null;
        var titleMarks = WordMarks(entry.Title, words);
        // Outside components the title must share a word, or "combo" lists every combo box example and parameter.
        if (entry.Group != Components && titleMarks.Count == 0)
            return null;
        var note = entry.Group == Components ? Snippet(entry.Text, words) : null;
        return new SearchHit(entry, entry.Group, 100 + titleMarks.Count * 20, new MarkedText(entry.Title, titleMarks), Snippet: note);
    }

    private readonly record struct NameMatch(int Tier, int Start, int Length);

    /// <summary>Matches a name ignoring case, punctuation and the Nx/Telerik/Nep prefix. Tier 3 exact … 0 anywhere.</summary>
    private static NameMatch? MatchName(string name, string q, string stripped)
    {
        var (norm, map) = Normalize(name);
        if (norm.Length == 0)
            return null;
        var offset = norm.Length - StripPrefix(norm).Length;

        (string, int)[] candidates = offset > 0 ? [(norm, 0), (norm[offset..], offset)] : [(norm, 0)];
        string[] queries = q == stripped ? [q] : [q, stripped];
        NameMatch? best = null;
        foreach (var (candidate, at) in candidates)
        {
            foreach (var query in queries)
            {
                int tier, pos;
                if (candidate == query) (tier, pos) = (3, 0);
                else if (candidate.StartsWith(query, StringComparison.Ordinal)) (tier, pos) = (2, 0);
                else if ((pos = WordStartIndex(name, map, candidate, at, query)) >= 0) tier = 1;
                else if (query.Length >= 2 && (pos = candidate.IndexOf(query, StringComparison.Ordinal)) >= 0) tier = 0;
                else continue;

                var start = map[at + pos];
                var end = map[at + pos + query.Length - 1] + 1;
                if (best is null || tier > best.Value.Tier)
                    best = new NameMatch(tier, start, end - start);
            }
        }
        return best;
    }

    /// <summary>Where <paramref name="query"/> starts a word of the name (after a space or punctuation, or at a capital).</summary>
    private static int WordStartIndex(string name, int[] map, string candidate, int at, string query)
    {
        for (var pos = candidate.IndexOf(query, StringComparison.Ordinal); pos >= 0; pos = candidate.IndexOf(query, pos + 1, StringComparison.Ordinal))
        {
            var i = map[at + pos];
            if (i == 0 || !char.IsLetterOrDigit(name[i - 1]) || (char.IsUpper(name[i]) && char.IsLower(name[i - 1])))
                return pos;
        }
        return -1;
    }

    /// <summary>Close spellings of component names and aliases, for "dropdwonlist" or a mistyped address.</summary>
    private static IEnumerable<SearchHit> Typos(string stripped)
    {
        foreach (var entry in Entries.Where(e => e.Group == Components))
        {
            var best = entry.Aliases.Prepend(entry.Title)
                .Select(n => (Name: n, Distance: Distance(StripPrefix(Normalize(n).Text), stripped)))
                .MinBy(x => x.Distance);
            if (best.Distance <= Math.Max(1, stripped.Length / 4))
                yield return new SearchHit(entry, entry.Group, 50 - best.Distance, MarkedText.Plain(entry.Title),
                    Alias: best.Name == entry.Title ? null : MarkedText.Plain(best.Name));
        }
    }

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var diagonal = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        return row[b.Length];
    }

    private static (string Text, int[] Map) Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        var map = new List<int>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsLetterOrDigit(text[i]))
                continue;
            sb.Append(char.ToLowerInvariant(text[i]));
            map.Add(i);
        }
        return (sb.ToString(), map.ToArray());
    }

    private static string StripPrefix(string norm)
    {
        foreach (var prefix in Prefixes)
        {
            if (norm.Length > prefix.Length && norm.StartsWith(prefix, StringComparison.Ordinal))
                return norm[prefix.Length..];
        }
        return norm;
    }

    private static MarkedText Mark(string text, int start, int length) => new(text, [(start, length)]);

    private static List<(int Start, int Length)> WordMarks(string text, string[] words)
    {
        var marks = new List<(int Start, int Length)>();
        foreach (var word in words)
        {
            var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (at >= 0 && !marks.Any(m => at < m.Start + m.Length && m.Start < at + word.Length))
                marks.Add((at, word.Length));
        }
        marks.Sort();
        return marks;
    }

    /// <summary>About 90 characters of the text around the first matched word.</summary>
    private static MarkedText Snippet(string text, string[] words)
    {
        var first = words.Select(w => text.IndexOf(w, StringComparison.OrdinalIgnoreCase)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, first - 30);
        if (start > 0)
        {
            var space = text.IndexOf(' ', start);
            start = space >= 0 && space < first ? space + 1 : start;
        }
        var length = Math.Min(90, text.Length - start);
        var part = (start > 0 ? "…" : "") + text.Substring(start, length) + (start + length < text.Length ? "…" : "");
        return new MarkedText(part, WordMarks(part, words));
    }

    // ── Building ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<SearchEntry> Build()
    {
        var list = new List<SearchEntry>();
        var order = DemoCatalog.Components.Select((c, i) => (c.Slug, i)).ToDictionary(x => x.Slug, x => x.i);
        var pages = ReadPages();

        foreach (var (component, i) in DemoCatalog.Components.Select((c, i) => (c, i)))
        {
            list.Add(new SearchEntry(Components, component.Name, $"{component.Slug}/overview", $"{component.Category} · Overview", null,
                component.Aliases, $"{component.Summary} {string.Join(" · ", component.Features)}", i));
        }

        foreach (var page in pages.Where(p => p.Route.EndsWith("/examples", StringComparison.Ordinal)))
        {
            var slug = page.Route.Split('/')[0];
            if (DemoCatalog.Find(slug) is not { } component)
                continue;
            foreach (var (heading, j) in page.Sections.Select((h, j) => (h, j)))
            {
                list.Add(new SearchEntry(Examples, heading, $"{slug}/examples#{Anchor(heading)}", $"{component.Name} › Examples", null,
                    [], $"{component.Name} {heading}", order[slug] * 100 + j));
            }
        }

        list.AddRange(ParameterEntries(pages, order));

        list.Add(new SearchEntry(Guides, "Overview", "", "Guide · Home", null, ["Home", "All components"], "Every component at a glance", 0));
        foreach (var (page, i) in pages.Where(p => p.Route.StartsWith("guides/", StringComparison.Ordinal)).Select((p, i) => (p, i)))
        {
            var title = page.Title ?? page.Route;
            list.Add(new SearchEntry(Guides, title, page.Route, "Guide", null, [], title, (i + 1) * 100));
            foreach (var (heading, j) in page.Sections.Select((h, j) => (h, j)))
                list.Add(new SearchEntry(Guides, heading, $"{page.Route}#{Anchor(heading)}", $"Guide › {title}", null, [], $"{title} {heading}", (i + 1) * 100 + j + 1));
        }
        return list;
    }

    private sealed record PageSource(string Route, string? Title, IReadOnlyList<string> Sections, IReadOnlyList<string> Headings);

    /// <summary>The .razor pages embedded in this assembly (Nx.Demo.csproj): route, h1, doc-section titles, every h2.</summary>
    private static List<PageSource> ReadPages()
    {
        var assembly = typeof(SiteSearchIndex).Assembly;
        var pages = new List<PageSource>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".razor", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();

            var routes = PageDirective().Matches(source).Select(m => m.Groups[1].Value.Trim('/')).ToList();
            if (routes.Count == 0)
                continue;
            var route = routes.FirstOrDefault(r => r.Contains('/')) ?? routes[0];
            var h1 = H1().Match(source);
            pages.Add(new PageSource(route, h1.Success ? Clean(h1.Groups[1].Value) : null, SectionTitles(source),
                H2().Matches(source).Select(m => Clean(m.Groups[1].Value)).ToList()));
        }
        return pages;
    }

    /// <summary>The first h2 inside each <c>&lt;div class="doc-section"&gt;</c>.</summary>
    private static List<string> SectionTitles(string source)
    {
        var titles = new List<string>();
        var opens = DocSection().Matches(source);
        for (var i = 0; i < opens.Count; i++)
        {
            var end = i + 1 < opens.Count ? opens[i + 1].Index : source.Length;
            var h2 = H2().Match(source, opens[i].Index);
            if (h2.Success && h2.Index < end && Clean(h2.Groups[1].Value) is { Length: > 0 } title && !title.Contains('@'))
                titles.Add(title);
        }
        return titles;
    }

    private static string Clean(string html) =>
        WebUtility.HtmlDecode(Whitespace().Replace(Tags().Replace(html, ""), " ")).Trim();

    /// <summary>Every public component's [Parameter]s, linked to the API page that documents the component.</summary>
    private static IEnumerable<SearchEntry> ParameterEntries(List<PageSource> pages, Dictionary<string, int> order)
    {
        var apiPages = pages.Where(p => p.Route.EndsWith("/api", StringComparison.Ordinal)).ToList();
        var docs = LoadXmlDocs();
        var types = typeof(NxButton).Assembly.GetExportedTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (Type: t, Name: t.Name.Split('`')[0]));

        foreach (var (type, name) in types)
        {
            if (FindApiPage(name, apiPages) is not { } found || !order.TryGetValue(found.Slug, out var componentOrder))
                continue;
            var (slug, anchor) = found;
            var url = anchor is null ? $"{slug}/api" : $"{slug}/api#{anchor}";
            var isMain = DemoCatalog.Find(slug)?.Name == name;
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ParameterAttribute>() is { CaptureUnmatchedValues: false });
            foreach (var (property, j) in properties.Select((p, j) => (p, j)))
            {
                docs.TryGetValue($"P:{property.DeclaringType!.FullName}.{property.Name}", out var summary);
                yield return new SearchEntry(Parameters, property.Name, url, $"{name} › API", TypeName(property.PropertyType),
                    [$"{name}.{property.Name}"], $"{name} {summary}", componentOrder * 1000 + (isMain ? 0 : 500) + j);
            }
        }
    }

    /// <summary>The component's own API page, or the one with a heading naming it (NxTab on the tab strip's), else by name prefix.</summary>
    private static (string Slug, string? Anchor)? FindApiPage(string typeName, List<PageSource> apiPages)
    {
        if (DemoCatalog.Components.FirstOrDefault(c => c.Name == typeName) is { } own)
        {
            var page = apiPages.FirstOrDefault(p => p.Route == $"{own.Slug}/api");
            var heading = page?.Headings.FirstOrDefault(h => h == "Parameters" || h == $"{typeName} Parameters" || h == typeName);
            return (own.Slug, heading is null ? null : Anchor(heading));
        }
        foreach (var page in apiPages)
        {
            var heading = page.Headings.FirstOrDefault(h => Words().Matches(h).Any(w => w.Value == typeName));
            if (heading is not null)
                return (page.Route.Split('/')[0], Anchor(heading));
        }
        var parent = DemoCatalog.Components.Where(c => typeName.StartsWith(c.Name, StringComparison.Ordinal)).MaxBy(c => c.Name.Length);
        return parent is null ? null : (parent.Slug, null);
    }

    /// <summary>The library's XML doc summaries, by member id; empty when the file is not beside the app.</summary>
    private static Dictionary<string, string> LoadXmlDocs()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Nx.Components.xml");
        if (!File.Exists(path))
            return [];
        return XDocument.Load(path).Descendants("member")
            .Where(m => m.Element("summary") is not null)
            .ToDictionary(m => (string)m.Attribute("name")!, m => Whitespace().Replace(DocText(m.Element("summary")!), " ").Trim());
    }

    private static string DocText(XElement element) => string.Concat(element.Nodes().Select(n => n switch
    {
        XText text => text.Value,
        XElement { Name.LocalName: "see" } see => (string?)see.Attribute("langword")
                                                  ?? ((string?)see.Attribute("cref"))?.Split('.', ':').Last().Split('`')[0]
                                                  ?? see.Value,
        XElement e => DocText(e),
        _ => "",
    }));

    private static string TypeName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner)
            return TypeName(inner) + "?";
        if (!type.IsGenericType)
            return type.Name switch
            {
                "String" => "string", "Boolean" => "bool", "Int32" => "int", "Double" => "double", "Decimal" => "decimal",
                "Single" => "float", "Object" => "object", "Int64" => "long", _ => type.Name,
            };
        return $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
    }

    [GeneratedRegex("[^a-z0-9]+")] private static partial Regex NonAlnum();
    [GeneratedRegex(@"@page\s+""([^""]*)""")] private static partial Regex PageDirective();
    [GeneratedRegex(@"<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline)] private static partial Regex H1();
    [GeneratedRegex(@"<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline)] private static partial Regex H2();
    [GeneratedRegex(@"<div\s+class=""doc-section[""\s]")] private static partial Regex DocSection();
    [GeneratedRegex("<[^>]+>")] private static partial Regex Tags();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"\w+")] private static partial Regex Words();
}
