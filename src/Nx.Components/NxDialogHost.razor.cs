using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Nx.Components;

/// <summary>
/// Renders the dialogs <see cref="NxDialogService"/> opens. Put one in the layout, after the page content. Content
/// inside a dialog can take the dialog as <c>[CascadingParameter] NxDialogReference</c> to close it.
/// </summary>
/// <example>
/// <code>
/// @@Body
/// &lt;NxDialogHost /&gt;
/// </code>
/// </example>
public partial class NxDialogHost
{
    [Inject] private NxDialogService Dialogs { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized() => Dialogs.Changed += HandleChanged;

    private void HandleChanged() => _ = InvokeAsync(StateHasChanged);

    private static string ContentClass(NxDialogOptions options) =>
        options.FillContent ? "nx-dialog-content nx-dialog-content-fill" : "nx-dialog-content";

    private static string? ContentStyle(NxDialogOptions options)
    {
        var parts = new List<string>();
        if (options.MaxWidth is { } maxWidth)
            parts.Add($"max-width: {maxWidth}");
        if (options.MaxHeight is { } maxHeight)
            parts.Add($"max-height: {maxHeight}; overflow: auto");
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>The close button, Escape or the backdrop: dismissed, so <c>null</c>.</summary>
    private static void HandleVisibleChanged(NxDialogReference dialog, bool visible)
    {
        if (!visible)
            dialog.Close();
    }

    /// <summary>Enter in a prompt's text box confirms, as a one-field form would.</summary>
    private async Task HandleContentKeyDown(NxDialogReference dialog, KeyboardEventArgs e)
    {
        if (dialog.IsPrompt && e.Key == "Enter")
            await ConfirmAsync(dialog);
    }

    /// <summary>The confirm checks in order — ValidateAsync, Validate, OnConfirmAsync — then closes with <c>true</c>.</summary>
    private async Task ConfirmAsync(NxDialogReference dialog)
    {
        if (dialog.Busy || dialog.IsClosed)
            return;
        var options = dialog.Options;
        dialog.Busy = true;
        dialog.Error = null;
        try
        {
            if (options.ValidateAsync is not null && !await options.ValidateAsync())
                return;
            if (options.Validate?.Invoke() is { Length: > 0 } message)
            {
                dialog.Error = message;
                return;
            }
            if (options.OnConfirmAsync is not null && !await options.OnConfirmAsync())
                return;
            dialog.Close(true);
        }
        finally
        {
            dialog.Busy = false;
        }
    }

    /// <summary>Stops listening, and dismisses what is still open so no caller waits for ever.</summary>
    public void Dispose()
    {
        Dialogs.Changed -= HandleChanged;
        foreach (var dialog in Dialogs.OpenDialogs)
            dialog.Close();
    }
}
