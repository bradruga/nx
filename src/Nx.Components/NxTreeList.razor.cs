using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// A tree with columns: <see cref="NxTreeView{TItem}"/> (expand, select, the ARIA tree's keys) whose rows are cells under
/// a header. Built from a flat list as ERP's data comes (<see cref="ItemId"/> and <see cref="ParentId"/>); columns show a
/// property by name or a template. No sorting, filtering, paging or editing: what ERP's yard page uses.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
/// <example>
/// <code>
/// &lt;NxTreeList Data="locations" ItemId="l =&gt; l.ResourceId" ParentId="l =&gt; l.CurrentContainerId"
///             @bind-SelectedItems="selected" ExpandedItems="startExpanded" Height="100%"&gt;
///     &lt;Columns&gt;
///         &lt;NxTreeListColumn Field="@nameof(Location.Name)" Title="Location" Width="200px" Expandable="true" /&gt;
///         &lt;NxTreeListColumn Field="@nameof(Location.AggregatePieceCount)" Title="# Pieces" Width="100px" /&gt;
///     &lt;/Columns&gt;
/// &lt;/NxTreeList&gt;
/// </code>
/// </example>
[CascadingTypeParameter(nameof(TItem))]
public partial class NxTreeList<TItem>
{
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>> Getters = new();

    private readonly List<Column> columns = [];
    private NxTreeView<TItem>? tree;

    /// <summary>A registered column, as the list draws it.</summary>
    internal sealed record Column(string? Field, string? Title, string? Width, bool Expandable, RenderFragment<TItem>? Template)
    {
        /// <summary>What the header and layout show; templates are new objects on every render and are left out.</summary>
        public (string?, string?, string?, bool) Shown => (Field, Title, Width, Expandable);
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>Every item of the tree, flat.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>An item's unique id.</summary>
    [Parameter, EditorRequired] public Func<TItem, object>? ItemId { get; set; }

    /// <summary>An item's parent id; <c>null</c> (or an id not in <see cref="Data"/>) for a top item.</summary>
    [Parameter, EditorRequired] public Func<TItem, object?>? ParentId { get; set; }

    /// <summary>The <see cref="NxTreeListColumn{TItem}"/>s.</summary>
    [Parameter] public RenderFragment? Columns { get; set; }

    /// <summary>Shown when there are no items. Default: "No items".</summary>
    [Parameter] public string NoDataText { get; set; } = "No items";

    // ── Selection and expansion (as NxTreeView) ───────────────────────────────

    /// <summary>None, one or several rows selectable. Default: <see cref="NxTreeSelectionMode.Single"/>.</summary>
    [Parameter] public NxTreeSelectionMode SelectionMode { get; set; } = NxTreeSelectionMode.Single;

    /// <summary>The selected items. Use <c>@bind-SelectedItems</c>.</summary>
    [Parameter] public IEnumerable<TItem>? SelectedItems { get; set; }

    /// <summary>Raised when the selection changes, with the selected items in tree order.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    /// <summary>The selected item, for single selection. Use <c>@bind-SelectedItem</c>.</summary>
    [Parameter] public TItem? SelectedItem { get; set; }

    /// <summary>Raised when the single selection changes.</summary>
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }

    /// <summary>The expanded items: pass a list to start with some open. Use <c>@bind-ExpandedItems</c> to follow them.</summary>
    [Parameter] public IEnumerable<TItem>? ExpandedItems { get; set; }

    /// <summary>Raised when a row is expanded or collapsed, with every expanded item.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> ExpandedItemsChanged { get; set; }

    // ── Look ──────────────────────────────────────────────────────────────────

    /// <summary>The list's height; the rows scroll under the header. <c>100%</c> fills a container with a height.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>The tree's accessible name.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The tree's <c>id</c>; rows are <c>{Id}-{n}</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Expands every row that has children.</summary>
    public Task ExpandAllAsync() => tree?.ExpandAllAsync() ?? Task.CompletedTask;

    /// <summary>Collapses every row.</summary>
    public Task CollapseAllAsync() => tree?.CollapseAllAsync() ?? Task.CompletedTask;

    /// <summary>Moves keyboard focus to the tree.</summary>
    public ValueTask FocusAsync() => tree?.FocusAsync() ?? ValueTask.CompletedTask;

    // ── Columns ───────────────────────────────────────────────────────────────

    internal void SetColumn(Column? old, Column column)
    {
        // By reference: two columns can hold equal values.
        var at = old is null ? -1 : columns.FindIndex(c => ReferenceEquals(c, old));
        var redraw = at < 0 || columns[at].Shown != column.Shown;
        if (at < 0)
            columns.Add(column);
        else
            columns[at] = column;
        if (redraw)
            StateHasChanged();
    }

    internal void RemoveColumn(Column column)
    {
        var at = columns.FindIndex(c => ReferenceEquals(c, column));
        if (at < 0)
            return;
        columns.RemoveAt(at);
        StateHasChanged();
    }

    // The tree column comes first: the Expandable one, else the first declared.
    private Column? TreeColumn => columns.FirstOrDefault(c => c.Expandable) ?? columns.FirstOrDefault();
    private IEnumerable<Column> Ordered => TreeColumn is { } first ? [first, .. columns.Where(c => !ReferenceEquals(c, first))] : [];

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-treelist" : $"nx-treelist {Class}";

    private string? OuterStyle
    {
        get
        {
            var height = string.IsNullOrEmpty(Height) ? null : $"height: {Height}";
            return string.IsNullOrEmpty(Style) ? height : height is null ? Style : $"{height}; {Style}";
        }
    }

    // The table is never narrower than its columns; a narrow container scrolls sideways.
    private string? TableStyle
    {
        get
        {
            var widths = columns.Select(c => c.Width).Where(w => !string.IsNullOrEmpty(w)).ToList();
            return widths.Count == 0 ? null : $"min-width: calc({string.Join(" + ", widths)} + var(--nx-treelist-chrome))";
        }
    }

    private string? CellStyle(Column column)
    {
        if (string.IsNullOrEmpty(column.Width))
            return null;
        // The tree column's least width counts its indent, so a deeper row never pushes the other columns right.
        return ReferenceEquals(column, TreeColumn)
            ? $"min-width: max(0px, calc({column.Width} - var(--nx-treeview-level, 0) * var(--nx-treeview-indent)))"
            : $"width: {column.Width}";
    }

    private string TextOf(TItem item) => TreeColumn is { } column ? Convert.ToString(ValueOf(item, column), CultureInfo.CurrentCulture) ?? "" : item?.ToString() ?? "";

    private static object? ValueOf(TItem item, Column column)
    {
        if (item is null || string.IsNullOrEmpty(column.Field))
            return null;
        var getter = Getters.GetOrAdd((item.GetType(), column.Field), key =>
        {
            var info = key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance)
                       ?? throw new InvalidOperationException($"NxTreeList: {key.Item1.Name} has no public property '{key.Item2}'.");
            return o => info.GetValue(o);
        });
        return getter(item);
    }
}
