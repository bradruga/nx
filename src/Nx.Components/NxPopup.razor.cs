using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Any content in a floating box next to an element on the page (its anchor, found by <see cref="AnchorSelector"/>):
/// below, above or beside it, lined up at the start, center or end, flipping and shifting to stay on screen. Closes on
/// a press outside or Escape. For a filter menu, a details card, a small form.
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

    private const string ModulePath = "./_content/Nx.Components/nx-anchor.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-popup-{Guid.NewGuid():N}"[..18];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxPopup>? dotNet;
    private ElementReference popup;
    private bool open;

    /// <summary>Shows the popup. Use <c>@bind-Visible</c>; it turns false when the popup closes itself.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <summary>Raised when the popup closes itself (a press outside, Escape).</summary>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>A CSS selector for the element the popup opens next to, e.g. <c>#status-filter</c>.</summary>
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
        closeOnEscape = CloseOnEscape
    };

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
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

    /// <summary>Called by nx-anchor.js on a press outside or Escape.</summary>
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
                await module.InvokeVoidAsync("close", PopupId);
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        dotNet?.Dispose();
    }
}
