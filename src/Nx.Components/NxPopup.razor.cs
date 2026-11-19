using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Any content in a floating box next to an element on the page (its anchor, found by <see cref="AnchorSelector"/>):
/// below, above or beside it, lined up at the start, center or end, flipping and shifting to stay on screen. Closes on
/// a press outside or Escape. For a filter menu, a details card, a small form. With <see cref="OpenOnHover"/> it is a
/// hover card: it opens when the pointer rests on an anchor and stays while the pointer is on it, so its links can be used.
/// </summary>
/// <example>
/// <code>
/// &lt;NxPopup @bind-Visible="filterOpen" AnchorSelector="#status-filter" Align="NxPopupAlign.End" Width="260px"&gt;
///     …
/// &lt;/NxPopup&gt;
/// </code>
/// </example>
public partial class NxPopup : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-anchor.js measures the anchor and the viewport, places the popup, follows scrolling and resizing, and reports
    // presses outside and Escape. Its state is keyed by the popup's id: the element is gone by the time it closes.
    // With OpenOnHover it also watches the anchors while the popup is closed, and says when to open and for which.

    private const string ModulePath = "./_content/Nx.Components/nx-anchor.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-popup-{Guid.NewGuid():N}"[..18];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxPopup>? dotNet;
    private ElementReference popup;
    private bool open;
    private (string, int, int)? watched;      // the hover watch as last set up: selector and delays

    /// <summary>Shows the popup. Use <c>@bind-Visible</c>; it turns false when the popup closes itself.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <summary>Raised when the popup closes itself (a press outside, Escape).</summary>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>A CSS selector for the element the popup opens next to, e.g. <c>#status-filter</c>. With <see cref="OpenOnHover"/> it may match many (<c>.job</c>): one popup serves them all.</summary>
    [Parameter, EditorRequired] public string AnchorSelector { get; set; } = "";

    /// <summary>The side of the anchor it opens on. Default: <see cref="NxPopupPlacement.Bottom"/>.</summary>
    [Parameter] public NxPopupPlacement Placement { get; set; }

    /// <summary>How it lines up with the anchor. Default: <see cref="NxPopupAlign.Start"/>.</summary>
    [Parameter] public NxPopupAlign Align { get; set; }

    /// <summary>Pixels between the anchor and the popup. Default: <c>4</c>.</summary>
    [Parameter] public int Offset { get; set; } = 4;

    /// <summary>The popup's width (any CSS width). Default: its content's.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>The popup's tallest; it scrolls beyond. Default: what fits on screen.</summary>
    [Parameter] public string? MaxHeight { get; set; }

    /// <summary>Closes on a press outside the popup and its anchor. Default: <c>true</c>.</summary>
    [Parameter] public bool CloseOnOutsideClick { get; set; } = true;

    /// <summary>Closes on Escape. Default: <c>true</c>.</summary>
    [Parameter] public bool CloseOnEscape { get; set; } = true;

    /// <summary>
    /// A hover card: opens when the pointer rests on an anchor (or the keyboard focuses one), stays open while the pointer
    /// is on the anchor or the popup, and closes soon after it leaves both. Use with <c>@bind-Visible</c>.
    /// </summary>
    [Parameter] public bool OpenOnHover { get; set; }

    /// <summary>With <see cref="OpenOnHover"/>, milliseconds the pointer must rest on an anchor. Default: <c>400</c>.</summary>
    [Parameter] public int ShowDelay { get; set; } = 400;

    /// <summary>With <see cref="OpenOnHover"/>, milliseconds before it closes once the pointer has left the anchor and the popup. Default: <c>250</c>.</summary>
    [Parameter] public int HideDelay { get; set; } = 250;

    /// <summary>With <see cref="OpenOnHover"/>, raised before it opens for an anchor, with that anchor's <c>data-nx-context</c>: set the content, or cancel.</summary>
    [Parameter] public EventCallback<NxPopupOpeningEventArgs> OnOpening { get; set; }

    /// <summary>The popup's ARIA role. Default: <c>dialog</c>.</summary>
    [Parameter] public string? Role { get; set; } = "dialog";

    /// <summary>The popup's accessible name.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The popup's <c>id</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the popup.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Any other attributes go to the popup.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Places the popup again, after its anchor moved without a scroll or resize.</summary>
    public async Task RefreshAsync()
    {
        if (module is not null && open)
            await module.InvokeVoidAsync("place", PopupId);
    }

    private string PopupId => Id ?? generatedId;
    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-popup" : $"nx-popup {Class}";

    private object Options => new
    {
        anchor = AnchorSelector,
        placement = Placement.ToString().ToLowerInvariant(),
        align = Align.ToString().ToLowerInvariant(),
        offset = Offset,
        width = Width,
        maxHeight = MaxHeight,
        closeOnOutside = CloseOnOutsideClick,
        closeOnEscape = CloseOnEscape,
        hover = OpenOnHover
    };

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        (string, int, int)? watch = OpenOnHover ? (AnchorSelector, ShowDelay, HideDelay) : null;
        if (watch != watched)
        {
            watched = watch;
            module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            dotNet ??= DotNetObjectReference.Create(this);
            if (module is not null)
            {
                if (watch is null)
                    await module.InvokeVoidAsync("unwatchHover", PopupId);
                else
                    await module.InvokeVoidAsync("watchHover", PopupId, dotNet, new { anchor = AnchorSelector, showDelay = ShowDelay, hideDelay = HideDelay });
            }
        }
        if (Visible == open && !Visible)
            return;
        module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;
        if (Visible)
        {
            dotNet ??= DotNetObjectReference.Create(this);
            // Every render while open: the content or the options may have changed its size or place.
            await module.InvokeVoidAsync(open ? "update" : "open", PopupId, popup, dotNet, Options);
            open = true;
        }
        else
        {
            open = false;
            await module.InvokeVoidAsync("close", PopupId);
        }
    }

    /// <summary>Called by nx-anchor.js when the pointer has rested on an anchor, or the keyboard focused one.</summary>
    [JSInvokable]
    public async Task<bool> OnHoverOpen(string? context)
    {
        var shown = false;
        await InvokeAsync(async () =>
        {
            var args = new NxPopupOpeningEventArgs(context);
            await OnOpening.InvokeAsync(args);
            if (args.Cancel)
                return;
            shown = true;
            var opened = !Visible;
            Visible = true;
            // The page hears first: a render of it before then would pass Visible back as false and close the popup under its content.
            if (opened)
                await VisibleChanged.InvokeAsync(true);
            StateHasChanged();
        });
        return shown;
    }

    /// <summary>Called by nx-anchor.js on a press outside, Escape, or the pointer leaving a hover card.</summary>
    [JSInvokable]
    public Task OnDismiss() => InvokeAsync(async () =>
    {
        if (!Visible)
            return;
        Visible = false;
        StateHasChanged();
        await VisibleChanged.InvokeAsync(false);
    });

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("unwatchHover", PopupId);
                await module.InvokeVoidAsync("close", PopupId);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        dotNet?.Dispose();
    }
}
