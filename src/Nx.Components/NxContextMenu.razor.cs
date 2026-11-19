using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A menu opened by a right click, or a long press on touch screens, on the element it wraps or the one matched by
/// <see cref="Target"/>; also opened from code at a point. An <see cref="NxMenu"/> underneath: nested submenus, icons,
/// shortcut text, separators, disabled and check items, keyboard, flipping, and a bottom sheet on phones.
/// Mark rows with <c>data-nx-context="…"</c> to learn which one was pressed.
/// </summary>
/// <example>
/// <code>
/// &lt;NxContextMenu Items="rowMenu" OnItemClick="RunAsync"&gt;
///     &lt;table&gt;… &lt;tr data-nx-context="@@pour.Id"&gt; …&lt;/table&gt;
/// &lt;/NxContextMenu&gt;
/// </code>
/// </example>
public partial class NxContextMenu : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-menu.js listens on the target for contextmenu and long-press touches, cancels the browser's own menu,
    // and calls OnContextRequested with the point and the pressed element's data-nx-context. NxMenu does the rest.

    private const string ModulePath = "./_content/Nx.Components/nx-menu.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string watchKey = Guid.NewGuid().ToString("N");
    private IJSObjectReference? module;
    private DotNetObjectReference<NxContextMenu>? selfReference;
    private NxMenu? menu;
    private ElementReference wrapper;
    private (string?, bool, bool)? watched;

    /// <summary>The element whose right click opens the menu. Leave empty and set <see cref="Target"/> for another element.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>A CSS selector for the target, when the menu does not wrap it.</summary>
    [Parameter] public string? Target { get; set; }

    /// <summary>The items.</summary>
    [Parameter] public IReadOnlyList<NxMenuItem>? Items { get; set; }

    /// <summary>Raised when the user picks an item. <c>Context</c> is the pressed element's <c>data-nx-context</c>.</summary>
    [Parameter] public EventCallback<NxMenuItemClickEventArgs> OnItemClick { get; set; }

    /// <summary>Raised before the menu opens: change <see cref="Items"/> for the pressed element, or cancel.</summary>
    [Parameter] public EventCallback<NxContextMenuOpeningEventArgs> OnOpening { get; set; }

    /// <summary>Turns the menu off; the browser's own context menu shows instead.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Accessible name of the menu.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The menu host's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the menu host.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Whether the menu is open.</summary>
    public bool IsOpen => menu?.IsOpen ?? false;

    /// <summary>Opens the menu at a viewport point, as a right click there would (without <see cref="OnOpening"/>).</summary>
    public Task OpenAtAsync(double x, double y, string? context = null) =>
        menu?.OpenAtAsync(x, y, context) ?? Task.CompletedTask;

    /// <summary>Closes the menu.</summary>
    public Task CloseAsync() => menu?.CloseAsync() ?? Task.CompletedTask;

    private string MenuClass => string.IsNullOrEmpty(Class) ? "nx-context-menu" : $"nx-context-menu {Class}";

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Removed while its start-up calls were still on their way: the module or the circuit is gone, nothing to set up.
        try { await AfterRenderAsync(firstRender); }
        catch (Exception gone) when (gone is JSDisconnectedException or ObjectDisposedException or OperationCanceledException) { }
    }

    private async Task AfterRenderAsync(bool firstRender)
    {
        var key = (Target, ChildContent is not null, Disabled);
        if (watched == key)
            return;
        watched = key;
        module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;
        if (Disabled)
        {
            await module.InvokeVoidAsync("unwatchContext", watchKey);
            return;
        }
        selfReference ??= DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("watchContext", watchKey, selfReference,
            ChildContent is not null ? (object)wrapper : null, Target);
    }

    /// <summary>Called by nx-menu.js on a right click or long press. Not for application use.</summary>
    [JSInvokable]
    public Task OnContextRequested(double x, double y, string? context) => InvokeAsync(async () =>
    {
        var args = new NxContextMenuOpeningEventArgs(x, y, context);
        await OnOpening.InvokeAsync(args);
        if (args.Cancel || menu is null)
            return;
        await menu.OpenAtAsync(x, y, context);
    });

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("unwatchContext", watchKey);
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
