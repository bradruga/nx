using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Picks a color from a palette: a button showing the current color opens a grid of swatches (the Office palette by
/// default), moved through with the arrow keys. The value is the swatch's CSS color string, e.g. <c>#4371c4</c>.
/// <see cref="Clearable"/> adds "No color".
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
    // keys, focuses the selected swatch on open, and reports a press or Tab outside the popup.

    private const string ModulePath = "./_content/Nx.Components/nx-colorpicker.js";

    [GeneratedRegex(@"^rgba?\(\s*(\d{1,3})[\s,]+(\d{1,3})[\s,]+(\d{1,3})", RegexOptions.IgnoreCase)]
    private static partial Regex Rgb();

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-colorpicker-{Guid.NewGuid():N}"[..24];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxColorPicker>? selfReference;
    private ElementReference toggle;
    private ElementReference popup;
    private ElementReference body;
    private ElementReference palette;
    private string? current;
    private bool isOpen, openPending, closePending, returnFocusPending;

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
    internal static string? Normalize(string color)
    {
        var text = color.Trim();
        if (text.StartsWith('#'))
        {
            var hex = text[1..];
            if (hex.Length is 3 or 4)
                hex = string.Concat(hex[..3].Select(c => $"{c}{c}"));
            return hex.Length is 6 or 8 && hex.All(Uri.IsHexDigit) ? $"#{hex[..6].ToLowerInvariant()}" : null;
        }
        var rgb = Rgb().Match(text);
        if (!rgb.Success)
            return null;
        var parts = Enumerable.Range(1, 3).Select(i => Math.Min(255, int.Parse(rgb.Groups[i].Value, CultureInfo.InvariantCulture)));
        return "#" + string.Concat(parts.Select(p => p.ToString("x2", CultureInfo.InvariantCulture)));
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        validation.Attach(CascadedEditContext, ValueExpression);
        current = string.IsNullOrWhiteSpace(Value) ? null : Value;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
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
            await module.InvokeVoidAsync("open", toggle, popup, body, palette, selfReference);
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
            catch (JSDisconnectedException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        selfReference?.Dispose();
    }
}
