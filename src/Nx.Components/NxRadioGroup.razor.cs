using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// A group of radio buttons bound to one value. Items come from <see cref="Data"/> (values, or records read with
/// <see cref="ItemText"/> and <see cref="ItemValue"/>) or from <see cref="NxRadioButton{TValue}"/> children. Built on native radio inputs, so the browser provides the
/// roving tab stop: Tab enters at the checked button and the arrow keys move and check.
/// </summary>
/// <typeparam name="TItem">The type of the <see cref="Data"/> items; inferred from <c>Data</c>. With declared buttons only, pass <c>TItem</c> and <c>TValue</c>.</typeparam>
/// <typeparam name="TValue">The bound value type, read from each item by <see cref="ItemValue"/>; inferred from <c>@bind-Value</c>.</typeparam>
/// <example>
/// <code>
/// &lt;NxRadioGroup Data="customers" ItemText="c =&gt; c.Name" ItemValue="c =&gt; c.Id"
///               @bind-Value="order.CustomerId" Label="Bill to" /&gt;
///
/// &lt;NxRadioGroup TItem="Shift" TValue="Shift" @bind-Value="order.Shift" Label="Shift"&gt;
///     &lt;NxRadioButton Value="Shift.Day" Label="Day" /&gt;
///     &lt;NxRadioButton Value="Shift.Night" Label="Night" /&gt;
/// &lt;/NxRadioGroup&gt;
/// </code>
/// </example>
[CascadingTypeParameter(nameof(TValue))]
public partial class NxRadioGroup<TItem, TValue> : IRadioGroup<TValue>, IDisposable
{
    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-radiogroup-{Guid.NewGuid():N}"[..22];
    private readonly List<NxRadioButton<TValue>> buttons = [];

    /// <summary>Creates the component.</summary>
    public NxRadioGroup() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>The checked item's value. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Raised when the user checks an item.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>The items, when not declared as <see cref="NxRadioButton{TValue}"/> children: records, or plain values (enum values, strings, ids).</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>With <see cref="Data"/>, the label text for an item. Default: <c>ToString()</c>.</summary>
    [Parameter] public Func<TItem, string>? ItemText { get; set; }

    /// <summary>With <see cref="Data"/>, the value an item binds. Optional when <typeparamref name="TItem"/> is <typeparamref name="TValue"/>.</summary>
    [Parameter] public Func<TItem, TValue>? ItemValue { get; set; }

    /// <summary>With <see cref="Data"/>, returns <c>true</c> for items that cannot be chosen.</summary>
    [Parameter] public Func<TItem, bool>? ItemDisabled { get; set; }

    /// <summary>With <see cref="Data"/>, renders an item's label instead of <see cref="ItemText"/>.</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary><see cref="NxRadioButton{TValue}"/> children. Rendered after any <see cref="Data"/> items.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The group's caption, which names it for screen readers.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Items stacked or in a row. Default: <see cref="NxOrientation.Vertical"/>.</summary>
    [Parameter] public NxOrientation Orientation { get; set; } = NxOrientation.Vertical;

    /// <summary>Disables every item.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The native radio <c>name</c>, shared by the items. Generated when not set.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>The group element's <c>id</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    string IRadioGroup<TValue>.GroupName => Name ?? generatedId;
    private string GroupId => Id ?? generatedId;
    private string LabelId => $"{GroupId}-label";
    bool IRadioGroup<TValue>.IsInvalid => validation.IsInvalid;

    bool IRadioGroup<TValue>.IsChecked(TValue? value) => IsChecked(value);

    private bool IsChecked(TValue? value) => EqualityComparer<TValue>.Default.Equals(value, Value);

    private TValue? ValueOf(TItem item)
    {
        if (ItemValue is not null)
            return ItemValue(item);
        if (item is TValue value)
            return value;
        if (item is null)
            return default;
        throw new InvalidOperationException(
            $"NxRadioGroup needs ItemValue to read a {typeof(TValue).Name} from a {typeof(TItem).Name}.");
    }

    private string CssClass => string.Join(' ', new[]
    {
        "nx-radiogroup",
        Orientation == NxOrientation.Horizontal ? "nx-radiogroup-horizontal" : "nx-radiogroup-vertical",
        Disabled             ? "nx-radiogroup-disabled" : null,
        validation.IsInvalid ? "nx-radiogroup-invalid" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    /// <inheritdoc />
    protected override void OnParametersSet() => validation.Attach(CascadedEditContext, ValueExpression);

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // Declared items render once with their own parameters; a new Value or validation state must
        // reach them too, and their parameters alone may not have changed.
        foreach (var button in buttons)
            button.Refresh();
    }

    void IRadioGroup<TValue>.Register(NxRadioButton<TValue> button) => buttons.Add(button);

    void IRadioGroup<TValue>.Unregister(NxRadioButton<TValue> button) => buttons.Remove(button);

    async Task IRadioGroup<TValue>.CheckAsync(TValue? value)
    {
        if (Disabled || IsChecked(value))
            return;
        await ValueChanged.InvokeAsync(value);
        validation.NotifyFieldChanged();
    }

    /// <inheritdoc />
    public void Dispose() => validation.Dispose();
}

/// <summary>What an <see cref="NxRadioButton{TValue}"/> needs from its group, whatever the group's item type.</summary>
internal interface IRadioGroup<TValue>
{
    string GroupName { get; }
    bool Disabled { get; }
    bool IsInvalid { get; }
    bool IsChecked(TValue? value);
    void Register(NxRadioButton<TValue> button);
    void Unregister(NxRadioButton<TValue> button);
    Task CheckAsync(TValue? value);
}
