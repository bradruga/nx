using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>What <see cref="NxDialogService.ShowAsync"/> shows: the title, content, buttons, size and checks of one dialog.</summary>
public sealed class NxDialogOptions
{
    /// <summary>The title.</summary>
    public string? Title { get; set; }

    /// <summary>A plain-text message; line breaks (<c>\n</c>) show. Rendered as text, never as markup.</summary>
    public string? Message { get; set; }

    /// <summary>Content under the message: a form, a grid, any markup.</summary>
    public RenderFragment? Content { get; set; }

    /// <summary>Content at the start of the button row, e.g. a "Don't ask again" check box.</summary>
    public RenderFragment? Footer { get; set; }

    /// <summary>Shows the confirm button. Default: <c>true</c>.</summary>
    public bool ShowConfirmButton { get; set; } = true;

    /// <summary>The confirm button's text. Default: "OK".</summary>
    public string ConfirmText { get; set; } = "OK";

    /// <summary>The confirm button's look. Default: <see cref="NxButtonVariant.Primary"/>; <see cref="NxButtonVariant.Danger"/> for a delete.</summary>
    public NxButtonVariant ConfirmVariant { get; set; } = NxButtonVariant.Primary;

    /// <summary>Shows the cancel button, which closes with <c>false</c>.</summary>
    public bool ShowCancelButton { get; set; }

    /// <summary>The cancel button's text. Default: "Cancel".</summary>
    public string CancelText { get; set; } = "Cancel";

    /// <summary>Shows the close button; it, Escape and <see cref="NxDialogReference.Close"/> without a result close with <c>null</c>. Default: <c>true</c>.</summary>
    public bool Closable { get; set; } = true;

    /// <summary>
    /// Runs on confirm before anything else; <c>false</c> keeps the dialog open (show your own message). The confirm
    /// button shows as busy while it runs.
    /// </summary>
    public Func<Task<bool>>? ValidateAsync { get; set; }

    /// <summary>Runs on confirm after <see cref="ValidateAsync"/>: a message keeps the dialog open and is shown above the buttons; <c>null</c> or empty passes.</summary>
    public Func<string?>? Validate { get; set; }

    /// <summary>Runs last on confirm, e.g. to save; <c>false</c> keeps the dialog open.</summary>
    public Func<Task<bool>>? OnConfirmAsync { get; set; }

    /// <summary>Width as CSS, e.g. <c>800px</c> or <c>90vw</c>. Default: 400 px, or the content's width with <see cref="MaxWidth"/>.</summary>
    public string? Width { get; set; }

    /// <summary>Height as CSS. Default: the content's.</summary>
    public string? Height { get; set; }

    /// <summary>The widest the content grows, as CSS; the dialog then sizes to its content up to this.</summary>
    public string? MaxWidth { get; set; }

    /// <summary>The tallest the content grows, as CSS; past it the content scrolls.</summary>
    public string? MaxHeight { get; set; }

    /// <summary>The content fills the dialog's height (a grid in a dialog of a set <see cref="Height"/>).</summary>
    public bool FillContent { get; set; }

    /// <summary>Dims the page behind and keeps focus inside. Default: <c>true</c>.</summary>
    public bool Modal { get; set; } = true;

    /// <summary>Lets the title bar move the dialog.</summary>
    public bool Draggable { get; set; }

    /// <summary>Lets the edges resize the dialog.</summary>
    public bool Resizable { get; set; }

    /// <summary>Shows the maximize button.</summary>
    public bool Maximizable { get; set; }

    /// <summary>Fills the screen on a phone, for a dialog with a lot in it. Default: <c>false</c>, a centered card.</summary>
    public bool FullScreenOnPhone { get; set; }

    /// <summary>The dialog's <c>id</c>; its title is <c>{Id}-title</c>, its confirm button <c>{Id}-confirm</c>, cancel <c>{Id}-cancel</c>.</summary>
    public string? Id { get; set; }

    /// <summary>Extra CSS class on the dialog.</summary>
    public string? Class { get; set; }
}
