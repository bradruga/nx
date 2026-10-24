using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// Picks any number of items, shown as chips or as a summary ("3 selected"): a filter box in the field, select-all,
/// a selection limit, and a phone sheet that applies on Done. The same engine as <see cref="NxComboBox{TItem, TValue}"/>
/// with <c>Multiple</c>: data sources, templates, grouping, virtualization, columns and validation work the same.
/// Bind with <c>@bind-Values</c> and/or <c>@bind-SelectedItems</c>.
/// </summary>
/// <typeparam name="TItem">The list item type.</typeparam>
/// <typeparam name="TValue">The bound value type, read from each item by <see cref="ItemValue"/>.</typeparam>
/// <example>
/// <code>
/// &lt;NxMultiSelect Data="plants" ItemText="p =&gt; p.Name" ItemValue="p =&gt; p.Id" @bind-Values="filter.PlantIds" SelectAll="true" /&gt;
/// </code>
/// </example>
[CascadingTypeParameter(nameof(TItem))]
public partial class NxMultiSelect<TItem, TValue>
{
    private NxComboBox<TItem, TValue>? combo;
    private ComboMode mode = new();

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>The items. Filtered in memory as the user types.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>Loads items for the typed text instead of <see cref="Data"/>, as for NxComboBox.</summary>
    [Parameter] public Func<NxComboBoxRequest, Task<IEnumerable<TItem>>>? DataProvider { get; set; }

    /// <summary>With <see cref="DataProvider"/>, milliseconds to wait after typing before loading. Default: <c>300</c>.</summary>
    [Parameter] public int DebounceDelay { get; set; } = 300;

    /// <summary>The text shown for an item and on its chip, and matched by the filter. Default: <c>ToString()</c>.</summary>
    [Parameter] public Func<TItem, string>? ItemText { get; set; }

    /// <summary>The value bound to <see cref="Values"/>. Optional when <typeparamref name="TItem"/> is <typeparamref name="TValue"/>.</summary>
    [Parameter] public Func<TItem, TValue>? ItemValue { get; set; }

    /// <summary>Groups the list under headers by this key.</summary>
    [Parameter] public Func<TItem, string?>? ItemGroup { get; set; }

    // ── Values ────────────────────────────────────────────────────────────────

    /// <summary>The selected items' values. Use <c>@bind-Values</c>.</summary>
    [Parameter] public IEnumerable<TValue>? Values { get; set; }

    /// <summary>Raised when the selection changes.</summary>
    [Parameter] public EventCallback<IEnumerable<TValue>> ValuesChanged { get; set; }

    /// <summary>Identifies the bound collection for <c>EditForm</c> validation. Set by <c>@bind-Values</c>.</summary>
    [Parameter] public Expression<Func<IEnumerable<TValue>?>>? ValuesExpression { get; set; }

    /// <summary>The selected items. Use <c>@bind-SelectedItems</c>.</summary>
    [Parameter] public IEnumerable<TItem>? SelectedItems { get; set; }

    /// <summary>Raised when the selection changes.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    // ── Selection ─────────────────────────────────────────────────────────────

    /// <summary>Adds a "Select all" row that selects every listed (filtered) item, or clears them all.</summary>
    [Parameter] public bool SelectAll { get; set; }

    /// <summary>The most items that can be selected; the rest are greyed out once it is reached.</summary>
    [Parameter] public int? MaxSelections { get; set; }

    /// <summary>
    /// Above this many selected items the field shows <see cref="SummaryFormat"/> ("5 selected") instead of chips.
    /// <c>0</c> always shows the summary. Default: none, always chips.
    /// </summary>
    [Parameter] public int? SummaryThreshold { get; set; }

    /// <summary>The summary text, with <c>{0}</c> for the count. Default: <c>"{0} selected"</c>.</summary>
    [Parameter] public string SummaryFormat { get; set; } = "{0} selected";

    /// <summary>Shows at most this many chips and a <c>+N more</c> chip for the rest.</summary>
    [Parameter] public int? MaxVisibleChips { get; set; }

    /// <summary>Renders a chip's content. Default: the item text.</summary>
    [Parameter] public RenderFragment<TItem>? ChipTemplate { get; set; }

