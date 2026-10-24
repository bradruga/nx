namespace Nx.Components;

/// <summary>
/// One dialog opened by <see cref="NxDialogService.Open"/>: its <see cref="Result"/>, and <see cref="Close"/> to end it from
/// code — from a timer, or from content that cascades it (<c>[CascadingParameter] NxDialogReference Dialog</c>).
/// </summary>
public sealed class NxDialogReference
{
    private readonly TaskCompletionSource<bool?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<NxDialogReference> onClosed;

    internal NxDialogReference(NxDialogOptions options, string id, Action<NxDialogReference> onClosed)
    {
        Options = options;
        Id = id;
        this.onClosed = onClosed;
    }

    /// <summary>The options it was opened with.</summary>
    public NxDialogOptions Options { get; }

    /// <summary>
    /// Completes when the dialog closes: <c>true</c> confirmed, <c>false</c> cancelled, <c>null</c> dismissed (close
    /// button, Escape, <see cref="Close"/> without a result).
    /// </summary>
    public Task<bool?> Result => result.Task;

    /// <summary><c>true</c> once the dialog has closed.</summary>
    public bool IsClosed => result.Task.IsCompleted;

    /// <summary>Closes the dialog with <paramref name="value"/> as its result, skipping the confirm checks. Safe from any thread.</summary>
    public void Close(bool? value = null)
    {
        if (result.TrySetResult(value))
            onClosed(this);
    }

    internal string Id { get; }

    /// <summary>The message <see cref="NxDialogOptions.Validate"/> returned, shown above the buttons.</summary>
    internal string? Error { get; set; }

    /// <summary>The confirm checks are running.</summary>
    internal bool Busy { get; set; }

    /// <summary>For a prompt: the text box's text.</summary>
    internal string? PromptText { get; set; }

    internal bool IsPrompt { get; init; }

    internal string? PromptPlaceholder { get; init; }
}
