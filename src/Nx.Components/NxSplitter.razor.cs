using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Two or more <see cref="NxSplitterPane"/>s side by side or stacked, with draggable split bars between them:
/// sizes in px or %, min and max, collapsible panes, <c>@bind-Size</c> per pane, keyboard resizing and nesting.
/// A pane collapses through <c>@bind-Collapsed</c>, from a button of your own. On phones the panes stack, each under its title.
/// </summary>
/// <example>
/// <code>
/// &lt;NxSplitter Style="height: 480px"&gt;
///     &lt;NxSplitterPane @bind-Size="listWidth" Min="200px" @bind-Collapsed="listHidden" Title="Jobs"&gt;…&lt;/NxSplitterPane&gt;
///     &lt;NxSplitterPane Title="Details"&gt;…&lt;/NxSplitterPane&gt;
/// &lt;/NxSplitter&gt;
/// </code>
/// </example>
public partial class NxSplitter : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-splitter.js drags the bars and moves them with the arrow keys, then reports the changed pane's
    // final size here; it also keeps each bar's aria-valuenow current. Sizes and collapsing are C#.

    private const string ModulePath = "./_content/Nx.Components/nx-splitter.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly List<NxSplitterPane> panes = [];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxSplitter>? selfReference;
    private ElementReference root;
    private bool refreshQueued;

    /// <summary>The <see cref="NxSplitterPane"/> children.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary><see cref="NxOrientation.Horizontal"/> (default): panes side by side. <see cref="NxOrientation.Vertical"/>: stacked.</summary>
    [Parameter] public NxOrientation Orientation { get; set; } = NxOrientation.Horizontal;

    /// <summary>The outer element's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element. Give the splitter a height, e.g. <c>"height: 480px"</c>.</summary>
    [Parameter] public string? Style { get; set; }

    internal bool Horizontal => Orientation == NxOrientation.Horizontal;
    private string OrientationName => Horizontal ? "horizontal" : "vertical";

    private string CssClass => string.Join(' ', new[]
    {
        "nx-splitter",
        $"nx-splitter-{OrientationName}",
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    // ── Panes ─────────────────────────────────────────────────────────────────

    internal void Register(NxSplitterPane pane)
    {
        panes.Add(pane);
        RefreshPanes();
    }

    internal void Unregister(NxSplitterPane pane)
    {
        if (panes.Remove(pane))
            RefreshPanes();
    }

    internal int IndexOf(NxSplitterPane pane) => panes.IndexOf(pane);

    internal NxSplitterPane? PaneAt(int index) => index >= 0 && index < panes.Count ? panes[index] : null;

    /// <summary>Re-renders every pane: a bar shows its neighbours' collapse state, which a pane alone does not re-render for.</summary>
    internal void RefreshPanes()
    {
        if (refreshQueued)
            return;
        refreshQueued = true;
        _ = InvokeAsync(() =>
        {
            refreshQueued = false;
            foreach (var pane in panes)
                pane.Refresh();
        });
    }

    /// <summary>Called by nx-splitter.js when a drag or key changes a pane's size. Not for application use.</summary>
    [JSInvokable]
    public Task OnPaneResized(int index, string size) =>
        InvokeAsync(async () =>
        {
            if (PaneAt(index) is { } pane)
                await pane.ResizeAsync(size);
        });

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            selfReference = DotNetObjectReference.Create(this);
            if (module is not null)
                await module.InvokeVoidAsync("init", root, selfReference);
        }
    }

    internal async Task AfterPaneRenderAsync()
    {
        if (module is not null)
            await module.InvokeVoidAsync("refresh", root);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", root);
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        selfReference?.Dispose();
    }
}
