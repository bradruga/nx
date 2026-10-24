using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// An <see cref="NxButton"/> that opens a menu of <see cref="NxMenuItem"/>s below it — the same menu as
/// <see cref="NxContextMenu"/>: submenus, icons, shortcut text, separators, disabled and check items, keyboard,
/// and a bottom sheet on phones. Every NxButton variant and size applies.
/// </summary>
/// <example>
/// <code>
/// &lt;NxDropDownButton Items="exportItems" OnItemClick="ExportAsync"&gt;Export&lt;/NxDropDownButton&gt;
/// </code>
/// </example>
public partial class NxDropDownButton : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IJSObjectReference? inputModule;
    private readonly string generatedId = $"nx-ddb-{Guid.NewGuid():N}"[..16];
    private NxMenu? menu;
    private ElementReference anchor;
    private bool open;

    /// <summary>The button label. Without one (an <see cref="Icon"/> only) there is no arrow.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Icon before the label, or alone for an icon-only button (give it an <see cref="AriaLabel"/>).</summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    /// <summary>The menu items.</summary>
    [Parameter] public IReadOnlyList<NxMenuItem>? Items { get; set; }

    /// <summary>Raised when the user picks an item.</summary>
    [Parameter] public EventCallback<NxMenuItemClickEventArgs> OnItemClick { get; set; }

    /// <summary>Visual style, as NxButton. Default: <see cref="NxButtonVariant.Secondary"/>.</summary>
    [Parameter] public NxButtonVariant Variant { get; set; } = NxButtonVariant.Secondary;

    /// <summary>Size, as NxButton. Default: <see cref="NxButtonSize.Medium"/>.</summary>
    [Parameter] public NxButtonSize Size { get; set; } = NxButtonSize.Medium;

    /// <summary>Shows the down arrow after the label. Default: <c>true</c>.</summary>
    [Parameter] public bool ShowArrow { get; set; } = true;

    /// <summary>Aligns the menu's right edge with the button's. Default: left edges aligned.</summary>
    [Parameter] public bool AlignEnd { get; set; }

    /// <summary>Stretches the button to its container's width.</summary>
    [Parameter] public bool FullWidth { get; set; }

    /// <summary>Disables the button.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The button's accessible name; required for icon-only buttons.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The menu's accessible name. Default: <see cref="AriaLabel"/>.</summary>
    [Parameter] public string? MenuAriaLabel { get; set; }

    /// <summary>Tooltip text on the button.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>The button's <c>id</c>; the menu gets <c>{Id}-menu</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Whether the menu is open.</summary>
    public bool IsOpen => open;

    /// <summary>Opens the menu.</summary>
    public Task OpenAsync() => menu?.OpenBelowAsync(anchor, AlignEnd) ?? Task.CompletedTask;

    /// <summary>Closes the menu.</summary>
    public Task CloseAsync() => menu?.CloseAsync() ?? Task.CompletedTask;

    private string ButtonId => Id ?? generatedId;
    private string MenuId => $"{ButtonId}-menu";
    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-dropdownbutton" : $"nx-dropdownbutton {Class}";

    private Dictionary<string, object> ButtonAttributes => new()
    {
        ["id"] = ButtonId,
        ["aria-haspopup"] = "menu",
        ["aria-expanded"] = open ? "true" : "false",
        ["aria-controls"] = MenuId
    };

    private Task ToggleAsync() => open ? CloseAsync() : OpenAsync();

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!open && !Disabled && e.Key is "ArrowDown" or "ArrowUp")
            await OpenAsync();
    }

    private void HandleOpenChanged(bool isOpen) => open = isOpen;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Up and Down open the menu; their default would scroll the page (and a scroll moves the menu off its button).
        if (!firstRender)
            return;
        inputModule = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Nx.Components/nx-input.js");
        if (inputModule is not null)
            await inputModule.InvokeVoidAsync("preventKeys", anchor, new[] { "ArrowDown", "ArrowUp" });
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
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
