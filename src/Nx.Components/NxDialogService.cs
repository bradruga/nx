namespace Nx.Components;

/// <summary>
/// Dialogs from code: <see cref="AlertAsync"/>, <see cref="ConfirmAsync"/>, <see cref="PromptAsync"/>, any dialog with
/// <see cref="ShowAsync"/>, and a progress dialog. Register it with <c>services.AddNxDialogs()</c> and put one
/// <see cref="NxDialogHost"/> in the layout; the host renders what the service opens. Safe to call from any thread.
/// </summary>
/// <example>
/// <code>
/// @@inject NxDialogService Dialogs
///
/// if (await Dialogs.ConfirmAsync("Delete pour P-1042?", "Delete", confirmText: "Delete"))
///     await DeleteAsync();
/// </code>
/// </example>
public sealed class NxDialogService
{
    private readonly object gate = new();
    private readonly List<NxDialogReference> dialogs = [];
    private readonly List<NxDialogProgress> progresses = [];
    private int nextId;

    /// <summary>Raised when a dialog opens or closes, or a progress dialog changes. For <see cref="NxDialogHost"/>.</summary>
    internal event Action? Changed;

    /// <summary>A <see cref="NxDialogHost"/> is listening.</summary>
    internal bool HasHost => Changed is not null;

    /// <summary>The dialogs open now, oldest first.</summary>
    public IReadOnlyList<NxDialogReference> OpenDialogs
    {
        get
        {
            lock (gate)
                return dialogs.ToList();
        }
    }

    internal IReadOnlyList<NxDialogProgress> Progresses()
    {
        lock (gate)
            return progresses.Where(p => p.IsVisible).ToList();
    }

    /// <summary>Opens a dialog and returns at once; await <see cref="NxDialogReference.Result"/>, or close it from code.</summary>
    public NxDialogReference Open(NxDialogOptions options) => OpenCore(options, isPrompt: false, placeholder: null, text: null);

    /// <summary>Shows a dialog: <c>true</c> confirmed, <c>false</c> cancelled, <c>null</c> dismissed.</summary>
    public Task<bool?> ShowAsync(NxDialogOptions options) => Open(options).Result;

    /// <summary>Shows a message with an OK button; completes when it is closed.</summary>
    public Task AlertAsync(string message, string? title = null, string confirmText = "OK") =>
        ShowAsync(new NxDialogOptions { Title = title, Message = message, ConfirmText = confirmText });

    /// <summary>Asks a question: <c>true</c> for the confirm button, <c>false</c> for cancel, the close button or Escape.</summary>
    public async Task<bool> ConfirmAsync(string message, string? title = null, string confirmText = "OK", string cancelText = "Cancel",
        NxButtonVariant confirmVariant = NxButtonVariant.Primary) =>
        await ShowAsync(new NxDialogOptions
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText,
            CancelText = cancelText,
            ConfirmVariant = confirmVariant,
            ShowCancelButton = true
        }) == true;

    /// <summary>Asks for a line of text: what was typed on OK (or Enter), <c>null</c> on cancel, close or Escape.</summary>
    public async Task<string?> PromptAsync(string message, string? title = null, string? value = null, string? placeholder = null,
        string confirmText = "OK", string cancelText = "Cancel")
    {
        var options = new NxDialogOptions
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText,
            CancelText = cancelText,
            ShowCancelButton = true
        };
        var dialog = OpenCore(options, isPrompt: true, placeholder, value);
        return await dialog.Result == true ? dialog.PromptText ?? "" : null;
    }

    /// <summary>A "working…" dialog with a spinner, shown at the first report and closed by <c>Close</c> or <c>Dispose</c>.</summary>
    public NxDialogProgress CreateProgress(string title = "Working…")
    {
        lock (gate)
        {
            var progress = new NxDialogProgress(title, $"nx-dialog-progress-{++nextId}", OnProgressChanged);
            progresses.Add(progress);
            return progress;
        }
    }

    private NxDialogReference OpenCore(NxDialogOptions options, bool isPrompt, string? placeholder, string? text)
    {
        if (!HasHost)
            throw new InvalidOperationException("NxDialogService has no NxDialogHost to show dialogs in: put <NxDialogHost /> in the layout.");
        NxDialogReference dialog;
        lock (gate)
        {
            dialog = new NxDialogReference(options, options.Id ?? $"nx-dialog-{++nextId}", OnClosed)
            {
                IsPrompt = isPrompt,
                PromptPlaceholder = placeholder,
                PromptText = text
            };
            dialogs.Add(dialog);
        }
        Changed?.Invoke();
        return dialog;
    }

    private void OnClosed(NxDialogReference dialog)
    {
        lock (gate)
            dialogs.Remove(dialog);
        Changed?.Invoke();
    }

    private void OnProgressChanged(NxDialogProgress progress)
    {
        if (progress.IsClosed)
        {
            lock (gate)
                progresses.Remove(progress);
        }
        Changed?.Invoke();
    }
}

