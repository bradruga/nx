using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A row of joined buttons that works like radio buttons — one choice, bound with <c>@bind-Value</c> — or, with
/// <see cref="Multiple"/>, like check boxes bound with <c>@bind-Values</c>. Items come from <see cref="Data"/> or from
/// <see cref="NxToggleGroupItem{TValue}"/> children. One tab stop: arrow keys move between items (and select, in
/// single mode), Home and End jump to the ends.
/// </summary>
/// <typeparam name="TValue">The bound value type; inferred from <c>@bind-Value</c> or <c>@bind-Values</c>.</typeparam>
/// <example>
/// <code>
/// &lt;NxToggleGroup @bind-Value="view"&gt;
///     &lt;NxToggleGroupItem Value="View.List" Label="List" /&gt;
///     &lt;NxToggleGroupItem Value="View.Board" Label="Board" /&gt;
/// &lt;/NxToggleGroup&gt;
/// </code>
/// </example>
[CascadingTypeParameter(nameof(TValue))]
public partial class NxToggleGroup<TValue> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-input.js moves focus between items on the arrow, Home and End keys (roving tabindex) and, in
    // single mode, clicks the item it lands on so selection follows focus. It runs in the browser
    // because a server round trip per key lets fast typing outrun the focus. The tab stop is tracked
    // here from the items' focus events.

    private const string ModulePath = "./_content/Nx.Components/nx-input.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly List<NxToggleGroupItem<TValue>> items = [];
    private IJSObjectReference? module;
    private ElementReference container;
    private TValue? focusedValue;
    private bool hasFocusedValue;

    /// <summary>Creates the component.</summary>
    public NxToggleGroup() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The selected item's value. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Raised when the selection changes.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>Any number of items can be on. Bind <see cref="Values"/> instead of <see cref="Value"/>.</summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>With <see cref="Multiple"/>, the values that are on. Use <c>@bind-Values</c>.</summary>
    [Parameter] public IEnumerable<TValue>? Values { get; set; }

    /// <summary>Raised when an item is toggled with <see cref="Multiple"/>.</summary>
    [Parameter] public EventCallback<IEnumerable<TValue>> ValuesChanged { get; set; }

    /// <summary>Identifies the bound collection for <c>EditForm</c> validation. Set by <c>@bind-Values</c>.</summary>
    [Parameter] public Expression<Func<IEnumerable<TValue>?>>? ValuesExpression { get; set; }

    /// <summary>In single mode, clicking the selected item turns it off, leaving no selection.</summary>
    [Parameter] public bool AllowDeselect { get; set; }

    // ── Items ─────────────────────────────────────────────────────────────────

    /// <summary>The items, when not declared as <see cref="NxToggleGroupItem{TValue}"/> children.</summary>
    [Parameter] public IEnumerable<TValue>? Data { get; set; }

    /// <summary>With <see cref="Data"/>, an item's text. Default: <c>ToString()</c>.</summary>
    [Parameter] public Func<TValue, string>? ItemText { get; set; }

    /// <summary>With <see cref="Data"/>, returns <c>true</c> for items that cannot be chosen.</summary>
    [Parameter] public Func<TValue, bool>? ItemDisabled { get; set; }

    /// <summary>With <see cref="Data"/>, renders an item's content (icon and text) instead of <see cref="ItemText"/>.</summary>
    [Parameter] public RenderFragment<TValue>? ItemTemplate { get; set; }

    /// <summary><see cref="NxToggleGroupItem{TValue}"/> children. Rendered after any <see cref="Data"/> items.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    // ── Appearance ────────────────────────────────────────────────────────────

    /// <summary>Item height, on the <see cref="NxButton"/> scale. Default: <see cref="NxSize.Medium"/>.</summary>
    [Parameter] public NxSize Size { get; set; } = NxSize.Medium;

    /// <summary>Stretches the group to its container, sharing the width between items.</summary>
    [Parameter] public bool FullWidth { get; set; }

    /// <summary>Disables every item.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The group's accessible name, when no visible label names it.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The group element's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the group.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the group.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>aria-labelledby</c>…) are passed to the group element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // ── State shared with items ───────────────────────────────────────────────

    internal bool IsInvalid => validation.IsInvalid;

    internal bool IsSelected(TValue? value) => Multiple
        ? (Values ?? []).Contains(value!, EqualityComparer<TValue>.Default)
        : EqualityComparer<TValue>.Default.Equals(value, Value);

    /// <summary>
    /// The one item in the tab order: the last one focused, else the selected one, else the first enabled one.
    /// </summary>
    internal bool IsTabStop(NxToggleGroupItem<TValue> item)
    {
        var enabled = items.Where(i => !i.IsDisabled).ToList();
        if (enabled.Count == 0)
            return false;
        var stop = (hasFocusedValue ? enabled.FirstOrDefault(i => Same(i.Value, focusedValue)) : null)
                   ?? enabled.FirstOrDefault(i => IsSelected(i.Value))
                   ?? enabled[0];
        return ReferenceEquals(stop, item);
    }

    private static bool Same(TValue? a, TValue? b) => EqualityComparer<TValue>.Default.Equals(a, b);

    private string CssClass => string.Join(' ', new[]
    {
        "nx-togglegroup",
        Size switch { NxSize.Small => "nx-togglegroup-sm", NxSize.Large => "nx-togglegroup-lg", _ => "nx-togglegroup-md" },
        FullWidth            ? "nx-togglegroup-full-width" : null,
        Disabled             ? "nx-togglegroup-disabled" : null,
        validation.IsInvalid ? "nx-togglegroup-invalid" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Multiple)
            validation.Attach(CascadedEditContext, ValuesExpression);
        else
            validation.Attach(CascadedEditContext, ValueExpression);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is not null)
                await module.InvokeVoidAsync("roving", container, ".nx-toggle");
        }
        // Items render with their own parameters; selection and the tab stop live here.
        foreach (var item in items)
            item.Refresh();
    }

    internal void Register(NxToggleGroupItem<TValue> item) => items.Add(item);

    internal void Unregister(NxToggleGroupItem<TValue> item) => items.Remove(item);

    // ── Selection ─────────────────────────────────────────────────────────────

    internal async Task ActivateAsync(NxToggleGroupItem<TValue> item)
    {
        if (Disabled || item.IsDisabled)
            return;
        focusedValue = item.Value;
        hasFocusedValue = true;

        if (Multiple)
        {
            var current = (Values ?? []).ToList();
            if (!current.Remove(item.Value!))
                current.Add(item.Value!);
            await ValuesChanged.InvokeAsync(current);
        }
        else if (IsSelected(item.Value))
        {
            if (!AllowDeselect)
                return;
            await ValueChanged.InvokeAsync(default);
        }
        else
        {
            await ValueChanged.InvokeAsync(item.Value);
        }
        validation.NotifyFieldChanged();
        StateHasChanged();
    }

    /// <summary>An item gained focus (Tab, click, or the arrow keys in nx-input.js): it becomes the tab stop.</summary>
    internal void NoteFocus(NxToggleGroupItem<TValue> item)
    {
        if (hasFocusedValue && Same(item.Value, focusedValue))
            return;
        focusedValue = item.Value;
        hasFocusedValue = true;
        StateHasChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        validation.Dispose();
        if (module is null)
            return;
        try
        {
            await module.InvokeVoidAsync("dispose", container);
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
