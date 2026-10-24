using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxTreeViewTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private sealed record Perm(int Id, int? ParentId, string Text);

    private sealed record Folder(string Name, List<Folder> Folders);

    private static readonly List<Perm> Perms =
    [
        new(1, null, "Portal"),
        new(2, 1, "Projects"),
        new(3, 2, "Edit pours"),
        new(4, 1, "Yard"),
        new(5, null, "Admin")
    ];

    private IRenderedComponent<NxTreeView<Perm>> RenderFlat(Action<ComponentParameterCollectionBuilder<NxTreeView<Perm>>>? configure = null) =>
        Render<NxTreeView<Perm>>(p =>
        {
            p.Add(x => x.Id, "t");
            p.Add(x => x.Data, Perms);
            p.Add(x => x.ItemId, n => n.Id);
            p.Add(x => x.ParentId, n => n.ParentId);
            p.Add(x => x.ItemText, n => n.Text);
            p.Add(x => x.AriaLabel, "Permissions");
            configure?.Invoke(p);
        });

    private static List<string> Shown(IRenderedComponent<NxTreeView<Perm>> cut) =>
        cut.FindAll(".nx-treeview-content").Select(e => e.TextContent).ToList();

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Flat_TopItemsCollapsed_AriaTree()
    {
        var cut = RenderFlat();

        var tree = cut.Find("ul[role=tree]#t");
        Assert.That(tree.GetAttribute("aria-label"), Is.EqualTo("Permissions"));
        Assert.That(tree.GetAttribute("tabindex"), Is.EqualTo("0"));
        Assert.That(Shown(cut), Is.EqualTo(new[] { "Portal", "Admin" }));
        var portal = cut.Find("#t-0");
        Assert.That(portal.GetAttribute("aria-expanded"), Is.EqualTo("false"));
        Assert.That(portal.GetAttribute("aria-level"), Is.EqualTo("1"));
        Assert.That(portal.GetAttribute("aria-setsize"), Is.EqualTo("2"));
        Assert.That(portal.GetAttribute("aria-selected"), Is.EqualTo("false"));
        Assert.That(cut.Find("#t-4").HasAttribute("aria-expanded"), Is.False);
        Assert.That(module.Invocations.Single(i => i.Identifier == "preventKeys").Arguments[1], Does.Contain("ArrowDown"));
    }

    [Test]
    public void Nested_ByChildren_ExpandedItemsShown()
    {
        var plant = new Folder("Plant", [new Folder("Forms", []), new Folder("Mixes", [])]);
        var cut = Render<NxTreeView<Folder>>(p => p
            .Add(x => x.Data, new[] { plant, new Folder("Office", []) })
            .Add(x => x.Children, f => f.Folders)
            .Add(x => x.ItemText, f => f.Name)
            .Add(x => x.ExpandedItems, new[] { plant }));

        Assert.That(cut.FindAll(".nx-treeview-content").Select(e => e.TextContent), Is.EqualTo(new[] { "Plant", "Forms", "Mixes", "Office" }));
        Assert.That(cut.FindAll("ul[role=group] > li")[1].GetAttribute("aria-level"), Is.EqualTo("2"));
    }

    [Test]
    public void Empty_NoDataText_Template()
    {
        Assert.That(Render<NxTreeView<Perm>>(p => p.Add(x => x.NoDataText, "No permissions")).Find(".nx-treeview-empty").TextContent, Is.EqualTo("No permissions"));

        var cut = RenderFlat(p => p.Add(x => x.ItemTemplate, n => b => b.AddMarkupContent(0, $"<b>{n.Id}</b>")));
        Assert.That(cut.FindAll(".nx-treeview-content b").Select(e => e.TextContent), Is.EqualTo(new[] { "1", "5" }));
    }

    [Test]
    public void ParentIdWithoutItemId_Throws() =>
        Assert.Throws<InvalidOperationException>(() => Render<NxTreeView<Perm>>(p => p.Add(x => x.Data, Perms).Add(x => x.ParentId, n => n.ParentId)));

    // ── Expanding and selecting ───────────────────────────────────────────────

    [Test]
    public async Task Toggle_Expands_RaisesExpandedItems()
    {
        IEnumerable<Perm>? expanded = null;
        var cut = RenderFlat(p => p.Add(x => x.ExpandedItemsChanged, e => expanded = e));

        await cut.Find("#t-0 .nx-treeview-toggle").ClickAsync(new MouseEventArgs());

        Assert.That(Shown(cut), Is.EqualTo(new[] { "Portal", "Projects", "Yard", "Admin" }));
        Assert.That(expanded!.Select(n => n.Id), Is.EqualTo(new[] { 1 }));
        Assert.That(cut.Find("#t-0").GetAttribute("aria-expanded"), Is.EqualTo("true"));

        await cut.Find("#t-0 > .nx-treeview-row").DoubleClickAsync(new MouseEventArgs());
        Assert.That(Shown(cut), Is.EqualTo(new[] { "Portal", "Admin" }));
    }

    [Test]
    public async Task ExpandAll_CollapseAll()
    {
        var cut = RenderFlat();
        await cut.InvokeAsync(cut.Instance.ExpandAllAsync);
        Assert.That(Shown(cut), Has.Count.EqualTo(5));
        await cut.InvokeAsync(cut.Instance.CollapseAllAsync);
        Assert.That(Shown(cut), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Click_SelectsOne_RaisesBoth()
    {
        Perm? item = null;
        IEnumerable<Perm>? items = null;
        var cut = RenderFlat(p => p.Add(x => x.SelectedItemChanged, v => item = v).Add(x => x.SelectedItemsChanged, v => items = v));

        await cut.Find("#t-4 > .nx-treeview-row").ClickAsync(new MouseEventArgs());

        Assert.That(item!.Text, Is.EqualTo("Admin"));
        Assert.That(items!.Single().Text, Is.EqualTo("Admin"));
        Assert.That(cut.Find("#t-4").GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(cut.Find("#t-4 > .nx-treeview-row").ClassList, Does.Contain("nx-treeview-row-selected"));
    }

    [Test]
    public async Task Multiple_ClicksToggle_InTreeOrder()
    {
        IEnumerable<Perm>? items = null;
        var cut = RenderFlat(p => p.Add(x => x.SelectionMode, NxTreeSelectionMode.Multiple).Add(x => x.SelectedItemsChanged, v => items = v));
        Assert.That(cut.Find("[role=tree]").GetAttribute("aria-multiselectable"), Is.EqualTo("true"));

        await cut.Find("#t-4 > .nx-treeview-row").ClickAsync(new MouseEventArgs());
        await cut.Find("#t-0 > .nx-treeview-row").ClickAsync(new MouseEventArgs());
        Assert.That(items!.Select(n => n.Id), Is.EqualTo(new[] { 1, 5 }));

        await cut.Find("#t-4 > .nx-treeview-row").ClickAsync(new MouseEventArgs());
        Assert.That(items!.Select(n => n.Id), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void SelectedItem_FromOutside_AndNoneMode()
    {
        var cut = RenderFlat(p => p.Add(x => x.SelectedItem, Perms[4]));
        Assert.That(cut.Find("#t-4").GetAttribute("aria-selected"), Is.EqualTo("true"));

        cut.Render(p => p.Add(x => x.SelectedItem, Perms[0]));
        Assert.That(cut.Find("#t-0").GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(cut.Find("#t-4").GetAttribute("aria-selected"), Is.EqualTo("false"));

        var none = RenderFlat(p => p.Add(x => x.SelectionMode, NxTreeSelectionMode.None));
        Assert.That(none.Find("#t-0").HasAttribute("aria-selected"), Is.False);
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Keys_MoveExpandSelect_ActiveDescendant()
    {
        Perm? item = null;
        var cut = RenderFlat(p => p.Add(x => x.SelectedItemChanged, v => item = v));
        var tree = () => cut.Find("[role=tree]");

        await tree().FocusAsync(new FocusEventArgs());
        Assert.That(tree().GetAttribute("aria-activedescendant"), Is.EqualTo("t-0"));

        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.That(cut.Find("#t-0").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.That(tree().GetAttribute("aria-activedescendant"), Is.EqualTo("t-1"));
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.That(tree().GetAttribute("aria-activedescendant"), Is.EqualTo("t-3"));
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        Assert.That(tree().GetAttribute("aria-activedescendant"), Is.EqualTo("t-4"));
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.That(tree().GetAttribute("aria-activedescendant"), Is.EqualTo("t-0"));
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.That(cut.Find("#t-0").GetAttribute("aria-expanded"), Is.EqualTo("false"));
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        await tree().KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.That(item!.Text, Is.EqualTo("Admin"));
        Assert.That(cut.Find("#t-4 > .nx-treeview-row").ClassList, Does.Contain("nx-treeview-row-active"));
        Assert.That(module.Invocations.Any(i => i.Identifier == "reveal"), Is.True);
    }

    // ── Drag and drop ─────────────────────────────────────────────────────────

    private async Task<NxTreeDropEventArgs<Perm>?> DropAsync(IRenderedComponent<NxTreeView<Perm>> cut, string from, string to, double offsetY)
    {
        NxTreeDropEventArgs<Perm>? args = null;
        cut.Render(p => p.Add(x => x.OnDrop, e => args = e));
        await cut.Find($"#{from} > .nx-treeview-row").DragStartAsync(new DragEventArgs());
        await cut.Find($"#{to} > .nx-treeview-row").DragEnterAsync(new DragEventArgs());
        await cut.Find($"#{to} > .nx-treeview-row").DropAsync(new DragEventArgs { OffsetY = offsetY });
        return args;
    }

    [TestCase(4, NxTreeDropPosition.Before)]
    [TestCase(16, NxTreeDropPosition.Inside)]
    [TestCase(28, NxTreeDropPosition.After)]
    public async Task Drop_PositionFromTheRowHalf(double offsetY, NxTreeDropPosition expected)
    {
        var cut = RenderFlat(p => p.Add(x => x.Draggable, true));
        Assert.That(cut.Find("#t-4 > .nx-treeview-row").GetAttribute("draggable"), Is.EqualTo("true"));

        var args = await DropAsync(cut, "t-4", "t-0", offsetY);

        Assert.That(args!.Item.Text, Is.EqualTo("Admin"));
        Assert.That(args.Destination.Text, Is.EqualTo("Portal"));
        Assert.That(args.Position, Is.EqualTo(expected));
    }

    [Test]
    public async Task Drop_IntoItsOwnBranch_Refused()
    {
        var cut = RenderFlat(p => p.Add(x => x.Draggable, true).Add(x => x.ExpandedItems, new[] { Perms[0] }));
        Assert.That(await DropAsync(cut, "t-0", "t-1", 16), Is.Null);
        Assert.That(await DropAsync(cut, "t-0", "t-0", 16), Is.Null);
    }

    [Test]
    public async Task NotDraggable_NoDrop()
    {
        var cut = RenderFlat();
        Assert.That(cut.Find("#t-4 > .nx-treeview-row").HasAttribute("draggable"), Is.False);
        Assert.That(await DropAsync(cut, "t-4", "t-0", 16), Is.Null);
    }
}
