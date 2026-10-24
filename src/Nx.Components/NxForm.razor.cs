using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// An <c>EditForm</c> that lays out <see cref="NxFormItem"/>s (and <see cref="NxFormGroup"/>s) in a grid of
/// <see cref="Columns"/>, labels above or beside them, one column on a narrow screen. Items name a property of
/// <see cref="Model"/> and render a fitting Nx editor, or take your own; each shows its validation messages.
/// Validates with DataAnnotations unless <see cref="Validation"/> holds other validators.
/// </summary>
/// <example>
/// <code>
/// &lt;NxForm Model="order" Columns="2" OnValidSubmit="SaveAsync"&gt;
///     &lt;NxFormItem Field="@@nameof(Order.Customer)" /&gt;
///     &lt;NxFormItem Field="@@nameof(Order.PourDate)" Label="Pour date" /&gt;
///     &lt;Buttons&gt;&lt;NxButton Type="NxButtonType.Submit"&gt;Save&lt;/NxButton&gt;&lt;/Buttons&gt;
/// &lt;/NxForm&gt;
/// </code>
/// </example>
public partial class NxForm
{
    private readonly string generatedId = $"nx-form-{Guid.NewGuid():N}"[..16];
    private EditContext? editContext;
    private bool explicitContext;

    /// <summary>The object being edited; the form makes its own <see cref="EditContext"/> for it. Set this or <see cref="EditContext"/>.</summary>
    [Parameter] public object? Model { get; set; }

    /// <summary>An edit context you made; set this or <see cref="Model"/>. Reads back the form's context either way.</summary>
    [Parameter]
#pragma warning disable BL0007 // As EditForm's own EditContext: a set from outside, a read of the context in use.
    public EditContext? EditContext
#pragma warning restore BL0007
    {
        get => editContext;
        set
        {
            // As EditForm: the getter returns the context made for Model too.
            editContext = value;
            explicitContext = value is not null;
        }
    }

    /// <summary>Raised on submit when validation passes.</summary>
    [Parameter] public EventCallback<EditContext> OnValidSubmit { get; set; }

    /// <summary>Raised on submit when validation fails.</summary>
    [Parameter] public EventCallback<EditContext> OnInvalidSubmit { get; set; }

    /// <summary>Raised on every submit, without validating; not with <see cref="OnValidSubmit"/> or <see cref="OnInvalidSubmit"/>.</summary>
    [Parameter] public EventCallback<EditContext> OnSubmit { get; set; }

    /// <summary>Grid columns for the items. Items span them with <see cref="NxFormItem.ColSpan"/>. Default: <c>1</c>.</summary>
    [Parameter] public int Columns { get; set; } = 1;

    /// <summary>Space between columns, as CSS, e.g. <c>24px</c>. Default: <c>--nx-form-column-gap</c>.</summary>
    [Parameter] public string? ColumnSpacing { get; set; }

    /// <summary>Labels above the editors (<see cref="NxOrientation.Vertical"/>, default) or beside them.</summary>
    [Parameter] public NxOrientation Orientation { get; set; } = NxOrientation.Vertical;

    /// <summary>Validators such as <c>&lt;FluentValidationValidator /&gt;</c>. Default: <c>&lt;DataAnnotationsValidator /&gt;</c>; leave empty for none.</summary>
    [Parameter] public RenderFragment? Validation { get; set; }

    /// <summary>The items, groups and any other markup.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The buttons row under the items. The form renders no buttons of its own.</summary>
    [Parameter] public RenderFragment? Buttons { get; set; }

    /// <summary>Enter in a text field submits the form, as browsers do. <c>false</c> turns that off. Default: <c>true</c>.</summary>
    [Parameter] public bool SubmitOnEnter { get; set; } = true;

    /// <summary>The form's <c>id</c>; generated editors get <c>{Id}-{Field}</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the form.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the form.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the <c>&lt;form&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Validates every field and shows the messages. Returns <c>true</c> when there are none.</summary>
    public bool IsValid() => editContext?.Validate() ?? true;

    internal string FormId => Id ?? generatedId;

    private string CssClass => string.Join(' ', new[]
    {
        "nx-form",
        Orientation == NxOrientation.Horizontal ? "nx-form-horizontal" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if ((Model is null) == !explicitContext)
            throw new InvalidOperationException("NxForm needs a Model or an EditContext, not both.");
        if (!explicitContext && (editContext is null || !ReferenceEquals(editContext.Model, Model)))
            editContext = new EditContext(Model!);
    }
}
