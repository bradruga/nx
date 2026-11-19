using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A pad to sign on with a mouse, pen or finger. Binds the signature as a PNG data URL (<c>data:image/png;base64,…</c>),
/// <c>null</c> when empty; a clear button starts over. Takes part in <c>EditForm</c> validation.
/// </summary>
/// <example>
/// <code>
/// &lt;NxSignature @bind-Value="response.SignatureSource" StrokeWidth="3" Width="300px" Height="150px" /&gt;
/// </code>
/// </example>
public partial class NxSignature : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-signature.js draws with pointer events on a canvas and exports the PNG when a stroke ends.

    private const string ModulePath = "./_content/Nx.Components/nx-signature.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private IJSObjectReference? module;
    private DotNetObjectReference<NxSignature>? dotNet;
    private ElementReference root;
    private ElementReference canvas;
    private string? lastValue;
    private string? pendingValue;
    private bool hasPending;
    private string? shownOptions;

    /// <summary>Creates the component.</summary>
    public NxSignature() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>The signature as a PNG data URL, <c>null</c> when empty. Use <c>@bind-Value</c>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised when a stroke ends, and with <c>null</c> when the pad is cleared.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>The pad's width. Default: <c>300px</c>.</summary>
    [Parameter] public string Width { get; set; } = "300px";

    /// <summary>The pad's height. Default: <c>150px</c>.</summary>
    [Parameter] public string Height { get; set; } = "150px";

    /// <summary>The pen's width in pixels. Default: <c>2</c>.</summary>
    [Parameter] public double StrokeWidth { get; set; } = 2;

    /// <summary>The ink. It is saved in the image, so it does not follow the theme. Default: <c>#000000</c>.</summary>
    [Parameter] public string Color { get; set; } = "#000000";

    /// <summary>The paper, saved in the image too. Default: <c>#ffffff</c>.</summary>
    [Parameter] public string BackgroundColor { get; set; } = "#ffffff";

    /// <summary>The saved image's size over the pad's. Default: <c>2</c> (sharp when printed).</summary>
    [Parameter] public double ExportScale { get; set; } = 2;

    /// <summary>Shown on the empty pad. Default: "Sign here".</summary>
    [Parameter] public string? Placeholder { get; set; } = "Sign here";

    /// <summary>Greys the pad out; no signing or clearing.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows the signature without letting it change.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>The pad's accessible name. Default: "Signature".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes go to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Clears the pad and raises <see cref="ValueChanged"/> with <c>null</c>.</summary>
    public async Task ClearAsync()
    {
        if (module is not null)
            await module.InvokeVoidAsync("clear", root);
        await CommitAsync(null);
    }

    private bool IsEmpty => string.IsNullOrEmpty(lastValue);
    private bool Editable => !Disabled && !ReadOnly;
    private string OuterStyle => string.IsNullOrEmpty(Style) ? $"width: {Width}; height: {Height}" : $"width: {Width}; height: {Height}; {Style}";
    private string OptionsKey => string.Create(CultureInfo.InvariantCulture, $"{StrokeWidth}|{Color}|{BackgroundColor}|{ExportScale}");
    private object Options => new { strokeWidth = StrokeWidth, color = Color, background = BackgroundColor, exportScale = ExportScale };

    private string CssClass
    {
        get
        {
            var css = "nx-signature";
            if (Disabled) css += " nx-signature-disabled";
            if (ReadOnly) css += " nx-signature-readonly";
            if (validation.IsInvalid) css += " nx-signature-invalid";
            return string.IsNullOrEmpty(Class) ? css : $"{css} {Class}";
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        validation.Attach(CascadedEditContext, ValueExpression);
        // Only an outside change to Value redraws the pad: its own export coming back is not one.
        if (module is null || !string.Equals(Value, lastValue, StringComparison.Ordinal))
        {
            lastValue = Value;
            pendingValue = Value;
            hasPending = true;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            dotNet = DotNetObjectReference.Create(this);
            shownOptions = OptionsKey;
            hasPending = false;
            if (module is not null)
                await module.InvokeVoidAsync("init", root, canvas, dotNet, Options, pendingValue);
            return;
        }
        if (module is null)
            return;
        if (OptionsKey != shownOptions)
        {
            shownOptions = OptionsKey;
            await module.InvokeVoidAsync("setOptions", root, Options);
        }
        if (hasPending)
        {
            hasPending = false;
            await module.InvokeVoidAsync("setValue", root, pendingValue);
        }
    }

    /// <summary>Called by nx-signature.js with the PNG when a stroke ends.</summary>
    [JSInvokable]
    public Task OnSigned(string dataUrl) => InvokeAsync(() => CommitAsync(dataUrl));

    private async Task CommitAsync(string? value)
    {
        if (string.Equals(value, lastValue, StringComparison.Ordinal))
            return;
        lastValue = value;
        await ValueChanged.InvokeAsync(value);
        validation.NotifyFieldChanged();
        StateHasChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        validation.Dispose();
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", root);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        dotNet?.Dispose();
    }
}
