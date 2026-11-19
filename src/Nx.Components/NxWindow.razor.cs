using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A dialog window, modal or not: title with icon, close / minimize / maximize, dragging by the title bar,
/// resizing from the edges, size presets or explicit size, centered or positioned, focus trap and focus
/// return, Escape and backdrop close, stacking of nested windows, and body and footer slots. Full screen on
/// phones. Show and hide it with <c>@bind-Visible</c>. With an <see cref="NxWindowHost"/> in the layout the window is drawn
/// there, clear of any transformed or clipping ancestor; see <see cref="Inline"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxWindow @bind-Visible="editing" Title="Edit pour"&gt;
///     &lt;ChildContent&gt;…&lt;/ChildContent&gt;
///     &lt;Footer&gt;&lt;NxButton OnClick="SaveAsync" Variant="NxButtonVariant.Primary"&gt;Save&lt;/NxButton&gt;&lt;/Footer&gt;
/// &lt;/NxWindow&gt;
/// </code>
/// </example>
public partial class NxWindow : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-window.js places the window, drags and resizes it, stacks windows, traps Tab, catches Escape,
    // locks page scroll and returns focus on close. The window's state (visible, minimized, maximized) is C#.

    private const string ModulePath = "./_content/Nx.Components/nx-window.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    private readonly string generatedId = $"nx-window-{Guid.NewGuid():N}"[..20];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxWindow>? selfReference;
    private ElementReference window;
    private ElementReference backdrop;
    private bool minimized, maximized;
    private bool? lastMinimized, lastMaximized;
    private bool jsOpen, openPending, closePending;
    private bool? renderedModal;
    private object? appliedOptions;
    private NxWindowHost? host;
    private NxWindowOutlet? outlet;
    private bool drawingInHost, shownInHost;

    // ── Visibility ────────────────────────────────────────────────────────────

    /// <summary>Whether the window is shown. Use <c>@bind-Visible</c>.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <summary>Raised when the user closes the window (close button, Escape, backdrop).</summary>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>Raised before the user closes the window; set <see cref="NxWindowClosingEventArgs.Cancel"/> to keep it open.</summary>
    [Parameter] public EventCallback<NxWindowClosingEventArgs> OnClosing { get; set; }

    /// <summary>Blocks the page behind with a backdrop, traps focus and locks page scroll. Default: <c>true</c>.</summary>
    [Parameter] public bool Modal { get; set; } = true;

    /// <summary>Closes on <kbd>Escape</kbd>. Default: <c>true</c>.</summary>
    [Parameter] public bool CloseOnEscape { get; set; } = true;

    /// <summary>Closes on a click on the backdrop of a modal window.</summary>
    [Parameter] public bool CloseOnBackdropClick { get; set; }

    // ── Title bar ─────────────────────────────────────────────────────────────

    /// <summary>Title text; also the dialog's accessible name.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Rich title content, in place of <see cref="Title"/>.</summary>
    [Parameter] public RenderFragment? TitleContent { get; set; }

    /// <summary>Icon before the title.</summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    /// <summary>Extra buttons in the title bar, before minimize, maximize and close.</summary>
    [Parameter] public RenderFragment? HeaderActions { get; set; }

    /// <summary>Shows the close button. Default: <c>true</c>.</summary>
    [Parameter] public bool Closable { get; set; } = true;

    /// <summary>Shows the minimize button: the window shrinks to its title bar.</summary>
    [Parameter] public bool Minimizable { get; set; }

    /// <summary>Shows the maximize / restore button; a double click on the title bar does the same.</summary>
    [Parameter] public bool Maximizable { get; set; }

    /// <summary>Whether the window is shrunk to its title bar. Use <c>@bind-Minimized</c>, or leave it to the button.</summary>
    [Parameter] public bool Minimized { get; set; }

    /// <summary>Raised when the minimize button changes <see cref="Minimized"/>.</summary>
    [Parameter] public EventCallback<bool> MinimizedChanged { get; set; }

    /// <summary>Whether the window fills the viewport. Use <c>@bind-Maximized</c>, or leave it to the button.</summary>
    [Parameter] public bool Maximized { get; set; }

    /// <summary>Raised when the maximize button or a title-bar double click changes <see cref="Maximized"/>.</summary>
    [Parameter] public EventCallback<bool> MaximizedChanged { get; set; }

    // ── Content ───────────────────────────────────────────────────────────────

    /// <summary>The body. It scrolls when taller than the window.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The footer, usually action buttons; aligned to the end.</summary>
    [Parameter] public RenderFragment? Footer { get; set; }

    // ── Size and position ─────────────────────────────────────────────────────

    /// <summary>Width preset. Default: <see cref="NxWindowSize.Medium"/>.</summary>
    [Parameter] public NxWindowSize Size { get; set; } = NxWindowSize.Medium;

    /// <summary>Explicit width, any CSS length (<c>"720px"</c>, <c>"80vw"</c>); overrides <see cref="Size"/>.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Explicit height, any CSS length. Default: as tall as the content, up to the viewport.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Distance from the viewport's left edge, any CSS length. With neither Left nor Top the window is centered.</summary>
    [Parameter] public string? Left { get; set; }

    /// <summary>Distance from the viewport's top edge, any CSS length.</summary>
    [Parameter] public string? Top { get; set; }

    /// <summary>Raised when the user finishes dragging the window, or a resize moved its left or top edge. Save it to pass back as <see cref="Left"/> and <see cref="Top"/>.</summary>
    [Parameter] public EventCallback<NxWindowMovedEventArgs> OnMoved { get; set; }

    /// <summary>Raised when the user finishes resizing the window. Save it to pass back as <see cref="Width"/> and <see cref="Height"/>.</summary>
    [Parameter] public EventCallback<NxWindowResizedEventArgs> OnResized { get; set; }

    /// <summary>Lets the user move the window by its title bar. Default: <c>true</c>.</summary>
    [Parameter] public bool Draggable { get; set; } = true;

    /// <summary>Lets the user resize the window from its edges and corners.</summary>
    [Parameter] public bool Resizable { get; set; }

    /// <summary>Smallest width while resizing, in px. Default: <c>240</c>.</summary>
    [Parameter] public int MinWidth { get; set; } = 240;

    /// <summary>Smallest height while resizing, in px. Default: <c>120</c>.</summary>
    [Parameter] public int MinHeight { get; set; } = 120;

    /// <summary>Fills the screen at 600 px and below. <c>false</c> keeps it a centered card, for a short dialog. Default: <c>true</c>.</summary>
    [Parameter] public bool FullScreenOnPhone { get; set; } = true;

    /// <summary>
    /// Keeps the window where it is declared even when the layout has an <see cref="NxWindowHost"/>. Content drawn in the host
    /// loses the cascading values (an <c>EditContext</c> from an <c>EditForm</c> around the window), the inherited CSS and the
    /// event bubbling of the place it was declared in; an inline window keeps them.
    /// </summary>
    [Parameter] public bool Inline { get; set; }

    // ── Attributes ────────────────────────────────────────────────────────────

    /// <summary>The window's <c>id</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the window.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Any other attributes (<c>aria-describedby</c>, <c>data-*</c>…) are passed to the window element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Closes the window as the close button does: <see cref="OnClosing"/> first, then <see cref="VisibleChanged"/>.</summary>
    public async Task CloseAsync()
    {
        if (!Visible)
            return;
        var args = new NxWindowClosingEventArgs();
        await OnClosing.InvokeAsync(args);
        if (args.Cancel)
            return;
        Visible = false;
        closePending = jsOpen;
        await VisibleChanged.InvokeAsync(false);
        StateHasChanged();
    }

    /// <summary>Moves the window back to the middle of the viewport.</summary>
    public async Task CenterAsync()
    {
        if (module is not null && jsOpen)
            await module.InvokeVoidAsync("center", WindowId);
    }

    /// <summary>Puts the window above every other open window.</summary>
    public async Task BringToFrontAsync()
    {
        if (module is not null && jsOpen)
            await module.InvokeVoidAsync("front", WindowId);
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string WindowId => Id ?? generatedId;
    private string TitleId => $"{WindowId}-title";

    private string CssClass => string.Join(' ', new[]
    {
        "nx-window",
        Width is null ? $"nx-window-{Size.ToString().ToLowerInvariant()}" : null,
        Modal ? "nx-window-modal" : null,
        maximized && !minimized ? "nx-window-maximized" : null,
        minimized ? "nx-window-minimized" : null,
        Resizable ? "nx-window-resizable" : null,
        Draggable ? "nx-window-draggable" : null,
        FullScreenOnPhone ? null : "nx-window-compact",
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private string BackdropClass => minimized ? "nx-window-backdrop nx-window-backdrop-hidden" : "nx-window-backdrop";

    private object Options => new
    {
        modal = Modal && !minimized,
        trapFocus = Modal && !minimized,
        draggable = Draggable,
        closeOnEscape = CloseOnEscape && Closable,
        width = Width,
        height = Height,
        left = Left,
        top = Top,
        minWidth = MinWidth,
        minHeight = MinHeight
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Only an outside change replaces the state the buttons set.
        if (lastMinimized != Minimized)
        {
            lastMinimized = Minimized;
            minimized = Minimized;
        }
        if (lastMaximized != Maximized)
        {
            lastMaximized = Maximized;
            maximized = Maximized;
        }
        // Without AddNxDialogs or a host in the layout the window is drawn in place. An open window that changes
        // place gets new elements, so it is closed and opened again in JS.
        var target = Inline ? null : Services.GetService<NxWindowHostState>()?.Host;
        if (target != host)
        {
            host?.Remove(this);
            host = target;
            host?.Add(this);
            outlet = null;
            shownInHost = false;
            if (jsOpen)
            {
                closePending = true;
                openPending = Visible;
            }
        }
        if (Visible && !jsOpen)
            openPending = true;
        else if (!Visible && jsOpen)
            closePending = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!openPending && !closePending && !jsOpen)
            return;
        module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;

        if (closePending)
        {
            closePending = false;
            jsOpen = false;
            renderedModal = null;
            await module.InvokeVoidAsync("close", WindowId);
        }
        if (openPending && Visible)
        {
            openPending = false;
            selfReference ??= DotNetObjectReference.Create(this);
            var options = Options;
            await module.InvokeVoidAsync("open", WindowId, window, Modal ? backdrop : (object?)null, selfReference, options);
            appliedOptions = options;
            renderedModal = Modal;
            jsOpen = true;
            return;
        }
        if (!jsOpen)
            return;
        if (renderedModal != Modal)
        {
            renderedModal = Modal;
            await module.InvokeVoidAsync("rebind", WindowId, window, Modal ? backdrop : (object?)null);
        }
        var current = Options;
        if (!Equals(current, appliedOptions))
        {
            appliedOptions = current;
            await module.InvokeVoidAsync("update", WindowId, current);
        }
    }

    /// <summary>Called by nx-window.js on Escape. Not for application use.</summary>
    [JSInvokable]
    public Task OnEscape() => InvokeAsync(CloseAsync);

    /// <summary>Called by nx-window.js when a drag or resize moved the window. Not for application use.</summary>
    [JSInvokable]
    public Task NotifyMoved(int left, int top) => InvokeAsync(() => OnMoved.InvokeAsync(new NxWindowMovedEventArgs(left, top)));

    /// <summary>Called by nx-window.js when a resize ends. Not for application use.</summary>
    [JSInvokable]
    public Task NotifyResized(int width, int height) => InvokeAsync(() => OnResized.InvokeAsync(new NxWindowResizedEventArgs(width, height)));

    /// <summary>The host gave the window its place there.</summary>
    internal void Attach(NxWindowOutlet place) => outlet = place;

    // Opening and closing add and remove the outlet, which is the host's to render; in between only the outlet
    // is redrawn. Redrawing the whole host instead would never end for a window declared inside another: the outer
    // one's content would hand the inner one new parameters, and that one would ask for the host again.
    private void RefreshHost()
    {
        if (Visible != shownInHost)
        {
            shownInHost = Visible;
            if (!Visible)
                outlet = null;
            host!.Refresh();
        }
        else if (Visible)
        {
            outlet?.Refresh();
        }
    }

    /// <summary>The window's markup, for its outlet in <see cref="NxWindowHost"/>.</summary>
    internal void RenderInHost(RenderTreeBuilder builder)
    {
        drawingInHost = true;
        try
        {
            BuildRenderTree(builder);
        }
        finally
        {
            drawingInHost = false;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private Task HandleCloseClick() => CloseAsync();

    private async Task HandleBackdropClick()
    {
        if (CloseOnBackdropClick)
            await CloseAsync();
    }

    private async Task HandleTitleDoubleClick(MouseEventArgs e)
    {
        if (Maximizable && !minimized)
            await ToggleMaximizeAsync();
    }

    private async Task ToggleMaximizeAsync()
    {
        maximized = !maximized;
        await MaximizedChanged.InvokeAsync(maximized);
    }

    private async Task ToggleMinimizeAsync()
    {
        minimized = !minimized;
        await MinimizedChanged.InvokeAsync(minimized);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        host?.Remove(this);
        if (module is not null)
        {
            try
            {
                if (jsOpen)
                    await module.InvokeVoidAsync("close", WindowId);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        selfReference?.Dispose();
    }
}
