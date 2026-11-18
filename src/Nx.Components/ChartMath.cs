using System.Globalization;

namespace Nx.Components;

/// <summary>Number formatting, round axis steps and SVG number text for <see cref="NxChart"/>.</summary>
internal static class ChartMath
{
    /// <summary>A .NET format (<c>C0</c>, <c>MMM yy</c>) or the composite form (<c>{0:C0}</c>) applied to a value.</summary>
    public static string Format(object? value, string? format, CultureInfo culture)
    {
        if (value is null)
            return "";
        if (string.IsNullOrEmpty(format))
            return value is IFormattable f ? f.ToString(value is double or float or decimal ? "#,0.##" : null, culture) : value.ToString() ?? "";
        if (format.Contains("{0"))
            return string.Format(culture, format, value);
        return value is IFormattable formattable ? formattable.ToString(format, culture) : value.ToString() ?? "";
    }

    /// <summary>A round step (1, 2 or 5 times a power of ten) near <paramref name="raw"/>.</summary>
    public static double NiceStep(double raw)
    {
        if (raw <= 0 || double.IsNaN(raw) || double.IsInfinity(raw))
            return 1;
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / power;
        var nice = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10;
        return nice * power;
    }

    /// <summary>
    /// A value axis' range and step: round numbers around the data, starting at 0 unless <paramref name="narrow"/>, with
    /// about <paramref name="ticks"/> steps. <paramref name="min"/> and <paramref name="max"/> override either end.
    /// </summary>
    public static (double Min, double Max, double Step) Scale(double dataMin, double dataMax, int ticks, bool narrow, double? min, double? max)
    {
        if (double.IsInfinity(dataMin) || double.IsInfinity(dataMax))
            (dataMin, dataMax) = (0, 1);
        if (!narrow)
        {
            dataMin = Math.Min(dataMin, 0);
            dataMax = Math.Max(dataMax, 0);
        }
        else
        {
            // A little room above and below, so the line does not run along the frame.
            var pad = (dataMax - dataMin) * 0.05;
            dataMin -= pad;
            dataMax += pad;
        }
        if (min is double lo) dataMin = lo;
        if (max is double hi) dataMax = hi;
        if (dataMax <= dataMin)
            dataMax = dataMin + (dataMin == 0 ? 1 : Math.Abs(dataMin) * 0.1);

        var step = NiceStep((dataMax - dataMin) / Math.Max(1, ticks));
        var niceMin = min ?? Math.Floor(dataMin / step) * step;
        var niceMax = max ?? Math.Ceiling(dataMax / step) * step;
        if (niceMax <= niceMin)
            niceMax = niceMin + step;
        return (niceMin, niceMax, step);
    }

    /// <summary>A number for an SVG attribute: invariant, at most two decimals.</summary>
    public static string N(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The rough width of a line of text, for laying out labels before the browser draws them.</summary>
    public static double TextWidth(string? text, double fontSize = 12) => (text?.Length ?? 0) * fontSize * 0.56;
}
