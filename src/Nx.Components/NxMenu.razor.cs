using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A menu of <see cref="NxMenuItem"/>s with icons, shortcut text, separators, disabled and check items and nested
/// submenus, opened at a point or under an element. It flips at the viewport edges, takes the keyboard (arrows,
/// Home/End, first letters, Right/Left into and out of submenus, Escape), closes on an outside press, and is a
/// bottom sheet with drill-in submenus on phones. The menu behind <see cref="NxContextMenu"/>,
/// <see cref="NxDropDownButton"/> and <see cref="NxSplitButton"/>; open it from code with <see cref="OpenAtAsync"/>.
/// </summary>
public partial class NxMenu : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-menu.js places the menu and its submenus, moves focus with the keys, opens submenus on hover and
    // reports an outside press or Escape (OnDismiss). Which items there are, and picking one, is C#.

    private const string ModulePath = "./_content/Nx.Components/nx-menu.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-menu-{Guid.NewGuid():N}"[..17];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxMenu>? selfReference;
    private ElementReference host;
    private bool isOpen, openPending, closePending, jsOpen;
    private object how = new { x = 0d, y = 0d };
    private bool focusFirst = true;
    private string? context;

    /// <summary>The items.</summary>
    [Parameter] public IReadOnlyList<NxMenuItem>? Items { get; set; }

    /// <summary>Raised when the user picks an item (not a submenu parent, not a disabled item); the menu has closed.</summary>
    [Parameter] public EventCallback<NxMenuItemClickEventArgs> OnItemClick { get; set; }

    /// <summary>Raised when the menu opens (<c>true</c>) and closes (<c>false</c>), however it happened.</summary>
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Accessible name of the menu.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The menu host's <c>id</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the menu host.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Whether the menu is open.</summary>
    public bool IsOpen => isOpen;

    /// <summary>Opens the menu at a viewport point, e.g. a pointer position. <paramref name="context"/> comes back in <c>OnItemClick</c>.</summary>
    public Task OpenAtAsync(double x, double y, string? context = null, bool focusFirstItem = true) =>
        OpenAsync(new { x, y }, context, focusFirstItem);

    /// <summary>Opens the menu under <paramref name="anchor"/>, left-aligned (or right-aligned with <paramref name="alignEnd"/>); above it when there is no room below.</summary>
    public Task OpenBelowAsync(ElementReference anchor, bool alignEnd = false, bool focusFirstItem = true) =>
        OpenAsync(new { anchor, alignEnd }, null, focusFirstItem);

    /// <summary>Closes the menu; focus goes back where it was.</summary>
    public async Task CloseAsync()
    {
        if (!isOpen)
            return;
        isOpen = false;
        closePending = jsOpen;
        StateHasChanged();
        await OpenChanged.InvokeAsync(false);
    }

    private async Task OpenAsync(object placement, string? openContext, bool focus)
    {
        how = placement;
        context = openContext;
        focusFirst = focus;
        isOpen = true;
        openPending = true;
        StateHasChanged();
        await OpenChanged.InvokeAsync(true);
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string MenuId => Id ?? generatedId;
    private string HostClass => string.IsNullOrEmpty(Class) ? "nx-menu" : $"nx-menu {Class}";

    private static bool HasIcons(IReadOnlyList<NxMenuItem> items) => items.Any(i => i.Icon is not null);

    private static string ItemClass(NxMenuItem item) => string.Join(' ', new[]
    {
        "nx-menu-item",
        item.Disabled ? "nx-menu-item-disabled" : null,
        item.HasSubmenu ? "nx-menu-item-parent" : null
    }.Where(c => c is not null));

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Removed while its start-up calls were still on their way: the module or the circuit is gone, nothing to set up.
        try { await AfterRenderAsync(firstRender); }
        catch (Exception gone) when (gone is JSDisconnectedException or ObjectDisposedException or OperationCanceledException) { }
    }

    private async Task AfterRenderAsync(bool firstRender)
    {
        if (!openPending && !closePending)
            return;
        module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;
        if (closePending)
        {
            closePending = false;
            jsOpen = false;
            await module.InvokeVoidAsync("close", MenuId, (object?)null);
        }
        if (openPending && isOpen)
        {
            openPending = false;
            selfReference ??= DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("open", MenuId, host, selfReference, how, focusFirst);
            jsOpen = true;
        }
    }

    /// <summary>Called by nx-menu.js on an outside press, Escape or Tab. Not for application use.</summary>
    [JSInvokable]
    public Task OnDismiss() => InvokeAsync(CloseAsync);

    private Task DismissAsync() => CloseAsync();

    private async Task HandleItemClickAsync(NxMenuItem item)
    {
        if (item.Disabled || item.HasSubmenu || item.IsSeparator)
            return;
        var openContext = context;
        await CloseAsync();
        await OnItemClick.InvokeAsync(new NxMenuItemClickEventArgs(item, openContext));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                if (jsOpen)
                    await module.InvokeVoidAsync("close", MenuId, false);
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
