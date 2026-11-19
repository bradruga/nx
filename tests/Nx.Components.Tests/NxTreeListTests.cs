using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxTreeListTests : BunitContext
{
    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-input.js").Mode = JSRuntimeMode.Loose;
    }

    private sealed record Spot(int Id, int? Parent, string Name, int Pieces);

    private static readonly Spot[] Yard =
    [
        new(1, null, "Lincoln Ave", 18),
        new(2, 1, "Row A", 10),
        new(3, 2, "A-1", 6),
        new(4, 2, "A-2", 4),
        new(5, 1, "Row B", 8),
        new(6, null, "Stockyards", 5),
    ];

    private static RenderFragment Columns(bool reverse = false, bool withTemplate = false) => b =>
    {
        void Column(int seq, Func<Spot, object?> value, string title, string width, bool expandable)
        {
            b.OpenComponent<NxTreeListColumn<Spot>>(seq);
            b.AddComponentParameter(seq + 1, nameof(NxTreeListColumn<Spot>.Value), value);
            b.AddComponentParameter(seq + 2, nameof(NxTreeListColumn<Spot>.Title), title);
            b.AddComponentParameter(seq + 3, nameof(NxTreeListColumn<Spot>.Width), width);
            b.AddComponentParameter(seq + 4, nameof(NxTreeListColumn<Spot>.Expandable), expandable);
            b.CloseComponent();
        }
        if (reverse)
        {
            Column(0, s => s.Pieces, "# Pieces", "100px", false);
            Column(10, s => s.Name, "Location", "200px", true);
        }
        else
        {
            Column(0, s => s.Name, "Location", "200px", true);
            Column(10, s => s.Pieces, "# Pieces", "100px", false);
        }
        if (withTemplate)
        {
            b.OpenComponent<NxTreeListColumn<Spot>>(20);
            b.AddComponentParameter(21, nameof(NxTreeListColumn<Spot>.Title), "Big");
            b.AddComponentParameter(22, nameof(NxTreeListColumn<Spot>.Template), (RenderFragment<Spot>)(s => t => t.AddContent(0, s.Pieces > 9 ? "yes" : "no")));
            b.CloseComponent();
        }
    };

    // Columns and Data are arguments: bUnit merges a fragment added twice instead of replacing it.
    private IRenderedComponent<NxTreeList<Spot>> RenderList(Action<ComponentParameterCollectionBuilder<NxTreeList<Spot>>>? configure = null,
        RenderFragment? columns = null, IEnumerable<Spot>? data = null) =>
        Render<NxTreeList<Spot>>(p =>
        {
            p.Add(x => x.Id, "yard");
            p.Add(x => x.Data, data ?? Yard);
            p.Add(x => x.ItemId, s => s.Id);
            p.Add(x => x.ParentId, s => s.Parent);
            p.Add(x => x.Columns, columns ?? Columns());
            p.Add(x => x.AriaLabel, "Yard");
            configure?.Invoke(p);
        });

    private static string[] Row(IRenderedComponent<NxTreeList<Spot>> cut, int index) =>
        [.. cut.FindAll(".nx-treeview-row")[index].QuerySelectorAll(".nx-treelist-td").Select(td => td.TextContent.Trim())];

    [Test]
    public void Render_HeaderAndTopRows()
    {
        var cut = RenderList();

        var headers = cut.FindAll(".nx-treelist-th");
        Assert.That(headers.Select(h => h.TextContent), Is.EqualTo(new[] { "Location", "# Pieces" }));
        Assert.That(headers[0].GetAttribute("style"), Is.EqualTo("min-width: max(0px, calc(200px - var(--nx-treeview-level, 0) * var(--nx-treeview-indent)))"));
        Assert.That(headers[1].GetAttribute("style"), Is.EqualTo("width: 100px"));
        Assert.That(cut.Find(".nx-treelist-header").GetAttribute("aria-hidden"), Is.EqualTo("true"));
        Assert.That(cut.Find("[role=tree]").GetAttribute("aria-label"), Is.EqualTo("Yard"));
        Assert.That(cut.FindAll(".nx-treeview-row"), Has.Count.EqualTo(2));
        Assert.That(Row(cut, 0), Is.EqualTo(new[] { "Lincoln Ave", "# Pieces: 18" }));
        Assert.That(cut.Find(".nx-treelist-table").GetAttribute("style"), Is.EqualTo("min-width: calc(200px + 100px + var(--nx-treelist-chrome))"));
    }

    [Test]
    public void ExpandedItems_OpenAtStart()
    {
        var cut = RenderList(p => p.Add(x => x.ExpandedItems, [Yard[0], Yard[1]]));

        Assert.That(cut.FindAll(".nx-treeview-row").Select(r => r.QuerySelector(".nx-treelist-td")!.TextContent),
            Is.EqualTo(new[] { "Lincoln Ave", "Row A", "A-1", "A-2", "Row B", "Stockyards" }));
        Assert.That(cut.FindAll("[role=treeitem]")[2].GetAttribute("aria-level"), Is.EqualTo("3"));
    }

    [Test]
    public async Task Toggle_RaisesExpandedItems()
    {
        IEnumerable<Spot>? expanded = null;
        var cut = RenderList(p => p.Add(x => x.ExpandedItemsChanged, (IEnumerable<Spot> e) => expanded = e));

        await cut.Find(".nx-treeview-toggle").ClickAsync(new MouseEventArgs());

        Assert.That(expanded!.Select(s => s.Name), Is.EqualTo(new[] { "Lincoln Ave" }));
        Assert.That(cut.FindAll(".nx-treeview-row"), Has.Count.EqualTo(4));
    }

    [Test]
    public async Task Click_SelectsSingle()
    {
        IEnumerable<Spot>? selected = null;
        var cut = RenderList(p => p.Add(x => x.SelectedItemsChanged, (IEnumerable<Spot> s) => selected = s));

        await cut.FindAll(".nx-treeview-row")[1].ClickAsync(new MouseEventArgs());

        Assert.That(selected!.Single().Name, Is.EqualTo("Stockyards"));
        Assert.That(cut.FindAll("[role=treeitem]")[1].GetAttribute("aria-selected"), Is.EqualTo("true"));
    }

    [Test]
    public void SelectedItems_FromOutside()
    {
        var cut = RenderList(p => p.Add(x => x.SelectedItems, [Yard[5]]));
        Assert.That(cut.FindAll("[role=treeitem]")[1].GetAttribute("aria-selected"), Is.EqualTo("true"));
    }

    [Test]
    public void ExpandableColumn_DrawnFirst()
    {
        var cut = RenderList(columns: Columns(reverse: true));

        Assert.That(cut.FindAll(".nx-treelist-th").Select(h => h.TextContent), Is.EqualTo(new[] { "Location", "# Pieces" }));
        Assert.That(cut.FindAll(".nx-treeview-row")[0].QuerySelector(".nx-treelist-td-tree")!.TextContent, Is.EqualTo("Lincoln Ave"));
    }

    [Test]
    public void Template_RendersTheCell()
    {
        var cut = RenderList(columns: Columns(withTemplate: true));

        Assert.That(Row(cut, 0), Is.EqualTo(new[] { "Lincoln Ave", "# Pieces: 18", "Big: yes" }));
        Assert.That(Row(cut, 1)[2], Is.EqualTo("Big: no"));
    }

    [Test]
    public void Height_And_Empty()
    {
        var cut = RenderList(p => p.Add(x => x.Height, "100%").Add(x => x.NoDataText, "No locations"), data: []);

        Assert.That(cut.Find(".nx-treelist").GetAttribute("style"), Is.EqualTo("height: 100%"));
        Assert.That(cut.Find(".nx-treeview-empty").TextContent, Is.EqualTo("No locations"));
        Assert.That(cut.FindAll(".nx-treelist-th"), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ExpandAll_CollapseAll()
    {
        var cut = RenderList();
        await cut.InvokeAsync(cut.Instance.ExpandAllAsync);
        Assert.That(cut.FindAll(".nx-treeview-row"), Has.Count.EqualTo(6));
        await cut.InvokeAsync(cut.Instance.CollapseAllAsync);
        Assert.That(cut.FindAll(".nx-treeview-row"), Has.Count.EqualTo(2));
    }

    [Test]
    public void Cells_AlignWithTheHeader_AtEveryDepth()
    {
        var cut = RenderList(p => p.Add(x => x.ExpandedItems, [Yard[0], Yard[1]]));

        var headers = cut.FindAll(".nx-treelist-th").Select(h => h.GetAttribute("style")).ToList();
        var deep = cut.FindAll(".nx-treeview-row")[2];
        Assert.That(deep.GetAttribute("style"), Is.EqualTo("--nx-treeview-level: 2"));
        Assert.That(deep.QuerySelectorAll(".nx-treelist-td").Select(td => td.GetAttribute("style")), Is.EqualTo(headers));
    }

    [Test]
    public void NoExpandableColumn_TheFirstHoldsTheTree()
    {
        RenderFragment columns = b =>
        {
            b.OpenComponent<NxTreeListColumn<Spot>>(0);
            b.AddComponentParameter(1, nameof(NxTreeListColumn<Spot>.Value), (Func<Spot, object?>)(s => s.Pieces));
            b.AddComponentParameter(2, nameof(NxTreeListColumn<Spot>.Title), "# Pieces");
            b.CloseComponent();
            b.OpenComponent<NxTreeListColumn<Spot>>(10);
            b.AddComponentParameter(11, nameof(NxTreeListColumn<Spot>.Value), (Func<Spot, object?>)(s => s.Name));
            b.AddComponentParameter(12, nameof(NxTreeListColumn<Spot>.Title), "Location");
            b.CloseComponent();
        };
        var cut = RenderList(columns: columns);

        Assert.That(cut.Find(".nx-treelist-th-tree").TextContent, Is.EqualTo("# Pieces"));
        Assert.That(Row(cut, 0), Is.EqualTo(new[] { "18", "Location: Lincoln Ave" }));
        // No widths: no inline sizes, and the table has no least width.
        Assert.That(cut.Find(".nx-treelist-th").HasAttribute("style"), Is.False);
        Assert.That(cut.Find(".nx-treelist-table").HasAttribute("style"), Is.False);
    }

    [Test]
    public async Task Multiple_SelectsSeveral()
    {
        IEnumerable<Spot>? selected = null;
        var cut = RenderList(p => p
            .Add(x => x.SelectionMode, NxTreeSelectionMode.Multiple)
            .Add(x => x.SelectedItemsChanged, (IEnumerable<Spot> s) => selected = s));
        Assert.That(cut.Find("[role=tree]").GetAttribute("aria-multiselectable"), Is.EqualTo("true"));

        await cut.FindAll(".nx-treeview-row")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".nx-treeview-row")[0].ClickAsync(new MouseEventArgs());

        Assert.That(selected!.Select(s => s.Name), Is.EqualTo(new[] { "Lincoln Ave", "Stockyards" }));
    }

    [Test]
    public async Task SelectedItem_Single_Raised()
    {
        Spot? selected = null;
        var cut = RenderList(p => p.Add(x => x.SelectedItemChanged, (Spot? s) => selected = s));

        await cut.FindAll(".nx-treeview-row")[1].ClickAsync(new MouseEventArgs());

        Assert.That(selected, Is.SameAs(Yard[5]));
    }

    [Test]
    public async Task Keys_ExpandAndSelect_ThroughTheTree()
    {
        Spot? selected = null;
        var cut = RenderList(p => p.Add(x => x.SelectedItemChanged, (Spot? s) => selected = s));
        var tree = () => cut.Find("[role=tree]");

        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.That(cut.FindAll(".nx-treeview-row"), Has.Count.EqualTo(4));
        Assert.That(selected!.Name, Is.EqualTo("Row A"));
        Assert.That(tree().GetAttribute("aria-activedescendant"), Is.EqualTo("yard-1"));
    }

    [Test]
    public void Column_OutsideAList_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxTreeListColumn<Spot>>());
    }
}
