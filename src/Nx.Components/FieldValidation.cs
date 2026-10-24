using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// Connects an input component to its cascading <see cref="EditContext"/>: reports value changes,
/// exposes the validation CSS class, and re-renders the component when validation state changes.
/// Shared by <see cref="NxTextBox"/> and <see cref="NxComboBox{TItem, TValue}"/>.
/// </summary>
internal sealed class FieldValidation : IDisposable
{
    private readonly Action onStateChanged;
    private EditContext? editContext;
    private FieldIdentifier? fieldIdentifier;

    public FieldValidation(Action onStateChanged) => this.onStateChanged = onStateChanged;

    /// <summary>Call from <c>OnParametersSet</c>. Without both a context and an expression, validation is off.</summary>
    public void Attach<TValue>(EditContext? context, Expression<Func<TValue>>? valueExpression)
    {
        if (!ReferenceEquals(context, editContext))
        {
            if (editContext is not null)
                editContext.OnValidationStateChanged -= HandleValidationStateChanged;
            editContext = context;
            if (editContext is not null)
                editContext.OnValidationStateChanged += HandleValidationStateChanged;
        }

        fieldIdentifier = context is not null && valueExpression is not null
            ? FieldIdentifier.Create(valueExpression)
            : null;
    }

    /// <summary><c>true</c> when the field has validation messages.</summary>
    public bool IsInvalid => fieldIdentifier is { } f && editContext!.GetValidationMessages(f).Any();

    /// <summary>Blazor's <c>valid</c>/<c>invalid</c>/<c>modified</c> classes for the field, or empty.</summary>
    public string CssClass => fieldIdentifier is { } f ? editContext!.FieldCssClass(f) : "";

    /// <summary>Tells the form the value changed, which runs field validation.</summary>
    public void NotifyFieldChanged()
    {
        if (fieldIdentifier is { } f)
            editContext!.NotifyFieldChanged(f);
    }

    private void HandleValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) => onStateChanged();

    public void Dispose()
    {
        if (editContext is not null)
            editContext.OnValidationStateChanged -= HandleValidationStateChanged;
    }
}
