using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A tooltip for the element it wraps, or for any element matched by <see cref="Target"/>: shown on hover, keyboard
/// focus or click after a delay, or by a long press on touch screens; placed on a preferred side with flipping and
/// an arrow; plain <see cref="Text"/> or rich <see cref="Content"/>. It never takes pointer events, Escape hides it,
/// and the anchor is <c>aria-describedby</c> it.
/// </summary>
/// <example>
/// <code>
/// &lt;NxTooltip Text="Saves and sends to the plant"&gt;
///     &lt;NxButton Variant="NxButtonVariant.Primary"&gt;Release&lt;/NxButton&gt;
/// &lt;/NxTooltip&gt;
/// </code>
/// </example>
public partial class NxTooltip : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-tooltip.js listens on the anchor, shows and hides the tooltip after the delays, places it with
    // flipping, handles the long press and Escape, and sets aria-describedby. All in the browser: a tooltip
    // must appear without a server round trip. Blazor only renders the content.

    private const string ModulePath = "./_content/Nx.Components/nx-tooltip.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string generatedId = $"nx-tooltip-{Guid.NewGuid():N}"[..20];
    private IJSObjectReference? module;
    private ElementReference wrapper;
    private ElementReference tip;
    private object? appliedOptions;

    /// <summary>The anchor: the element the tooltip describes. Leave empty and set <see cref="Target"/> to describe another element.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>A CSS selector for the anchor, e.g. <c>"#save-button"</c>, when the tooltip does not wrap it.</summary>
    [Parameter] public string? Target { get; set; }

    /// <summary>The tooltip text.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>Rich tooltip content, in place of <see cref="Text"/>. Keep it short and non-interactive.</summary>
    [Parameter] public RenderFragment? Content { get; set; }

    /// <summary>The side it prefers; it flips when there is no room. Default: <see cref="NxTooltipPlacement.Top"/>.</summary>
    [Parameter] public NxTooltipPlacement Placement { get; set; } = NxTooltipPlacement.Top;

    /// <summary>What shows it. Default: <see cref="NxTooltipTrigger.Hover"/> | <see cref="NxTooltipTrigger.Focus"/>.</summary>
    [Parameter] public NxTooltipTrigger Trigger { get; set; } = NxTooltipTrigger.Hover | NxTooltipTrigger.Focus;

    /// <summary>Milliseconds the pointer must rest before it shows. Keyboard focus and clicks show it at once. Default: <c>400</c>.</summary>
    [Parameter] public int ShowDelay { get; set; } = 400;

    /// <summary>Milliseconds before it hides once the pointer leaves. Default: <c>100</c>.</summary>
    [Parameter] public int HideDelay { get; set; } = 100;

    /// <summary>Turns the tooltip off; it neither shows nor describes the anchor.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The tooltip element's <c>id</c>, which the anchor's <c>aria-describedby</c> names. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the tooltip.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Shows the tooltip now.</summary>
    public async Task ShowAsync()
    {
        if (module is not null)
            await module.InvokeVoidAsync("showNow", TipId);
    }

    /// <summary>Hides the tooltip now.</summary>
    public async Task HideAsync()
    {
        if (module is not null)
            await module.InvokeVoidAsync("hideNow", TipId);
    }

    private string TipId => Id ?? generatedId;
    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-tooltip" : $"nx-tooltip {Class}";

    private string[] Triggers => new[]
    {
        Trigger.HasFlag(NxTooltipTrigger.Hover) ? "hover" : null,
        Trigger.HasFlag(NxTooltipTrigger.Focus) ? "focus" : null,
        Trigger.HasFlag(NxTooltipTrigger.Click) ? "click" : null
    }.OfType<string>().ToArray();

    private (string?, bool, string, int, int, string, bool) OptionsKey =>
        (Target, ChildContent is not null, string.Join(',', Triggers), ShowDelay, HideDelay, Placement.ToString(), Disabled);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;
        var key = OptionsKey;
        if (!Equals(key, appliedOptions))
        {
            appliedOptions = key;
            await module.InvokeVoidAsync("init", TipId, tip, new
            {
                target = Target,
                wrapper = ChildContent is not null ? (object)wrapper : null,
                triggers = Triggers,
                showDelay = ShowDelay,
                hideDelay = HideDelay,
                placement = Placement.ToString().ToLowerInvariant(),
                disabled = Disabled
            });
        }
        else
        {
            await module.InvokeVoidAsync("refresh", TipId);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is null)
            return;
        try
        {
            await module.InvokeVoidAsync("dispose", TipId);
            await module.DisposeAsync();
        }
        catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
