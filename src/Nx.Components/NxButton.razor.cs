using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Nx.Components;

/// <summary>
/// A button with variants, sizes, an icon slot, a loading state and a guard against double clicks.
/// Renders a native <c>&lt;button&gt;</c>, so Enter, Space and form submission behave as the browser does.
/// </summary>
/// <example>
/// <code>
/// &lt;NxButton Variant="NxButtonVariant.Primary" OnClick="SaveAsync"&gt;Save&lt;/NxButton&gt;
/// </code>
/// </example>
public partial class NxButton
{
    private ElementReference element;
    private bool isBusy;

    // ── Content ───────────────────────────────────────────────────────────────

    /// <summary>The button label.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Icon markup, usually an inline <c>&lt;svg&gt;</c>. With no <see cref="ChildContent"/> the button
    /// renders icon-only and square; give it an <see cref="AriaLabel"/>.
    /// </summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    /// <summary>Which side of the label the icon sits on. Default: <see cref="NxIconPosition.Start"/>.</summary>
    [Parameter] public NxIconPosition IconPosition { get; set; } = NxIconPosition.Start;

    /// <summary>Replaces the label while <see cref="Loading"/> is on, e.g. "Saving…". Optional.</summary>
    [Parameter] public string? LoadingText { get; set; }

    // ── Appearance ────────────────────────────────────────────────────────────

    /// <summary>Visual style. Default: <see cref="NxButtonVariant.Secondary"/>.</summary>
    [Parameter] public NxButtonVariant Variant { get; set; } = NxButtonVariant.Secondary;

    /// <summary>Height and padding. Default: <see cref="NxButtonSize.Medium"/>.</summary>
    [Parameter] public NxButtonSize Size { get; set; } = NxButtonSize.Medium;

    /// <summary>When <c>true</c>, the button stretches to the width of its container.</summary>
    [Parameter] public bool FullWidth { get; set; }

    /// <summary>Extra CSS class applied to the <c>&lt;button&gt;</c>.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the <c>&lt;button&gt;</c>.</summary>
    [Parameter] public string? Style { get; set; }

    // ── Behavior ──────────────────────────────────────────────────────────────

    /// <summary>The HTML <c>type</c>. Default: <see cref="NxButtonType.Button"/>, so it never submits by accident.</summary>
    [Parameter] public NxButtonType Type { get; set; } = NxButtonType.Button;

    /// <summary>Renders the native <c>disabled</c> attribute: not focusable, no clicks, no form submission.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Shows a spinner in place of the icon and blocks clicks and form submission. The button stays
    /// focusable and reports <c>aria-busy</c> and <c>aria-disabled</c>.
    /// </summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>
    /// While an async <see cref="OnClick"/> is still running, further clicks are ignored. Default: <c>true</c>.
    /// </summary>
    [Parameter] public bool PreventDoubleClick { get; set; } = true;

    /// <summary>
    /// Shows the loading state for as long as an async <see cref="OnClick"/> runs, without the caller
    /// toggling <see cref="Loading"/>. Requires <see cref="PreventDoubleClick"/>.
    /// </summary>
    [Parameter] public bool AutoLoading { get; set; }

    /// <summary>Raised on click. Not raised while disabled, loading, or busy with a previous click.</summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    // ── Accessibility ─────────────────────────────────────────────────────────

    /// <summary>Accessible name. Required for icon-only buttons.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Tooltip text (<c>title</c> attribute).</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Any other attributes (<c>id</c>, <c>aria-*</c>, <c>data-*</c>, <c>form</c>…) are passed to the <c>&lt;button&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary><c>true</c> while an async <see cref="OnClick"/> is running.</summary>
    public bool IsBusy => isBusy;

    /// <summary>Moves keyboard focus to the button.</summary>
    public ValueTask FocusAsync() => element.FocusAsync();

    private bool IsIconOnly => Icon is not null && ChildContent is null;
    private bool ShowLoading => Loading || (AutoLoading && isBusy);
    private bool Blocked => !Disabled && (ShowLoading || isBusy);

    private string TypeAttribute => Type switch
    {
        NxButtonType.Submit => "submit",
        NxButtonType.Reset  => "reset",
        _                   => "button"
    };

    private string CssClass
    {
        get
        {
            var variant = Variant switch
            {
                NxButtonVariant.Primary => "nx-button-primary",
                NxButtonVariant.Danger  => "nx-button-danger",
                NxButtonVariant.Ghost   => "nx-button-ghost",
                NxButtonVariant.Link    => "nx-button-link",
                _                       => "nx-button-secondary"
            };
            var size = Size switch
            {
                NxButtonSize.Small => "nx-button-sm",
                NxButtonSize.Large => "nx-button-lg",
                _                  => "nx-button-md"
            };
            return string.Join(' ', new[]
            {
                "nx-button", variant, size,
                IsIconOnly   ? "nx-button-icon-only"  : null,
                ShowLoading  ? "nx-button-loading"    : null,
                FullWidth    ? "nx-button-full-width" : null,
                Class
            }.Where(c => !string.IsNullOrEmpty(c)));
        }
    }

    private async Task HandleClick(MouseEventArgs args)
    {
        if (Disabled || Blocked || !OnClick.HasDelegate)
            return;

        if (!PreventDoubleClick)
        {
            await OnClick.InvokeAsync(args);
            return;
        }

        isBusy = true;
        try
        {
            await OnClick.InvokeAsync(args);
        }
        finally
        {
            isBusy = false;
        }
    }
}
