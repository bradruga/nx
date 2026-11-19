using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A row of chips, one per item: text or a template, wrapping onto more lines. With <see cref="Removable"/>, each
/// chip has a remove button and Delete or Backspace removes the focused one; <see cref="OnRemove"/> tells you which,
/// and you take it out of <see cref="Data"/>. The arrow keys move between chips; the list is one tab stop.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
/// <example>
/// <code>
/// &lt;NxChipList Data="tickets" ItemText="t =&gt; t.Number" Removable="true" OnRemove="e =&gt; tickets.Remove(e.Item)" /&gt;
/// </code>
/// </example>
public partial class NxChipList<TItem> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-input.js moves focus between chips with the arrow keys (roving tabindex) without a round trip.

    private const string ModulePath = "./_content/Nx.Components/nx-input.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IJSObjectReference? module;
    private ElementReference list;
    private ElementReference[] chips = [];
    private List<TItem> items = [];
    private int focusIndex;
    private int? focusPending;

    /// <summary>The items, one chip each, in order.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>A chip's text, also its remove button's name. Default: <c>ToString()</c>.</summary>
    [Parameter] public Func<TItem, string>? ItemText { get; set; }

    /// <summary>Renders a chip's content in place of its text.</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Gives each chip a remove button, and lets Delete or Backspace remove the focused chip.</summary>
    [Parameter] public bool Removable { get; set; }

    /// <summary>Raised when the user removes a chip; take the item out of <see cref="Data"/>.</summary>
    [Parameter] public EventCallback<NxChipRemoveEventArgs<TItem>> OnRemove { get; set; }

    /// <summary>Filled (default) or outlined chips.</summary>
    [Parameter] public NxChipVariant Variant { get; set; } = NxChipVariant.Solid;

    /// <summary>Chip height. Default: <see cref="NxSize.Medium"/>.</summary>
    [Parameter] public NxSize Size { get; set; } = NxSize.Medium;

    /// <summary>Shown when there are no items; nothing when <c>null</c>.</summary>
    [Parameter] public string? NoDataText { get; set; }

    /// <summary>Greys the chips out and hides the remove buttons.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The list's accessible name.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The list's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the list.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the list.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the list.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool CanRemove => Removable && !Disabled;
    private string TextOf(TItem item) => ItemText?.Invoke(item) ?? item?.ToString() ?? "";

    private string CssClass => string.Join(' ', new[]
    {
        "nx-chiplist",
        Variant == NxChipVariant.Outline ? "nx-chiplist-outline" : null,
        Size switch { NxSize.Small => "nx-chiplist-sm", NxSize.Large => "nx-chiplist-lg", _ => null },
        Disabled ? "nx-chiplist-disabled" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        items = Data?.ToList() ?? [];
        // Only grow: Blazor sets @ref when an element is created, so a kept chip keeps its slot.
        if (chips.Length < items.Count)
            Array.Resize(ref chips, items.Count);
        focusIndex = Math.Clamp(focusIndex, 0, Math.Max(0, items.Count - 1));
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
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is not null)
                await module.InvokeVoidAsync("roving", list, ".nx-chiplist-chip");
        }
        // After a removal, focus stays at the same place: the next chip, else the new last one.
        if (focusPending is int at && items.Count > 0)
        {
            focusPending = null;
            focusIndex = Math.Min(at, items.Count - 1);
            await chips[focusIndex].FocusAsync();
        }
    }

    private async Task HandleKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Key is "Delete" or "Backspace" && CanRemove)
            await RemoveAsync(index);
    }

    private async Task RemoveAsync(int index)
    {
        if (!CanRemove || index >= items.Count)
            return;
        focusPending = index;
        await OnRemove.InvokeAsync(new NxChipRemoveEventArgs<TItem>(items[index]));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", list);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
    }
}
