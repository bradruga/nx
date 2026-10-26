using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// A chart drawn as SVG by .NET, with no chart library: line, area, column, bar and pie series
/// (<see cref="NxChartSeries"/>), a category axis (<see cref="NxChartCategoryAxis"/>, plain or dates) and value axes
/// (<see cref="NxChartValueAxis"/>), a legend that hides and shows series, and tooltips on hover, tap or the arrow
/// keys. It redraws to fit its container as that resizes.
/// </summary>
/// <example>
/// <code>
/// &lt;NxChart Title="Loads shipped" Height="320px"&gt;
///     &lt;NxChartSeries Name="Loads" Data="days" Field="@nameof(Day.Loads)" CategoryField="@nameof(Day.Date)" /&gt;
/// &lt;/NxChart&gt;
/// </code>
/// </example>
public partial class NxChart
{
    private readonly List<NxChartSeries> series = [];
    private readonly List<NxChartValueAxis> valueAxes = [];
    private readonly List<NxChartCategoryAxis> categoryAxes = [];
    private NxChartSurface? surface;

    /// <summary>The series and axes.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>A title above the chart.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>CSS height, e.g. <c>320px</c>, or <c>100%</c> of a container with a height. Default: <c>400px</c>.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>CSS width. Default: <c>100%</c>.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Where the legend sits. Default: <see cref="NxChartLegendPosition.Bottom"/>.</summary>
    [Parameter] public NxChartLegendPosition LegendPosition { get; set; } = NxChartLegendPosition.Bottom;

    /// <summary>Shows a tooltip on hover, tap and the arrow keys. Default: <c>true</c>.</summary>
    [Parameter] public bool ShowTooltips { get; set; } = true;

    /// <summary>The tooltip of every series without its own <c>TooltipTemplate</c>.</summary>
    [Parameter] public RenderFragment<NxChartPoint>? TooltipTemplate { get; set; }

    /// <summary>Raised when a point, column, bar or slice is clicked (or Enter pressed on it).</summary>
    [Parameter] public EventCallback<NxChartPoint> OnSeriesClick { get; set; }

    /// <summary>The culture for numbers, dates and week starts. Default: the current culture.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>The chart's accessible name. Default: <see cref="Title"/>.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The outer element's <c>id</c>.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class applied to the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes are passed to the outer element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Measures the container again and redraws, e.g. after it was shown. Resizes are followed on their own.</summary>
    public Task RefreshAsync() => surface?.RefreshAsync() ?? Task.CompletedTask;

    internal IReadOnlyList<NxChartSeries> Series => series;
    internal IReadOnlyList<NxChartValueAxis> ValueAxes => valueAxes;
    internal NxChartCategoryAxis? CategoryAxis => categoryAxes.FirstOrDefault();

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-chart" : $"nx-chart {Class}";

    private string RootStyle
    {
        get
        {
            var style = $"height: {Height ?? "400px"}; width: {Width ?? "100%"};";
            return string.IsNullOrEmpty(Style) ? style : $"{style} {Style}";
        }
    }

    internal void Register(NxChartSeries item) => series.Add(item);
    internal void Unregister(NxChartSeries item) => series.Remove(item);
    internal void Register(NxChartValueAxis item) => valueAxes.Add(item);
    internal void Unregister(NxChartValueAxis item) => valueAxes.Remove(item);
    internal void Register(NxChartCategoryAxis item) => categoryAxes.Add(item);
    internal void Unregister(NxChartCategoryAxis item) => categoryAxes.Remove(item);
}
