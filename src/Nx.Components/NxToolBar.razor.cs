using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A row of actions: <see cref="NxToolBarButton"/>s, <see cref="NxToolBarSeparator"/>s and an
/// <see cref="NxToolBarSpacer"/> to push the rest to the end. Buttons that do not fit move into a More menu at the
/// end (or the bar wraps or scrolls, see <see cref="Overflow"/>). One tab stop; the arrow keys, Home and End move
/// between the buttons, as an ARIA toolbar.
/// </summary>
/// <example>
/// <code>
/// &lt;NxToolBar AriaLabel="Sheet"&gt;
///     &lt;NxToolBarButton Text="New" Icon="@plus" OnClick="NewAsync" /&gt;
///     &lt;NxToolBarSeparator /&gt;
///     &lt;NxToolBarButton Text="Save" Variant="NxButtonVariant.Primary" OnClick="SaveAsync" /&gt;
/// &lt;/NxToolBar&gt;
/// </code>
/// </example>
public partial class NxToolBar : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-toolbar.js measures which items fit (ResizeObserver) and reports the keys of the rest; it also keeps one
    // tab stop and moves focus with the arrow keys in the browser.

    private const string ModulePath = "./_content/Nx.Components/nx-toolbar.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Dictionary<string, object> items = [];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxToolBar>? selfReference;
    private ElementReference bar;
    private IReadOnlyList<string> overflowed = [];
    private IReadOnlyList<NxMenuItem> moreItems = [];

    /// <summary>The buttons, separators and spacers.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>What happens to buttons that do not fit. Default: <see cref="NxToolBarOverflow.Menu"/>.</summary>
    [Parameter] public NxToolBarOverflow Overflow { get; set; } = NxToolBarOverflow.Menu;

    /// <summary>The buttons' size, unless a button sets its own. Default: <see cref="NxSize.Medium"/>.</summary>
    [Parameter] public NxSize Size { get; set; } = NxSize.Medium;

    /// <summary>The toolbar's accessible name.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The toolbar's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the toolbar.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the toolbar.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the toolbar.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => string.Join(' ', new[]
    {
        "nx-toolbar",
        Overflow switch { NxToolBarOverflow.Wrap => "nx-toolbar-wrap", NxToolBarOverflow.Scroll => "nx-toolbar-scroll", _ => null },
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    // ── Items ─────────────────────────────────────────────────────────────────

    internal void Register(string key, object item) => items[key] = item;

    internal void Unregister(string key)
    {
        if (items.Remove(key))
            Redraw();
    }

    /// <summary>An item's shown size may have changed (text, icon, visibility): measure again after the next render.</summary>
    internal void ItemChanged() => Redraw();

    internal bool IsOverflowed(string key) => Overflow == NxToolBarOverflow.Menu && overflowed.Contains(key);

    private void Redraw()
    {
        moreItems = BuildMoreItems();
        StateHasChanged();
    }

    /// <summary>The More menu: the overflowed buttons in order, with their separators between groups.</summary>
    private List<NxMenuItem> BuildMoreItems()
    {
        var menu = new List<NxMenuItem>();
        if (Overflow != NxToolBarOverflow.Menu)
            return menu;
        foreach (var key in overflowed)
        {
            switch (items.GetValueOrDefault(key))
            {
                case NxToolBarButton button when button.Visible:
                    menu.Add(new NxMenuItem(button.MenuText) { Id = key, Icon = button.Icon, Disabled = button.Disabled });
                    break;
                case NxToolBarSeparator when menu.Count > 0 && !menu[^1].IsSeparator:
                    menu.Add(NxMenuItem.Separator());
                    break;
            }
        }
        if (menu.Count > 0 && menu[^1].IsSeparator)
            menu.RemoveAt(menu.Count - 1);
        return menu;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet() => moreItems = BuildMoreItems();

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Removed while its start-up calls were still on their way: the module or the circuit is gone, nothing to set up.
        try { await AfterRenderAsync(firstRender); }
        catch (Exception gone) when (gone is JSDisconnectedException or ObjectDisposedException or OperationCanceledException) { }
    }

    private async Task AfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is null)
                return;
            selfReference = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("init", bar, Overflow == NxToolBarOverflow.Menu, selfReference);
        }
        else if (module is not null)
            await module.InvokeVoidAsync("refresh", bar, Overflow == NxToolBarOverflow.Menu);
    }

    /// <summary>Called by nx-toolbar.js with the keys of the items that do not fit. Not for application use.</summary>
    [JSInvokable]
    public Task OnOverflow(string[] keys) => InvokeAsync(() =>
    {
        overflowed = keys;
        Redraw();
        // Items whose own parameters did not change are not re-rendered by Blazor: tell each one.
        foreach (var item in items.Values)
        {
            if (item is NxToolBarButton button)
                button.Refresh();
            else if (item is NxToolBarSeparator separator)
                separator.Refresh();
        }
    });

    private async Task HandleMoreClickAsync(NxMenuItemClickEventArgs e)
    {
        if (e.Item.Id is { } key && items.GetValueOrDefault(key) is NxToolBarButton button)
            await button.ClickAsync(new MouseEventArgs());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", bar);
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
