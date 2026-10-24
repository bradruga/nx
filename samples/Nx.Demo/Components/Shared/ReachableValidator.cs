using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Nx.Demo.Data;

namespace Nx.Demo.Components.Shared;

/// <summary>A hand-made validator for the NxForm demo: a <see cref="ContactForm"/> needs a phone or an email.</summary>
public sealed class ReachableValidator : ComponentBase, IDisposable
{
    private ValidationMessageStore? store;
    private EditContext? context;

    [CascadingParameter] private EditContext? EditContext { get; set; }

    protected override void OnInitialized()
    {
        context = EditContext ?? throw new InvalidOperationException("ReachableValidator needs a cascading EditContext.");
        store = new ValidationMessageStore(context);
        context.OnValidationRequested += Validate;
        context.OnFieldChanged += FieldChanged;
    }

    private void Validate(object? sender, ValidationRequestedEventArgs e) => Check();

    private void FieldChanged(object? sender, FieldChangedEventArgs e) => Check();

    private void Check()
    {
        var model = (ContactForm)context!.Model;
        store!.Clear();
        if (string.IsNullOrWhiteSpace(model.Phone) && string.IsNullOrWhiteSpace(model.Email))
        {
            store.Add(context.Field(nameof(ContactForm.Phone)), "Give a phone or an email.");
            store.Add(context.Field(nameof(ContactForm.Email)), "Give a phone or an email.");
        }
        context.NotifyValidationStateChanged();
    }

    public void Dispose()
    {
        if (context is null)
            return;
        context.OnValidationRequested -= Validate;
        context.OnFieldChanged -= FieldChanged;
    }
}
