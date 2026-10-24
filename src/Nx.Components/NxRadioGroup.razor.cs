using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// A group of radio buttons bound to one value. Items come from <see cref="Data"/> or from
/// <see cref="NxRadioButton{TValue}"/> children. Built on native radio inputs, so the browser provides the
/// roving tab stop: Tab enters at the checked button and the arrow keys move and check.
/// </summary>
/// <typeparam name="TValue">The bound value type; inferred from <c>@bind-Value</c>.</typeparam>
/// <example>
/// <code>
/// &lt;NxRadioGroup @bind-Value="order.Shift" Label="Shift"&gt;
///     &lt;NxRadioButton Value="Shift.Day" Label="Day" /&gt;
///     &lt;NxRadioButton Value="Shift.Night" Label="Night" /&gt;
/// &lt;/NxRadioGroup&gt;
/// </code>
/// </example>
[CascadingTypeParameter(nameof(TValue))]
public partial class NxRadioGroup<TValue> : IDisposable
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

    /// <summary>The items, when not declared as <see cref="NxRadioButton{TValue}"/> children: enum values, strings, ids.</summary>
    [Parameter] public IEnumerable<TValue>? Data { get; set; }

    /// <summary>With <see cref="Data"/>, the label text for an item. Default: <c>ToString()</c>.</summary>
    [Parameter] public Func<TValue, string>? ItemText { get; set; }

    /// <summary>With <see cref="Data"/>, returns <c>true</c> for items that cannot be chosen.</summary>
    [Parameter] public Func<TValue, bool>? ItemDisabled { get; set; }

    /// <summary>With <see cref="Data"/>, renders an item's label instead of <see cref="ItemText"/>.</summary>
    [Parameter] public RenderFragment<TValue>? ItemTemplate { get; set; }

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

    internal string GroupName => Name ?? generatedId;
    private string GroupId => Id ?? generatedId;
    private string LabelId => $"{GroupId}-label";
    internal bool IsInvalid => validation.IsInvalid;

    internal bool IsChecked(TValue? value) => EqualityComparer<TValue>.Default.Equals(value, Value);

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

    internal void Register(NxRadioButton<TValue> button) => buttons.Add(button);

    internal void Unregister(NxRadioButton<TValue> button) => buttons.Remove(button);

    internal async Task CheckAsync(TValue? value)
    {
        if (Disabled || IsChecked(value))
            return;
        await ValueChanged.InvokeAsync(value);
        validation.NotifyFieldChanged();
    }

    /// <inheritdoc />
    public void Dispose() => validation.Dispose();
}
