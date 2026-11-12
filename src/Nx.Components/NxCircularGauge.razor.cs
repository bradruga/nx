using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// A value on a ring: the arc fills clockwise from the top as <see cref="Value"/> goes from <see cref="Min"/> to
/// <see cref="Max"/>, with anything you like in the middle. Telerik's single-scale, single-pointer gauge, flattened.
/// </summary>
/// <example>
/// <code>
/// &lt;NxCircularGauge Value="loadingPercent" AriaLabel="Loading"&gt;
///     &lt;CenterTemplate&gt;@context% loaded&lt;/CenterTemplate&gt;
/// &lt;/NxCircularGauge&gt;
/// </code>
/// </example>
public partial class NxCircularGauge
{
    /// <summary>The value shown; outside <see cref="Min"/>–<see cref="Max"/> it is clamped.</summary>
    [Parameter] public double Value { get; set; }

    /// <summary>The value of an empty ring. Default: <c>0</c>.</summary>
    [Parameter] public double Min { get; set; }

    /// <summary>The value of a full ring. Default: <c>100</c>.</summary>
    [Parameter] public double Max { get; set; } = 100;

    /// <summary>The arc's color. Default: <c>--nx-gauge-pointer</c> (the accent).</summary>
    [Parameter] public string? Color { get; set; }

    /// <summary>Content in the middle of the ring; the context is the (clamped) value.</summary>
    [Parameter] public RenderFragment<double>? CenterTemplate { get; set; }

    /// <summary>The ring's width and height. Default: <c>200px</c>.</summary>
    [Parameter] public string Size { get; set; } = "200px";

    /// <summary>Animates the arc to a new value. Default: <c>true</c>.</summary>
    [Parameter] public bool Transitions { get; set; } = true;

    /// <summary>The accessible name; screen readers also hear the value.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private double Clamped => Max <= Min ? Min : Math.Clamp(Value, Min, Max);
    private double Percent => Max <= Min ? 0 : (Clamped - Min) / (Max - Min) * 100;
    private string? PointerStyle => Color is null ? null : $"stroke: {Color}";
    private string OuterStyle => string.IsNullOrEmpty(Style) ? $"width: {Size}; height: {Size}" : $"width: {Size}; height: {Size}; {Style}";

    private string CssClass
    {
        get
        {
            var css = Transitions ? "nx-gauge" : "nx-gauge nx-gauge-static";
            return string.IsNullOrEmpty(Class) ? css : $"{css} {Class}";
        }
    }

    private static string Invariant(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
