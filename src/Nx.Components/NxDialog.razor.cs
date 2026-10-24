using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// A short modal dialog: a title, your content and a row of <see cref="Buttons"/>. It is an <see cref="NxWindow"/> with a
/// dialog's defaults — 400 px wide, not draggable, a centered card even on a phone — so it keeps the window's focus
/// trap, Escape, scroll lock and stacking. For a message or a yes/no question from code, use <see cref="NxDialogService"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxDialog @@bind-Visible="showSubmit" Title="Submit schedule for review"&gt;
///     &lt;NxTextArea Label="Note for the reviewer" @@bind-Value="note" /&gt;
///     &lt;Buttons&gt;
///         &lt;NxButton Variant="NxButtonVariant.Primary" OnClick="SubmitAsync"&gt;Submit&lt;/NxButton&gt;
///         &lt;NxButton OnClick="() =&gt; showSubmit = false"&gt;Cancel&lt;/NxButton&gt;
///     &lt;/Buttons&gt;
/// &lt;/NxDialog&gt;
/// </code>
/// </example>
public partial class NxDialog
{
    /// <summary>Shows the dialog. Use <c>@bind-Visible</c>; the close button and Escape set it to <c>false</c>.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <summary>Raised when the dialog closes itself (close button, Escape, backdrop).</summary>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>Raised before the dialog closes itself; set <c>Cancel</c> to keep it open.</summary>
    [Parameter] public EventCallback<NxWindowClosingEventArgs> OnClosing { get; set; }

    /// <summary>The title.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>The content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The buttons, at the end of the footer (full width on a phone).</summary>
    [Parameter] public RenderFragment? Buttons { get; set; }

    /// <summary>Width as CSS, e.g. <c>460px</c>. Default: 400 px, never wider than the screen.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Height as CSS. Default: the content's.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Dims the page behind and keeps focus inside. Default: <c>true</c>.</summary>
    [Parameter] public bool Modal { get; set; } = true;

    /// <summary>Shows the close button; with it Escape closes too. Default: <c>true</c>.</summary>
    [Parameter] public bool Closable { get; set; } = true;

    /// <summary>Escape closes the dialog (when <see cref="Closable"/>). Default: <c>true</c>.</summary>
    [Parameter] public bool CloseOnEscape { get; set; } = true;

    /// <summary>A click on the backdrop closes the dialog.</summary>
    [Parameter] public bool CloseOnBackdropClick { get; set; }

    /// <summary>Lets the title bar move the dialog.</summary>
    [Parameter] public bool Draggable { get; set; }

    /// <summary>Lets the edges resize the dialog.</summary>
    [Parameter] public bool Resizable { get; set; }

    /// <summary>Shows the maximize button.</summary>
    [Parameter] public bool Maximizable { get; set; }

    /// <summary>Fills the screen at 600 px and below, for a dialog with a lot in it. Default: <c>false</c>, a centered card.</summary>
    [Parameter] public bool FullScreenOnPhone { get; set; }

    /// <summary>The dialog's <c>id</c>; the title is <c>{Id}-title</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the dialog.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Any other attributes (<c>aria-describedby</c>, <c>data-*</c>…) are passed to the dialog element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-dialog" : $"nx-dialog {Class}";
}
