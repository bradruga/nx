using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A project schedule: a task list (tree columns, sorting, a filter row, cells edited in place) beside a week timeline
/// of bars, with dependency arrows between them and a divider to share the width. Bound to a flat list by field
/// name (<c>IdField</c>, <c>ParentIdField</c>, <c>TitleField</c>, <c>StartField</c>, <c>EndField</c>).
/// Bars are dragged to move a task and by an edge to change its start or end; every edit arrives in <see cref="OnUpdate"/>.
/// Editing a whole task is the page's own form, opened from <see cref="OnEdit"/>; the Gantt has none of its own.
/// </summary>
/// <typeparam name="TItem">The task type.</typeparam>
/// <example>
/// <code>
/// &lt;NxGantt Data="tasks" IdField="@nameof(Node.Id)" ParentIdField="@nameof(Node.ParentId)" Height="600px"
///          Dependencies="links" OnEdit="OpenTask" OnUpdate="SaveTask"&gt;
///     &lt;Columns&gt;
///         &lt;NxGanttColumn Field="@nameof(Node.Title)" Title="Node" Width="300px" Expandable="true" /&gt;
///         &lt;NxGanttColumn Field="@nameof(Node.Start)" Width="100px" DisplayFormat="{0:MM/dd/yy}" /&gt;
///     &lt;/Columns&gt;
/// &lt;/NxGantt&gt;
/// </code>
/// </example>
[CascadingTypeParameter(nameof(TItem))]
public partial class NxGantt<TItem> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-gantt.js keeps the two halves scrolling together, drags the divider, ends a cell's edit on a press outside it,
    // draws the line while a dependency is dragged from one bar to another, and shows where a dragged bar would land.

    private const string ModulePath = "./_content/Nx.Components/nx-gantt.js";
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Properties = new();

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-gantt-{Guid.NewGuid():N}"[..17];
    private readonly List<Column> columns = [];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxGantt<TItem>>? dotNet;
    private ElementReference root;

    private Dictionary<object, Node> byId = [];
    private List<Node> tops = [];
    private readonly HashSet<object> collapsedIds = [];
    private IEnumerable<TItem>? lastExpanded;
    private IEnumerable<TItem>? lastSelected;
    private bool initialized;
    private Node? selected;
    private Node? hovered;
    private (Node Node, Column Column)? editing;
    private object? editingOldValue;
    private Column? sortColumn;
    private bool sortDescending;
    // By the column's key: its record is new on every render, the column itself is not.
    private readonly Dictionary<object, string> filters = [];
    private string? treeWidth;

    /// <summary>A registered column, as the Gantt draws it.</summary>
    internal sealed record Column(object Key, string? Field, string? Title, string? Width, string? DisplayFormat, bool Expandable, bool Visible,
        bool Editable, bool Filterable, RenderFragment<TItem>? Template, RenderFragment<TItem>? EditorTemplate, RenderFragment? HeaderTemplate)
    {
        /// <summary>What the header and layout show; templates are new objects on every render and are left out.</summary>
        public (string?, string?, string?, string?, bool, bool, bool, bool, bool) Shown =>
            (Field, Title, Width, DisplayFormat, Expandable, Visible, Editable, Filterable, EditorTemplate is null);
    }

    /// <summary>One task.</summary>
    private sealed class Node(TItem item, object id, object? parentId, int index)
    {
        public TItem Item { get; } = item;
        public object Id { get; } = id;
        public object? ParentId { get; } = parentId;
        public int Index { get; } = index;
        public Node? Parent { get; set; }
        public List<Node> Children { get; } = [];
        public int Level { get; set; }
        public string Title { get; set; } = "";
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>Every task, flat: each names its parent by id.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>The property holding a task's id. Default: <c>Id</c>.</summary>
    [Parameter] public string IdField { get; set; } = "Id";

    /// <summary>The property holding its parent's id; <c>null</c> at the top. Default: <c>ParentId</c>.</summary>
    [Parameter] public string ParentIdField { get; set; } = "ParentId";

    /// <summary>The property shown on its bar. Default: <c>Title</c>.</summary>
    [Parameter] public string TitleField { get; set; } = "Title";

    /// <summary>The property holding when it starts. Default: <c>Start</c>.</summary>
    [Parameter] public string StartField { get; set; } = "Start";

    /// <summary>The property holding when it ends (the bar stops there). Default: <c>End</c>.</summary>
    [Parameter] public string EndField { get; set; } = "End";

    /// <summary>The <see cref="NxGanttColumn{TItem}"/>s of the task list.</summary>
    [Parameter] public RenderFragment? Columns { get; set; }

    // ── Dependencies ──────────────────────────────────────────────────────────

    /// <summary>The links between tasks, drawn as arrows.</summary>
    [Parameter] public System.Collections.IEnumerable? Dependencies { get; set; }

    /// <summary>The property holding a link's predecessor id. Default: <c>PredecessorId</c>.</summary>
    [Parameter] public string PredecessorIdField { get; set; } = "PredecessorId";

    /// <summary>The property holding a link's successor id. Default: <c>SuccessorId</c>.</summary>
    [Parameter] public string SuccessorIdField { get; set; } = "SuccessorId";

    /// <summary>The property holding a link's type (an <see cref="NxGanttDependencyType"/>, or an enum or number in its order). Default: <c>Type</c>.</summary>
    [Parameter] public string TypeField { get; set; } = "Type";

    /// <summary>Raised when the user drags from one bar's start or end to another bar; with a handler the handles show.</summary>
    [Parameter] public EventCallback<NxGanttDependencyCreateEventArgs<TItem>> OnDependencyCreate { get; set; }

    // ── Timeline ──────────────────────────────────────────────────────────────

    /// <summary>Pixels per day; the zoom. Default: <c>30</c>.</summary>
    [Parameter] public double SlotWidth { get; set; } = 30;

    /// <summary>A day's header text: a .NET format, as <c>{0:ddd}</c> or <c>ddd</c>. Default: <c>{0:%d}</c> (the day number).</summary>
    [Parameter] public string DayHeaderDateFormat { get; set; } = "{0:%d}";

    /// <summary>A week's header, in place of its first day's date; the context is that day.</summary>
    [Parameter] public RenderFragment<DateTime>? WeekHeaderTemplate { get; set; }

    /// <summary>A day's header, in place of <see cref="DayHeaderDateFormat"/>; the context is the day.</summary>
    [Parameter] public RenderFragment<DateTime>? DayHeaderTemplate { get; set; }

    /// <summary>A bar's content; it fills the bar's box. Default: the title.</summary>
    [Parameter] public RenderFragment<TItem>? TaskTemplate { get; set; }

    /// <summary>What shows under a bar while the pointer is on it. Default: the title and dates.</summary>
    [Parameter] public RenderFragment<TItem>? TooltipTemplate { get; set; }

    // ── Node list ─────────────────────────────────────────────────────────────

    /// <summary>The task list's width (CSS); dragging the divider changes it. Use <c>@bind-TreeListWidth</c>. Default: <c>40%</c>.</summary>
    [Parameter] public string? TreeListWidth { get; set; } = "40%";

    /// <summary>Raised with the new width (<c>"420px"</c>) when the divider is dragged.</summary>
    [Parameter] public EventCallback<string> TreeListWidthChanged { get; set; }

    /// <summary>A header click sorts the tasks by that column, among their siblings. Default: <c>false</c>.</summary>
    [Parameter] public bool Sortable { get; set; }

    /// <summary>A row of filter boxes under the headers: tasks containing the text, with their parents. Default: <c>false</c>.</summary>
    [Parameter] public bool FilterRow { get; set; }

    /// <summary>The expanded tasks; <c>null</c> expands every one. Use <c>@bind-ExpandedItems</c>.</summary>
    [Parameter] public IEnumerable<TItem>? ExpandedItems { get; set; }

    /// <summary>Raised when a task is expanded or collapsed, with every expanded task.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> ExpandedItemsChanged { get; set; }

    /// <summary>The selected task, in a list of one or none, as NxGrid's. Use <c>@bind-SelectedItems</c>.</summary>
    [Parameter] public IEnumerable<TItem>? SelectedItems { get; set; }

    /// <summary>Raised when another task is selected.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    // ── Editing ───────────────────────────────────────────────────────────────

    /// <summary><c>false</c> turns off cell editors, dragging bars, drawing dependencies and Delete. Default: <c>true</c>.</summary>
    [Parameter] public bool Editable { get; set; } = true;

    /// <summary>A task asked to be edited: a double-click on its row or bar, or Enter on its row. Open your form.</summary>
    [Parameter] public EventCallback<TItem> OnEdit { get; set; }

    /// <summary>
    /// Raised after any edit, as NxGrid's: a bar dragged or resized (the item is untouched; apply the changes), or a cell
    /// left (its editor already wrote the item). Bars drag only with a handler.
    /// </summary>
    [Parameter] public EventCallback<NxGanttUpdateArgs<TItem>> OnUpdate { get; set; }

    /// <summary>Delete was pressed on the selected task's row. Remove it from <see cref="Data"/>.</summary>
    [Parameter] public EventCallback<TItem> OnDelete { get; set; }

    /// <summary>The sort or the filters changed; read <see cref="HasSort"/> and <see cref="HasFilter"/>.</summary>
    [Parameter] public EventCallback OnStateChanged { get; set; }

    // ── Look ──────────────────────────────────────────────────────────────────

    /// <summary>Content of the toolbar over the Gantt.</summary>
    [Parameter] public RenderFragment? ToolBarTemplate { get; set; }

    /// <summary>The Gantt's height. Default: <c>600px</c>; <c>100%</c> fills a container with a height.</summary>
    [Parameter] public string? Height { get; set; } = "600px";

    /// <summary>The Gantt's width. Default: the container's.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Formats dates. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>The task list's accessible name. Default: "Tasks".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The outer element's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes go to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Methods and state ─────────────────────────────────────────────────────

    /// <summary>The tasks are sorted by a column.</summary>
    public bool HasSort => sortColumn is not null;

    /// <summary>A filter box has text.</summary>
    public bool HasFilter => filters.Values.Any(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Back to the data's order.</summary>
    public async Task ClearSortAsync()
    {
        sortColumn = null;
        sortDescending = false;
        StateHasChanged();
        await OnStateChanged.InvokeAsync();
    }

    /// <summary>Empties every filter box.</summary>
    public async Task ClearFilterAsync()
    {
        filters.Clear();
        StateHasChanged();
        await OnStateChanged.InvokeAsync();
    }

    /// <summary>Draws again after the data changed in place outside a render (dates moved, tasks added).</summary>
    public void Rebind()
    {
        Build();
        StateHasChanged();
    }

    // ── Columns ───────────────────────────────────────────────────────────────

    internal void SetColumn(Column? old, Column column)
    {
        var at = columns.FindIndex(c => c.Key == column.Key);
        var redraw = at < 0 || columns[at].Shown != column.Shown;
        if (at < 0)
            columns.Add(column);
        else
            columns[at] = column;
        if (redraw)
            StateHasChanged();
    }

    private static bool Same(Column? a, Column? b) => a is not null && b is not null && a.Key == b.Key;

    internal void RemoveColumn(Column column)
    {
        var at = columns.FindIndex(c => c.Key == column.Key);
        if (at < 0)
            return;
        columns.RemoveAt(at);
        filters.Remove(column.Key);
        StateHasChanged();
    }

    private IEnumerable<Column> Shown => columns.Where(c => c.Visible);
    private Column? TreeColumn => Shown.FirstOrDefault(c => c.Expandable);
    private string? FilterText(Column column) => filters.GetValueOrDefault(column.Key);
    private static double WidthOf(Column c) => double.TryParse((c.Width ?? "120").Replace("px", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 120;
    private double ColumnsWidth => Shown.Sum(WidthOf);

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string BaseId => Id ?? generatedId;
    private string RowId(Node t) => $"{BaseId}-r{t.Index}";
    private CultureInfo Formats => Culture ?? CultureInfo.CurrentCulture;
    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-gantt" : $"nx-gantt {Class}";
    private bool IsExpanded(Node t) => !collapsedIds.Contains(t.Id);
    private bool CanDrag(Node t) => Editable && OnUpdate.HasDelegate && t.Children.Count == 0 && t.Start is not null;

    private string? OuterStyle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Height)) parts.Add($"height: {Height}");
            if (!string.IsNullOrEmpty(Width)) parts.Add($"width: {Width}");
            parts.Add($"--nx-gantt-tree-width: {treeWidth ?? TreeListWidth ?? "40%"}");
            if (!string.IsNullOrEmpty(Style)) parts.Add(Style);
            return string.Join("; ", parts);
        }
    }

    private static string Px(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "px";

    private string CellText(Node task, Column column)
    {
        var value = column.Field is null ? null : Read(task.Item!, column.Field);
        if (value is null)
            return "";
        if (string.IsNullOrEmpty(column.DisplayFormat))
            return Convert.ToString(value, Formats) ?? "";
        var format = column.DisplayFormat.Contains("{0") ? column.DisplayFormat : $"{{0:{column.DisplayFormat}}}";
        return string.Format(Formats, format, value);
    }

    private string DayText(DateTime day)
    {
        var format = DayHeaderDateFormat.Contains("{0") ? DayHeaderDateFormat : $"{{0:{DayHeaderDateFormat}}}";
        return string.Format(Formats, format, day);
    }

    // ── Building the tree ─────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Build();
        // Only an outside change replaces what the user expanded, as NxTreeView.
        if (!initialized || !ReferenceEquals(ExpandedItems, lastExpanded))
        {
            lastExpanded = ExpandedItems;
            collapsedIds.Clear();
            if (ExpandedItems is not null)
            {
                var open = ExpandedItems.Select(IdOf).ToHashSet();
                foreach (var id in byId.Keys.Where(id => !open.Contains(id)))
                    collapsedIds.Add(id);
            }
        }
        // Likewise the selection.
        if (!ReferenceEquals(SelectedItems, lastSelected))
        {
            lastSelected = SelectedItems;
            selected = null;
            foreach (var item in SelectedItems ?? [])
            {
                selected = byId.GetValueOrDefault(IdOf(item));
                break;
            }
        }
        if (!initialized || !string.Equals(TreeListWidth, treeWidth, StringComparison.Ordinal))
            treeWidth = TreeListWidth;
        initialized = true;
    }

    /// <summary>Builds the tasks from the flat list on every parameter set: ERP edits them in place.</summary>
    private void Build()
    {
        var map = new Dictionary<object, Node>();
        foreach (var item in Data ?? [])
        {
            if (item is null)
                continue;
            var task = new Node(item, IdOf(item), Read(item, ParentIdField), map.Count)
            {
                Title = Convert.ToString(Read(item, TitleField), Formats) ?? "",
                Start = ToDate(Read(item, StartField, required: true)),
                End = ToDate(Read(item, EndField, required: true))
            };
            map.TryAdd(task.Id, task);
        }
        var top = new List<Node>();
        foreach (var task in map.Values)
        {
            if (task.ParentId is not null && map.TryGetValue(task.ParentId, out var parent) && parent != task)
            {
                task.Parent = parent;
                parent.Children.Add(task);
            }
            else
            {
                top.Add(task);
            }
        }
        void Levels(IEnumerable<Node> tasks, int level)
        {
            foreach (var task in tasks)
            {
                task.Level = level;
                Levels(task.Children, level + 1);
            }
        }
        Levels(top, 0);
        byId = map;
        tops = top;
        // The page re-renders while a cell is edited (its editor writes the item): keep pointing at the same tasks.
        selected = selected is null ? null : map.GetValueOrDefault(selected.Id);
        hovered = hovered is null ? null : map.GetValueOrDefault(hovered.Id);
        if (editing is { } e)
            editing = map.TryGetValue(e.Node.Id, out var node) ? (node, e.Column) : null;
    }

    private object IdOf(TItem item) => Read(item!, IdField, required: true)!;

    private static DateTime? ToDate(object? value) => value switch
    {
        DateTime d => d,
        DateTimeOffset o => o.LocalDateTime,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        _ => null
    };

    private static object? Read(object item, string field, bool required = false)
    {
        var info = Properties.GetOrAdd((item.GetType(), field), key => key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance));
        if (info is null && required)
            throw new InvalidOperationException($"NxGantt: {item.GetType().Name} has no public property '{field}'.");
        return info?.GetValue(item);
    }

    /// <summary>A change of <paramref name="field"/> to <paramref name="date"/>, in the property's own type.</summary>
    private static NxGanttChange<TItem> DateChange(TItem item, string field, DateTime date)
    {
        var info = Properties.GetOrAdd((item!.GetType(), field), key => key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance))!;
        var type = Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
        object value = date;
        if (type == typeof(DateOnly))
            value = DateOnly.FromDateTime(date);
        else if (type == typeof(DateTimeOffset))
            value = new DateTimeOffset(date);
        return new NxGanttChange<TItem> { Field = field, OldValue = info.GetValue(item), NewValue = value, ApplyAction = target => info.SetValue(target, value) };
    }

    // ── The rows shown: sorted among siblings, filtered with their parents, collapsed hidden ──

    private List<Node> Rows()
    {
        var active = filters.Where(f => !string.IsNullOrWhiteSpace(f.Value)).Select(f => (Column: columns.FirstOrDefault(c => c.Key == f.Key), f.Value)).Where(f => f.Column is not null).ToList();
        bool Matches(Node t) => active.All(f => CellText(t, f.Column!).Contains(f.Value.Trim(), StringComparison.OrdinalIgnoreCase));
        // A task stays when it or something under it matches.
        var keep = new HashSet<Node>();
        bool Mark(Node t)
        {
            var any = active.Count == 0 || Matches(t);
            foreach (var child in t.Children)
                any |= Mark(child);
            if (any) keep.Add(t);
            return any;
        }
        foreach (var top in tops)
            Mark(top);

        var rows = new List<Node>();
        void Walk(IEnumerable<Node> tasks)
        {
            foreach (var task in Sort(tasks))
            {
                if (!keep.Contains(task))
                    continue;
                rows.Add(task);
                // While filtering every match shows, collapsed or not.
                if (IsExpanded(task) || active.Count > 0)
                    Walk(task.Children);
            }
        }
        Walk(tops);
        return rows;
    }

    private IEnumerable<Node> Sort(IEnumerable<Node> tasks)
    {
        if (columns.FirstOrDefault(c => Same(c, sortColumn)) is not { Field: not null } column)
            return tasks;
        object? Key(Node t) => Read(t.Item!, column.Field!) is { } v and not string ? v : CellText(t, column);
        var comparer = Comparer<object?>.Create((a, b) => a is null ? (b is null ? 0 : -1) : b is null ? 1
            : a is string sa && b is string sb ? string.Compare(sa, sb, Formats, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering)
            : a is IComparable ca && a.GetType() == b.GetType() ? ca.CompareTo(b) : string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase));
        return sortDescending ? tasks.OrderByDescending(Key, comparer) : tasks.OrderBy(Key, comparer);
    }

    // ── Timeline geometry: days from the Sunday (culture's first day) before the first task ──

    private DateTime RangeStart
    {
        get
        {
            var first = byId.Values.Select(t => t.Start).Where(d => d is not null).DefaultIfEmpty(DateTime.Today).Min()!.Value.Date.AddDays(-7);
            var weekStart = Formats.DateTimeFormat.FirstDayOfWeek;
            return first.AddDays(-(((int)first.DayOfWeek - (int)weekStart + 7) % 7));
        }
    }

    private int DayCount
    {
        get
        {
            var last = byId.Values.Select(t => t.End ?? t.Start).Where(d => d is not null).DefaultIfEmpty(DateTime.Today).Max()!.Value.Date.AddDays(14);
            var days = (int)Math.Ceiling((last - RangeStart).TotalDays);
            return Math.Max(28, (days + 6) / 7 * 7);
        }
    }

    private double X(DateTime date) => (date - RangeStart).TotalDays * SlotWidth;

    private (double Left, double Width)? Bar(Node t)
    {
        if (t.Start is not { } start)
            return null;
        var end = t.End is { } e && e > start ? e : start;
        var left = X(start);
        return (left, Math.Max(X(end) - left, Math.Min(SlotWidth, 8)));
    }

    private sealed record Link(string Path);

    // Arrows run from the predecessor's start or end to the successor's: out, down and in, with room for the head.
    private List<Link> Links(List<Node> rows, double rowHeight)
    {
        var links = new List<Link>();
        if (Dependencies is null)
            return links;
        var rowOf = rows.Select((t, i) => (t, i)).ToDictionary(p => p.t, p => p.i);
        foreach (var dependency in Dependencies)
        {
            if (dependency is null || Read(dependency, PredecessorIdField) is not { } from || Read(dependency, SuccessorIdField) is not { } to)
                continue;
            if (!byId.TryGetValue(from, out var a) || !byId.TryGetValue(to, out var b) || !rowOf.TryGetValue(a, out var ra) || !rowOf.TryGetValue(b, out var rb))
                continue;
            if (Bar(a) is not { } barA || Bar(b) is not { } barB)
                continue;
            var type = Read(dependency, TypeField) switch
            {
                NxGanttDependencyType t => t,
                Enum e => (NxGanttDependencyType)Convert.ToInt32(e, CultureInfo.InvariantCulture),
                int n => (NxGanttDependencyType)n,
                _ => NxGanttDependencyType.FinishStart
            };
            var fromEnd = type is NxGanttDependencyType.FinishStart or NxGanttDependencyType.FinishFinish;
            var toStart = type is NxGanttDependencyType.FinishStart or NxGanttDependencyType.StartStart;
            var x1 = fromEnd ? barA.Left + barA.Width : barA.Left;
            var x2 = toStart ? barB.Left : barB.Left + barB.Width;
            var y1 = (ra + 0.5) * rowHeight;
            var y2 = (rb + 0.5) * rowHeight;
            const double gap = 8;
            var out1 = fromEnd ? x1 + gap : x1 - gap;
            var in2 = toStart ? x2 - gap : x2 + gap;
            var midY = y2 > y1 ? (rb + 0) * rowHeight : (rb + 1) * rowHeight;
            var path = (fromEnd && toStart && in2 >= out1) || (!fromEnd && !toStart && in2 <= out1)
                ? $"M{P(x1)},{P(y1)} H{P((out1 + in2) / 2)} V{P(y2)} H{P(x2)}"
                : $"M{P(x1)},{P(y1)} H{P(out1)} V{P(midY)} H{P(in2)} V{P(y2)} H{P(x2)}";
            links.Add(new Link(path));
        }
        return links;
    }

    private static string P(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        dotNet = DotNetObjectReference.Create(this);
        if (module is not null)
            await module.InvokeVoidAsync("init", root, dotNet, X(DateTime.Today));
    }

    // ── From the browser ──────────────────────────────────────────────────────

    /// <summary>Called by nx-gantt.js when the divider is let go, with the task list's width in pixels.</summary>
    [JSInvokable]
    public Task OnTreeWidth(double width) => InvokeAsync(async () =>
    {
        treeWidth = Px(Math.Round(width));
        StateHasChanged();
        await TreeListWidthChanged.InvokeAsync(treeWidth);
    });

    /// <summary>Called by nx-gantt.js on a press outside the cell being edited.</summary>
    [JSInvokable]
    public Task OnEditOutside() => InvokeAsync(EndEditAsync);

    /// <summary>Called by nx-gantt.js when a bar is let go: <paramref name="mode"/> is <c>move</c>, <c>start</c> or <c>end</c>, <paramref name="days"/> how far.</summary>
    [JSInvokable]
    public Task OnBarDrop(int index, string mode, int days) => InvokeAsync(async () =>
    {
        if (byId.Values.FirstOrDefault(t => t.Index == index) is { } task)
            await RescheduleAsync(task, mode switch { "start" => NxGanttUpdateKind.ResizeStart, "end" => NxGanttUpdateKind.ResizeEnd, _ => NxGanttUpdateKind.Move }, days);
    });

    /// <summary>Called by nx-gantt.js when a dependency is dropped from one bar's handle on another's.</summary>
    [JSInvokable]
    public Task OnLinkDrop(int fromIndex, bool fromEnd, int toIndex, bool toEnd) => InvokeAsync(async () =>
    {
        var a = byId.Values.FirstOrDefault(t => t.Index == fromIndex);
        var b = byId.Values.FirstOrDefault(t => t.Index == toIndex);
        if (a is null || b is null || a == b)
            return;
        var type = (fromEnd, toEnd) switch
        {
            (true, false) => NxGanttDependencyType.FinishStart,
            (true, true) => NxGanttDependencyType.FinishFinish,
            (false, false) => NxGanttDependencyType.StartStart,
            _ => NxGanttDependencyType.StartFinish
        };
        await OnDependencyCreate.InvokeAsync(new NxGanttDependencyCreateEventArgs<TItem>(a.Item, b.Item, type));
    });

    // ── Actions ───────────────────────────────────────────────────────────────

    private async Task SelectAsync(Node? task)
    {
        if (task == selected)
            return;
        selected = task;
        List<TItem> items = task is null ? [] : [task.Item];
        if (SelectedItemsChanged.HasDelegate)
            lastSelected = items;
        await SelectedItemsChanged.InvokeAsync(items);
    }

    /// <summary>Asks the page to move a task or one of its ends by whole days; a bar keeps at least a day. The item is not written.</summary>
    private async Task RescheduleAsync(Node task, NxGanttUpdateKind kind, int days)
    {
        if (days == 0 || !CanDrag(task) || task.Start is not { } start)
            return;
        var end = task.End is { } e && e > start ? e : start;
        var newStart = kind == NxGanttUpdateKind.ResizeEnd ? start : start.AddDays(days);
        var newEnd = kind == NxGanttUpdateKind.ResizeStart ? end : end.AddDays(days);
        if (kind == NxGanttUpdateKind.ResizeStart && newStart > end.AddDays(-1))
            newStart = end.AddDays(-1);
        if (kind == NxGanttUpdateKind.ResizeEnd && newEnd < start.AddDays(1))
            newEnd = start.AddDays(1);

        var changes = new List<NxGanttChange<TItem>>();
        if (newStart != start)
            changes.Add(DateChange(task.Item, StartField, newStart));
        if (newEnd != end)
            changes.Add(DateChange(task.Item, EndField, newEnd));
        if (changes.Count == 0)
            return;
        await OnUpdate.InvokeAsync(new NxGanttUpdateArgs<TItem> { Item = task.Item, Kind = kind, Changes = changes });
        Build();
        StateHasChanged();
    }

    private async Task ToggleAsync(Node task)
    {
        if (task.Children.Count == 0)
            return;
        if (!collapsedIds.Remove(task.Id))
            collapsedIds.Add(task.Id);
        var open = byId.Values.Where(t => t.Children.Count > 0 && IsExpanded(t)).OrderBy(t => t.Index).Select(t => t.Item).ToList();
        if (ExpandedItemsChanged.HasDelegate)
            lastExpanded = open;
        await ExpandedItemsChanged.InvokeAsync(open);
    }

    private async Task SortByAsync(Column column)
    {
        if (!Sortable || column.Field is null)
            return;
        if (Same(sortColumn, column))
        {
            // Ascending, descending, then back to the data's order.
            if (sortDescending) sortColumn = null;
            sortDescending = !sortDescending;
        }
        else
        {
            sortColumn = column;
            sortDescending = false;
        }
        await OnStateChanged.InvokeAsync();
    }

    private async Task FilterAsync(Column column, string? text)
    {
        filters[column.Key] = text ?? "";
        await OnStateChanged.InvokeAsync();
    }

    private async Task CellClickAsync(Node task, Column column)
    {
        await SelectAsync(task);
        if (editing is { } e && e.Node == task && Same(e.Column, column))
            return;
        await EndEditAsync();
        if (Editable && column.Editable && column.EditorTemplate is not null)
        {
            editing = (task, column);
            editingOldValue = column.Field is null ? null : Read(task.Item!, column.Field);
            if (module is not null)
                await module.InvokeVoidAsync("watchEdit", root, RowId(task));
        }
    }

    private async Task EndEditAsync()
    {
        if (editing is not { } e)
            return;
        editing = null;
        if (module is not null)
            await module.InvokeVoidAsync("unwatchEdit", root);
        Build();
        StateHasChanged();
        // The editor wrote the item; the change says what the cell's own field was and is.
        var changes = new List<NxGanttChange<TItem>>();
        if (e.Column.Field is { } field && Read(e.Node.Item!, field) is var value && !Equals(value, editingOldValue))
        {
            var info = Properties[(e.Node.Item!.GetType(), field)]!;
            changes.Add(new NxGanttChange<TItem> { Field = field, OldValue = editingOldValue, NewValue = value, ApplyAction = target => info.SetValue(target, value) });
        }
        editingOldValue = null;
        await OnUpdate.InvokeAsync(new NxGanttUpdateArgs<TItem> { Item = e.Node.Item, Kind = NxGanttUpdateKind.Edit, Changes = changes });
    }

    private async Task RowKeyDownAsync(KeyboardEventArgs e, Node task, List<Node> rows)
    {
        if (editing is not null)
        {
            if (e.Key is "Enter" or "Tab" or "Escape")
                await EndEditAsync();
            return;
        }
        var at = rows.IndexOf(task);
        switch (e.Key)
        {
            // Alt moves the bar a day; with Shift, its end.
            case "ArrowLeft" or "ArrowRight" when e.AltKey:
                await RescheduleAsync(task, e.ShiftKey ? NxGanttUpdateKind.ResizeEnd : NxGanttUpdateKind.Move, e.Key == "ArrowRight" ? 1 : -1);
                return;
            case "Delete" when Editable && OnDelete.HasDelegate:
                await OnDelete.InvokeAsync(task.Item);
                return;
            case "ArrowDown" when at < rows.Count - 1:
                await SelectAsync(rows[at + 1]);
                break;
            case "ArrowUp" when at > 0:
                await SelectAsync(rows[at - 1]);
                break;
            case "ArrowRight" when task.Children.Count > 0 && !IsExpanded(task):
            case "ArrowLeft" when task.Children.Count > 0 && IsExpanded(task):
                await ToggleAsync(task);
                break;
            case "ArrowLeft" when task.Parent is not null:
                await SelectAsync(task.Parent);
                break;
            case "Enter":
                await OnEdit.InvokeAsync(task.Item);
                return;
            default:
                return;
        }
        if (module is not null && selected is not null)
            await module.InvokeVoidAsync("focusRow", root, RowId(selected));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", root);
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        dotNet?.Dispose();
    }
}
