using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

// NxComboBox<TItem, TValue> is split across the following partial class files:
//
//   NxComboBox.razor            — markup: field, popup, mobile overlay, rows
//   NxComboBox.razor.cs         — parameters, state, lifecycle, JS interop
//   NxComboBox.Filtering.cs     — BuildRows, LoadAsync (DataProvider), the initial highlight
//   NxComboBox.Selection.cs     — single-select: select, clear, custom values, Value/SelectedItem sync
//   NxComboBox.Multiple.cs      — Multiple: chips, toggling, Values/SelectedItems sync
//   NxComboBox.Keyboard.cs      — keys, focus, blur, clicks
//   NxComboBox.Mobile.cs        — the full-screen overlay and viewport tracking
//   NxComboBox.Columns.cs       — multi-column list definitions and layout

namespace Nx.Components;

/// <summary>
/// A text input with a filterable dropdown list. Items come from <see cref="Data"/> or an async
/// <see cref="DataProvider"/>; bind the chosen value with <c>@bind-Value</c>, the chosen item with
/// <c>@bind-SelectedItem</c>, or both. Opt in to <see cref="Multiple"/> selection or a multi-column
/// list with <see cref="Columns"/>; on a phone the list opens full screen (<see cref="MobileMode"/>).
/// </summary>
/// <typeparam name="TItem">The list item type.</typeparam>
/// <typeparam name="TValue">The bound value type, read from each item by <see cref="ItemValue"/>.</typeparam>
/// <example>
/// <code>
/// &lt;NxComboBox Data="customers" ItemText="c =&gt; c.Name" ItemValue="c =&gt; c.Id"
///             @bind-Value="order.CustomerId" Placeholder="Customer" Clearable="true" /&gt;
/// </code>
/// </example>
[CascadingTypeParameter(nameof(TItem))]
public partial class NxComboBox<TItem, TValue> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-combobox.js places the popup (it is position: fixed so dialogs and scroll containers do not
    // clip it, which needs viewport measurement), cancels the browser default for arrow, page,
    // Enter, Escape and (when it toggles) Space keys, scrolls the active option into view, locks page
    // scroll under the mobile overlay and reports when the viewport crosses MobileBreakpoint.
    // Outside click needs no JS: the input keeps focus while the popup is used (mousedown is
    // cancelled there), so any click elsewhere blurs it, and blur closes the list.

    private const string ModulePath = "./_content/Nx.Components/nx-combobox.js";
    private const int PageSize = 10;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-combobox-{Guid.NewGuid():N}"[..21];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxComboBox<TItem, TValue>>? selfReference;
    private ElementReference input;
    private ElementReference field;
    private ElementReference popup;
    private ElementReference list;

    private List<TItem> source = [];
    private IEnumerable<TItem>? lastData;
    private List<Row> rows = [];
    private int activeRow = -1;
    private int itemCount;

    private string inputText = "";
    private string? customText;
    private bool isOpen;
    private bool isFiltering;
    private bool hasFocus;
    private bool providerLoading;
    private bool popupPlaced;
    private bool scrollPending;
    private bool closePending;
    private bool navigated;
    private bool syncPending;
    private bool notifying;

    private bool hasSelection;
    private TItem? selectedItem;
    private TValue? lastValue;                  // the Value parameter as last seen
    private TItem? lastSelectedItemParameter;   // the SelectedItem parameter as last seen
    private bool initialized;

    private CancellationTokenSource? providerCts;

    /// <summary>Creates the component.</summary>
    public NxComboBox() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>The items. Filtered in memory as the user types.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>
    /// Loads items for the typed text instead of <see cref="Data"/>: called when the list opens and,
    /// after <see cref="DebounceDelay"/>, as the user types. Returns the matching items; the
    /// provider does the filtering. Set <c>@bind-SelectedItem</c> too, so the current item's text
    /// can be shown before the list has ever loaded.
    /// </summary>
    [Parameter] public Func<NxComboBoxRequest, Task<IEnumerable<TItem>>>? DataProvider { get; set; }

    /// <summary>With <see cref="DataProvider"/>, milliseconds to wait after typing before loading. Default: <c>300</c>.</summary>
    [Parameter] public int DebounceDelay { get; set; } = 300;

    /// <summary>The text shown for an item, and matched by filtering. Default: <c>ToString()</c>.</summary>
    [Parameter] public Func<TItem, string>? ItemText { get; set; }

    /// <summary>The value bound to <see cref="Value"/>. Optional when <typeparamref name="TItem"/> is <typeparamref name="TValue"/>.</summary>
    [Parameter] public Func<TItem, TValue>? ItemValue { get; set; }

    /// <summary>Groups the list under headers by this key, in order of first appearance. <c>null</c> keys form an untitled group.</summary>
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

    // ── Multiple ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Selects any number of items, shown as chips in the field. Bind <see cref="Values"/> and/or
    /// <see cref="SelectedItems"/> instead of <see cref="Value"/> and <see cref="SelectedItem"/>.
    /// </summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>With <see cref="Multiple"/>, the selected items' values. Use <c>@bind-Values</c>.</summary>
    [Parameter] public IEnumerable<TValue>? Values { get; set; }

    /// <summary>Raised when the multiple selection changes.</summary>
    [Parameter] public EventCallback<IEnumerable<TValue>> ValuesChanged { get; set; }

    /// <summary>Identifies the bound collection for <c>EditForm</c> validation. Set by <c>@bind-Values</c>.</summary>
    [Parameter] public Expression<Func<IEnumerable<TValue>?>>? ValuesExpression { get; set; }

    /// <summary>With <see cref="Multiple"/>, the selected items. Use <c>@bind-SelectedItems</c>.</summary>
    [Parameter] public IEnumerable<TItem>? SelectedItems { get; set; }

    /// <summary>Raised when the multiple selection changes.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    /// <summary>With <see cref="Multiple"/>, shows at most this many chips and a <c>+N more</c> chip for the rest.</summary>
    [Parameter] public int? MaxVisibleChips { get; set; }

    /// <summary>Renders a chip's content. Default: the item text.</summary>
    [Parameter] public RenderFragment<TItem>? ChipTemplate { get; set; }

    // ── Filtering ─────────────────────────────────────────────────────────────

    /// <summary>How typed text filters the list. Default: <see cref="NxComboBoxFilter.Contains"/>.</summary>
    [Parameter] public NxComboBoxFilter Filter { get; set; } = NxComboBoxFilter.Contains;

    /// <summary>Replaces <see cref="Filter"/>: return <c>true</c> to keep an item for the typed text.</summary>
    [Parameter] public Func<TItem, string, bool>? CustomFilter { get; set; }

    /// <summary>
    /// With columns, typed text matches any column's text as well as <see cref="ItemText"/>. Set <c>false</c>
    /// to match <see cref="ItemText"/> only. Default: <c>true</c>.
    /// </summary>
    [Parameter] public bool FilterAllColumns { get; set; } = true;

    /// <summary>
    /// Typed text that matches no item is kept as the value instead of being discarded.
    /// Requires <typeparamref name="TValue"/> to be <see cref="string"/>; not available with <see cref="Multiple"/>.
    /// </summary>
    [Parameter] public bool AllowCustomValue { get; set; }

    // ── Columns ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <see cref="NxComboBoxColumn{TItem}"/> children. With any column the list renders as an aligned grid
    /// under a header row. The field still shows <see cref="ItemText"/>.
    /// </summary>
    [Parameter] public RenderFragment? Columns { get; set; }

    /// <summary>The same as <see cref="Columns"/>, as a list. Takes precedence over <see cref="Columns"/>.</summary>
    [Parameter] public IEnumerable<NxComboBoxColumnDefinition<TItem>>? ColumnDefinitions { get; set; }

    /// <summary>Width of the popup as a CSS length, e.g. <c>"560px"</c>. Default: as wide as the field, or the columns.</summary>
    [Parameter] public string? PopupWidth { get; set; }

    // ── Mobile ────────────────────────────────────────────────────────────────

    /// <summary>When the list opens as a full-screen overlay instead of a popup. Default: <see cref="NxComboBoxMobileMode.Auto"/>.</summary>
    [Parameter] public NxComboBoxMobileMode MobileMode { get; set; } = NxComboBoxMobileMode.Auto;

    /// <summary>With <see cref="NxComboBoxMobileMode.Auto"/>, the viewport width in pixels at or below which the overlay is used. Default: <c>600</c>.</summary>
    [Parameter] public int MobileBreakpoint { get; set; } = 600;

    /// <summary>The overlay's accessible name and search placeholder. Default: <see cref="Placeholder"/>, else "Search".</summary>
    [Parameter] public string? MobileTitle { get; set; }

    // ── Presentation ──────────────────────────────────────────────────────────

    /// <summary>Placeholder text shown while nothing is selected.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Shows a clear button while something is selected; with <see cref="Multiple"/> it clears every chip.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders each list item. Default: the item text, with the typed text highlighted. Ignored with columns.</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Renders the selected item in the field while it is not focused. Default: the item text.</summary>
    [Parameter] public RenderFragment<TItem>? SelectedItemTemplate { get; set; }

    /// <summary>Renders a group header. Default: the group key.</summary>
    [Parameter] public RenderFragment<string>? GroupHeaderTemplate { get; set; }

    /// <summary>Shown in the list when nothing matches. Default: <c>"No matches"</c>.</summary>
    [Parameter] public string NoDataText { get; set; } = "No matches";

    /// <summary>
    /// Renders only the visible items, so lists of tens of thousands stay fast. Every item is then
    /// <see cref="ItemHeight"/> pixels tall, including group headers.
    /// </summary>
    [Parameter] public bool Virtualize { get; set; }

    /// <summary>Row height in pixels under <see cref="Virtualize"/>. Default: <c>34</c>.</summary>
    [Parameter] public float ItemHeight { get; set; } = 34;

    /// <summary>Largest popup height in pixels; it shrinks further to fit the viewport. Default: <c>300</c>.</summary>
    [Parameter] public int PopupMaxHeight { get; set; } = 300;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Shows the loading state. A <see cref="DataProvider"/> shows it by itself while it runs.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The value can be read and copied but not changed; the list does not open.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    // ── Attributes ────────────────────────────────────────────────────────────

    /// <summary>The input's <c>id</c>, for a <c>&lt;label for&gt;</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>name</c>, <c>aria-label</c>…) are passed to the <c>&lt;input&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary><c>true</c> while the list is open, as a popup or the mobile overlay.</summary>
    public bool IsOpen => isOpen;

    /// <summary>Moves keyboard focus to the input.</summary>
    public ValueTask FocusAsync() => input.FocusAsync();

    // ── Rows ──────────────────────────────────────────────────────────────────

    /// <summary>One line of the list: a group header or an item. <see cref="ItemIndex"/> counts items only.</summary>
    private readonly record struct Row(bool IsHeader, string? Group, TItem Item, int ItemIndex, int RowIndex, SpecialRow Special = SpecialRow.None);

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string InputId => Id ?? generatedId;
    private string ListId => $"{InputId}-list";
    private string OptionId(int row) => $"{InputId}-opt-{row}";
    private bool Editable => !Disabled && !ReadOnly;
    private bool ShowLoading => Loading || providerLoading;
    private bool ShowClear => Clearable && Editable && (Multiple
        ? chips.Count > 0
        : hasSelection || customText is not null || inputText.Length > 0);
    // An editable field shows its text while focused; a pick-only one (NxDropDown) has no text to edit.
    private bool ShowSelectedTemplate => !Multiple && SelectedItemTemplate is not null && hasSelection && (!hasFocus || PickOnly) && !isOpen;
    private string? ActiveDescendant => isOpen && !overlayOpen && activeRow >= 0 && activeRow < rows.Count ? OptionId(activeRow) : null;

    /// <summary>The text the list is filtered by: the overlay's search box while it is open, else the field.</summary>
    private string FilterText => overlayOpen ? overlayText : inputText;

    private string? PlaceholderText => Multiple && chips.Count > 0 ? null : Placeholder ?? DefaultText;

    private string CssClass => string.Join(' ', new[]
    {
        "nx-combobox",
        isOpen               ? "nx-combobox-open" : null,
        Multiple             ? "nx-combobox-multiple" : null,
        PickOnly             ? "nx-combobox-pick" : null,
        Suggest              ? "nx-combobox-suggest" : null,
        UseOverlay           ? "nx-combobox-mobile" : null,
        Disabled             ? "nx-combobox-disabled" : null,
        ReadOnly             ? "nx-combobox-readonly" : null,
        validation.IsInvalid ? "nx-combobox-invalid" : null,
        ShowSelectedTemplate ? "nx-combobox-templated" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-combobox-input"
        : $"nx-combobox-input {validation.CssClass}";

    private string ItemStyle => Virtualize
        ? $"height: {ItemHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}px"
        : "";

    /// <summary>The typed text the default templates highlight, or empty.</summary>
    private string HighlightText => isFiltering && CustomFilter is null ? FilterText.Trim() : "";

    private string TextOf(TItem item) => ItemText?.Invoke(item) ?? item?.ToString() ?? "";

    private TValue? ValueOf(TItem item)
    {
        if (ItemValue is not null)
            return ItemValue(item);
        if (item is TValue value)
            return value;
        throw new InvalidOperationException(
            $"{GetType().Name} needs ItemValue to read a {typeof(TValue).Name} from a {typeof(TItem).Name}.");
    }

    private bool IsSelected(TItem item) => Multiple
        ? IsChosen(item)
        : hasSelection && EqualityComparer<TItem>.Default.Equals(item, selectedItem);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (AllowCustomValue && typeof(TValue) != typeof(string))
            throw new InvalidOperationException($"AllowCustomValue requires TValue to be string; it is {typeof(TValue).Name}.");
        if (AllowCustomValue && Multiple)
            throw new InvalidOperationException("AllowCustomValue is not available with Multiple.");

        if (Multiple)
            validation.Attach(CascadedEditContext, ValuesExpression);
        else
            validation.Attach(CascadedEditContext, ValueExpression);

        var dataChanged = false;
        if (!ReferenceEquals(Data, lastData))
        {
            lastData = Data;
            if (DataProvider is null)
            {
                source = Data?.ToList() ?? [];
                dataChanged = true;
                if (isOpen)
                    BuildRows();
            }
        }

        // While change callbacks are being raised, a parent may re-render between them with one
        // parameter updated and the other stale; the selection is already right.
        if (notifying)
            return;

        // An outside change replaces what the user is typing, even with the list open.
        if (Multiple)
            SyncMultipleFromParameters(dataChanged);
        else if (SyncSingleFromParameters() && isOpen)
            Close();

        initialized = true;

        if (!isOpen)
            inputText = DisplayText;
    }

    /// <summary>The text the field shows while the list is closed.</summary>
    private string DisplayText => Multiple ? "" : hasSelection ? TextOf(selectedItem!) : customText ?? "";

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            await module.InvokeVoidAsync("init", input);
            if (MobileMode == NxComboBoxMobileMode.Auto && module is not null)
            {
                selfReference = DotNetObjectReference.Create(this);
                isNarrow = await module.InvokeAsync<bool>("watchViewport", field, selfReference, MobileBreakpoint);
                if (isNarrow)
                    StateHasChanged();
            }
        }
        if (syncPending)
        {
            syncPending = false;
            await SyncBindingsAsync();
        }

        if (module is null)
            return;

        if (closePending)
        {
            closePending = false;
            await module.InvokeVoidAsync("close", field);
        }

        await AfterRenderOverlayAsync();

        if (isOpen && !overlayOpen && !popupPlaced)
        {
            popupPlaced = true;
            await module.InvokeAsync<string>("open", field, popup, list, PopupMaxHeight);
            scrollPending = true;
        }

        if (isOpen && scrollPending)
        {
            scrollPending = false;
            await module.InvokeVoidAsync("scrollToIndex", overlayOpen ? overlayList : list, activeRow, ItemHeight);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        providerCts?.Cancel();
        providerCts?.Dispose();
        validation.Dispose();
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("close", field);
                await module.InvokeVoidAsync("dispose", input, field);
                if (overlayOpen)
                    await module.InvokeVoidAsync("lockScroll", false);
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        selfReference?.Dispose();
    }
}
