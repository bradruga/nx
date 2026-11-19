using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A tree of items, from a flat list (<see cref="ItemId"/> and <see cref="ParentId"/>, as ERP's data comes) or from
/// <see cref="Children"/>. Expand and collapse by click or keys, select one or several, template each item, and drag
/// items onto, before or after others (<see cref="OnDrop"/>; you move them in your data). Keyboard as the ARIA tree:
/// arrows, Home, End, Enter and Space.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
/// <example>
/// <code>
/// &lt;NxTreeView Data="nodes" ItemId="n =&gt; n.Id" ParentId="n =&gt; n.ParentId" ItemText="n =&gt; n.Text"
///             @bind-SelectedItem="selected" @bind-ExpandedItems="expanded" /&gt;
/// </code>
/// </example>
public partial class NxTreeView<TItem> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-input.js cancels the page scroll of the keys the tree uses and scrolls the keyboard's item into view.

    private const string ModulePath = "./_content/Nx.Components/nx-input.js";
    private static readonly string[] TreeKeys = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End", " "];

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-treeview-{Guid.NewGuid():N}"[..20];
    private IJSObjectReference? module;
    private ElementReference tree;
    private bool keysApplied, revealPending;

    private List<Node> roots = [];
    private Dictionary<object, Node> byId = [];
    private readonly HashSet<object> expandedIds = [];
    private readonly HashSet<object> selectedIds = [];
    private Node? active;
    private Node? dragged, dropTarget;
    private IEnumerable<TItem>? lastExpanded, lastSelectedItems;
    private TItem? lastSelectedItem;
    private bool initialized;

    /// <summary>One item in the tree.</summary>
    private sealed class Node(TItem item, object id)
    {
        public TItem Item { get; } = item;
        public object Id { get; } = id;
        public Node? Parent { get; set; }
        public List<Node> Children { get; } = [];
        public int Level { get; set; }
        public int Index { get; set; }
        public int PosInSet { get; set; }
        public int SetSize { get; set; }
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>The items: every item of a flat tree (with <see cref="ParentId"/>), or the top items (with <see cref="Children"/>).</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>An item's unique id. Required for a flat tree; default for <see cref="Children"/>: the item itself.</summary>
    [Parameter] public Func<TItem, object>? ItemId { get; set; }

    /// <summary>An item's parent id in a flat tree; <c>null</c> (or an id not in <see cref="Data"/>) for a top item.</summary>
    [Parameter] public Func<TItem, object?>? ParentId { get; set; }

    /// <summary>An item's children, for a nested tree instead of <see cref="ParentId"/>.</summary>
    [Parameter] public Func<TItem, IEnumerable<TItem>?>? Children { get; set; }

    /// <summary>The text shown for an item. Default: <c>ToString()</c>.</summary>
    [Parameter] public Func<TItem, string>? ItemText { get; set; }

    /// <summary>Renders an item. Default: its text.</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Shown when there are no items. Default: "No items".</summary>
    [Parameter] public string NoDataText { get; set; } = "No items";

    // ── Selection and expansion ───────────────────────────────────────────────

    /// <summary>None, one or several items selectable. Default: <see cref="NxTreeSelectionMode.Single"/>.</summary>
    [Parameter] public NxTreeSelectionMode SelectionMode { get; set; } = NxTreeSelectionMode.Single;

    /// <summary>The selected items. Use <c>@bind-SelectedItems</c>.</summary>
    [Parameter] public IEnumerable<TItem>? SelectedItems { get; set; }

    /// <summary>Raised when the selection changes, with the selected items in tree order.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    /// <summary>The selected item, for single selection. Use <c>@bind-SelectedItem</c>.</summary>
    [Parameter] public TItem? SelectedItem { get; set; }

    /// <summary>Raised when the single selection changes.</summary>
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }

    /// <summary>The expanded items. Use <c>@bind-ExpandedItems</c>.</summary>
    [Parameter] public IEnumerable<TItem>? ExpandedItems { get; set; }

    /// <summary>Raised when an item is expanded or collapsed, with every expanded item.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> ExpandedItemsChanged { get; set; }

    // ── Drag and drop ─────────────────────────────────────────────────────────

    /// <summary>Lets items be dragged onto, before or after others with the mouse.</summary>
    [Parameter] public bool Draggable { get; set; }

    /// <summary>Raised on a drop; move the item in your data. Not raised for a drop on itself or inside itself.</summary>
    [Parameter] public EventCallback<NxTreeDropEventArgs<TItem>> OnDrop { get; set; }

    // ── Attributes ────────────────────────────────────────────────────────────

    /// <summary>The tree's accessible name.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The tree's <c>id</c>; items are <c>{Id}-{n}</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element, e.g. a height that scrolls.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Expands every item that has children.</summary>
    public Task ExpandAllAsync() => SetExpandedAsync(byId.Values.Where(n => n.Children.Count > 0).Select(n => n.Id));

    /// <summary>Collapses every item.</summary>
    public Task CollapseAllAsync() => SetExpandedAsync([]);

    /// <summary>Moves keyboard focus to the tree.</summary>
    public ValueTask FocusAsync() => tree.FocusAsync();

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string TreeId => Id ?? generatedId;
    private string NodeId(Node node) => $"{TreeId}-{node.Index}";
    private string TextOf(TItem item) => ItemText?.Invoke(item) ?? item?.ToString() ?? "";
    private object IdOf(TItem item) => ItemId?.Invoke(item) ?? item!;
    private bool IsExpanded(Node node) => expandedIds.Contains(node.Id);

    private string CssClass => string.Join(' ', new[]
    {
        "nx-treeview",
        dragged is not null ? "nx-treeview-dragging" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private string RowClass(Node node, bool selected) => string.Join(' ', new[]
    {
        "nx-treeview-row",
        selected ? "nx-treeview-row-selected" : null,
        node == active ? "nx-treeview-row-active" : null,
        node == dragged ? "nx-treeview-row-dragged" : null,
        node == dropTarget && dragged is not null ? "nx-treeview-row-drop" : null
    }.Where(c => c is not null));

    /// <summary>The items shown now, top to bottom.</summary>
    private List<Node> Visible()
    {
        var list = new List<Node>();
        void Walk(IEnumerable<Node> nodes)
        {
            foreach (var node in nodes)
            {
                list.Add(node);
                if (IsExpanded(node))
                    Walk(node.Children);
            }
        }
        Walk(roots);
        return list;
    }

    private IEnumerable<TItem> InTreeOrder(HashSet<object> ids) =>
        byId.Values.OrderBy(n => n.Index).Where(n => ids.Contains(n.Id)).Select(n => n.Item).ToList();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ParentId is not null && ItemId is null)
            throw new InvalidOperationException("NxTreeView needs ItemId with ParentId.");
        Build();

        // Only an outside change replaces what the user expanded or selected.
        if (!initialized || !ReferenceEquals(ExpandedItems, lastExpanded))
        {
            lastExpanded = ExpandedItems;
            expandedIds.Clear();
            foreach (var item in ExpandedItems ?? [])
                expandedIds.Add(IdOf(item));
        }
        if (!initialized || !ReferenceEquals(SelectedItems, lastSelectedItems) || !EqualityComparer<TItem>.Default.Equals(SelectedItem, lastSelectedItem))
        {
            var fromItems = !initialized || !ReferenceEquals(SelectedItems, lastSelectedItems);
            lastSelectedItems = SelectedItems;
            lastSelectedItem = SelectedItem;
            selectedIds.Clear();
            if (SelectedItem is not null && (!fromItems || SelectedItems is null))
                selectedIds.Add(IdOf(SelectedItem));
            else
                foreach (var item in SelectedItems ?? [])
                    selectedIds.Add(IdOf(item));
        }
        if (active is not null)
            active = byId.GetValueOrDefault(active.Id);
        initialized = true;
    }

    /// <summary>Builds the nodes from the data on every render: ERP edits its items in place.</summary>
    private void Build()
    {
        var map = new Dictionary<object, Node>();
        var top = new List<Node>();
        var index = 0;
        if (Children is not null && ParentId is null)
        {
            void Add(IEnumerable<TItem> items, Node? parent, List<Node> into)
            {
                foreach (var item in items)
                {
                    var node = new Node(item, IdOf(item)) { Parent = parent, Level = parent is null ? 0 : parent.Level + 1, Index = index++ };
                    if (!map.TryAdd(node.Id, node))
                        continue;
                    into.Add(node);
                    Add(Children(item) ?? [], node, node.Children);
                }
            }
            Add(Data ?? [], null, top);
        }
        else
        {
            foreach (var item in Data ?? [])
            {
                var node = new Node(item, IdOf(item)) { Index = index++ };
                map.TryAdd(node.Id, node);
            }
            foreach (var node in map.Values.OrderBy(n => n.Index))
            {
                var parentId = ParentId?.Invoke(node.Item);
                if (parentId is not null && map.TryGetValue(parentId, out var parent) && parent != node && !IsAncestor(node, parent))
                {
                    node.Parent = parent;
                    parent.Children.Add(node);
                }
                else
                    top.Add(node);
            }
            void Levels(IEnumerable<Node> nodes, int level)
            {
                foreach (var node in nodes)
                {
                    node.Level = level;
                    Levels(node.Children, level + 1);
                }
            }
            Levels(top, 0);
        }
        void Positions(List<Node> siblings)
        {
            for (var i = 0; i < siblings.Count; i++)
            {
                siblings[i].PosInSet = i + 1;
                siblings[i].SetSize = siblings.Count;
                Positions(siblings[i].Children);
            }
        }
        Positions(top);
        roots = top;
        byId = map;
    }

    /// <summary>Whether <paramref name="candidate"/> is <paramref name="node"/> or above it.</summary>
    private static bool IsAncestor(Node candidate, Node node)
    {
        for (var n = node.Parent; n is not null; n = n.Parent)
        {
            if (n == candidate)
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null || roots.Count == 0)
            return;
        if (!keysApplied)
        {
            keysApplied = true;
            await module.InvokeVoidAsync("preventKeys", tree, TreeKeys);
        }
        if (revealPending)
        {
            revealPending = false;
            await module.InvokeVoidAsync("reveal", tree, ".nx-treeview-row-active");
        }
    }

    // ── Expanding and selecting ───────────────────────────────────────────────

    private async Task ToggleAsync(Node node)
    {
        if (node.Children.Count == 0)
            return;
        var ids = expandedIds.ToHashSet();
        if (!ids.Remove(node.Id))
            ids.Add(node.Id);
        await SetExpandedAsync(ids);
    }

    private async Task SetExpandedAsync(IEnumerable<object> ids)
    {
        expandedIds.Clear();
        expandedIds.UnionWith(ids);
        var items = InTreeOrder(expandedIds);
        if (ExpandedItemsChanged.HasDelegate)
            lastExpanded = items;
        await ExpandedItemsChanged.InvokeAsync(items);
        StateHasChanged();
    }

    private async Task SelectAsync(Node node)
    {
        switch (SelectionMode)
        {
            case NxTreeSelectionMode.None:
                return;
            case NxTreeSelectionMode.Single:
                if (selectedIds.Count == 1 && selectedIds.Contains(node.Id))
                    return;
                selectedIds.Clear();
                selectedIds.Add(node.Id);
                break;
            default:
                if (!selectedIds.Remove(node.Id))
                    selectedIds.Add(node.Id);
                break;
        }
        var items = InTreeOrder(selectedIds);
        if (SelectedItemsChanged.HasDelegate)
            lastSelectedItems = items;
        await SelectedItemsChanged.InvokeAsync(items);
        if (SelectionMode == NxTreeSelectionMode.Single)
        {
            if (SelectedItemChanged.HasDelegate)
                lastSelectedItem = node.Item;
            await SelectedItemChanged.InvokeAsync(node.Item);
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private async Task HandleRowClick(Node node)
    {
        active = node;
        await SelectAsync(node);
    }

    private void HandleFocus()
    {
        if (active is not null)
            return;
        active = Visible().FirstOrDefault(n => selectedIds.Contains(n.Id)) ?? roots.FirstOrDefault();
        revealPending = true;
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        var visible = Visible();
        if (visible.Count == 0)
            return;
        active ??= visible[0];
        var at = Math.Max(0, visible.IndexOf(active));
        switch (e.Key)
        {
            case "ArrowDown":
                active = visible[Math.Min(at + 1, visible.Count - 1)];
                break;
            case "ArrowUp":
                active = visible[Math.Max(at - 1, 0)];
                break;
            case "Home":
                active = visible[0];
                break;
            case "End":
                active = visible[^1];
                break;
            case "ArrowRight":
                if (active.Children.Count > 0 && !IsExpanded(active))
                    await ToggleAsync(active);
                else if (active.Children.Count > 0)
                    active = active.Children[0];
                break;
            case "ArrowLeft":
                if (active.Children.Count > 0 && IsExpanded(active))
                    await ToggleAsync(active);
                else if (active.Parent is not null)
                    active = active.Parent;
                break;
            case "Enter" or " ":
                if (SelectionMode == NxTreeSelectionMode.None)
                    await ToggleAsync(active);
                else
                    await SelectAsync(active);
                break;
            default:
                return;
        }
        revealPending = true;
    }

    private void HandleDragStart(Node node)
    {
        if (Draggable)
            dragged = node;
    }

    private void HandleDragEnd()
    {
        dragged = null;
        dropTarget = null;
    }

    private void HandleDragEnter(Node node)
    {
        if (dragged is not null)
            dropTarget = node;
    }

    /// <summary>The top quarter of a row drops before it, the bottom quarter after it, the middle onto it.</summary>
    private async Task HandleDropAsync(Node node, DragEventArgs e)
    {
        var item = dragged;
        dragged = null;
        dropTarget = null;
        if (item is null || item == node || IsAncestor(item, node))
            return;
        const double rowHeight = 32;
        var position = e.OffsetY < rowHeight * 0.25 ? NxTreeDropPosition.Before
                     : e.OffsetY > rowHeight * 0.75 ? NxTreeDropPosition.After
                     : NxTreeDropPosition.Inside;
        await OnDrop.InvokeAsync(new NxTreeDropEventArgs<TItem>(item.Item, node.Item, position));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                if (keysApplied)
                    await module.InvokeVoidAsync("dispose", tree);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
    }
}
