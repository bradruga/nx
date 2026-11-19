namespace Nx.Components;

/// <summary>How an <see cref="NxChartSeries{TItem}"/> is drawn.</summary>
public enum NxChartSeriesType
{
    /// <summary>Points joined by straight lines, with markers.</summary>
    Line,

    /// <summary>A line with the space under it filled.</summary>
    Area,

    /// <summary>Vertical bars, one per category.</summary>
    Column,

    /// <summary>Horizontal bars, one per category; the categories run down the side.</summary>
    Bar,

    /// <summary>Slices of a circle, one per item; no axes.</summary>
    Pie
}
