using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// Picks a number between <see cref="Min"/> and <see cref="Max"/> by dragging a thumb. A native range input, so
/// arrows, Page Up/Down, Home and End, touch and screen readers work as everywhere; adds a filled track, labelled
/// ticks every <see cref="LargeStep"/>, optional − / + buttons and the value beside it.
/// </summary>
/// <typeparam name="TValue">A number type: <c>int</c>, <c>long</c>, <c>decimal</c>, <c>double</c>…</typeparam>
/// <example>
/// <code>
/// &lt;NxSlider TValue="int" Min="0" Max="10" LargeStep="5" @bind-Value="level" /&gt;
/// </code>
/// </example>
public partial class NxSlider<TValue> : IDisposable where TValue : struct, System.Numerics.INumber<TValue>
{
    private readonly FieldValidation validation;
    private readonly string generatedId = $"nx-slider-{Guid.NewGuid():N}"[..18];
    private TValue current;
    private TValue? lastParameter;
    private TValue lastRaised;
    private string currentText = "0";

    /// <summary>Creates the component.</summary>
    public NxSlider() => validation = new FieldValidation(StateHasChanged);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>The number. Use <c>@bind-Value</c>.</summary>
    [Parameter] public TValue Value { get; set; }

    /// <summary>Raised as the thumb moves (or on release, without <see cref="Immediate"/>), and on the buttons.</summary>
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<TValue>>? ValueExpression { get; set; }

    /// <summary>The lowest value. Default: <c>0</c>.</summary>
    [Parameter] public TValue Min { get; set; } = TValue.Zero;

    /// <summary>The highest value. Default: <c>10</c>.</summary>
    [Parameter] public TValue Max { get; set; } = TValue.CreateChecked(10);

    /// <summary>One step of the thumb, the arrow keys and the buttons. Default: <c>1</c>.</summary>
    [Parameter] public TValue SmallStep { get; set; } = TValue.One;

    /// <summary>A labelled tick every this many; <c>0</c> for no ticks. Default: <c>0</c>.</summary>
    [Parameter] public TValue LargeStep { get; set; } = TValue.Zero;

    /// <summary>A .NET number format for the tick labels and the shown value, e.g. <c>"0.0"</c> or <c>"P0"</c>.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>Raises <c>ValueChanged</c> while dragging; <c>false</c> raises it once, on release. Default: <c>true</c>.</summary>
    [Parameter] public bool Immediate { get; set; } = true;

    /// <summary>Shows − and + buttons at the ends that move one <see cref="SmallStep"/>.</summary>
    [Parameter] public bool ShowButtons { get; set; }

    /// <summary>Shows the value after the slider.</summary>
    [Parameter] public bool ShowValue { get; set; }

    /// <summary>Label text, rendered as a <c>&lt;label&gt;</c> tied to the slider.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The range input's <c>id</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>aria-label</c>, <c>name</c>…) are passed to the range input.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string InputId => Id ?? generatedId;
    private string ValueText => TickText(current);
    private string RootStyle => $"--nx-slider-percent: {Percent(current)}; {Style}".TrimEnd(' ', ';');

    private string CssClass => string.Join(' ', new[]
    {
        "nx-slider",
        Disabled ? "nx-slider-disabled" : null,
        validation.IsInvalid ? "nx-slider-invalid" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    private string InputCssClass => string.IsNullOrEmpty(validation.CssClass) ? "nx-slider-input" : $"nx-slider-input {validation.CssClass}";

    private List<TValue> Ticks
    {
        get
        {
            var ticks = new List<TValue>();
            if (LargeStep <= TValue.Zero || Max <= Min)
                return ticks;
            for (var tick = Min; tick <= Max && ticks.Count <= 100; tick += LargeStep)
                ticks.Add(tick);
            return ticks;
        }
    }

    private static string Text(TValue value) => value.ToString(null, CultureInfo.InvariantCulture);

    private string TickText(TValue value) => value.ToString(Format, CultureInfo.CurrentCulture);

    private string Percent(TValue value)
    {
        var span = double.CreateChecked(Max - Min);
        var at = span <= 0 ? 0 : Math.Clamp(double.CreateChecked(value - Min) / span, 0, 1);
        return (at * 100).ToString("0.###", CultureInfo.InvariantCulture) + "%";
    }

    private TValue Clamp(TValue value) => TValue.Clamp(value, Min, TValue.Max(Min, Max));

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        validation.Attach(CascadedEditContext, ValueExpression);
        // Only an outside change replaces where the thumb is.
        if (lastParameter != Value)
        {
            lastParameter = Value;
            current = lastRaised = Clamp(Value);
        }
        currentText = Text(current);
    }

    private async Task HandleInputAfter()
    {
        if (!TValue.TryParse(currentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed))
            return;
        current = Clamp(typed);
        if (Immediate)
            await RaiseAsync();
    }

    private Task HandleChange() => Immediate ? Task.CompletedTask : RaiseAsync();

    private async Task StepAsync(int direction)
    {
        current = Clamp(current + (direction < 0 ? -SmallStep : SmallStep));
        currentText = Text(current);
        await RaiseAsync();
    }

    private async Task RaiseAsync()
    {
        if (lastRaised == current)
            return;
        lastRaised = current;
        await ValueChanged.InvokeAsync(current);
        validation.NotifyFieldChanged();
    }

    /// <inheritdoc />
    public void Dispose() => validation.Dispose();
}
