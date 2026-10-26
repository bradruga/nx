namespace Nx.Components;

/// <summary>What an <see cref="NxChartCategoryAxis"/> holds.</summary>
public enum NxChartAxisType
{
    /// <summary>Dates when every category value is a date, else plain categories.</summary>
    Auto,

    /// <summary>Plain categories, in the order they come.</summary>
    Category,

    /// <summary>Dates grouped by <see cref="NxChartCategoryAxis.BaseUnit"/>, every step shown even when it has no data.</summary>
    Date
}
