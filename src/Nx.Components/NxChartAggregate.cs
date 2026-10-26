namespace Nx.Components;

/// <summary>How a series combines the items that fall in one date category (an hour, day, month…).</summary>
public enum NxChartAggregate
{
    /// <summary>The largest value.</summary>
    Max,

    /// <summary>The smallest value.</summary>
    Min,

    /// <summary>The total.</summary>
    Sum,

    /// <summary>The average.</summary>
    Average,

    /// <summary>How many items there are.</summary>
    Count,

    /// <summary>The first item's value.</summary>
    First
}
