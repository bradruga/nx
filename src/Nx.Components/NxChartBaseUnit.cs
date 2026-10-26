namespace Nx.Components;

/// <summary>The step of a date category axis: one category per hour, day, week, month or year.</summary>
public enum NxChartBaseUnit
{
    /// <summary>Picked from the dates: hours within a day or two, months when every date is the 1st, else days.</summary>
    Auto,

    /// <summary>One category per hour.</summary>
    Hours,

    /// <summary>One category per day.</summary>
    Days,

    /// <summary>One category per week, starting on the culture's first day of the week.</summary>
    Weeks,

    /// <summary>One category per month.</summary>
    Months,

    /// <summary>One category per year.</summary>
    Years
}
