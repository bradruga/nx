using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxTileLayoutTests : BunitContext
{
    private BunitJSModuleInterop module = default!;
    private readonly List<NxTileReorderEventArgs> reorders = [];
    private readonly List<NxTileResizeEventArgs> resizes = [];

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-tilelayout.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private sealed record TileSpec(string Id, int Cols = 1, int Rows = 1, bool Visible = true);

    private static RenderFragment Tiles(params TileSpec[] tiles) => b =>
    {
        var seq = 0;
        foreach (var t in tiles)
        {
            b.OpenComponent<NxTileLayoutItem>(seq++);
            b.SetKey(t.Id);
            b.AddComponentParameter(seq++, nameof(NxTileLayoutItem.Id), t.Id);
            b.AddComponentParameter(seq++, nameof(NxTileLayoutItem.HeaderText), $"Tile {t.Id}");
            b.AddComponentParameter(seq++, nameof(NxTileLayoutItem.ColSpan), t.Cols);
            b.AddComponentParameter(seq++, nameof(NxTileLayoutItem.RowSpan), t.Rows);
            b.AddComponentParameter(seq++, nameof(NxTileLayoutItem.Visible), t.Visible);
            b.AddComponentParameter(seq++, nameof(NxTileLayoutItem.ChildContent), (RenderFragment)(c => c.AddContent(0, $"Body {t.Id}")));
            b.CloseComponent();
        }
    };

    private IRenderedComponent<NxTileLayout> RenderLayout(Action<ComponentParameterCollectionBuilder<NxTileLayout>>? configure = null, string? rowHeight = "100px", int columns = 4, params TileSpec[] tiles) =>
        Render<NxTileLayout>(p =>
        {
            p.Add(x => x.Id, "t");
            p.Add(x => x.Columns, columns);
            p.Add(x => x.RowHeight, rowHeight);
            p.Add(x => x.OnReorder, e => reorders.Add(e));
            p.Add(x => x.OnResize, e => resizes.Add(e));
            p.Add(x => x.ChildContent, Tiles(tiles.Length > 0 ? tiles : [new("a", 2), new("b"), new("c", 1, 2)]));
            configure?.Invoke(p);
        });

    private static List<string?> Order(IRenderedComponent<NxTileLayout> cut) =>
        cut.FindAll("[data-nx-tile]").Select(t => t.GetAttribute("data-nx-tile")).ToList();

    [Test]
    public void Render_GridAndTiles_InOrder_WithSpans()
    {
        var cut = RenderLayout(p => p.Add(x => x.ColumnWidth, "300px").Add(x => x.Gap, "8px").Add(x => x.AriaLabel, "Board"));

        var grid = cut.Find(".nx-tilelayout-grid");
        Assert.That(grid.GetAttribute("role"), Is.EqualTo("list"));
        Assert.That(grid.GetAttribute("aria-label"), Is.EqualTo("Board"));
        Assert.That(grid.GetAttribute("style"), Is.EqualTo("--nx-tilelayout-columns: repeat(4, minmax(0, 300px)); --nx-tilelayout-row: 100px; --nx-tilelayout-gap: 8px;"));
        Assert.That(Order(cut), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(cut.Find("[data-nx-tile=a]").GetAttribute("style"), Is.EqualTo("--nx-tile-cols: 2; --nx-tile-rows: 1;"));
        Assert.That(cut.Find("[data-nx-tile=c]").GetAttribute("style"), Is.EqualTo("--nx-tile-cols: 1; --nx-tile-rows: 2;"));
        Assert.That(cut.Find("[data-nx-tile=a] .nx-tile-title").TextContent, Is.EqualTo("Tile a"));
        Assert.That(cut.Find("[data-nx-tile=a] .nx-tile-body").TextContent.Trim(), Is.EqualTo("Body a"));
        Assert.That(cut.Find("[data-nx-tile=a] .nx-tile-body").GetAttribute("aria-labelledby"), Is.EqualTo("t-a"));
        Assert.That(cut.Find("[data-nx-tile=a] .nx-tile-header").HasAttribute("tabindex"), Is.False, "not editable: no tab stop");
        Assert.That(cut.FindAll(".nx-tile-resize"), Is.Empty);
    }

    [Test]
    public void ColSpan_ClampedToColumns_AutoRowsByDefault()
    {
        var cut = RenderLayout(rowHeight: null, columns: 2, tiles: [new TileSpec("wide", 5)]);
        Assert.That(cut.Find("[data-nx-tile=wide]").GetAttribute("style"), Does.StartWith("--nx-tile-cols: 2;"));
        Assert.That(cut.Find(".nx-tilelayout-grid").GetAttribute("style"), Does.Contain("repeat(2, 1fr)").And.Contain("--nx-tilelayout-row: auto"));
    }

    [Test]
    public async Task Drop_MovesToTheTargetsPlace_RaisesOnReorder()
    {
        var cut = RenderLayout(p => p.Add(x => x.Reorderable, true));

        await cut.Instance.OnTileDrop("c", "a");

        Assert.That(Order(cut), Is.EqualTo(new[] { "c", "a", "b" }));
        Assert.That(reorders.Single().Id, Is.EqualTo("c"));
        Assert.That((reorders[0].OldIndex, reorders[0].NewIndex), Is.EqualTo((2, 0)));
        Assert.That(cut.Find(".nx-tilelayout-sr[role=status]").TextContent, Is.EqualTo("Tile c moved to place 1 of 3."));
    }

    [Test]
    public async Task Drop_IgnoredWhenNotReorderable()
    {
        var cut = RenderLayout();
        await cut.Instance.OnTileDrop("c", "a");
        Assert.That(Order(cut), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(reorders, Is.Empty);
    }

    [Test]
    public async Task ResizeHandle_StoresSpans_RaisesOnResize()
    {
        var cut = RenderLayout(p => p.Add(x => x.Resizable, true));
        Assert.That(cut.FindAll(".nx-tile-resize"), Has.Count.EqualTo(3));
        Assert.That(cut.Find(".nx-tilelayout-grid").GetAttribute("data-resizable"), Is.EqualTo("true"));

        await cut.Instance.OnTileResize("b", 9, 3);

        Assert.That(cut.Find("[data-nx-tile=b]").GetAttribute("style"), Is.EqualTo("--nx-tile-cols: 4; --nx-tile-rows: 3;"));
        Assert.That((resizes.Single().ColSpan, resizes[0].RowSpan), Is.EqualTo((4, 3)));
    }

    [Test]
    public async Task Keyboard_CtrlMoves_ShiftResizes_FocusFollows()
    {
        var cut = RenderLayout(p => p.Add(x => x.Reorderable, true).Add(x => x.Resizable, true));
        var header = cut.Find("[data-nx-tile=a] .nx-tile-header");
        Assert.That(header.GetAttribute("tabindex"), Is.EqualTo("0"));
        Assert.That(header.GetAttribute("aria-describedby"), Is.EqualTo("t-help"));

        await cut.Find("[data-nx-tile=a] .nx-tile-header").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight", CtrlKey = true });
        Assert.That(Order(cut), Is.EqualTo(new[] { "b", "a", "c" }));
        Assert.That(module.Invocations.Last(i => i.Identifier == "focusTile").Arguments[1], Is.EqualTo("a"));

        await cut.Find("[data-nx-tile=a] .nx-tile-header").KeyDownAsync(new KeyboardEventArgs { Key = "End", CtrlKey = true });
        Assert.That(Order(cut), Is.EqualTo(new[] { "b", "c", "a" }));

        await cut.Find("[data-nx-tile=a] .nx-tile-header").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft", ShiftKey = true });
        await cut.Find("[data-nx-tile=a] .nx-tile-header").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });
        Assert.That(cut.Find("[data-nx-tile=a]").GetAttribute("style"), Is.EqualTo("--nx-tile-cols: 1; --nx-tile-rows: 2;"));
        Assert.That(cut.Find(".nx-tilelayout-sr[role=status]").TextContent, Is.EqualTo("Tile a is 1 column wide and 2 rows tall."));
        Assert.That(reorders, Has.Count.EqualTo(2));
        Assert.That(resizes, Has.Count.EqualTo(2));
    }

    [Test]
    public void Hidden_KeepsItsPlace()
    {
        var cut = RenderLayout(tiles: [new("a"), new("b", Visible: false), new("c")]);
        Assert.That(Order(cut), Is.EqualTo(new[] { "a", "c" }));
        Assert.That(cut.Instance.GetState().ItemStates.Select(s => (s.Id, s.Order, s.Visible)),
            Is.EqualTo(new[] { ("a", 0, true), ("b", 1, false), ("c", 2, true) }));
    }

    [Test]
    public async Task State_RoundTrips()
    {
        var cut = RenderLayout(p => p.Add(x => x.Reorderable, true).Add(x => x.Resizable, true));
        await cut.Instance.OnTileDrop("c", "a");
        await cut.Instance.OnTileResize("b", 3, 2);
        var saved = cut.Instance.GetState();

        var fresh = RenderLayout();
        await fresh.InvokeAsync(() => fresh.Instance.SetState(saved));

        Assert.That(Order(fresh), Is.EqualTo(new[] { "c", "a", "b" }));
        Assert.That(fresh.Find("[data-nx-tile=b]").GetAttribute("style"), Is.EqualTo("--nx-tile-cols: 3; --nx-tile-rows: 2;"));
    }

    [Test]
    public async Task OutsideSpanChange_ReplacesTheUsers()
    {
        var cut = RenderLayout(p => p.Add(x => x.Resizable, true), tiles: [new TileSpec("a", 2)]);
        await cut.Instance.OnTileResize("a", 3, 1);

        cut.Render(p => p.Add(x => x.ChildContent, Tiles(new TileSpec("a", 1))));

        Assert.That(cut.Find("[data-nx-tile=a]").GetAttribute("style"), Is.EqualTo("--nx-tile-cols: 1; --nx-tile-rows: 1;"));
    }

    [Test]
    public void ItemOutsideALayout_Throws() => Assert.Throws<InvalidOperationException>(() => Render<NxTileLayoutItem>());
}
