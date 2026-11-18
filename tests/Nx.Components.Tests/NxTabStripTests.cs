using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxTabStripTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-tabstrip.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private static RenderFragment Text(string text) => b => b.AddContent(0, text);

    private sealed record TabSpec(string Id, bool Disabled = false, bool Closable = false);

    /// <summary>A bound strip over the given tabs; ActiveTabChanged writes back like @bind-ActiveTab.</summary>
    private IRenderedComponent<NxTabStrip> RenderBound(List<string?> raised, IEnumerable<TabSpec> specs, string? active = null,
        Action<ComponentParameterCollectionBuilder<NxTabStrip>>? configure = null)
    {
        var tabs = specs.ToList();
        IRenderedComponent<NxTabStrip>? cut = null;
        cut = Render<NxTabStrip>(p =>
        {
            p.Add(x => x.Id, "t");
            p.Add(x => x.ActiveTab, active);
            p.Add(x => x.ActiveTabChanged, (string? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.ActiveTab, v));
            });
            p.Add(x => x.ChildContent, b =>
            {
                var seq = 0;
                foreach (var tab in tabs)
                {
                    b.OpenComponent<NxTab>(seq++);
                    b.SetKey(tab.Id);
                    b.AddAttribute(seq++, nameof(NxTab.Id), tab.Id);
                    b.AddAttribute(seq++, nameof(NxTab.Title), tab.Id.ToUpperInvariant());
                    b.AddAttribute(seq++, nameof(NxTab.Disabled), tab.Disabled);
                    b.AddAttribute(seq++, nameof(NxTab.Closable), tab.Closable);
                    b.AddAttribute(seq++, nameof(NxTab.ChildContent), Text($"Panel {tab.Id}"));
                    b.CloseComponent();
                }
            });
            configure?.Invoke(p);
        });
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == tabs.Count);
        return cut;
    }

    private static readonly TabSpec[] ThreeTabs = [new("a"), new("b"), new("c")];

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_TablistTabsAndActivePanel()
    {
        var cut = RenderBound([], ThreeTabs, configure: p => p.Add(x => x.AriaLabel, "Job"));

        var list = cut.Find("[role=tablist]");
        Assert.That(list.GetAttribute("aria-label"), Is.EqualTo("Job"));
        Assert.That(list.GetAttribute("aria-orientation"), Is.EqualTo("horizontal"));
        var tabs = cut.FindAll("[role=tab]");
        Assert.That(tabs.Select(t => t.TextContent.Trim()), Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(tabs[0].GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(tabs[0].GetAttribute("tabindex"), Is.EqualTo("0"));
        Assert.That(tabs[1].GetAttribute("tabindex"), Is.EqualTo("-1"));
        Assert.That(tabs[0].GetAttribute("aria-controls"), Is.EqualTo("t-panel-a"));

        var panels = cut.FindAll("[role=tabpanel]");
        Assert.That(panels, Has.Count.EqualTo(1));
        Assert.That(panels[0].Id, Is.EqualTo("t-panel-a"));
        Assert.That(panels[0].GetAttribute("aria-labelledby"), Is.EqualTo("t-tab-a"));
        Assert.That(panels[0].TextContent.Trim(), Is.EqualTo("Panel a"));
        Assert.That(cut.Find("#t").ClassList, Does.Contain("nx-tabstrip-top"));
    }

    [Test]
    public void ActiveTab_ParameterSelects()
    {
        var cut = RenderBound([], ThreeTabs, active: "b");

        Assert.That(cut.Find("#t-tab-b").GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(cut.Find("[role=tabpanel]").TextContent.Trim(), Is.EqualTo("Panel b"));

        cut.Render(p => p.Add(x => x.ActiveTab, "c"));
        Assert.That(cut.Find("[role=tabpanel]").TextContent.Trim(), Is.EqualTo("Panel c"));
    }

    [Test]
    public void UnknownOrDisabledActive_FallsBackToFirstEnabled()
    {
        var cut = RenderBound([], [new("a", Disabled: true), new("b"), new("c")], active: "zzz");

        Assert.That(cut.Find("#t-tab-b").GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(cut.Find("#t-tab-a").GetAttribute("aria-disabled"), Is.EqualTo("true"));
    }

    [Test]
    public void TitleIsTheKeyWithoutAnId()
    {
        var cut = Render<NxTabStrip>(p => p
            .Add(x => x.Id, "t")
            .Add(x => x.ActiveTab, "Second")
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxTab>(0); b.AddAttribute(1, nameof(NxTab.Title), "First"); b.CloseComponent();
                b.OpenComponent<NxTab>(2); b.AddAttribute(3, nameof(NxTab.Title), "Second"); b.CloseComponent();
            }));
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 2);

        Assert.That(cut.Find("#t-tab-second").GetAttribute("aria-selected"), Is.EqualTo("true"));
    }

    [Test]
    public void IconAndHeaderTemplate()
    {
        var cut = Render<NxTabStrip>(p => p.Add(x => x.ChildContent, b =>
        {
            b.OpenComponent<NxTab>(0);
            b.AddAttribute(1, nameof(NxTab.Title), "Lines");
            b.AddAttribute(2, nameof(NxTab.Icon), Text("I"));
            b.CloseComponent();
            b.OpenComponent<NxTab>(3);
            b.AddAttribute(4, nameof(NxTab.Id), "issues");
            b.AddAttribute(5, nameof(NxTab.HeaderTemplate), (RenderFragment)(h => h.AddMarkupContent(0, "<b>Issues 3</b>")));
            b.CloseComponent();
        }));
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 2);

        Assert.That(cut.Find(".nx-tabstrip-tab-icon").TextContent, Is.EqualTo("I"));
        Assert.That(cut.FindAll("[role=tab]")[1].InnerHtml, Does.Contain("<b>Issues 3</b>"));
    }

    [TestCase(NxTabPosition.Left, "vertical")]
    [TestCase(NxTabPosition.Right, "vertical")]
    [TestCase(NxTabPosition.Bottom, "horizontal")]
    public void Position_ClassAndOrientation(NxTabPosition position, string orientation)
    {
        var cut = RenderBound([], ThreeTabs, configure: p => p.Add(x => x.Position, position));

        Assert.That(cut.Find("#t").ClassList, Does.Contain($"nx-tabstrip-{position.ToString().ToLowerInvariant()}"));
        Assert.That(cut.Find("[role=tablist]").GetAttribute("aria-orientation"), Is.EqualTo(orientation));
    }

    [Test]
    public void FullWidth_OnlyForHorizontal()
    {
        var top = RenderBound([], ThreeTabs, configure: p => p.Add(x => x.FullWidth, true));
        Assert.That(top.Find("#t").ClassList, Does.Contain("nx-tabstrip-full-width"));

        var left = RenderBound([], ThreeTabs, configure: p => p.Add(x => x.FullWidth, true).Add(x => x.Position, NxTabPosition.Left));
        Assert.That(left.Find(".nx-tabstrip-left").ClassList, Does.Not.Contain("nx-tabstrip-full-width"));
    }

    // ── Activation ────────────────────────────────────────────────────────────

    [Test]
    public async Task Click_ActivatesAndRaises()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, ThreeTabs);

        await cut.Find("#t-tab-c").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { "c" }));
        Assert.That(cut.Find("#t-tab-c").GetAttribute("tabindex"), Is.EqualTo("0"));
        Assert.That(cut.Find("[role=tabpanel]").TextContent.Trim(), Is.EqualTo("Panel c"));
    }

    [Test]
    public async Task Click_ActiveOrDisabled_DoesNothing()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, [new("a"), new("b", Disabled: true)]);

        await cut.Find("#t-tab-a").ClickAsync(new MouseEventArgs());
        await cut.Find("#t-tab-b").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task SelectAsync_Activates()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, ThreeTabs);

        await cut.InvokeAsync(() => cut.Instance.SelectAsync("b"));

        Assert.That(raised, Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public void Js_InitAndRefreshCalled()
    {
        RenderBound([], ThreeTabs);

        Assert.That(module.Invocations.Any(i => i.Identifier == "init"), Is.True);
        Assert.That(module.Invocations.Any(i => i.Identifier == "refresh"), Is.True);
    }

    // ── Lazy and KeepAlive ────────────────────────────────────────────────────

    [Test]
    public async Task Lazy_OnlyActivePanelRendered()
    {
        var cut = RenderBound([], ThreeTabs);

        await cut.Find("#t-tab-b").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll("[role=tabpanel]").Select(p => p.Id), Is.EqualTo(new[] { "t-panel-b" }));
    }

    [Test]
    public async Task KeepAlive_VisitedPanelsStayHidden()
    {
        var cut = RenderBound([], ThreeTabs, configure: p => p.Add(x => x.KeepAlive, true));

        await cut.Find("#t-tab-b").ClickAsync(new MouseEventArgs());

        var panels = cut.FindAll("[role=tabpanel]");
        Assert.That(panels.Select(p => p.Id), Is.EqualTo(new[] { "t-panel-a", "t-panel-b" }));
        Assert.That(cut.Find("#t-panel-a").HasAttribute("hidden"), Is.True);
        Assert.That(cut.Find("#t-panel-b").HasAttribute("hidden"), Is.False);
    }

    // ── Closing ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Close_RaisesEvent_InactiveKeepsActive()
    {
        var raised = new List<string?>();
        var closed = new List<string>();
        var cut = RenderBound(raised, [new("a", Closable: true), new("b", Closable: true)],
            configure: p => p.Add(x => x.OnTabClose, (NxTabCloseEventArgs e) => closed.Add(e.TabId)));

        Assert.That(cut.Find("#t-tab-b .nx-tabstrip-close").GetAttribute("aria-label"), Is.EqualTo("Close B"));
        await cut.Find("#t-tab-b .nx-tabstrip-close").ClickAsync(new MouseEventArgs());

        Assert.That(closed, Is.EqualTo(new[] { "b" }));
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task CloseActive_MovesToRightNeighbourElseLeft()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, [new("a", Closable: true), new("b", Closable: true), new("c", Closable: true)], active: "b");

        await cut.Find("#t-tab-b .nx-tabstrip-close").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new[] { "c" }));

        await cut.Find("#t-tab-c .nx-tabstrip-close").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new[] { "c", "b" }));
    }

    [Test]
    public async Task DeleteKey_ClosesClosableOnly()
    {
        var closed = new List<string>();
        var cut = RenderBound([], [new("a", Closable: true), new("b")],
            configure: p => p.Add(x => x.OnTabClose, (NxTabCloseEventArgs e) => closed.Add(e.TabId)));

        await cut.Find("#t-tab-b").KeyDownAsync(new KeyboardEventArgs { Key = "Delete" });
        await cut.Find("#t-tab-a").KeyDownAsync(new KeyboardEventArgs { Key = "Delete" });

        Assert.That(closed, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void DisabledTab_HasNoCloseButton()
    {
        var cut = RenderBound([], [new("a"), new("b", Disabled: true, Closable: true)]);

        Assert.That(cut.FindAll("#t-tab-b .nx-tabstrip-close"), Is.Empty);
    }

    // ── Dynamic tabs ──────────────────────────────────────────────────────────

    [Test]
    public void RemovedTab_DisappearsFromHeader()
    {
        var tabs = new List<string> { "a", "b", "c" };
        var cut = Render<NxTabStrip>(p => p.Add(x => x.Id, "t").Add(x => x.ChildContent, Fragment(tabs)));
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 3);

        tabs.Remove("b");
        cut.Render(p => p.Add(x => x.ChildContent, Fragment(tabs)));

        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 2);
        Assert.That(cut.FindAll("[role=tab]").Select(t => t.Id), Is.EqualTo(new[] { "t-tab-a", "t-tab-c" }));
    }

    [Test]
    public void ChangedTitle_RedrawsHeader()
    {
        var title = "Before";
        RenderFragment content = b =>
        {
            b.OpenComponent<NxTab>(0);
            b.AddAttribute(1, nameof(NxTab.Id), "x");
            b.AddAttribute(2, nameof(NxTab.Title), title);
            b.CloseComponent();
        };
        var cut = Render<NxTabStrip>(p => p.Add(x => x.Id, "t").Add(x => x.ChildContent, content));
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 1);

        title = "After";
        cut.Render(p => p.Add(x => x.ChildContent, content));

        cut.WaitForState(() => cut.Find("#t-tab-x").TextContent.Trim() == "After");
    }

    [Test]
    public void TabOutsideStrip_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxTab>(p => p.Add(x => x.Title, "Lost")));
    }

    [Test]
    public void Tabs_CarryTheirKeyForAContextMenu()
    {
        var cut = RenderBound([], ThreeTabs, "a");
        Assert.That(cut.FindAll("[role=tab]").Select(t => t.GetAttribute("data-nx-context")), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void TabAddedInTheMiddleOrAtTheFrontOfTheMarkup_LandsThere()
    {
        var ids = new List<string> { "a", "b", "c" };
        var cut = Render<NxTabStrip>(p => p.Add(x => x.Id, "t").Add(x => x.Reorderable, true).Add(x => x.ChildContent, Fragment(ids)));
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 3);

        ids.Insert(1, "x");
        cut.Render(p => p.Add(x => x.ChildContent, Fragment(ids)));
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 4);
        Assert.That(cut.FindAll("[role=tab]").Select(e => e.GetAttribute("data-nx-context")), Is.EqualTo(new[] { "a", "x", "b", "c" }));

        ids.Insert(0, "first");
        cut.Render(p => p.Add(x => x.ChildContent, Fragment(ids)));
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 5);
        Assert.That(cut.FindAll("[role=tab]").First().GetAttribute("data-nx-context"), Is.EqualTo("first"));
    }

    // ── Reordering ────────────────────────────────────────────────────────────

    private static string[] Order(IRenderedComponent<NxTabStrip> cut) => cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim()).ToArray();

    [Test]
    public async Task Reorderable_Drop_MovesTheTab_RaisesOldAndNewPlace_ActiveStays()
    {
        var moves = new List<NxTabReorderEventArgs>();
        var cut = RenderBound([], ThreeTabs, "a", p => p.Add(x => x.Reorderable, true).Add(x => x.OnTabReorder, (NxTabReorderEventArgs e) => moves.Add(e)));
        Assert.That(cut.Find("[role=tablist]").GetAttribute("data-reorderable"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-tabstrip").ClassList, Does.Contain("nx-tabstrip-reorderable"));

        await cut.InvokeAsync(() => cut.Instance.OnTabDrop(0, 2));

        Assert.That(Order(cut), Is.EqualTo(new[] { "B", "C", "A" }));
        Assert.That(moves.Select(m => (m.TabId, m.OldIndex, m.NewIndex)), Is.EqualTo(new[] { ("a", 0, 2) }));
        Assert.That(cut.Find("[aria-selected=true]").TextContent.Trim(), Is.EqualTo("A"));
    }

    [Test]
    public async Task Reorderable_CtrlShiftArrows_MoveOnePlace_StopAtTheEnds()
    {
        var moves = new List<NxTabReorderEventArgs>();
        var cut = RenderBound([], ThreeTabs, "a", p => p.Add(x => x.Reorderable, true).Add(x => x.OnTabReorder, (NxTabReorderEventArgs e) => moves.Add(e)));

        await cut.Find("#t-tab-a").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight", CtrlKey = true, ShiftKey = true });
        Assert.That(Order(cut), Is.EqualTo(new[] { "B", "A", "C" }));
        Assert.That(module.Invocations.Any(i => i.Identifier == "focusTab" && (string?)i.Arguments[1] == "t-tab-a"), Is.True);

        await cut.Find("#t-tab-b").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft", CtrlKey = true, ShiftKey = true });
        await cut.Find("#t-tab-b").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown", CtrlKey = true, ShiftKey = true });
        Assert.That(moves, Has.Count.EqualTo(1), "already first; Up and Down belong to tabs on the side");
    }

    [Test]
    public async Task NotReorderable_NothingMoves()
    {
        var moves = 0;
        var cut = RenderBound([], ThreeTabs, "a", p => p.Add(x => x.OnTabReorder, (NxTabReorderEventArgs _) => moves++));
        Assert.That(cut.Find("[role=tablist]").HasAttribute("data-reorderable"), Is.False);

        await cut.InvokeAsync(() => cut.Instance.OnTabDrop(0, 2));
        await cut.Find("#t-tab-a").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight", CtrlKey = true, ShiftKey = true });

        Assert.That(Order(cut), Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(moves, Is.Zero);
    }

    [Test]
    public async Task Reorderable_SideTabs_CtrlShiftUpDownMove_LeftRightDoNot()
    {
        var moves = new List<NxTabReorderEventArgs>();
        var cut = RenderBound([], ThreeTabs, "a", p => p.Add(x => x.Reorderable, true).Add(x => x.Position, NxTabPosition.Left)
            .Add(x => x.OnTabReorder, (NxTabReorderEventArgs e) => moves.Add(e)));

        await cut.Find("#t-tab-a").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight", CtrlKey = true, ShiftKey = true });
        Assert.That(Order(cut), Is.EqualTo(new[] { "A", "B", "C" }));

        await cut.Find("#t-tab-a").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown", CtrlKey = true, ShiftKey = true });
        Assert.That(Order(cut), Is.EqualTo(new[] { "B", "A", "C" }));
        Assert.That(moves.Select(m => (m.TabId, m.OldIndex, m.NewIndex)), Is.EqualTo(new[] { ("a", 0, 1) }));
    }

    [Test]
    public async Task Reorderable_ArrowsWithoutBothCtrlAndShift_DoNotMove()
    {
        var moves = 0;
        var cut = RenderBound([], ThreeTabs, "a", p => p.Add(x => x.Reorderable, true).Add(x => x.OnTabReorder, (NxTabReorderEventArgs _) => moves++));

        await cut.Find("#t-tab-a").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight", CtrlKey = true });
        await cut.Find("#t-tab-a").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        Assert.That(Order(cut), Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(moves, Is.Zero);
    }

    [Test]
    public async Task Reorderable_DropOnItsOwnPlaceOrFromNowhere_RaisesNothing()
    {
        var moves = 0;
        var cut = RenderBound([], ThreeTabs, "a", p => p.Add(x => x.Reorderable, true).Add(x => x.OnTabReorder, (NxTabReorderEventArgs _) => moves++));

        await cut.InvokeAsync(() => cut.Instance.OnTabDrop(1, 1));
        await cut.InvokeAsync(() => cut.Instance.OnTabDrop(7, 0));
        await cut.InvokeAsync(() => cut.Instance.OnTabDrop(-1, 0));

        Assert.That(Order(cut), Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(moves, Is.Zero);
    }

    [Test]
    public async Task Reorderable_DropPastTheEnd_GoesLast()
    {
        var moves = new List<NxTabReorderEventArgs>();
        var cut = RenderBound([], ThreeTabs, "a", p => p.Add(x => x.Reorderable, true).Add(x => x.OnTabReorder, (NxTabReorderEventArgs e) => moves.Add(e)));

        await cut.InvokeAsync(() => cut.Instance.OnTabDrop(0, 99));

        Assert.That(Order(cut), Is.EqualTo(new[] { "B", "C", "A" }));
        Assert.That(moves.Single().NewIndex, Is.EqualTo(2));
    }

    [Test]
    public async Task Reorderable_TheStripKeepsTheOrder_WhenThePageRendersAgain()
    {
        var ids = new List<string> { "a", "b", "c" };
        var cut = Render<NxTabStrip>(p => p.Add(x => x.Id, "t").Add(x => x.Reorderable, true).Add(x => x.ChildContent, WithBodies(ids)));
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 3);

        await cut.InvokeAsync(() => cut.Instance.OnTabDrop(0, 2));
        cut.Render(p => p.Add(x => x.ChildContent, WithBodies(ids)));

        Assert.That(Order(cut), Is.EqualTo(new[] { "b", "c", "a" }));
    }

    [Test]
    public async Task Reorderable_TabAddedLater_GoesAfterItsMarkupNeighbour_WhereverThatWasDragged()
    {
        var ids = new List<string> { "a", "b", "c" };
        var cut = Render<NxTabStrip>(p => p.Add(x => x.Id, "t").Add(x => x.Reorderable, true).Add(x => x.ChildContent, WithBodies(ids)));
        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 3);
        await cut.InvokeAsync(() => cut.Instance.OnTabDrop(0, 2));

        ids.Insert(1, "x");
        cut.Render(p => p.Add(x => x.ChildContent, WithBodies(ids)));

        cut.WaitForState(() => cut.FindAll("[role=tab]").Count == 4);
        Assert.That(Order(cut), Is.EqualTo(new[] { "b", "c", "a", "x" }));
    }

    [Test]
    public async Task Reorder_KeepAlivePanelsSurviveTheMove()
    {
        var cut = RenderBound([], ThreeTabs, "a", p => p.Add(x => x.Reorderable, true).Add(x => x.KeepAlive, true));
        await cut.Find("#t-tab-b").ClickAsync(new MouseEventArgs());

        await cut.InvokeAsync(() => cut.Instance.OnTabDrop(1, 0));

        Assert.That(cut.FindAll("[role=tabpanel]").Select(p => p.Id), Is.EquivalentTo(new[] { "t-panel-a", "t-panel-b" }));
        Assert.That(cut.Find("#t-panel-b").HasAttribute("hidden"), Is.False);
    }

    /// <summary>Tabs with a body, so each re-renders with the strip and reports its place in the markup.</summary>
    private static RenderFragment WithBodies(List<string> ids) => b =>
    {
        foreach (var id in ids)
        {
            b.OpenComponent<NxTab>(0);
            b.SetKey(id);
            b.AddAttribute(1, nameof(NxTab.Id), id);
            b.AddAttribute(2, nameof(NxTab.Title), id);
            b.AddAttribute(3, nameof(NxTab.ChildContent), Text($"Panel {id}"));
            b.CloseComponent();
        }
    };

    private static RenderFragment Fragment(List<string> ids) => b =>
    {
        foreach (var id in ids)
        {
            b.OpenComponent<NxTab>(0);
            b.SetKey(id);
            b.AddAttribute(1, nameof(NxTab.Id), id);
            b.AddAttribute(2, nameof(NxTab.Title), id);
            b.AddAttribute(3, nameof(NxTab.ChildContent), (RenderFragment)(c => c.AddContent(0, id)));
            b.CloseComponent();
        }
    };
}
