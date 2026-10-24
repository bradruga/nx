using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Tabs over panels of content: declarative <see cref="NxTab"/> children with text, icon or template headers,
/// disabled and closable tabs, lazy or kept-alive panels, <c>@bind-ActiveTab</c>, tabs on any side, a header
/// that scrolls with arrows when the tabs do not fit, and the ARIA tablist keyboard pattern.
/// </summary>
/// <example>
/// <code>
/// &lt;NxTabStrip @bind-ActiveTab="tab"&gt;
///     &lt;NxTab Title="Details"&gt;…&lt;/NxTab&gt;
///     &lt;NxTab Title="Pours"&gt;…&lt;/NxTab&gt;
/// &lt;/NxTabStrip&gt;
/// </code>
/// </example>
public partial class NxTabStrip : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-tabstrip.js moves focus between tabs with the arrow keys and clicks the one it lands on, shows
    // and scrolls the overflow arrows, and keeps the active tab in view. Which tab is active is C#.

    private const string ModulePath = "./_content/Nx.Components/nx-tabstrip.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-tabstrip-{Guid.NewGuid():N}"[..21];
    private readonly List<NxTab> tabs = [];
    private readonly HashSet<string> visited = [];
    private IJSObjectReference? module;
    private ElementReference header;
    private ElementReference list;
    private string? active;             // the active key as last seen or raised
    private string? lastActiveTab;
    private bool refreshPending = true;
    private bool redrawQueued;

    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>The <see cref="NxTab"/> children.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The active tab's <see cref="NxTab.Id"/> (or title). Use <c>@bind-ActiveTab</c>. Default: the first enabled tab.</summary>
    [Parameter] public string? ActiveTab { get; set; }

    /// <summary>Raised when the user activates another tab, or closing the active tab moves to its neighbour.</summary>
    [Parameter] public EventCallback<string?> ActiveTabChanged { get; set; }

    /// <summary>Raised when the user closes a closable tab; remove it from the markup to make it go.</summary>
    [Parameter] public EventCallback<NxTabCloseEventArgs> OnTabClose { get; set; }

    /// <summary>
    /// <c>false</c> (default): only the active panel is rendered; switching tabs rebuilds it. <c>true</c>: a panel stays
    /// rendered, hidden, once it has been shown, so its state (scroll, typed text, loaded data) survives switching.
    /// </summary>
    [Parameter] public bool KeepAlive { get; set; }

    /// <summary>Which side the tabs sit on. Default: <see cref="NxTabPosition.Top"/>.</summary>
    [Parameter] public NxTabPosition Position { get; set; } = NxTabPosition.Top;

    /// <summary>Stretches the tabs to share the full width (top and bottom positions).</summary>
    [Parameter] public bool FullWidth { get; set; }

    /// <summary>Accessible name of the tab list.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The outer element's <c>id</c>; tabs get <c>{Id}-tab-{key}</c>, panels <c>{Id}-panel-{key}</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element, e.g. a height for the left and right positions.</summary>
    [Parameter] public string? Style { get; set; }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Activates the tab with this key, as a click would (disabled tabs excepted).</summary>
    public Task SelectAsync(string key) =>
        tabs.FirstOrDefault(t => t.Key == key) is { } tab ? ActivateAsync(tab) : Task.CompletedTask;

    // ── Registration ──────────────────────────────────────────────────────────

    internal void Register(NxTab tab)
    {
        tabs.Add(tab);
        Redraw();
    }

    internal void Unregister(NxTab tab)
    {
        if (tabs.Remove(tab))
            Redraw();
    }

    internal void TabChanged() => Redraw();

    /// <summary>The strip draws headers before its tabs receive new parameters; draw once more afterwards.</summary>
    private void Redraw()
    {
        refreshPending = true;
        if (redrawQueued)
            return;
        redrawQueued = true;
        _ = InvokeAsync(() =>
        {
            redrawQueued = false;
            StateHasChanged();
        });
    }

    // ── Active tab ────────────────────────────────────────────────────────────

    /// <summary>The bound key when it names an enabled tab, else the first enabled tab.</summary>
    private NxTab? ActiveTabItem =>
        tabs.FirstOrDefault(t => t.Key == active && !t.Disabled) ?? tabs.FirstOrDefault(t => !t.Disabled);

    private bool IsActive(NxTab tab) => ReferenceEquals(tab, ActiveTabItem);

    private bool ShouldRender(NxTab tab) => IsActive(tab) || (KeepAlive && visited.Contains(tab.Key));

    private async Task ActivateAsync(NxTab tab)
    {
        if (tab.Disabled || IsActive(tab))
            return;
        active = tab.Key;
        lastActiveTab = tab.Key;
        refreshPending = true;
        await ActiveTabChanged.InvokeAsync(tab.Key);
    }

    private async Task CloseAsync(NxTab tab)
    {
        if (!tab.Closable || tab.Disabled)
            return;
        if (IsActive(tab))
        {
            // Move to the neighbour first: the right one, else the left one.
            var index = tabs.IndexOf(tab);
            var neighbour = tabs.Skip(index + 1).FirstOrDefault(t => !t.Disabled)
                         ?? tabs.Take(index).LastOrDefault(t => !t.Disabled);
            active = neighbour?.Key;
            lastActiveTab = active;
            await ActiveTabChanged.InvokeAsync(active);
        }
        visited.Remove(tab.Key);
        await OnTabClose.InvokeAsync(new NxTabCloseEventArgs(tab.Key));
    }

    private async Task HandleTabKeyDown(KeyboardEventArgs e, NxTab tab)
    {
        if (e.Key == "Delete")
            await CloseAsync(tab);
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string BaseId => Id ?? generatedId;
    private string Slug(NxTab tab) => new string(tab.Key.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
    private string TabId(NxTab tab) => $"{BaseId}-tab-{Slug(tab)}";
    private string PanelId(NxTab tab) => $"{BaseId}-panel-{Slug(tab)}";
    private bool Vertical => Position is NxTabPosition.Left or NxTabPosition.Right;

    private string CssClass => string.Join(' ', new[]
    {
        "nx-tabstrip",
        $"nx-tabstrip-{Position.ToString().ToLowerInvariant()}",
        FullWidth && !Vertical ? "nx-tabstrip-full-width" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private static string TabClass(NxTab tab, bool isActive) => string.Join(' ', new[]
    {
        "nx-tabstrip-tab",
        isActive ? "nx-tabstrip-tab-active" : null,
        tab.Disabled ? "nx-tabstrip-tab-disabled" : null,
        tab.Closable ? "nx-tabstrip-tab-closable" : null
    }.Where(c => c is not null));

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ActiveTab != lastActiveTab)
        {
            lastActiveTab = ActiveTab;
            active = ActiveTab;
            refreshPending = true;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ActiveTabItem is { } current)
            visited.Add(current.Key);
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is not null)
                await module.InvokeVoidAsync("init", header, list);
        }
        if (refreshPending && module is not null)
        {
            refreshPending = false;
            await module.InvokeVoidAsync("refresh", list);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is null)
            return;
        try
        {
            await module.InvokeVoidAsync("dispose", list);
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
