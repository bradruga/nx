using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// A pick-only list: no typing; letters jump to items by their first characters, as a native select does.
/// Built on the <see cref="NxComboBox{TItem, TValue}"/> engine, so it has the same data sources, templates,
/// grouping, virtualization, columns, phone sheet and validation. <see cref="DefaultText"/> adds a "none" item.
/// </summary>
/// <typeparam name="TItem">The list item type.</typeparam>
/// <typeparam name="TValue">The bound value type, read from each item by <see cref="ItemValue"/>.</typeparam>
/// <example>
/// <code>
/// &lt;NxDropDown Data="shifts" ItemText="s =&gt; s.Name" ItemValue="s =&gt; s.Id" @bind-Value="crew.ShiftId" DefaultText="Any shift" /&gt;
/// </code>
/// </example>
[CascadingTypeParameter(nameof(TItem))]
public partial class NxDropDown<TItem, TValue>
{
    private NxComboBox<TItem, TValue>? combo;
    private ComboMode mode = new() { PickOnly = true };

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>The items.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>Loads the items instead of <see cref="Data"/> each time the list opens, as for NxComboBox; the request's filter is always empty.</summary>
    [Parameter] public Func<NxComboBoxRequest, Task<IEnumerable<TItem>>>? DataProvider { get; set; }

    /// <summary>With <see cref="DataProvider"/>, finds the item of a bound value the list has not loaded, so the field shows its text straight away.</summary>
    [Parameter] public Func<TValue, Task<TItem?>>? ItemByValue { get; set; }

    /// <summary>The text shown for an item, and matched by type-ahead. Default: <c>ToString()</c>.</summary>
    [Parameter] public Func<TItem, string>? ItemText { get; set; }

    /// <summary>The value bound to <see cref="Value"/>. Optional when <typeparamref name="TItem"/> is <typeparamref name="TValue"/>.</summary>
    [Parameter] public Func<TItem, TValue>? ItemValue { get; set; }

    /// <summary>Groups the list under headers by this key.</summary>
    [Parameter] public Func<TItem, string?>? ItemGroup { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The selected item's value. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Raised when the selection changes.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>The selected item. Use <c>@bind-SelectedItem</c>.</summary>
    [Parameter] public TItem? SelectedItem { get; set; }

    /// <summary>Raised when the selection changes.</summary>
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }

    // ── Presentation ──────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a first item with this text that selects nothing (the value becomes <c>default</c>), and shows it
    /// while nothing is selected — "Any shift", "(none)".
    /// </summary>
    [Parameter] public string? DefaultText { get; set; }

    /// <summary>Text shown while nothing is selected, when there is no <see cref="DefaultText"/>.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Shows a clear button while something is selected.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders each list item. Default: the item text.</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Renders the selected item in the field. Default: the item text.</summary>
    [Parameter] public RenderFragment<TItem>? SelectedItemTemplate { get; set; }

    /// <summary>Renders a group header. Default: the group key.</summary>
    [Parameter] public RenderFragment<string>? GroupHeaderTemplate { get; set; }

    /// <summary>Shown in the list when there are no items. Default: <c>"No items"</c>.</summary>
    [Parameter] public string NoDataText { get; set; } = "No items";

    /// <summary><see cref="NxComboBoxColumn{TItem}"/> children: the list renders as an aligned grid under a header row.</summary>
    [Parameter] public RenderFragment? Columns { get; set; }

    /// <summary>The same as <see cref="Columns"/>, as a list.</summary>
    [Parameter] public IEnumerable<NxComboBoxColumnDefinition<TItem>>? ColumnDefinitions { get; set; }

    /// <summary>Width of the popup as a CSS length. Default: as wide as the field, or the columns.</summary>
    [Parameter] public string? PopupWidth { get; set; }

    /// <summary>Row count from which the list renders only the rows in view, all one height. Default: <c>200</c>.</summary>
    [Parameter] public int VirtualizeThreshold { get; set; } = 200;

    /// <summary>Row height in pixels of a virtualized list. Default: measured from the rendered rows.</summary>
    [Parameter] public float? ItemHeight { get; set; }

    /// <summary>Largest popup height in pixels. Default: <c>300</c>.</summary>
    [Parameter] public int PopupMaxHeight { get; set; } = 300;

    // ── Mobile ────────────────────────────────────────────────────────────────

    /// <summary>When the list opens as a full-screen sheet. Default: <see cref="NxMobileMode.Never"/>.</summary>
    [Parameter] public NxMobileMode MobileMode { get; set; } = NxMobileMode.Never;

    /// <summary>With Auto, the viewport width in pixels at or below which the sheet is used. Default: <c>600</c>.</summary>
    [Parameter] public int MobileBreakpoint { get; set; } = 600;

    /// <summary>The sheet's title. Default: <see cref="Placeholder"/>, else "Search".</summary>
    [Parameter] public string? MobileTitle { get; set; }

    // ── State and attributes ──────────────────────────────────────────────────

    /// <summary>Shows the loading state.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows the value; the list does not open.</summary>
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

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-dropdown" : $"nx-dropdown {Class}";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var next = new ComboMode { PickOnly = true, DefaultText = DefaultText };
        if (next != mode)
            mode = next;
    }
}
