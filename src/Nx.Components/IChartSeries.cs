using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>An <see cref="NxChartSeries{TItem}"/> as its <see cref="NxChart"/> reads it, whatever its item type.</summary>
internal interface IChartSeries
{
    NxChartSeriesType Type { get; }
    string? Name { get; }
    string? Color { get; }
    string? Axis { get; }
    bool Stacked { get; }
    NxChartDashType DashType { get; }
    double? LineWidth { get; }
    string? LineColor { get; }
    bool? Markers { get; }
    bool VisibleInLegend { get; }
    NxChartAggregate Aggregate { get; }
    bool ShowLabels { get; }
    string? LabelFormat { get; }
    double Gap { get; }
    double Spacing { get; }
    RenderFragment<NxChartPoint>? TooltipTemplate { get; }

    /// <summary>Whether items carry their own category (<c>ItemCategory</c>).</summary>
    bool HasCategory { get; }

    /// <summary>Every item with its value, category and color.</summary>
    IEnumerable<(object? Item, object? Value, object? Category, string? Color)> Read();
}