    // ── Filtering ─────────────────────────────────────────────────────────────

    /// <summary>Typing in the field filters the list. <c>false</c>: no typing; letters jump to items instead. Default: <c>true</c>.</summary>
    [Parameter] public bool Filterable { get; set; } = true;

    /// <summary>How typed text filters the list. Default: <see cref="NxComboBoxFilter.Contains"/>.</summary>
    [Parameter] public NxComboBoxFilter Filter { get; set; } = NxComboBoxFilter.Contains;

    /// <summary>Replaces <see cref="Filter"/>: return <c>true</c> to keep an item for the typed text.</summary>
    [Parameter] public Func<TItem, string, bool>? CustomFilter { get; set; }

    /// <summary>With columns, typed text matches any column's text. Default: <c>true</c>.</summary>
    [Parameter] public bool FilterAllColumns { get; set; } = true;

    // ── Presentation ──────────────────────────────────────────────────────────

    /// <summary>Text shown while nothing is selected.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Shows a clear-all button while anything is selected.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders each list item. Default: the item text with the typed text highlighted.</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Renders a group header. Default: the group key.</summary>
    [Parameter] public RenderFragment<string>? GroupHeaderTemplate { get; set; }

    /// <summary>Shown in the list when nothing matches. Default: <c>"No matches"</c>.</summary>
    [Parameter] public string NoDataText { get; set; } = "No matches";

    /// <summary><see cref="NxComboBoxColumn{TItem}"/> children: the list renders as an aligned grid.</summary>
    [Parameter] public RenderFragment? Columns { get; set; }

    /// <summary>The same as <see cref="Columns"/>, as a list.</summary>
    [Parameter] public IEnumerable<NxComboBoxColumnDefinition<TItem>>? ColumnDefinitions { get; set; }

    /// <summary>Width of the popup as a CSS length.</summary>
    [Parameter] public string? PopupWidth { get; set; }

    /// <summary>Renders only the visible items; every item is then <see cref="ItemHeight"/> pixels tall.</summary>
    [Parameter] public bool Virtualize { get; set; }

    /// <summary>Row height in pixels under <see cref="Virtualize"/>. Default: <c>34</c>.</summary>
    [Parameter] public float ItemHeight { get; set; } = 34;

    /// <summary>Largest popup height in pixels. Default: <c>300</c>.</summary>
    [Parameter] public int PopupMaxHeight { get; set; } = 300;

    // ── Mobile ────────────────────────────────────────────────────────────────

    /// <summary>When the list opens as a full-screen sheet, whose changes apply on Done. Default: <see cref="NxComboBoxMobileMode.Auto"/>.</summary>
    [Parameter] public NxComboBoxMobileMode MobileMode { get; set; } = NxComboBoxMobileMode.Auto;

    /// <summary>With Auto, the viewport width in pixels at or below which the sheet is used. Default: <c>600</c>.</summary>
    [Parameter] public int MobileBreakpoint { get; set; } = 600;

    /// <summary>The sheet's title and search placeholder. Default: <see cref="Placeholder"/>, else "Search".</summary>
    [Parameter] public string? MobileTitle { get; set; }

    // ── State and attributes ──────────────────────────────────────────────────

    /// <summary>Shows the loading state.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows the selection; the list does not open and chips cannot be removed.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>The field's <c>id</c>, for a <c>&lt;label for&gt;</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>name</c>, <c>aria-label</c>…) go to the field's input.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary><c>true</c> while the list is open.</summary>
    public bool IsOpen => combo?.IsOpen ?? false;

    /// <summary>Moves keyboard focus to the field.</summary>
    public ValueTask FocusAsync() => combo?.FocusAsync() ?? ValueTask.CompletedTask;

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-multiselect" : $"nx-multiselect {Class}";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var next = new ComboMode
        {
            PickOnly = !Filterable,
            SelectAll = SelectAll,
            MaxSelections = MaxSelections,
            SummaryThreshold = SummaryThreshold,
            SummaryFormat = SummaryFormat
        };
        if (next != mode)
            mode = next;
    }
}
