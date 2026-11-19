using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>One pane of an <see cref="NxSplitter"/>. Panes without a <see cref="Size"/> share the space that is left.</summary>
public partial class NxSplitterPane : IDisposable
{
    private readonly string generatedId = $"nx-pane-{Guid.NewGuid():N}"[..17];
    private string? size;
    private string? lastSize;
    private bool collapsed;
    private bool? lastCollapsed;
    private (string?, bool) rendered;

    [CascadingParameter] private NxSplitter? Splitter { get; set; }

    /// <summary>Width (or height, when stacked) as a CSS length: <c>"30%"</c>, <c>"240px"</c>. Use <c>@bind-Size</c>. None: takes the rest.</summary>
    [Parameter] public string? Size { get; set; }

    /// <summary>Raised when the user resizes the pane, in the unit it was given (% stays %, else px).</summary>
    [Parameter] public EventCallback<string?> SizeChanged { get; set; }

    /// <summary>Smallest size while resizing, <c>"120px"</c> or <c>"10%"</c>.</summary>
    [Parameter] public string? Min { get; set; }

    /// <summary>Largest size while resizing.</summary>
    [Parameter] public string? Max { get; set; }

    /// <summary>Whether the bar next to this pane resizes it. Default: <c>true</c>.</summary>
    [Parameter] public bool Resizable { get; set; } = true;

    /// <summary>Whether the pane is collapsed to nothing, its content kept. The bar beside it hides too. The splitter draws no button for it: bind it (<c>@bind-Collapsed</c>) to your own.</summary>
    [Parameter] public bool Collapsed { get; set; }

    /// <summary>Raised when <see cref="ToggleAsync"/> collapses or expands the pane.</summary>
    [Parameter] public EventCallback<bool> CollapsedChanged { get; set; }

    /// <summary>Name of the pane: the bar's accessible name and, on phones, the header over it.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>The pane's content. It scrolls when larger than the pane.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The pane's <c>id</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the pane.</summary>
    [Parameter] public string? Class { get; set; }

    internal string PaneId => Id ?? generatedId;
    internal bool IsCollapsed => collapsed;
    private string ContentId => $"{PaneId}-content";
    private int Index => Splitter!.IndexOf(this);

    private string PaneClass => string.Join(' ', new[]
    {
        "nx-splitter-pane",
        size is null ? "nx-splitter-pane-flexible" : null,
        collapsed ? "nx-splitter-pane-collapsed" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private string PaneStyle
    {
        get
        {
            if (collapsed)
                return "flex: 0 0 0px";
            var (minProp, maxProp) = Splitter!.Horizontal ? ("min-width", "max-width") : ("min-height", "max-height");
            var style = size is null ? "flex: 1 1 0px" : $"flex: 0 0 {size}";
            if (Min is not null) style += $"; {minProp}: {Min}";
            if (Max is not null) style += $"; {maxProp}: {Max}";
            return style;
        }
    }

    /// <summary>A bar moves when neither neighbour is collapsed and the pane it would resize is resizable.</summary>
    private bool BarMovable(NxSplitterPane? prev)
    {
        if (prev is null || prev.collapsed || collapsed)
            return false;
        var resized = prev.size is not null || size is null ? prev : this;
        return resized.Resizable;
    }

    /// <summary>A bar beside a collapsed pane is hidden with it: there is nothing for it to resize.</summary>
    private string BarClass(NxSplitterPane? prev) =>
        prev is { collapsed: true } || collapsed ? "nx-splitter-bar nx-splitter-bar-fixed nx-splitter-bar-hidden"
        : BarMovable(prev) ? "nx-splitter-bar" : "nx-splitter-bar nx-splitter-bar-fixed";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Splitter is null)
            throw new InvalidOperationException("NxSplitterPane must be placed inside an NxSplitter.");
        Splitter.Register(this);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Only an outside change replaces what a drag or a button set.
        if (Size != lastSize)
        {
            lastSize = Size;
            size = Size;
        }
        if (lastCollapsed != Collapsed)
        {
            lastCollapsed = Collapsed;
            collapsed = Collapsed;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Removed while its start-up calls were still on their way: the module or the circuit is gone, nothing to set up.
        try { await AfterRenderAsync(firstRender); }
        catch (Exception gone) when (gone is JSDisconnectedException or ObjectDisposedException or OperationCanceledException) { }
    }

    private async Task AfterRenderAsync(bool firstRender)
    {
        if (rendered != (size, collapsed))
        {
            rendered = (size, collapsed);
            await Splitter!.AfterPaneRenderAsync();
        }
    }

    internal void Refresh() => StateHasChanged();

    // ── Actions ───────────────────────────────────────────────────────────────

    internal async Task ResizeAsync(string newSize)
    {
        size = newSize;
        lastSize = newSize;
        StateHasChanged();
        await SizeChanged.InvokeAsync(newSize);
    }

    /// <summary>Collapses or expands the pane, for a button of your own.</summary>
    public async Task ToggleAsync()
    {
        collapsed = !collapsed;
        await CollapsedChanged.InvokeAsync(collapsed);
        Splitter!.RefreshPanes();
    }

    /// <inheritdoc />
    public void Dispose() => Splitter?.Unregister(this);
}
