using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A primary action and an arrow beside it that opens a menu of further actions: two joined <see cref="NxButton"/>s
/// and the <see cref="NxMenu"/> of <see cref="NxContextMenu"/> and <see cref="NxDropDownButton"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxSplitButton Variant="NxButtonVariant.Primary" OnClick="SaveAsync" Items="saveItems" OnItemClick="SaveAsAsync"&gt;Save&lt;/NxSplitButton&gt;
/// </code>
/// </example>
public partial class NxSplitButton : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IJSObjectReference? inputModule;
    private readonly string generatedId = $"nx-split-{Guid.NewGuid():N}"[..18];
    private NxMenu? menu;
    private ElementReference anchor;
    private bool open;

    /// <summary>The primary action's label.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Icon of the primary action.</summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    /// <summary>Raised by the primary action.</summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>The menu items.</summary>
    [Parameter] public IReadOnlyList<NxMenuItem>? Items { get; set; }

    /// <summary>Raised when the user picks a menu item.</summary>
    [Parameter] public EventCallback<NxMenuItemClickEventArgs> OnItemClick { get; set; }

    /// <summary>Visual style of both halves, as NxButton. Default: <see cref="NxButtonVariant.Secondary"/>.</summary>
    [Parameter] public NxButtonVariant Variant { get; set; } = NxButtonVariant.Secondary;

    /// <summary>Size, as NxButton. Default: <see cref="NxSize.Medium"/>.</summary>
    [Parameter] public NxSize Size { get; set; } = NxSize.Medium;

    /// <summary>Shows the primary action's loading state, as NxButton.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Disables both halves.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Disables only the menu arrow.</summary>
    [Parameter] public bool MenuDisabled { get; set; }

    /// <summary>Accessible name of the primary action (for an icon-only one) and of the pair.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Accessible name of the arrow and the menu. Default: "More options".</summary>
    [Parameter] public string? MenuAriaLabel { get; set; }

    /// <summary>Tooltip text on the primary action.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>The primary button's <c>id</c>; the arrow gets <c>{Id}-toggle</c>, the menu <c>{Id}-menu</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Whether the menu is open.</summary>
    public bool IsOpen => open;

    /// <summary>Opens the menu.</summary>
    public Task OpenAsync() => menu?.OpenBelowAsync(anchor, alignEnd: true) ?? Task.CompletedTask;

    /// <summary>Closes the menu.</summary>
    public Task CloseAsync() => menu?.CloseAsync() ?? Task.CompletedTask;

    private string MainId => Id ?? generatedId;
    private string MenuId => $"{MainId}-menu";
    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-splitbutton" : $"nx-splitbutton {Class}";

    private Dictionary<string, object> MainAttributes => new() { ["id"] = MainId };

    private Dictionary<string, object> ToggleAttributes => new()
    {
        ["id"] = $"{MainId}-toggle",
        ["aria-haspopup"] = "menu",
        ["aria-expanded"] = open ? "true" : "false",
        ["aria-controls"] = MenuId
    };

    private Task ToggleAsync() => open ? CloseAsync() : OpenAsync();

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        // Alt+Down on the primary action, or Down on the arrow, opens the menu; plain Down on the main button does not.
        if (open || Disabled || MenuDisabled || e.Key != "ArrowDown")
            return;
        await OpenAsync();
    }

    private void HandleOpenChanged(bool isOpen) => open = isOpen;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Removed while its start-up calls were still on their way: the module or the circuit is gone, nothing to set up.
        try { await AfterRenderAsync(firstRender); }
        catch (Exception gone) when (gone is JSDisconnectedException or ObjectDisposedException or OperationCanceledException) { }
    }

    private async Task AfterRenderAsync(bool firstRender)
    {
        // Down opens the menu; its default would scroll the page (and a scroll moves the menu off the button).
        if (!firstRender)
            return;
        inputModule = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Nx.Components/nx-input.js");
        if (inputModule is not null)
            await inputModule.InvokeVoidAsync("preventKeys", anchor, new[] { "ArrowDown" });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (inputModule is null)
            return;
        try
        {
            await inputModule.InvokeVoidAsync("dispose", anchor);
            await inputModule.DisposeAsync();
        }
        catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
