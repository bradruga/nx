using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A number input for <c>int</c>, <c>long</c>, <c>short</c>, <c>decimal</c>, <c>double</c> and <c>float</c>, nullable or not.
/// Min/max/step, spin buttons with press-and-hold, arrow and page keys, culture-aware format and parsing,
/// and the same label, floating label, prefix/suffix and clear options as <see cref="NxTextBox"/>.
/// </summary>
/// <typeparam name="TValue">The bound number type.</typeparam>
/// <example>
/// <code>
/// &lt;NxNumericTextBox Label="Unit price" @bind-Value="line.Price" Min="0" Step="0.25m" Format="N2"&gt;
///     &lt;Prefix&gt;$&lt;/Prefix&gt;
/// &lt;/NxNumericTextBox&gt;
/// </code>
/// </example>
public partial class NxNumericTextBox<TValue> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-input.js cancels the caret moves of the arrow and page keys (they step the value here), and on
    // focus swaps the display text for the edit text (and selects it with SelectOnFocus) in the browser, so
    // a late swap from here cannot land on typed text. Parsing, formatting and stepping are C#.

    private const string ModulePath = "./_content/Nx.Components/nx-input.js";
    private const int HoldDelay = 400;      // ms before a held spin button starts repeating
    private const int HoldInterval = 60;    // ms between repeats

    private static readonly Type Underlying = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
    private static readonly bool IsNullable = Nullable.GetUnderlyingType(typeof(TValue)) is not null;
    private static readonly bool IsIntegral =
        Underlying == typeof(int) || Underlying == typeof(long) || Underlying == typeof(short);
    private static readonly bool IsSupported = IsIntegral ||
        Underlying == typeof(decimal) || Underlying == typeof(double) || Underlying == typeof(float);

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-numeric-{Guid.NewGuid():N}"[..20];
    private IJSObjectReference? module;
    private ElementReference input;
    private string currentText = "";
    private decimal? current;           // the value as last seen or raised
    private bool hasFocus;
    private bool hasMin, hasMax, hasStep;
    private bool? appliedSelectOnFocus;
    private CancellationTokenSource? spin;

    /// <summary>Creates the component.</summary>
    public NxNumericTextBox() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The number. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Raised when the value is committed: on Enter, on leaving the field, on every step.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>Smallest allowed value. Typed values are clamped to it when committed.</summary>
    [Parameter] public TValue? Min { get; set; }

    /// <summary>Largest allowed value. Typed values are clamped to it when committed.</summary>
    [Parameter] public TValue? Max { get; set; }

    /// <summary>How much ↑, ↓ and the spin buttons change the value; PgUp and PgDn change it ten times as much. Default: 1.</summary>
    [Parameter] public TValue? Step { get; set; }

    /// <summary>Rounds committed values to this many decimal places (integers always round to 0).</summary>
    [Parameter] public int? Decimals { get; set; }

    // ── Format ────────────────────────────────────────────────────────────────

    /// <summary>
    /// How the value shows while the field is not focused: a .NET numeric format such as <c>"N2"</c>, <c>"C"</c> or
    /// <c>"#,##0.0"</c>. While focused the plain number shows, ready to edit. Default: <c>F{Decimals}</c>, else the plain number.
    /// </summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>Culture for formatting and parsing: decimal and group separators, currency symbol. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    // ── Label and text ────────────────────────────────────────────────────────

    /// <summary>Label text, rendered as a <c>&lt;label&gt;</c> tied to the input.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Renders <see cref="Label"/> inside the field, lifted on focus or once there is a value.</summary>
    [Parameter] public bool FloatingLabel { get; set; }

    /// <summary>Placeholder text shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Content inside the field before the number: a currency sign, an icon.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary>Content inside the field after the number: <c>%</c>, a unit.</summary>
    [Parameter] public RenderFragment? Suffix { get; set; }

    // ── Behavior ──────────────────────────────────────────────────────────────

    /// <summary>Shows up and down spin buttons; holding one repeats. Default: <c>true</c>.</summary>
    [Parameter] public bool ShowSpinButtons { get; set; } = true;

    /// <summary>Selects the whole number when the field gains focus, so typing replaces it.</summary>
    [Parameter] public bool SelectOnFocus { get; set; }

    /// <summary>Shows a clear button while there is a value: <c>null</c> for nullable types, else Min or 0.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the native <c>readonly</c> attribute; keys and spin buttons do nothing.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Focuses the input after it first renders.</summary>
    [Parameter] public bool AutoFocus { get; set; }

    // ── Attributes and events ─────────────────────────────────────────────────

    /// <summary>The input's <c>id</c>. Generated when not set, so the label always points at it.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>name</c>, <c>aria-*</c>…) are passed to the <c>&lt;input&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Raised when the input gains focus.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the input loses focus, after the typed value is committed.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnBlur { get; set; }

    /// <summary>Moves keyboard focus to the input.</summary>
    public ValueTask FocusAsync() => input.FocusAsync();

    // ── Numbers ───────────────────────────────────────────────────────────────

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;
    private decimal? MinValue => hasMin ? ToDecimal(Min) : null;
    private decimal? MaxValue => hasMax ? ToDecimal(Max) : null;
    private decimal StepValue => hasStep && ToDecimal(Step) is decimal s && s > 0 ? s : 1m;
    private int? RoundTo => IsIntegral ? 0 : Decimals;

    private static decimal? ToDecimal(TValue? value)
    {
        if (value is null)
            return null;
        try
        {
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static TValue FromDecimal(decimal value) =>
        (TValue)Convert.ChangeType(value, Underlying, CultureInfo.InvariantCulture);

    /// <summary>Rounds, then clamps to Min/Max and to the type's own range.</summary>
    private decimal Normalize(decimal value)
    {
        if (RoundTo is int places)
            value = Math.Round(value, places, MidpointRounding.AwayFromZero);
        if (MinValue is decimal min && value < min)
            value = min;
        if (MaxValue is decimal max && value > max)
            value = max;
        var (typeMin, typeMax) = TypeRange;
        return Math.Clamp(value, typeMin, typeMax);
    }

    private static (decimal Min, decimal Max) TypeRange =>
        Underlying == typeof(int)   ? (int.MinValue, int.MaxValue) :
        Underlying == typeof(long)  ? (long.MinValue, long.MaxValue) :
        Underlying == typeof(short) ? (short.MinValue, short.MaxValue) :
                                      (decimal.MinValue, decimal.MaxValue);   // float and double exceed decimal

    private string DisplayFormat => Format ?? (RoundTo is int places && places > 0 ? $"F{places}" : "0.############################");

    private string FormatDisplay(decimal? value) => value?.ToString(DisplayFormat, EffectiveCulture) ?? "";

    private string FormatEdit(decimal? value) => value?.ToString("0.############################", EffectiveCulture) ?? "";

    private const NumberStyles ParseStyles =
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign |
        NumberStyles.AllowTrailingSign | NumberStyles.AllowParentheses | NumberStyles.AllowDecimalPoint;

    /// <summary>
    /// Parses typed or pasted text in <see cref="EffectiveCulture"/>. Currency and percent symbols and group
    /// separators are dropped first — including the no-break spaces some cultures format with, which
    /// <see cref="NumberStyles.Currency"/> rejects.
    /// </summary>
    private bool TryParse(string text, out decimal? value)
    {
        value = null;
        var format = EffectiveCulture.NumberFormat;
        var cleaned = text
            .Replace(format.CurrencySymbol, "")
            .Replace(format.PercentSymbol, "")
            .Replace("%", "")
            .Replace(format.NumberGroupSeparator, "")
            .Replace(format.CurrencyGroupSeparator, "")
            .Replace("\u00A0", "")
            .Replace("\u202F", "")
            .Trim();
        if (cleaned.Length == 0)
            return text.Trim().Length == 0;
        if (decimal.TryParse(cleaned, ParseStyles, EffectiveCulture, out var parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private string InputId => Id ?? generatedId;
    private bool Editable => !Disabled && !ReadOnly;

    /// <summary>Focused and editable: the plain number shows. A read-only box keeps its formatted text — nothing to edit.</summary>
    private bool Editing => hasFocus && Editable;
    private bool ShowClear => Clearable && Editable && current is not null && (IsNullable || current != ClearedValue);
    private bool AtMin => current is decimal c && MinValue is decimal min && c <= min;
    private bool AtMax => current is decimal c && MaxValue is decimal max && c >= max;

    private string InputModeAttribute => MinValue is decimal min && min >= 0
        ? (IsIntegral ? "numeric" : "decimal")
        : "text";

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-numeric-input"
        : $"nx-numeric-input {validation.CssClass}";

    private string? AriaValueNow => current?.ToString(CultureInfo.InvariantCulture);
    private string? AriaValueMin => MinValue?.ToString(CultureInfo.InvariantCulture);
    private string? AriaValueMax => MaxValue?.ToString(CultureInfo.InvariantCulture);
    private string? AriaValueText => current is null ? null : FormatDisplay(current);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // For a non-nullable TValue, Min="0" and no Min at all look the same once assigned; ask the
        // parameter list which ones were passed. Like the properties themselves, a parameter that is not
        // passed keeps its last state.
        if (parameters.TryGetValue<object?>(nameof(Min), out var min))
            hasMin = min is not null;
        if (parameters.TryGetValue<object?>(nameof(Max), out var max))
            hasMax = max is not null;
        if (parameters.TryGetValue<object?>(nameof(Step), out var step))
            hasStep = step is not null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!IsSupported)
            throw new InvalidOperationException(
                $"NxNumericTextBox supports int, long, short, decimal, double and float, nullable or not; not {typeof(TValue).Name}.");

        validation.Attach(CascadedEditContext, ValueExpression);

        var incoming = ToDecimal(Value);
        if (incoming != current || (!hasFocus && currentText != FormatDisplay(current)))
        {
            current = incoming;
            currentText = Editing ? FormatEdit(current) : FormatDisplay(current);
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is not null)
                await module.InvokeVoidAsync("preventKeys", input, new[] { "ArrowUp", "ArrowDown", "PageUp", "PageDown" });
            if (AutoFocus)
                await input.FocusAsync();
        }
        if (module is not null && appliedSelectOnFocus != SelectOnFocus)
        {
            appliedSelectOnFocus = SelectOnFocus;
            await module.InvokeVoidAsync("editOnFocus", input, SelectOnFocus);
        }
    }

    // ── Committing ────────────────────────────────────────────────────────────

    private decimal ClearedValue => MinValue is decimal min && min > 0 ? min : 0m;

    /// <summary>Parses the typed text. Empty is null (or kept as is for non-nullable types); invalid text reverts.</summary>
    private async Task CommitTextAsync()
    {
        if (!TryParse(currentText, out var parsed) || (parsed is null && !IsNullable))
        {
            currentText = Editing ? FormatEdit(current) : FormatDisplay(current);
            return;
        }
        await CommitAsync(parsed is decimal p ? Normalize(p) : null);
    }

    private async Task CommitAsync(decimal? value)
    {
        var changed = value != current;
        current = value;
        currentText = Editing ? FormatEdit(value) : FormatDisplay(value);
        if (!changed)
            return;
        await ValueChanged.InvokeAsync(value is decimal v ? FromDecimal(v) : default);
        validation.NotifyFieldChanged();
    }

    /// <summary>Adds <paramref name="steps"/> × Step to the typed (or current) value.</summary>
    private async Task StepAsync(int steps)
    {
        if (!Editable)
            return;
        var start = TryParse(currentText, out var typed) && typed is not null ? typed.Value : current ?? ClearedValue;
        var next = Normalize(start + steps * StepValue);
        await CommitAsync(next);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    // @bind protects typed text from a slower render, as in NxTextBox; nothing commits until change.
    private void HandleInputAfter() => currentText ??= "";

    private Task HandleChange() => CommitTextAsync();

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!Editable || e.AltKey || e.CtrlKey || e.MetaKey)
            return;
        switch (e.Key)
        {
            case "ArrowUp":   await StepAsync(+1);  break;
            case "ArrowDown": await StepAsync(-1);  break;
            case "PageUp":    await StepAsync(+10); break;
            case "PageDown":  await StepAsync(-10); break;
            case "Enter":     await CommitTextAsync(); break;
            case "Escape" when Clearable: await ClearAsync(); break;
        }
    }

    private async Task HandleFocus(FocusEventArgs e)
    {
        hasFocus = true;
        // nx-input.js has already swapped in the edit text; only before it has loaded is that done here.
        if (appliedSelectOnFocus is null && Editing && currentText == FormatDisplay(current))
            currentText = FormatEdit(current);
        await OnFocus.InvokeAsync(e);
    }

    private async Task HandleBlur(FocusEventArgs e)
    {
        hasFocus = false;
        StopSpin();
        await CommitTextAsync();
        currentText = FormatDisplay(current);
        await OnBlur.InvokeAsync(e);
    }

    private async Task ClearAsync()
    {
        await CommitAsync(IsNullable ? null : ClearedValue);
    }

    private async Task HandleClearClick()
    {
        await ClearAsync();
        await input.FocusAsync();
    }

    // ── Spin buttons with press-and-hold ──────────────────────────────────────

    private async Task StartSpinAsync(int direction)
    {
        StopSpin();
        if (!Editable)
            return;
        var cts = spin = new CancellationTokenSource();
        await StepAsync(direction);
        try
        {
            await Task.Delay(HoldDelay, cts.Token);
            while (!cts.IsCancellationRequested && !(direction > 0 ? AtMax : AtMin))
            {
                await StepAsync(direction);
                StateHasChanged();
                await Task.Delay(HoldInterval, cts.Token);
            }
        }
        catch (TaskCanceledException)
        {
            // Released.
        }
    }

    private void StopSpin()
    {
        spin?.Cancel();
        spin?.Dispose();
        spin = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        StopSpin();
        validation.Dispose();
        if (module is null)
            return;
        try
        {
            await module.InvokeVoidAsync("dispose", input);
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the browser cleaned up with it.
        }
    }
}
