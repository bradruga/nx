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
        NxGanttUpdateArgs<Job>? last = null;
        var cut = RenderGantt(p => p.Add(x => x.OnUpdate, (NxGanttUpdateArgs<Job> a) => { last = a; updated.Add(a.Item.Title); }), jobs, editor: true);

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
        // The cell's own field, before and after, as NxGrid's cell change.
        Assert.That(last!.Kind, Is.EqualTo(NxGanttUpdateKind.Edit));
        Assert.That(last.Changes.Select(c => (c.Field, c.OldValue, c.NewValue)), Is.EqualTo(new[] { ("Title", (object?)"Design", (object?)"Design 2") }));
    }

    // ── Dragging bars ─────────────────────────────────────────────────────────

    [Test]
    public void Bars_DragOnlyWithOnUpdate_SummariesNever()
    {
        Assert.That(RenderGantt().FindAll("[data-nx-gantt-drag]"), Is.Empty);

        var cut = RenderGantt(p => p.Add(x => x.OnUpdate, (NxGanttUpdateArgs<Job> _) => { }));
        var bars = cut.FindAll(".nx-gantt-bar");
        Assert.That(bars.Select(b => b.HasAttribute("data-nx-gantt-drag")), Is.EqualTo(new[] { false, true, true, true }), "Design is a summary");
        Assert.That(bars[1].QuerySelectorAll(".nx-gantt-grip").Select(g => g.GetAttribute("data-nx-gantt-grip")), Is.EqualTo(new[] { "start", "end" }));
        Assert.That(bars[0].QuerySelectorAll(".nx-gantt-grip"), Is.Empty);
    }

    [Test]
    public async Task BarDrop_Move_RaisesChanges_ItemUntouchedUntilApplied()
    {
        var jobs = Jobs();
        NxGanttUpdateArgs<Job>? args = null;
        var cut = RenderGantt(p => p.Add(x => x.OnUpdate, (NxGanttUpdateArgs<Job> a) => args = a), jobs);

        await cut.InvokeAsync(() => cut.Instance.OnBarDrop(3, "move", 2));

        Assert.That(args!.Kind, Is.EqualTo(NxGanttUpdateKind.Move));
        Assert.That(args.Item, Is.SameAs(jobs[3]));
        Assert.That(args.Changes.Select(c => (c.Field, c.OldValue, c.NewValue)), Is.EqualTo(new[]
        {
            ("Start", (object?)Monday.AddDays(10), (object?)Monday.AddDays(12)),
            ("End", (object?)Monday.AddDays(15), (object?)Monday.AddDays(17)),
        }));
        Assert.That(jobs[3].Start, Is.EqualTo(Monday.AddDays(10)), "the page applies the change");

        args.Apply();
        Assert.That((jobs[3].Start, jobs[3].End), Is.EqualTo((Monday.AddDays(12), Monday.AddDays(17))));
    }

    [Test]
    public async Task BarDrop_Resize_OneEnd_KeepsAtLeastADay()
    {
        var jobs = Jobs();
        var raised = new List<NxGanttUpdateArgs<Job>>();
        var cut = RenderGantt(p => p.Add(x => x.OnUpdate, (NxGanttUpdateArgs<Job> a) => raised.Add(a)), jobs);

        await cut.InvokeAsync(() => cut.Instance.OnBarDrop(3, "end", -1));
        Assert.That(raised[0].Kind, Is.EqualTo(NxGanttUpdateKind.ResizeEnd));
        Assert.That(raised[0].Changes.Select(c => (c.Field, c.NewValue)), Is.EqualTo(new[] { ("End", (object?)Monday.AddDays(14)) }));

        // Pour is five days: its start can come up four at most, its end back four.
        await cut.InvokeAsync(() => cut.Instance.OnBarDrop(3, "start", 30));
        Assert.That(raised[1].Kind, Is.EqualTo(NxGanttUpdateKind.ResizeStart));
        Assert.That(raised[1].Changes.Single().NewValue, Is.EqualTo(Monday.AddDays(14)));
        await cut.InvokeAsync(() => cut.Instance.OnBarDrop(3, "end", -30));
        Assert.That(raised[2].Changes.Single().NewValue, Is.EqualTo(Monday.AddDays(11)));

        // A summary, or no days at all, raises nothing.
        await cut.InvokeAsync(() => cut.Instance.OnBarDrop(0, "move", 3));
        await cut.InvokeAsync(() => cut.Instance.OnBarDrop(3, "move", 0));
        Assert.That(raised, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task AltArrows_MoveTheBar_WithShiftItsEnd()
    {
        var raised = new List<NxGanttUpdateArgs<Job>>();
        var cut = RenderGantt(p => p.Add(x => x.OnUpdate, (NxGanttUpdateArgs<Job> a) => raised.Add(a)));

        await cut.FindAll("[role=row]")[3].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight", AltKey = true });
        await cut.FindAll("[role=row]")[3].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft", AltKey = true, ShiftKey = true });

        Assert.That(raised.Select(a => a.Kind), Is.EqualTo(new[] { NxGanttUpdateKind.Move, NxGanttUpdateKind.ResizeEnd }));
        Assert.That(raised[0].Changes.Select(c => c.NewValue), Is.EqualTo(new object?[] { Monday.AddDays(11), Monday.AddDays(16) }));
        Assert.That(raised[1].Changes.Single().NewValue, Is.EqualTo(Monday.AddDays(14)));
    }

    [Test]
    public async Task EditableFalse_NoEditor_NoDrag_NoHandles_NoDelete()
    {
        var raised = 0;
        var cut = RenderGantt(p => p
            .Add(x => x.Editable, false)
            .Add(x => x.OnUpdate, (NxGanttUpdateArgs<Job> _) => raised++)
            .Add(x => x.OnDelete, (Job _) => raised++)
            .Add(x => x.OnDependencyCreate, (NxGanttDependencyCreateEventArgs<Job> _) => raised++), editor: true);

        Assert.That(cut.FindAll("[data-nx-gantt-drag], .nx-gantt-grip, .nx-gantt-handle"), Is.Empty);
        await cut.FindAll("[role=gridcell]")[0].ClickAsync(new MouseEventArgs());
        Assert.That(cut.FindAll(".ed"), Is.Empty);
        await cut.FindAll("[role=row]")[3].KeyDownAsync(new KeyboardEventArgs { Key = "Delete" });
        await cut.InvokeAsync(() => cut.Instance.OnBarDrop(3, "move", 2));
        Assert.That(raised, Is.Zero);
    }

    [Test]
    public async Task Selection_BindsAsAListOfOne_DeleteRaisesOnDelete()
    {
        var jobs = Jobs();
        IEnumerable<Job> picked = [];
        Job? deleted = null;
        IRenderedComponent<NxGantt<Job>>? cut = null;
        cut = RenderGantt(p => p
            .Add(x => x.SelectedItems, picked)
            .Add(x => x.SelectedItemsChanged, (IEnumerable<Job> items) => { picked = items; cut?.Render(q => q.Add(x => x.SelectedItems, items)); })
            .Add(x => x.OnDelete, (Job j) => deleted = j), jobs);

        await cut.FindAll(".nx-gantt-bar")[3].ClickAsync(new MouseEventArgs());
        Assert.That(picked, Is.EqualTo(new[] { jobs[3] }));
        Assert.That(cut.Find("[aria-selected=true] .nx-gantt-cell").TextContent, Is.EqualTo("Pour"));

        // Set from outside.
        cut.Render(p => p.Add(x => x.SelectedItems, new List<Job> { jobs[1] }));
        Assert.That(cut.Find("[aria-selected=true] .nx-gantt-cell").TextContent, Is.EqualTo("Drawings"));

        await cut.Find("[aria-selected=true]").KeyDownAsync(new KeyboardEventArgs { Key = "Delete" });
        Assert.That(deleted, Is.SameAs(jobs[1]));
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

    // ── Dates in other types ──────────────────────────────────────────────────

    private sealed class Plan
    {
        public int Id { get; init; }
        public int? ParentId { get; init; }
        public string Title { get; set; } = "";
        public DateOnly Start { get; set; }
        public DateTimeOffset? End { get; set; }
    }

    [Test]
    public async Task BarDrop_DateOnlyAndOffsetFields_ChangesKeepThePropertyType()
    {
        var plan = new Plan { Id = 1, Title = "Pour", Start = DateOnly.FromDateTime(Monday), End = new DateTimeOffset(Monday.AddDays(5)) };
        NxGanttUpdateArgs<Plan>? args = null;
        var cut = Render<NxGantt<Plan>>(p => p
            .Add(x => x.Data, new[] { plan })
            .Add(x => x.OnUpdate, (NxGanttUpdateArgs<Plan> a) => args = a));

        await cut.InvokeAsync(() => cut.Instance.OnBarDrop(0, "move", 2));

        Assert.That(args!.Changes.Select(c => c.NewValue),
            Is.EqualTo(new object?[] { DateOnly.FromDateTime(Monday.AddDays(2)), new DateTimeOffset(Monday.AddDays(7)) }));
        args.Apply();
        Assert.That(plan.Start, Is.EqualTo(DateOnly.FromDateTime(Monday.AddDays(2))));
        Assert.That(plan.End, Is.EqualTo(new DateTimeOffset(Monday.AddDays(7))));
    }

    // ── Dependencies ──────────────────────────────────────────────────────────

    [Test]
    public void Dependencies_PathRunsBetweenTheEndsTheTypeNames()
    {
        // SlotWidth 20: Drawings is 160–280px on row 1, Pour 360–460px on row 3; rows are 36px.
        var cut = RenderGantt(p => p.Add(x => x.SlotWidth, 20).Add(x => x.Dependencies, new object[]
        {
            new Link(2, 4),
            new { PredecessorId = 2, SuccessorId = 4, Type = 3 },
            new Link(2, 4, NxGanttDependencyType.FinishFinish),
        }));

        Assert.That(cut.FindAll(".nx-gantt-links > path").Select(p => p.GetAttribute("d")), Is.EqualTo(new[]
        {
            "M280,54 H320 V126 H360",
            "M160,54 H152 V108 H352 V126 H360",
            "M280,54 H288 V108 H468 V126 H460",
        }));
    }

    [Test]
    public void Dependencies_ToACollapsedTask_NotDrawn()
    {
        var cut = RenderGantt(p => p
            .Add(x => x.ExpandedItems, new List<Job>())
            .Add(x => x.Dependencies, new[] { new Link(2, 4), new Link(1, 4) }));

        Assert.That(cut.FindAll(".nx-gantt-links > path"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LinkDrop_TypeFromTheEnds_SameBarIgnored()
    {
        var created = new List<NxGanttDependencyType>();
        var cut = RenderGantt(p => p.Add(x => x.OnDependencyCreate, (NxGanttDependencyCreateEventArgs<Job> e) => created.Add(e.Type)));

        await cut.InvokeAsync(() => cut.Instance.OnLinkDrop(1, true, 2, true));
        await cut.InvokeAsync(() => cut.Instance.OnLinkDrop(1, false, 2, false));
        await cut.InvokeAsync(() => cut.Instance.OnLinkDrop(1, false, 2, true));
        await cut.InvokeAsync(() => cut.Instance.OnLinkDrop(2, true, 2, false));

        Assert.That(created, Is.EqualTo(new[] { NxGanttDependencyType.FinishFinish, NxGanttDependencyType.StartStart, NxGanttDependencyType.StartFinish }));
    }

    // ── Expanding ─────────────────────────────────────────────────────────────

    [Test]
    public void ExpandedItems_FromOutside_CollapsesTheOthers()
    {
        var jobs = Jobs();
        var cut = RenderGantt(p => p.Add(x => x.ExpandedItems, new List<Job>()), jobs);

        Assert.That(Titles(cut), Is.EqualTo(new[] { "Design", "Pour" }));
        Assert.That(cut.FindAll("[role=row]")[0].GetAttribute("aria-expanded"), Is.EqualTo("false"));

        cut.Render(p => p.Add(x => x.ExpandedItems, new List<Job> { jobs[0] }));
        Assert.That(Titles(cut), Has.Length.EqualTo(4));
    }

    [Test]
    public async Task ExpandedItems_SameListAgain_KeepsWhatTheUserCollapsed()
    {
        var jobs = Jobs();
        var expanded = new List<Job> { jobs[0] };
        var cut = RenderGantt(p => p.Add(x => x.ExpandedItems, expanded), jobs);

        await cut.Find(".nx-gantt-toggle").ClickAsync(new MouseEventArgs());
        cut.Render(p => p.Add(x => x.Data, jobs));

        Assert.That(Titles(cut), Is.EqualTo(new[] { "Design", "Pour" }));
    }

    [Test]
    public async Task ArrowLeft_CollapsesAParent_ArrowRightExpandsIt()
    {
        var cut = RenderGantt();

        await cut.FindAll("[role=row]")[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.That(Titles(cut), Is.EqualTo(new[] { "Design", "Pour" }));

        await cut.FindAll("[role=row]")[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.That(Titles(cut), Has.Length.EqualTo(4));
    }

    [Test]
    public async Task ArrowLeft_OnAChild_SelectsItsParent_FocusesItsRow()
    {
        var cut = RenderGantt();

        await cut.FindAll("[role=row]")[2].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.That(cut.Find("[aria-selected=true] .nx-gantt-cell").TextContent, Is.EqualTo("Design"));
        Assert.That(module.Invocations.Last(i => i.Identifier == "focusRow").Arguments[1], Is.EqualTo("g-r0"));
    }

    [Test]
    public async Task ArrowUp_SelectsTheRowAbove()
    {
        var cut = RenderGantt();

        await cut.FindAll("[role=row]")[3].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.That(cut.Find("[aria-selected=true] .nx-gantt-cell").TextContent, Is.EqualTo("Review"));
    }

    // ── Filtering and sorting ─────────────────────────────────────────────────

    [Test]
    public async Task Filter_ShowsMatchesUnderACollapsedParent()
    {
        var cut = RenderGantt(p => p.Add(x => x.FilterRow, true));
        await cut.Find(".nx-gantt-toggle").ClickAsync(new MouseEventArgs());

        await cut.InvokeAsync(() => cut.Find("input[aria-label='Filter Crew']").Input("pe"));
        cut.WaitForState(() => Titles(cut).Contains("Review"), TimeSpan.FromSeconds(2));

        Assert.That(Titles(cut), Is.EqualTo(new[] { "Design", "Review" }));
    }

    [Test]
    public async Task Filter_NothingMatches_SaysSo()
    {
        var cut = RenderGantt(p => p.Add(x => x.FilterRow, true));

        await cut.InvokeAsync(() => cut.Find("input[aria-label='Filter Task']").Input("zzz"));
        cut.WaitForState(() => cut.FindAll(".nx-gantt-empty").Count == 1, TimeSpan.FromSeconds(2));

        Assert.That(cut.Find(".nx-gantt-empty").TextContent, Is.EqualTo("No tasks match"));
    }

    [Test]
    public void NoData_SaysNoTasks()
    {
        var cut = RenderGantt(jobs: []);

        Assert.That(cut.Find(".nx-gantt-empty").TextContent, Is.EqualTo("No tasks"));
    }

    [Test]
    public async Task ClearSort_BackToDataOrder_RaisesOnStateChanged()
    {
        var changed = 0;
        var cut = RenderGantt(p => p.Add(x => x.Sortable, true).Add(x => x.OnStateChanged, () => changed++));
        await cut.FindAll(".nx-gantt-sort")[0].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".nx-gantt-sort")[0].ClickAsync(new MouseEventArgs());

        await cut.InvokeAsync(cut.Instance.ClearSortAsync);

        Assert.That(Titles(cut), Is.EqualTo(new[] { "Design", "Drawings", "Review", "Pour" }));
        Assert.That(cut.Instance.HasSort, Is.False);
        Assert.That(changed, Is.EqualTo(3));
    }

    // ── Cell editing ──────────────────────────────────────────────────────────

    [Test]
    public async Task CellEdit_EscapeEnds_UnchangedRaisesNoChanges()
    {
        NxGanttUpdateArgs<Job>? last = null;
        var cut = RenderGantt(p => p.Add(x => x.OnUpdate, (NxGanttUpdateArgs<Job> a) => last = a), editor: true);
        await cut.FindAll("[role=gridcell]")[0].ClickAsync(new MouseEventArgs());

        await cut.FindAll("[role=row]")[0].KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.That(cut.FindAll(".ed"), Is.Empty);
        Assert.That(last!.Kind, Is.EqualTo(NxGanttUpdateKind.Edit));
        Assert.That(last.Changes, Is.Empty);
        Assert.That(module.Invocations.Count(i => i.Identifier == "unwatchEdit"), Is.EqualTo(1));
    }

    [Test]
    public async Task CellEdit_ArrowKeysStayInTheEditor()
    {
        var cut = RenderGantt(p => p.Add(x => x.OnUpdate, (NxGanttUpdateArgs<Job> _) => { }), editor: true);
        await cut.FindAll("[role=gridcell]")[0].ClickAsync(new MouseEventArgs());

        await cut.FindAll("[role=row]")[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(cut.FindAll(".ed"), Has.Count.EqualTo(1));
        Assert.That(cut.Find("[aria-selected=true] .nx-gantt-cell .ed"), Is.Not.Null);
    }

    [Test]
    public async Task CellEdit_ClickAnotherRow_EndsTheFirstEdit_OpensTheNext()
    {
        var jobs = Jobs();
        var updated = new List<string>();
        IRenderedComponent<NxGantt<Job>>? cut = null;
        // As a page does: its OnUpdate handler re-renders it, which passes the Gantt its parameters again.
        cut = RenderGantt(p => p.Add(x => x.OnUpdate, (NxGanttUpdateArgs<Job> a) =>
        {
            updated.Add(a.Item.Title);
            cut?.Render(q => q.Add(x => x.Data, jobs));
        }), jobs, editor: true);
        await cut.FindAll("[role=gridcell]")[0].ClickAsync(new MouseEventArgs());

        // Three cells a row: the second row's first cell.
        await cut.FindAll("[role=gridcell]")[3].ClickAsync(new MouseEventArgs());

        Assert.That(updated, Is.EqualTo(new[] { "Design" }));
        Assert.That(cut.FindAll("[role=row]")[1].QuerySelectorAll(".ed"), Has.Length.EqualTo(1));
        Assert.That(cut.FindAll(".ed"), Has.Count.EqualTo(1));
    }

    // ── Bars ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Bar_DoubleClick_RaisesOnEdit()
    {
        Job? edited = null;
        var cut = RenderGantt(p => p.Add(x => x.OnEdit, (Job j) => edited = j));

        await cut.FindAll(".nx-gantt-bar")[2].DoubleClickAsync(new MouseEventArgs());

        Assert.That(edited!.Title, Is.EqualTo("Review"));
    }

    [Test]
    public async Task Bar_Hover_ShowsTitleAndDates_LeaveHides()
    {
        var cut = RenderGantt();

        await cut.FindAll(".nx-gantt-bar")[3].MouseEnterAsync(new MouseEventArgs());
        Assert.That(cut.Find(".nx-gantt-tooltip").TextContent, Does.Contain("Pour").And.Contain("Thu, Nov 19").And.Contain("Tue, Nov 24"));

        await cut.FindAll(".nx-gantt-bar")[3].MouseLeaveAsync(new MouseEventArgs());
        Assert.That(cut.FindAll(".nx-gantt-tooltip"), Is.Empty);
    }

    [Test]
    public async Task TooltipTemplate_ReplacesTheDefault()
    {
        var cut = RenderGantt(p => p.Add(x => x.TooltipTemplate, j => b => b.AddMarkupContent(0, $"<b class=\"tip\">{j.Crew}</b>")));

        await cut.FindAll(".nx-gantt-bar")[3].MouseEnterAsync(new MouseEventArgs());

        Assert.That(cut.Find(".nx-gantt-tooltip .tip").TextContent, Is.EqualTo("Crew A"));
    }

    [Test]
    public void DayHeaderDateFormat_FormatsTheDays()
    {
        var cut = RenderGantt(p => p.Add(x => x.DayHeaderDateFormat, "ddd"));

        Assert.That(cut.FindAll(".nx-gantt-day").Take(2).Select(d => d.TextContent), Is.EqualTo(new[] { "Sun", "Mon" }));
    }

    [Test]
    public async Task Rebind_RedrawsAfterAnInPlaceChange()
    {
        var jobs = Jobs();
        var cut = RenderGantt(p => p.Add(x => x.SlotWidth, 20), jobs);
        jobs[3].Start = Monday.AddDays(12);

        await cut.InvokeAsync(cut.Instance.Rebind);

        Assert.That(cut.FindAll(".nx-gantt-bar")[3].GetAttribute("style"), Does.StartWith("left: 400px; width: 60px;"));
    }
}
