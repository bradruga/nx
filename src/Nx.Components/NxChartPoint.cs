namespace Nx.Components;

/// <summary>One point of an <see cref="NxChart"/>: the tooltip template's context and the click event's data.</summary>
public sealed class NxChartPoint
{
    internal NxChartPoint(string? seriesName, object? dataItem, object? category, double? value, string formattedValue, double? percentage)
    {
        SeriesName = seriesName;
        DataItem = dataItem;
        Category = category;
        Value = value;
        FormattedValue = formattedValue;
        Percentage = percentage;
    }

    /// <summary>The series' <c>Name</c>.</summary>
    public string? SeriesName { get; }

    /// <summary>The item from the series' <c>Data</c> (for a date category, the first item in it).</summary>
    public object? DataItem { get; }

    /// <summary>The category: the axis text, or the date at the start of a date category.</summary>
    public object? Category { get; }

    /// <summary>The value drawn.</summary>
    public double? Value { get; }

    /// <summary>The value through the value axis' label format.</summary>
    public string FormattedValue { get; }

    /// <summary>For a pie slice, its share of the whole, from 0 to 1.</summary>
    public double? Percentage { get; }
}
