using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// A typed date with no calendar: <see cref="NxDatePicker{TValue}"/>'s field on its own, for a date entered beside
/// an <see cref="NxCalendar"/> or where a popup is in the way. Masked typing in the culture's order, Up and Down
/// step the day, month or year under the caret, min/max, clear and validation.
/// </summary>
/// <typeparam name="TValue"><see cref="DateTime"/> or <see cref="DateOnly"/>, nullable or not. A <see cref="DateTime"/> keeps its time of day.</typeparam>
/// <example>
/// <code>
/// &lt;NxDateInput Label="Ship date" @bind-Value="order.ShipDate" /&gt;
/// </code>
/// </example>
public partial class NxDateInput<TValue> : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-picker.js masks typed digits into the edit pattern (as in NxDatePicker) and steps the part under the
    // caret on Up and Down. Parsing and committing are C#.

    private const string ModulePath = "./_content/Nx.Components/nx-picker.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-dateinput-{Guid.NewGuid():N}"[..22];
    private IJSObjectReference? module;
    private ElementReference input;
    private string currentText = "";
    private DateOnly? current;          // the value as last seen or raised
    private DateFormatter formatter = null!;
    private string? appliedMask;
    private bool hasFocus;

    /// <summary>Creates the component.</summary>
    public NxDateInput() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>The date. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Raised when the value is committed: on Enter, on leaving the field, on Up or Down.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>Earliest allowed date. Typed dates before it are moved up to it.</summary>
    [Parameter] public DateOnly? Min { get; set; }

    /// <summary>Latest allowed date. Typed dates after it are moved back to it.</summary>
    [Parameter] public DateOnly? Max { get; set; }

    /// <summary>How the date shows while the field is not focused, as in <see cref="NxDatePicker{TValue}.Format"/>. Default: <c>"d"</c>.</summary>
    [Parameter] public string Format { get; set; } = "d";

    /// <summary>Culture for the format and the typing order. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Label text, rendered as a <c>&lt;label&gt;</c> tied to the input.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Renders <see cref="Label"/> inside the field, lifted on focus or once there is a value.</summary>
    [Parameter] public bool FloatingLabel { get; set; }

    /// <summary>Placeholder text shown while the field is empty. Default: the edit pattern, e.g. <c>mm/dd/yyyy</c>.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Shows a clear button while there is a date. Nullable types only.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the native <c>readonly</c> attribute.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Focuses the input after it first renders.</summary>
    [Parameter] public bool AutoFocus { get; set; }

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

    /// <summary>Raised when the input loses focus, after the typed date is committed.</summary>
    [Parameter] public EventCallback<FocusEventArgs> OnBlur { get; set; }

    /// <summary>Moves keyboard focus to the input.</summary>
    public ValueTask FocusAsync() => input.FocusAsync();

    private string InputId => Id ?? generatedId;
    private bool Editable => !Disabled && !ReadOnly;
    private bool Editing => hasFocus && Editable;
    private bool ShowClear => Clearable && DateConversion<TValue>.IsNullable && Editable && current is not null;
    private string Shown(DateOnly? date) => Editing ? formatter.Edit(date) : formatter.Display(date);

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass)
        ? "nx-dateinput-input"
        : $"nx-dateinput-input {validation.CssClass}";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!DateConversion<TValue>.IsSupported)
            throw new InvalidOperationException(DateConversion<TValue>.Unsupported("NxDateInput"));

        validation.Attach(CascadedEditContext, ValueExpression);
        formatter = new DateFormatter(Format, Culture ?? CultureInfo.CurrentCulture);

        var incoming = DateConversion<TValue>.ToDate(Value);
        if (incoming != current || (!hasFocus && currentText != formatter.Display(current)))
        {
            current = incoming;
            currentText = Shown(current);
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (module is not null && AutoFocus)
                await input.FocusAsync();
        }
        if (module is not null && appliedMask != formatter.Mask)
        {
            appliedMask = formatter.Mask;
            await module.InvokeVoidAsync("mask", input, appliedMask);
            await module.InvokeVoidAsync("stepDate", input, appliedMask, formatter.Order);
        }
    }

    /// <summary>
    /// Parses the typed text. Empty is null (kept as is for non-nullable types); unreadable text reverts; dates
    /// outside Min/Max are moved to the nearest limit.
    /// </summary>
    private async Task CommitTextAsync()
    {
        if (!formatter.TryParse(currentText, out var parsed) || (parsed is null && !DateConversion<TValue>.IsNullable))
        {
            currentText = Shown(current);
            return;
        }
        if (parsed is DateOnly d)
        {
            if (Min is DateOnly min && d < min) d = min;
            if (Max is DateOnly max && d > max) d = max;
            parsed = d;
        }
        await CommitAsync(parsed);
    }

    private async Task CommitAsync(DateOnly? date)
    {
        var changed = date != current;
        current = date;
        currentText = Shown(date);
        if (!changed)
            return;
        await ValueChanged.InvokeAsync(DateConversion<TValue>.FromDate(date, Value));
        validation.NotifyFieldChanged();
    }

    // @bind protects typed text from a slower render; nothing commits until change, Enter or blur.
    private void HandleInputAfter() => currentText ??= "";

    private Task HandleChange() => CommitTextAsync();

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!Editable)
            return;
        if (e.Key == "Enter")
            await CommitTextAsync();
        else if (e.Key == "Escape" && ShowClear)
            await CommitAsync(null);
    }

    private async Task HandleFocus(FocusEventArgs e)
    {
        hasFocus = true;
        if (Editing && currentText == formatter.Display(current))
            currentText = formatter.Edit(current);
        await OnFocus.InvokeAsync(e);
    }

    private async Task HandleBlur(FocusEventArgs e)
    {
        hasFocus = false;
        await CommitTextAsync();
        currentText = formatter.Display(current);
        await OnBlur.InvokeAsync(e);
    }

    private async Task HandleClearClick()
    {
        await CommitAsync(null);
        await input.FocusAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        validation.Dispose();
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("disposeStepper", input);
                await module.InvokeVoidAsync("dispose", input);
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
    }
}
