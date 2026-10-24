using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// The validation messages of one field of the cascading <see cref="EditContext"/> (an <see cref="NxForm"/> or
/// <c>EditForm</c>), each with an alert icon, in <c>--nx-validation-color</c>. Name the field with <see cref="For"/>,
/// or with <see cref="Field"/> on the form's model. Renders an empty live region while the field is valid.
/// </summary>
/// <example>
/// <code>
/// &lt;NxValidationMessage For="() =&gt; order.PourDate" /&gt;
/// </code>
/// </example>
public partial class NxValidationMessage : IDisposable
{
    private EditContext? subscribed;
    private FieldIdentifier? field;
    private IReadOnlyList<string> messages = [];

    [CascadingParameter] private EditContext? EditContext { get; set; }

    /// <summary>The field, as a lambda: <c>For="() =&gt; order.PourDate"</c>.</summary>
    [Parameter] public Expression<Func<object?>>? For { get; set; }

    /// <summary>The field by property name, on <see cref="Model"/>. Used when <see cref="For"/> is not set.</summary>
    [Parameter] public string? Field { get; set; }

    /// <summary>The object that owns <see cref="Field"/>. Default: the form's model.</summary>
    [Parameter] public object? Model { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => string.Join(' ', new[]
    {
        "nx-validation",
        messages.Count > 0 ? "nx-validation-invalid" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(EditContext, subscribed))
        {
            if (subscribed is not null)
                subscribed.OnValidationStateChanged -= HandleValidationStateChanged;
            subscribed = EditContext;
            if (subscribed is not null)
                subscribed.OnValidationStateChanged += HandleValidationStateChanged;
        }

        field = For is not null ? FieldIdentifier.Create(For)
              : Field is not null && (Model ?? EditContext?.Model) is { } model ? new FieldIdentifier(model, Field)
              : null;
        messages = Read();
    }

    private IReadOnlyList<string> Read() =>
        EditContext is not null && field is { } f ? EditContext.GetValidationMessages(f).ToList() : [];

    private void HandleValidationStateChanged(object? sender, ValidationStateChangedEventArgs e)
    {
        var next = Read();
        if (next.SequenceEqual(messages))
            return;
        messages = next;
        StateHasChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (subscribed is not null)
            subscribed.OnValidationStateChanged -= HandleValidationStateChanged;
    }
}
