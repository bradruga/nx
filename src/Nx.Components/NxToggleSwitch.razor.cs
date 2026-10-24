using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// An on/off switch bound to a <c>bool</c>. Renders a <c>&lt;button role="switch"&gt;</c>; use it for settings
/// that take effect at once, and <see cref="NxCheckBox{TValue}"/> for choices submitted with a form.
/// </summary>
/// <example>
/// <code>
/// &lt;NxToggleSwitch @bind-Value="settings.Notify" Label="Email me when it ships" /&gt;
/// </code>
/// </example>
public partial class NxToggleSwitch : IDisposable
{
    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-switch-{Guid.NewGuid():N}"[..18];
    private ElementReference button;

    /// <summary>Creates the component.</summary>
    public NxToggleSwitch() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>On or off. Use <c>@bind-Value</c>.</summary>
    [Parameter] public bool Value { get; set; }

    /// <summary>Raised when the user flips the switch.</summary>
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<bool>>? ValueExpression { get; set; }

    /// <summary>Label text.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Rich label content, after <see cref="Label"/>.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Which side of the switch the label sits on. Default: <see cref="NxLabelPosition.End"/>.</summary>
    [Parameter] public NxLabelPosition LabelPosition { get; set; } = NxLabelPosition.End;

    /// <summary>State text while on, e.g. "On" or "Yes".</summary>
    [Parameter] public string? OnText { get; set; }

    /// <summary>State text while off, e.g. "Off" or "No".</summary>
    [Parameter] public string? OffText { get; set; }

    /// <summary>Where <see cref="OnText"/> / <see cref="OffText"/> show. Default: <see cref="NxToggleTextPosition.Inside"/> the track.</summary>
    [Parameter] public NxToggleTextPosition StateTextPosition { get; set; } = NxToggleTextPosition.Inside;

    /// <summary>Track size. Default: <see cref="NxSize.Medium"/>.</summary>
    [Parameter] public NxSize Size { get; set; } = NxSize.Medium;

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows a spinner in the thumb and ignores clicks while a change is being saved. Stays focusable.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>The switch's <c>id</c>, which the label targets. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>aria-label</c>, <c>aria-describedby</c>…) are passed to the switch.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Moves keyboard focus to the switch.</summary>
    public ValueTask FocusAsync() => button.FocusAsync();

    private string SwitchId => Id ?? generatedId;
    private bool HasLabel => Label is not null || ChildContent is not null;
    private string? StateText => Value ? OnText : OffText;
    private bool HasStateText => OnText is not null || OffText is not null;

    private string CssClass => string.Join(' ', new[]
    {
        "nx-switch",
        Size switch { NxSize.Small => "nx-switch-sm", NxSize.Large => "nx-switch-lg", _ => "nx-switch-md" },
        Value                ? "nx-switch-on" : null,
        HasStateText && StateTextPosition == NxToggleTextPosition.Inside ? "nx-switch-text-inside" : null,
        LabelPosition == NxLabelPosition.Start ? "nx-switch-label-start" : null,
        Disabled             ? "nx-switch-disabled" : null,
        Loading              ? "nx-switch-loading" : null,
        validation.IsInvalid ? "nx-switch-invalid" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    /// <inheritdoc />
    protected override void OnParametersSet() => validation.Attach(CascadedEditContext, ValueExpression);

    private async Task HandleClick()
    {
        if (Disabled || Loading)
            return;
        await ValueChanged.InvokeAsync(!Value);
        validation.NotifyFieldChanged();
    }

    /// <inheritdoc />
    public void Dispose() => validation.Dispose();
}
