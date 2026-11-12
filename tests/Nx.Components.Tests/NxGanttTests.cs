using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxGanttTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-gantt.js");
        module.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-input.js").Mode = JSRuntimeMode.Loose;
    }

    private sealed class Job
    {
        public int Id { get; init; }
        public int? ParentId { get; init; }
        public string Title { get; set; } = "";
        public string Crew { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    private sealed record Link(int PredecessorId, int SuccessorId, NxGanttDependencyType Type = NxGanttDependencyType.FinishStart);

    private static readonly DateTime Monday = new(2026, 11, 9);

    private static List<Job> Jobs() =>
    [
        new() { Id = 1, Title = "Design", Start = Monday, End = Monday.AddDays(10) },
        new() { Id = 2, ParentId = 1, Title = "Drawings", Crew = "Drafting", Start = Monday, End = Monday.AddDays(6) },
        new() { Id = 3, ParentId = 1, Title = "Review", Crew = "PE", Start = Monday.AddDays(6), End = Monday.AddDays(10) },
        new() { Id = 4, Title = "Pour", Crew = "Crew A", Start = Monday.AddDays(10), End = Monday.AddDays(15) },
    ];

    private static RenderFragment Columns(bool editor = false) => b =>
    {
        b.OpenComponent<NxGanttColumn<Job>>(0);
        b.AddComponentParameter(1, nameof(NxGanttColumn<Job>.Field), nameof(Job.Title));
        b.AddComponentParameter(2, nameof(NxGanttColumn<Job>.Title), "Task");
        b.AddComponentParameter(3, nameof(NxGanttColumn<Job>.Width), "200px");
        b.AddComponentParameter(4, nameof(NxGanttColumn<Job>.Expandable), true);
        if (editor)
            b.AddComponentParameter(5, nameof(NxGanttColumn<Job>.EditorTemplate), (RenderFragment<Job>)(j => t => t.AddMarkupContent(0, "<input class=\"ed\" />")));
        b.CloseComponent();
        b.OpenComponent<NxGanttColumn<Job>>(10);
        b.AddComponentParameter(11, nameof(NxGanttColumn<Job>.Field), nameof(Job.Crew));
        b.AddComponentParameter(12, nameof(NxGanttColumn<Job>.Width), "100px");
        b.CloseComponent();
        b.OpenComponent<NxGanttColumn<Job>>(20);
        b.AddComponentParameter(21, nameof(NxGanttColumn<Job>.Field), nameof(Job.Start));
        b.AddComponentParameter(22, nameof(NxGanttColumn<Job>.DisplayFormat), "{0:MM/dd/yy}");
        b.CloseComponent();
    };

    private IRenderedComponent<NxGantt<Job>> RenderGantt(Action<ComponentParameterCollectionBuilder<NxGantt<Job>>>? configure = null,
        List<Job>? jobs = null, bool editor = false) =>
        Render<NxGantt<Job>>(p =>
        {
            p.Add(x => x.Id, "g");
            p.Add(x => x.Data, jobs ?? Jobs());
            p.Add(x => x.Columns, Columns(editor));
            p.Add(x => x.Culture, CultureInfo.GetCultureInfo("en-US"));
            configure?.Invoke(p);
        });

    private static string[] Titles(IRenderedComponent<NxGantt<Job>> cut) =>
        [.. cut.FindAll("[role=row]").Select(r => r.QuerySelector(".nx-gantt-cell")!.TextContent)];

    [Test]
    public void Render_TreeRows_Cells_Bars()
    {
        var cut = RenderGantt();

        Assert.That(Titles(cut), Is.EqualTo(new[] { "Design", "Drawings", "Review", "Pour" }));
        var rows = cut.FindAll("[role=row]");
        Assert.That(rows[1].GetAttribute("aria-level"), Is.EqualTo("2"));
        Assert.That(rows[0].GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(rows[3].QuerySelectorAll(".nx-gantt-cell")[2].TextContent, Is.EqualTo("11/19/26"));
        Assert.That(cut.Find("[role=treegrid]").GetAttribute("aria-label"), Is.EqualTo("Tasks"));
        Assert.That(cut.FindAll(".nx-gantt-bar"), Has.Count.EqualTo(4));
        Assert.That(cut.FindAll(".nx-gantt-bar-summary"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Bars_PlacedBySlotWidth()
    {
        var cut = RenderGantt(p => p.Add(x => x.SlotWidth, 20));

        // The timeline starts on the Sunday a week before the first task: Nov 1. Monday Nov 9 is day 8.
        var bar = cut.FindAll(".nx-gantt-bar")[1];
        Assert.That(bar.GetAttribute("style"), Does.StartWith("left: 160px; width: 120px;"));
        Assert.That(cut.FindAll(".nx-gantt-week")[0].TextContent, Is.EqualTo("Nov 1, 2026"));
    }

    [Test]
    public async Task Toggle_Collapses_RaisesExpandedItems()
    {
        IEnumerable<Job>? expanded = null;
        var cut = RenderGantt(p => p.Add(x => x.ExpandedItemsChanged, (IEnumerable<Job> e) => expanded = e));

        await cut.Find(".nx-gantt-toggle").ClickAsync(new MouseEventArgs());

        Assert.That(Titles(cut), Is.EqualTo(new[] { "Design", "Pour" }));
        Assert.That(expanded, Is.Empty);
    }

    [Test]
    public void Dependencies_DrawnBetweenShownBars()
    {
        var cut = RenderGantt(p => p.Add(x => x.Dependencies, new[] { new Link(2, 3), new Link(3, 4), new Link(9, 4) }));

        var paths = cut.FindAll(".nx-gantt-links > path");
        Assert.That(paths, Has.Count.EqualTo(2));
        Assert.That(paths[0].GetAttribute("marker-end"), Is.EqualTo("url(#g-arrow)"));
    }

    [Test]
    public async Task CellEdit_OpensOnClick_SurvivesRerender_RaisesOnUpdateOnLeave()
    {
        var updated = new List<string>();
        var jobs = Jobs();
        var cut = RenderGantt(p => p.Add(x => x.OnUpdate, (Job j) => updated.Add(j.Title)), jobs, editor: true);

        await cut.FindAll("[role=gridcell]")[0].ClickAsync(new MouseEventArgs());
        Assert.That(cut.FindAll(".ed"), Has.Count.EqualTo(1));
        Assert.That(module.Invocations.Count(i => i.Identifier == "watchEdit"), Is.EqualTo(1));

        // The page re-renders while the editor writes the item: the edit stays open.
        jobs[0].Title = "Design 2";
        cut.Render(p => p.Add(x => x.Data, jobs));
        Assert.That(cut.FindAll(".ed"), Has.Count.EqualTo(1));

        await cut.InvokeAsync(cut.Instance.OnEditOutside);
        Assert.That(cut.FindAll(".ed"), Is.Empty);
        Assert.That(updated, Is.EqualTo(new[] { "Design 2" }));
        Assert.That(Titles(cut)[0], Is.EqualTo("Design 2"));
    }

    [Test]
    public async Task Keys_MoveSelect_EnterEdits()
    {
        Job? edited = null;
        var cut = RenderGantt(p => p.Add(x => x.OnEdit, (Job j) => edited = j));

        await cut.FindAll("[role=row]")[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.That(cut.Find("[aria-selected=true] .nx-gantt-cell").TextContent, Is.EqualTo("Drawings"));
        await cut.Find("[aria-selected=true]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.That(edited!.Title, Is.EqualTo("Drawings"));

        await cut.FindAll("[role=row]")[0].DoubleClickAsync(new MouseEventArgs());
        Assert.That(edited.Title, Is.EqualTo("Design"));
    }

    [Test]
    public async Task Sort_AscendingDescendingOff()
    {
        var cut = RenderGantt(p => p.Add(x => x.Sortable, true));
        var header = () => cut.FindAll(".nx-gantt-sort")[0];

        await header().ClickAsync(new MouseEventArgs());
        await header().ClickAsync(new MouseEventArgs());
        Assert.That(Titles(cut), Is.EqualTo(new[] { "Pour", "Design", "Review", "Drawings" }));
        Assert.That(cut.Instance.HasSort, Is.True);
        Assert.That(cut.FindAll(".nx-gantt-th")[0].GetAttribute("aria-sort"), Is.EqualTo("descending"));

        await header().ClickAsync(new MouseEventArgs());
        Assert.That(Titles(cut), Is.EqualTo(new[] { "Design", "Drawings", "Review", "Pour" }));
        Assert.That(cut.Instance.HasSort, Is.False);
    }

    [Test]
    public async Task Filter_KeepsMatchesAndParents_Clear()
    {
        var cut = RenderGantt(p => p.Add(x => x.FilterRow, true));

        cut.Find("input[aria-label='Filter Crew']").Input("pe");
        cut.WaitForState(() => Titles(cut).Length == 2, TimeSpan.FromSeconds(2));
        Assert.That(Titles(cut), Is.EqualTo(new[] { "Design", "Review" }));
        Assert.That(cut.Instance.HasFilter, Is.True);

        await cut.InvokeAsync(cut.Instance.ClearFilterAsync);
        Assert.That(Titles(cut), Has.Length.EqualTo(4));
    }

    [Test]
    public async Task LinkDrop_RaisesDependencyCreate()
    {
        NxGanttDependencyCreateEventArgs<Job>? created = null;
        var cut = RenderGantt(p => p.Add(x => x.OnDependencyCreate, (NxGanttDependencyCreateEventArgs<Job> e) => created = e));

        Assert.That(cut.FindAll(".nx-gantt-handle"), Has.Count.EqualTo(8));
        await cut.InvokeAsync(() => cut.Instance.OnLinkDrop(1, true, 2, false));

        Assert.That(created!.Predecessor.Title, Is.EqualTo("Drawings"));
        Assert.That(created.Successor.Title, Is.EqualTo("Review"));
        Assert.That(created.Type, Is.EqualTo(NxGanttDependencyType.FinishStart));
    }

    [Test]
    public async Task Divider_ReportsTheWidth()
    {
        var widths = new List<string>();
        var cut = RenderGantt(p => p.Add(x => x.TreeListWidthChanged, (string w) => widths.Add(w)));
        Assert.That(cut.Find(".nx-gantt").GetAttribute("style"), Does.Contain("--nx-gantt-tree-width: 40%"));

        await cut.InvokeAsync(() => cut.Instance.OnTreeWidth(321.4));

        Assert.That(widths, Is.EqualTo(new[] { "321px" }));
        Assert.That(cut.Find(".nx-gantt").GetAttribute("style"), Does.Contain("--nx-gantt-tree-width: 321px"));
    }

    [Test]
    public void Templates_ReplaceTheDefaults()
    {
        var cut = RenderGantt(p => p
            .Add(x => x.TaskTemplate, j => b => b.AddMarkupContent(0, $"<i class=\"t\">{j.Title}</i>"))
            .Add(x => x.WeekHeaderTemplate, d => b => b.AddContent(0, d.ToString("MMM d", CultureInfo.InvariantCulture)))
            .Add(x => x.DayHeaderTemplate, d => b => b.AddContent(0, d.ToString("ddd", CultureInfo.InvariantCulture)[..1])));

        Assert.That(cut.FindAll(".nx-gantt-bar .t"), Has.Count.EqualTo(4));
        Assert.That(cut.FindAll(".nx-gantt-week")[0].TextContent, Is.EqualTo("Nov 1"));
        Assert.That(cut.FindAll(".nx-gantt-day")[0].TextContent, Is.EqualTo("S"));
    }

    [Test]
    public void MissingDateField_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RenderGantt(p => p.Add(x => x.StartField, "From")));
    }
}
