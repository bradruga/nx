using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// A check box bound to <c>bool</c>, or to <c>bool?</c> where <c>null</c> shows as indeterminate.
/// Renders a <c>&lt;button role="checkbox"&gt;</c>, so it needs no JS for the mixed state and its label is a real <c>&lt;label&gt;</c>.
/// </summary>
/// <typeparam name="TValue"><c>bool</c> or <c>bool?</c>; inferred from <c>@bind-Value</c>.</typeparam>
/// <example>
/// <code>
/// &lt;NxCheckBox @bind-Value="order.Rush" Label="Rush order" /&gt;
/// </code>
/// </example>
public partial class NxCheckBox<TValue> : IDisposable
{
    private static readonly bool IsNullable = typeof(TValue) == typeof(bool?);

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-checkbox-{Guid.NewGuid():N}"[..20];
    private ElementReference button;

    /// <summary>Creates the component.</summary>
    public NxCheckBox() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary><c>true</c>, <c>false</c>, or (for <c>bool?</c>) <c>null</c> for indeterminate. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Raised when the user toggles the box.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>
    /// With <c>bool?</c>, clicking cycles unchecked → checked → indeterminate. Otherwise an indeterminate box
    /// becomes checked on the first click and then toggles between checked and unchecked.
    /// </summary>
    [Parameter] public bool ThreeState { get; set; }

    /// <summary>Label text.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Rich label content, after <see cref="Label"/>.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Which side of the box the label sits on. Default: <see cref="NxLabelPosition.End"/>.</summary>
    [Parameter] public NxLabelPosition LabelPosition { get; set; } = NxLabelPosition.End;

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Focusable and announced as read-only, but clicks do nothing.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>The box's <c>id</c>, which the label targets. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>aria-label</c>, <c>aria-describedby</c>…) are passed to the box.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Moves keyboard focus to the box.</summary>
    public ValueTask FocusAsync() => button.FocusAsync();

    private string BoxId => Id ?? generatedId;
    private bool? State => Value is bool b ? b : null;
    private bool HasLabel => Label is not null || ChildContent is not null;

    private string AriaChecked => State switch
    {
        true  => "true",
        false => "false",
        null  => "mixed"
    };

    private string CssClass => string.Join(' ', new[]
    {
        "nx-checkbox",
        State == true            ? "nx-checkbox-checked" : null,
        State is null            ? "nx-checkbox-indeterminate" : null,
        LabelPosition == NxLabelPosition.Start ? "nx-checkbox-label-start" : null,
        Disabled                 ? "nx-checkbox-disabled" : null,
        ReadOnly                 ? "nx-checkbox-readonly" : null,
        validation.IsInvalid     ? "nx-checkbox-invalid" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (typeof(TValue) != typeof(bool) && !IsNullable)
            throw new InvalidOperationException($"NxCheckBox binds bool or bool?, not {typeof(TValue).Name}.");
        if (ThreeState && !IsNullable)
            throw new InvalidOperationException("ThreeState needs a bool? value.");
        validation.Attach(CascadedEditContext, ValueExpression);
    }

    private bool? Next() => State switch
    {
        false => true,
        true  => ThreeState ? null : false,
        null  => ThreeState ? false : true
    };

    private async Task HandleClick()
    {
        if (Disabled || ReadOnly)
            return;
        var next = Next();
        await ValueChanged.InvokeAsync(next is bool b ? (TValue)(object)b : default);
        validation.NotifyFieldChanged();
    }

    /// <inheritdoc />
    public void Dispose() => validation.Dispose();
}
