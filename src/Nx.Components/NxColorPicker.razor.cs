using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Picks a color from a palette: a button showing the current color opens a grid of swatches (the Office palette by
/// default), moved through with the arrow keys. The value is the swatch's CSS color string, e.g. <c>#4371c4</c>.
/// <see cref="Clearable"/> adds "No color"; <see cref="View"/> adds or swaps in a gradient for any color.
/// </summary>
/// <example>
/// <code>
/// &lt;NxColorPicker @bind-Value="crew.Color" AriaLabel="Crew color" /&gt;
/// </code>
/// </example>
public partial class NxColorPicker : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-colorpicker.js places the popup (through nx-popup.js), moves focus through the grid with the arrow
    // keys, focuses the selected swatch (or the gradient) on open, and reports a press or Tab outside the popup.

    private const string ModulePath = "./_content/Nx.Components/nx-colorpicker.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-colorpicker-{Guid.NewGuid():N}"[..24];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxColorPicker>? selfReference;
    private ElementReference toggle;
    private ElementReference popup;
    private ElementReference body;
    private string? current, draft;
    private bool isOpen, openPending, closePending, returnFocusPending, gradientTab;

    /// <summary>Creates the component.</summary>
    public NxColorPicker() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>The color, as the palette writes it (e.g. <c>#4371c4</c>); <c>null</c> for none. Use <c>@bind-Value</c>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised when a swatch (or "No color") is picked.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>The swatches, as CSS colors, in reading order. Default: <see cref="NxColorPalettes.Office"/>.</summary>
    [Parameter] public IEnumerable<string>? Colors { get; set; }

    /// <summary>Swatches per row. Default: <c>10</c>.</summary>
    [Parameter] public int Columns { get; set; } = 10;

    /// <summary>What the popup offers: the palette, a gradient for any color (applied with a button), or both on tabs. Default: <see cref="NxColorPickerView.Palette"/>.</summary>
    [Parameter] public NxColorPickerView View { get; set; }

    /// <summary>Adds a "No color" button, which sets the value to <c>null</c>.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Shows the color's text beside the swatch.</summary>
    [Parameter] public bool ShowValue { get; set; }

    /// <summary>With <see cref="ShowValue"/>, the text while there is no color. Default: "None".</summary>
    [Parameter] public string Placeholder { get; set; } = "None";

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The accessible name of the button and the palette, e.g. "Crew color". The current color is added to it.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The button's <c>id</c>, for a <c>&lt;label for&gt;</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the button.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary><c>true</c> while the palette is open.</summary>
    public bool IsOpen => isOpen;

    /// <summary>Moves keyboard focus to the button.</summary>
    public ValueTask FocusAsync() => toggle.FocusAsync();

    private IReadOnlyList<string> Palette => Colors as IReadOnlyList<string> ?? Colors?.ToList() ?? NxColorPalettes.Office;
    private string ToggleId => Id ?? generatedId;
    private string PaletteId => $"{ToggleId}-palette";
    private int FocusIndex => Math.Max(0, Palette.ToList().FindIndex(c => Same(c, current)));
    private bool ShowsGradient => View == NxColorPickerView.Gradient || (View == NxColorPickerView.Both && gradientTab);
    private string TabClass(bool gradient) => gradient == gradientTab ? "nx-colorpicker-tab nx-colorpicker-tab-active" : "nx-colorpicker-tab";
    private string ToggleLabel => $"{AriaLabel ?? "Color"}: {current ?? "none"}";
    private string SwatchCssClass => current is null ? "nx-colorpicker-swatch nx-colorpicker-swatch-empty" : "nx-colorpicker-swatch";
    private string? SwatchStyle => current is null ? null : $"background-color: {current}";

    private string CssClass => string.Join(' ', new[]
    {
        "nx-colorpicker",
        isOpen ? "nx-colorpicker-open" : null,
        Disabled ? "nx-colorpicker-disabled" : null,
        validation.IsInvalid ? "nx-colorpicker-invalid" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private string ToggleCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-colorpicker-toggle"
        : $"nx-colorpicker-toggle {validation.CssClass}";

    /// <summary>Two CSS colors are the same color: hex in any case or length, or rgb().</summary>
    internal static bool Same(string? a, string? b) =>
        a is not null && b is not null && (string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase) || (Normalize(a) is { } na && na == Normalize(b)));

    /// <summary><c>#rrggbb</c> for a hex or rgb() color, else <c>null</c>.</summary>
    internal static string? Normalize(string color) => ColorMath.Parse(color) is { } c ? ColorMath.ToHex(c with { A = 1 }) : null;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        validation.Attach(CascadedEditContext, ValueExpression);
        current = string.IsNullOrWhiteSpace(Value) ? null : Value;
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
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;
        if (closePending)
        {
            closePending = false;
            await module.InvokeVoidAsync("close", toggle);
        }
        if (openPending && isOpen)
        {
            openPending = false;
            selfReference ??= DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("open", toggle, popup, body, selfReference);
        }
        if (returnFocusPending)
        {
            returnFocusPending = false;
            await toggle.FocusAsync();
        }
    }

    /// <summary>Called by nx-colorpicker.js on a press or focus outside the popup. Not for application use.</summary>
    [JSInvokable]
    public Task OnOutsidePress() => InvokeAsync(() =>
    {
        Close(returnFocus: false);
        StateHasChanged();
    });

    private void Open()
    {
        if (Disabled || isOpen)
            return;
        isOpen = true;
        openPending = true;
        draft = current;
        // With both, a color that is not a swatch opens on the gradient.
        gradientTab = current is not null && !Palette.Any(c => Same(c, current));
    }

    private void ShowTab(bool gradient)
    {
        if (gradientTab == gradient)
            return;
        gradientTab = gradient;
        openPending = true;     // the popup changed size: place it again
    }

    private void Close(bool returnFocus)
    {
        if (!isOpen)
            return;
        isOpen = false;
        openPending = false;
        closePending = true;
        returnFocusPending = returnFocus;
    }

    private void HandleToggleClick()
    {
        if (isOpen)
            Close(returnFocus: true);
        else
            Open();
    }

    private void HandleToggleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "ArrowDown" or "ArrowUp" && !isOpen)
            Open();
        // Escape pressed before focus has moved into the popup still lands here.
        else if (e.Key == "Escape" && isOpen)
            Close(returnFocus: true);
    }

    private void HandlePopupKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            Close(returnFocus: true);
    }

    private async Task PickAsync(string? color)
    {
        Close(returnFocus: true);
        if (Same(color, current) || (color is null && current is null))
            return;
        current = color;
        await ValueChanged.InvokeAsync(color);
        validation.NotifyFieldChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        validation.Dispose();
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("close", toggle);
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
