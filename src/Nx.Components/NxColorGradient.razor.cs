using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Picks any color: drag in the saturation and brightness area, a hue slider, an optional opacity slider, and
/// HEX or RGB text inputs, with a preview. Shows inline (put it in a dialog or popup yourself); for a pick from a
/// set palette use <see cref="NxColorPicker"/>. The value is a CSS color string in <see cref="ValueFormat"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxColorGradient @bind-Value="colors.Text" ShowOpacityEditor="false" /&gt;
/// </code>
/// </example>
public partial class NxColorGradient : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-colorgradient.js follows a drag in the area (pointer capture, moving the thumb at once) and reports where
    // it is, a frame at a time. The arrow keys on the thumb, the sliders and the text inputs are Blazor.

    private const string ModulePath = "./_content/Nx.Components/nx-colorgradient.js";
    private static readonly string[] AreaKeys = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End", "PageUp", "PageDown"];

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-colorgradient-{Guid.NewGuid():N}"[..26];
    private readonly Dictionary<string, string> partTexts = new() { ["R"] = "", ["G"] = "", ["B"] = "", ["A"] = "" };
    private IJSObjectReference? module;
    private DotNetObjectReference<NxColorGradient>? selfReference;
    private ElementReference area;
    private ElementReference thumb;
    private Hsva hsv = new(0, 0, 0, 1);
    private string? lastParameter, lastRaised;
    private string hueText = "0", alphaText = "100", hexText = "";
    private NxColorFormat view;
    private bool viewSet;

    /// <summary>Creates the component.</summary>
    public NxColorGradient() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>The color, e.g. <c>#4371c4</c>. Use <c>@bind-Value</c>. Hex or rgb() in; <see cref="ValueFormat"/> out.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised as the color changes: while dragging, on each slider step, and when a typed color is committed.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>How the value is written. Default: <see cref="NxColorFormat.Hex"/>.</summary>
    [Parameter] public NxColorFormat ValueFormat { get; set; } = NxColorFormat.Hex;

    /// <summary>Which text inputs show first: one HEX box or R, G, B boxes. The button beside them switches. Default: <see cref="NxColorFormat.Hex"/>.</summary>
    [Parameter] public NxColorFormat Format { get; set; } = NxColorFormat.Hex;

    /// <summary>Shows the opacity slider (and an A box with RGB). Default: <c>true</c>.</summary>
    [Parameter] public bool ShowOpacityEditor { get; set; } = true;

    /// <summary>Turns every part off.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The group's accessible name. Default: "Color".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The outer element's <c>id</c>; the HEX input is <c>{Id}-hex</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Moves keyboard focus to the saturation and brightness thumb.</summary>
    public ValueTask FocusAsync() => thumb.FocusAsync();

    private string BaseId => Id ?? generatedId;
    private IEnumerable<string> Parts => ShowOpacityEditor ? ["R", "G", "B", "A"] : ["R", "G", "B"];
    private Rgba Color => ColorMath.ToRgba(hsv);
    private static int Pct(double fraction) => (int)Math.Round(fraction * 100);
    private static string PartLabel(string part) => part switch { "R" => "Red", "G" => "Green", "B" => "Blue", _ => "Opacity percent" };

    private string CssClass => string.Join(' ', new[]
    {
        "nx-colorgradient",
        Disabled ? "nx-colorgradient-disabled" : null,
        validation.IsInvalid ? "nx-colorgradient-invalid" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private string RootStyle
    {
        get
        {
            var c = Color;
            var opaque = ColorMath.ToHex(c with { A = 1 });
            var style = string.Create(CultureInfo.InvariantCulture,
                $"--nx-colorgradient-hue: {Math.Round(hsv.H)}; --nx-colorgradient-x: {hsv.S * 100:0.##}%; --nx-colorgradient-y: {(1 - hsv.V) * 100:0.##}%; --nx-colorgradient-color: {ColorMath.ToRgb(c)}; --nx-colorgradient-opaque: {opaque};");
            return string.IsNullOrEmpty(Style) ? style : $"{style} {Style}";
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        validation.Attach(CascadedEditContext, ValueExpression);
        if (!viewSet)
        {
            viewSet = true;
            view = Format;
        }
        // Only an outside change replaces the color; the user's own keeps its hue even at grey.
        if (Value != lastParameter)
        {
            lastParameter = Value;
            if (Value != lastRaised && ColorMath.Parse(Value) is { } parsed)
            {
                var next = ColorMath.ToHsv(parsed);
                hsv = next.S == 0 || next.V == 0 ? next with { H = hsv.H } : next;
                lastRaised = Value;
            }
        }
        SyncTexts();
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
        if (!firstRender)
            return;
        module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;
        selfReference = DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("init", area, thumb, AreaKeys, selfReference);
    }

    private void SyncTexts()
    {
        var c = Color;
        hueText = Math.Round(hsv.H).ToString(CultureInfo.InvariantCulture);
        alphaText = Pct(hsv.A).ToString(CultureInfo.InvariantCulture);
        hexText = ColorMath.ToHex(ShowOpacityEditor ? c : c with { A = 1 });
        partTexts["R"] = c.R.ToString(CultureInfo.InvariantCulture);
        partTexts["G"] = c.G.ToString(CultureInfo.InvariantCulture);
        partTexts["B"] = c.B.ToString(CultureInfo.InvariantCulture);
        partTexts["A"] = alphaText;
    }

    // ── Changing the color ────────────────────────────────────────────────────

    private async Task SetAsync(Hsva next)
    {
        if (Disabled)
            return;
        if (!ShowOpacityEditor)
            next = next with { A = 1 };
        hsv = next;
        SyncTexts();
        var c = Color;
        var value = ValueFormat == NxColorFormat.Rgb ? ColorMath.ToRgb(c) : ColorMath.ToHex(c);
        if (value == lastRaised)
            return;
        lastRaised = value;
        await ValueChanged.InvokeAsync(value);
        validation.NotifyFieldChanged();
    }

    /// <summary>Called by nx-colorgradient.js while the area is dragged. Not for application use.</summary>
    [JSInvokable]
    public Task OnAreaPick(double saturation, double value) => InvokeAsync(async () =>
    {
        await SetAsync(hsv with { S = Math.Clamp(saturation, 0, 1), V = Math.Clamp(value, 0, 1) });
        StateHasChanged();
    });

    private async Task HandleAreaKeyDown(KeyboardEventArgs e)
    {
        var step = e.ShiftKey ? 0.1 : 0.01;
        var next = e.Key switch
        {
            "ArrowLeft"  => hsv with { S = hsv.S - step },
            "ArrowRight" => hsv with { S = hsv.S + step },
            "ArrowUp"    => hsv with { V = hsv.V + step },
            "ArrowDown"  => hsv with { V = hsv.V - step },
            "PageUp"     => hsv with { V = hsv.V + 0.1 },
            "PageDown"   => hsv with { V = hsv.V - 0.1 },
            "Home"       => hsv with { S = 0 },
            "End"        => hsv with { S = 1 },
            _            => (Hsva?)null
        };
        if (next is { } n)
            await SetAsync(n with { S = Math.Clamp(Math.Round(n.S, 2), 0, 1), V = Math.Clamp(Math.Round(n.V, 2), 0, 1) });
    }

    private Task HandleHueInput() =>
        double.TryParse(hueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ? SetAsync(hsv with { H = h }) : Task.CompletedTask;

    private Task HandleAlphaInput() =>
        double.TryParse(alphaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? SetAsync(hsv with { A = Math.Clamp(a / 100, 0, 1) }) : Task.CompletedTask;

    /// <summary>A typed hex color, with or without the <c>#</c>; anything else puts the text back.</summary>
    private async Task CommitHexAsync()
    {
        var text = hexText.Trim();
        if (ColorMath.Parse(text.StartsWith('#') ? text : "#" + text) is { } parsed)
            await SetFromRgbaAsync(parsed);
        else
            SyncTexts();
    }

    private async Task CommitPartAsync(string part)
    {
        if (!int.TryParse(partTexts[part], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            SyncTexts();
            return;
        }
        var c = Color;
        var b = (byte)Math.Clamp(n, 0, 255);
        await SetFromRgbaAsync(part switch
        {
            "R" => c with { R = b },
            "G" => c with { G = b },
            "B" => c with { B = b },
            _   => c with { A = Math.Clamp(n, 0, 100) / 100.0 }
        });
    }

    private Task SetFromRgbaAsync(Rgba c)
    {
        var next = ColorMath.ToHsv(c);
        return SetAsync(next.S == 0 || next.V == 0 ? next with { H = hsv.H } : next);
    }

    private void ToggleView() => view = view == NxColorFormat.Hex ? NxColorFormat.Rgb : NxColorFormat.Hex;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        validation.Dispose();
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", area);
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
