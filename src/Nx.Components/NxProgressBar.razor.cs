using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// A horizontal bar showing how far along something is: <see cref="Value"/> out of <see cref="Max"/>, with a centered
/// label (the percentage, or <see cref="LabelTemplate"/>) that stays readable over the fill and the track. Or
/// <see cref="Indeterminate"/> while the amount is unknown. <see cref="Thin"/> draws a slim bar without the label.
/// </summary>
/// <example>
/// <code>
/// &lt;NxProgressBar Value="summary.PctPiecesProduced" /&gt;
/// &lt;NxProgressBar Value="processed" Max="chunks.Count"&gt;
///     &lt;LabelTemplate&gt;Processing transcript… @@context.Percent.ToString("0")%&lt;/LabelTemplate&gt;
/// &lt;/NxProgressBar&gt;
/// </code>
/// </example>
public partial class NxProgressBar
{
    /// <summary>How far along, from 0 to <see cref="Max"/>; outside that range it is clamped.</summary>
    [Parameter] public double Value { get; set; }

    /// <summary>The value of a full bar. Default: <c>100</c>.</summary>
    [Parameter] public double Max { get; set; } = 100;

    /// <summary>An animated bar for work whose amount is unknown; no value or label.</summary>
    [Parameter] public bool Indeterminate { get; set; }

    /// <summary>Shows the label in the middle of the bar. Default: <c>true</c>.</summary>
    [Parameter] public bool ShowLabel { get; set; } = true;

    /// <summary>The label's content. Default: the percentage, e.g. <c>42%</c>.</summary>
    [Parameter] public RenderFragment<NxProgressBarContext>? LabelTemplate { get; set; }

    /// <summary>A slim bar (<c>--nx-progressbar-thin-height</c>) with no label, for a step counter or a page header.</summary>
    [Parameter] public bool Thin { get; set; }

    /// <summary>The accessible name, e.g. "Pieces produced". Screen readers also hear the percentage.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Extra CSS class on the bar.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the bar, e.g. <c>--nx-progressbar-fill</c>.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the bar.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private double SafeMax => Max > 0 && double.IsFinite(Max) ? Max : 100;
    private double Clamped => double.IsFinite(Value) ? Math.Clamp(Value, 0, SafeMax) : 0;
    private double Percent => Clamped / SafeMax * 100;
    private string PercentText => $"{Math.Round(Percent).ToString(CultureInfo.CurrentCulture)}%";

    private static string Invariant(double value) => value.ToString(CultureInfo.InvariantCulture);

    private string? FillStyle => Indeterminate ? null : $"width: {Invariant(Math.Round(Percent, 3))}%";

    /// <summary>The light copy of the label shows only over the fill.</summary>
    private string ClipStyle => $"clip-path: inset(0 {Invariant(Math.Round(100 - Percent, 3))}% 0 0)";

    private RenderFragment Label => LabelTemplate is not null
        ? LabelTemplate(new NxProgressBarContext(Clamped, SafeMax, Percent))
        : builder => builder.AddContent(0, PercentText);

    private string CssClass => string.Join(' ', new[]
    {
        "nx-progressbar",
        Indeterminate ? "nx-progressbar-indeterminate" : null,
        Thin ? "nx-progressbar-thin" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));
}
