using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A dashboard grid of titled tiles (<see cref="NxTileLayoutItem"/>), each spanning columns and rows. With
/// <see cref="Reorderable"/> tiles are dragged by their header onto another's place; with <see cref="Resizable"/>
/// a corner handle changes their spans. The keyboard does both from a tile's header (Ctrl+arrows move, Shift+arrows
/// resize). <see cref="GetState"/> and <see cref="SetState"/> save and restore the arrangement. Under 600 px of
/// its own width the tiles stack in one column.
/// </summary>
/// <example>
/// <code>
/// &lt;NxTileLayout Columns="4" RowHeight="150px" Reorderable="true" Resizable="true" OnReorder="SaveAsync"&gt;
///     &lt;NxTileLayoutItem Id="sets" HeaderText="Today's sets" ColSpan="2" RowSpan="2"&gt;…&lt;/NxTileLayoutItem&gt;
/// &lt;/NxTileLayout&gt;
/// </code>
/// </example>
public partial class NxTileLayout : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-tilelayout.js follows a drag or a resize with the pointer and reports the drop or the new spans on release.

    private const string ModulePath = "./_content/Nx.Components/nx-tilelayout.js";
    private const string HelpText = "Ctrl and the arrow keys move this tile; Shift and the arrow keys resize it.";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-tilelayout-{Guid.NewGuid():N}"[..22];
    private readonly List<Tile> tiles = [];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxTileLayout>? selfReference;
    private ElementReference grid;
    private NxTileLayoutState? pendingState;
    private string? focusPending;
    private string announcement = "";

    /// <summary>One tile: its item and the place and size the layout gives it.</summary>
    private sealed class Tile(NxTileLayoutItem item)
    {
        public NxTileLayoutItem Item { get; } = item;
        public int Order { get; set; }
        public int ColSpan { get; set; } = item.ColSpan;
        public int RowSpan { get; set; } = item.RowSpan;
        public bool Visible { get; set; } = item.Visible;
        public (int, int, bool) Seen { get; set; } = (item.ColSpan, item.RowSpan, item.Visible);
    }

    /// <summary>The tiles: <see cref="NxTileLayoutItem"/>s.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The number of columns. Default: <c>3</c>.</summary>
    [Parameter] public int Columns { get; set; } = 3;

    /// <summary>The widest a column grows, e.g. <c>300px</c>; columns share the width up to it. Default: no limit.</summary>
    [Parameter] public string? ColumnWidth { get; set; }

    /// <summary>The height of one row, e.g. <c>150px</c>. Default: <c>auto</c> (rows fit their content; height is not resizable).</summary>
    [Parameter] public string? RowHeight { get; set; }

    /// <summary>The space between tiles, e.g. <c>12px</c>. Default: <c>--nx-tilelayout-gap</c>.</summary>
    [Parameter] public string? Gap { get; set; }

    /// <summary>Lets the user drag a tile by its header onto another tile's place.</summary>
    [Parameter] public bool Reorderable { get; set; }

    /// <summary>Lets the user resize a tile from its bottom-right handle.</summary>
    [Parameter] public bool Resizable { get; set; }

    /// <summary>Raised after the user moved a tile.</summary>
    [Parameter] public EventCallback<NxTileReorderEventArgs> OnReorder { get; set; }

    /// <summary>Raised after the user resized a tile.</summary>
    [Parameter] public EventCallback<NxTileResizeEventArgs> OnResize { get; set; }

    /// <summary>The layout's accessible name.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The outer element's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The arrangement now: each tile's id, order, spans and visibility.</summary>
    public NxTileLayoutState GetState() => new()
    {
        ItemStates = Ordered.Select(t => new NxTileLayoutItemState
        {
            Id = t.Item.Key, Order = t.Order, ColSpan = t.ColSpan, RowSpan = t.RowSpan, Visible = t.Visible
        }).ToList()
    };

    /// <summary>Restores an arrangement from <see cref="GetState"/>. Tiles it does not name keep their place after the others.</summary>
    public void SetState(NxTileLayoutState state)
    {
        pendingState = state;
        ApplyState();
        StateHasChanged();
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private bool Editable => Reorderable || Resizable;
    private string BaseId => Id ?? generatedId;
    private string HelpId => $"{BaseId}-help";
    private string HeaderId(Tile tile) => $"{BaseId}-{tile.Item.Key}";
    private IEnumerable<Tile> Ordered => tiles.OrderBy(t => t.Order);
    private int ColumnCount => Math.Max(1, Columns);
    private int SpanOf(Tile tile) => Math.Clamp(tile.ColSpan, 1, ColumnCount);

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-tilelayout" : $"nx-tilelayout {Class}";

    private string GridStyle
    {
        get
        {
            var column = ColumnWidth is null ? "1fr" : $"minmax(0, {ColumnWidth})";
            var style = $"--nx-tilelayout-columns: repeat({ColumnCount}, {column}); --nx-tilelayout-row: {RowHeight ?? "auto"};";
            return Gap is null ? style : $"{style} --nx-tilelayout-gap: {Gap};";
        }
    }

    private string TileStyle(Tile tile) => string.Create(CultureInfo.InvariantCulture, $"--nx-tile-cols: {SpanOf(tile)}; --nx-tile-rows: {Math.Max(1, tile.RowSpan)};");

    private static string TileClass(Tile tile) =>
        string.IsNullOrEmpty(tile.Item.Class) ? "nx-tile" : $"nx-tile {tile.Item.Class}";

    // ── Tiles ─────────────────────────────────────────────────────────────────

    internal void Register(NxTileLayoutItem item)
    {
        tiles.Add(new Tile(item) { Order = tiles.Count == 0 ? 0 : tiles.Max(t => t.Order) + 1 });
        ApplyState();
        StateHasChanged();
    }

    internal void Unregister(NxTileLayoutItem item)
    {
        if (tiles.RemoveAll(t => t.Item == item) > 0)
            StateHasChanged();
    }

    /// <summary>A tile's shown parameters changed: an outside change of its spans or visibility replaces the user's.</summary>
    internal void ItemChanged(NxTileLayoutItem item)
    {
        if (tiles.Find(t => t.Item == item) is { } tile)
        {
            var now = (item.ColSpan, item.RowSpan, item.Visible);
            if (now != tile.Seen)
            {
                tile.Seen = now;
                (tile.ColSpan, tile.RowSpan, tile.Visible) = now;
            }
        }
        StateHasChanged();
    }

    private void ApplyState()
    {
        if (pendingState is null)
            return;
        var known = pendingState.ItemStates.ToDictionary(s => s.Id);
        var next = known.Count;
        foreach (var tile in Ordered.ToList())
        {
            if (known.TryGetValue(tile.Item.Key, out var s))
            {
                tile.Order = s.Order;
                tile.ColSpan = Math.Max(1, s.ColSpan);
                tile.RowSpan = Math.Max(1, s.RowSpan);
                tile.Visible = s.Visible;
            }
            else
                tile.Order = next++;
        }
        Renumber();
        // Kept until every tile it names has registered, so a state set before the tiles render still applies.
        if (known.Keys.All(id => tiles.Exists(t => t.Item.Key == id)))
            pendingState = null;
    }

    private void Renumber()
    {
        var i = 0;
        foreach (var tile in Ordered.ToList())
            tile.Order = i++;
    }

    /// <summary>Moves a tile to another's place; the tiles between shift by one.</summary>
    private async Task MoveAsync(Tile tile, int newIndex)
    {
        var list = Ordered.ToList();
        var oldIndex = list.IndexOf(tile);
        newIndex = Math.Clamp(newIndex, 0, list.Count - 1);
        if (oldIndex < 0 || oldIndex == newIndex)
            return;
        list.RemoveAt(oldIndex);
        list.Insert(newIndex, tile);
        for (var i = 0; i < list.Count; i++)
            list[i].Order = i;
        announcement = $"{TitleOf(tile)} moved to place {newIndex + 1} of {list.Count}.";
        StateHasChanged();
        await OnReorder.InvokeAsync(new NxTileReorderEventArgs(tile.Item.Key, oldIndex, newIndex));
    }

    private async Task ResizeAsync(Tile tile, int colSpan, int rowSpan)
    {
        colSpan = Math.Clamp(colSpan, 1, ColumnCount);
        rowSpan = Math.Max(1, rowSpan);
        if (colSpan == SpanOf(tile) && rowSpan == tile.RowSpan)
            return;
        tile.ColSpan = colSpan;
        tile.RowSpan = rowSpan;
        announcement = $"{TitleOf(tile)} is {colSpan} {(colSpan == 1 ? "column" : "columns")} wide and {rowSpan} {(rowSpan == 1 ? "row" : "rows")} tall.";
        StateHasChanged();
        await OnResize.InvokeAsync(new NxTileResizeEventArgs(tile.Item.Key, colSpan, rowSpan));
    }

    private static string TitleOf(Tile tile) => tile.Item.HeaderText ?? tile.Item.Key;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is null)
                return;
            selfReference = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("init", grid, selfReference);
        }
        if (module is not null && focusPending is { } key)
        {
            focusPending = null;
            await module.InvokeVoidAsync("focusTile", grid, key);
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Called by nx-tilelayout.js when a dragged tile is dropped on another. Not for application use.</summary>
    [JSInvokable]
    public Task OnTileDrop(string key, string targetKey) => InvokeAsync(async () =>
    {
        var list = Ordered.ToList();
        var tile = list.Find(t => t.Item.Key == key);
        var target = list.FindIndex(t => t.Item.Key == targetKey);
        if (Reorderable && tile is not null && target >= 0)
            await MoveAsync(tile, target);
    });

    /// <summary>Called by nx-tilelayout.js when a resize handle is let go. Not for application use.</summary>
    [JSInvokable]
    public Task OnTileResize(string key, int colSpan, int rowSpan) => InvokeAsync(async () =>
    {
        if (Resizable && tiles.Find(t => t.Item.Key == key) is { } tile)
            await ResizeAsync(tile, colSpan, rowSpan);
        StateHasChanged();
    });

    private async Task HandleHeaderKeyDown(KeyboardEventArgs e, Tile tile)
    {
        var place = Ordered.ToList().IndexOf(tile);
        if (Reorderable && e.CtrlKey && !e.ShiftKey)
        {
            var to = e.Key switch
            {
                "ArrowLeft" or "ArrowUp"    => place - 1,
                "ArrowRight" or "ArrowDown" => place + 1,
                "Home"                      => 0,
                "End"                       => tiles.Count - 1,
                _                           => place
            };
            if (to != place)
            {
                focusPending = tile.Item.Key;
                await MoveAsync(tile, to);
            }
        }
        else if (Resizable && e.ShiftKey && !e.CtrlKey)
        {
            var (cols, rows) = e.Key switch
            {
                "ArrowRight" => (SpanOf(tile) + 1, tile.RowSpan),
                "ArrowLeft"  => (SpanOf(tile) - 1, tile.RowSpan),
                "ArrowDown" when RowHeight is not null => (SpanOf(tile), tile.RowSpan + 1),
                "ArrowUp" when RowHeight is not null   => (SpanOf(tile), tile.RowSpan - 1),
                _            => (SpanOf(tile), tile.RowSpan)
            };
            await ResizeAsync(tile, cols, rows);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", grid);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        selfReference?.Dispose();
    }
}
