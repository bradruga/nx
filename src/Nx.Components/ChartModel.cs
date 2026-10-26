using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Nx.Components;

/// <summary>
/// An <see cref="NxChart"/>'s data, ready to lay out: the categories (plain, or date steps with their items combined),
/// every series' values per category with its stacking base, and each value axis' range. Size-independent.
/// </summary>
internal sealed class ChartModel
{
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>> Getters = new();

    /// <summary>A series' values, one per category (or per slice for a pie).</summary>
    public sealed class Series
    {
        public required NxChartSeries Source { get; init; }
        public required int Index { get; init; }
        public required string Color { get; init; }
        public required double?[] Values { get; init; }
        public required object?[] Items { get; init; }
        public required string?[] PointColors { get; init; }
        public required bool Visible { get; init; }
        public double[] Base { get; set; } = [];
        public Axis Axis { get; set; } = null!;
        public int Slot { get; set; }
        public NxChartSeriesType Type => Source.Type;
        public bool HasMarkers => Source.Markers ?? Type == NxChartSeriesType.Line;
        public double Top(int i) => (Values[i] ?? 0) + (Base.Length > i ? Base[i] : 0);
    }

    /// <summary>A value axis and the range it shows.</summary>
    public sealed class Axis
    {
        public NxChartValueAxis? Source { get; init; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Step { get; set; }
        public int Side { get; set; }      // 0 left (or bottom for bars), 1 right (or top), -1 not drawn
        public string? Format => Source?.LabelFormat;
        public string? Title => Source?.Title;
        public bool Visible => Source?.Visible ?? true;
        public double Ratio(double value) => Max == Min ? 0 : (value - Min) / (Max - Min);

        public IEnumerable<double> Ticks()
        {
            for (var v = Min; v <= Max + Step / 1e6; v += Step)
                yield return Math.Abs(v) < Step / 1e6 ? 0 : v;
        }
    }

    public required IReadOnlyList<Series> AllSeries { get; init; }
    public required IReadOnlyList<object?> Categories { get; init; }
    public required IReadOnlyList<string> Labels { get; init; }
    public required IReadOnlyList<Axis> Axes { get; init; }
    public required bool IsPie { get; init; }
    public required bool IsHorizontal { get; init; }
    public required bool IsDate { get; init; }
    public int Slots { get; init; }

    public IEnumerable<Series> Shown => AllSeries.Where(s => s.Visible);

    // ── Building ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the model. <paramref name="hidden"/> holds the series (and, for a pie, the slices) the legend switched
    /// off, by index; <paramref name="culture"/> formats labels and buckets weeks.
    /// </summary>
    public static ChartModel Build(IReadOnlyList<NxChartSeries> series, NxChartCategoryAxis? categoryAxis,
        IReadOnlyList<NxChartValueAxis> valueAxes, ISet<int> hidden, CultureInfo culture, int targetTicks)
    {
        var pie = series.FirstOrDefault(s => s.Type == NxChartSeriesType.Pie);
        if (pie is not null)
            return BuildPie(pie, categoryAxis, hidden, culture);

        var raw = series.Select(s => Read(s)).ToList();
        var axisCategories = categoryAxis?.Categories?.Cast<object?>().ToList();
        var anyField = series.Any(s => s.CategoryField is not null);
        var isDate = categoryAxis?.Type == NxChartAxisType.Date
                     || (categoryAxis?.Type is null or NxChartAxisType.Auto && anyField
                         && raw.SelectMany(r => r.Categories).Where(c => c is not null).DefaultIfEmpty("x").All(c => AsDate(c) is not null));

        // Categories: date steps from the first to the last date, or every distinct category in the order met.
        List<object?> categories;
        NxChartBaseUnit unit = NxChartBaseUnit.Days;
        if (isDate)
        {
            var dates = raw.SelectMany(r => r.Categories).Select(AsDate).OfType<DateTime>().ToList();
            dates.AddRange(axisCategories?.Select(AsDate).OfType<DateTime>() ?? []);
            unit = categoryAxis?.BaseUnit is { } b and not NxChartBaseUnit.Auto ? b : AutoUnit(dates);
            categories = [];
            if (dates.Count > 0)
            {
                var first = Floor(dates.Min(), unit, culture);
                var last = Floor(dates.Max(), unit, culture);
                for (var d = first; d <= last && categories.Count < 2000; d = Next(d, unit))
                    categories.Add(d);
            }
        }
        else
        {
            categories = axisCategories ?? [];
            foreach (var r in raw.Where(r => r.Source.CategoryField is not null))
            {
                foreach (var c in r.Categories)
                {
                    if (!categories.Any(k => SameCategory(k, c)))
                        categories.Add(c);
                }
            }
            var longest = raw.Where(r => r.Source.CategoryField is null).Select(r => r.Values.Count).DefaultIfEmpty(0).Max();
            while (categories.Count < longest)
                categories.Add((categories.Count + 1).ToString(culture));
        }

        // Each series' values per category.
        var built = new List<Series>();
        for (var s = 0; s < raw.Count; s++)
        {
            var r = raw[s];
            var values = new double?[categories.Count];
            var items = new object?[categories.Count];
            var colors = new string?[categories.Count];
            if (r.Source.CategoryField is null)
            {
                for (var i = 0; i < Math.Min(r.Values.Count, categories.Count); i++)
                    (values[i], items[i], colors[i]) = (r.Values[i], r.Items[i], r.Colors[i]);
            }
            else
            {
                var buckets = new List<double>[categories.Count];
                for (var n = 0; n < r.Values.Count; n++)
                {
                    var at = isDate
                        ? (AsDate(r.Categories[n]) is DateTime d ? IndexOf(categories, Floor(d, unit, culture)) : -1)
                        : categories.FindIndex(k => SameCategory(k, r.Categories[n]));
                    if (at < 0)
                        continue;
                    items[at] ??= r.Items[n];
                    colors[at] ??= r.Colors[n];
                    if (r.Values[n] is double v)
                        (buckets[at] ??= []).Add(v);
                    else if (r.Source.Aggregate == NxChartAggregate.Count)
                        buckets[at] ??= [];
                }
                for (var i = 0; i < categories.Count; i++)
                    values[i] = buckets[i] is { } list ? Combine(list, r.Source.Aggregate) : null;
            }
            built.Add(new Series
            {
                Source = r.Source,
                Index = s,
                Color = r.Source.Color ?? Palette(s),
                Values = values,
                Items = items,
                PointColors = colors,
                Visible = !hidden.Contains(s)
            });
        }

        // Value axes, and which series each measures.
        var axes = valueAxes.Count == 0
            ? [new Axis()]
            : valueAxes.Select(a => new Axis { Source = a }).ToList();
        foreach (var s in built)
            s.Axis = axes.FirstOrDefault(a => s.Source.Axis is not null && a.Source?.Name == s.Source.Axis) ?? axes[0];
        var side = 0;
        foreach (var a in axes)
            a.Side = a.Visible && side < 2 ? side++ : -1;

        // Stacks: running totals of the stacked series of one type on one axis, positive and negative apart.
        foreach (var group in built.Where(s => s.Visible && s.Source.Stacked).GroupBy(s => (s.Type, s.Axis)))
        {
            var up = new double[categories.Count];
            var down = new double[categories.Count];
            foreach (var s in group)
            {
                s.Base = new double[categories.Count];
                for (var i = 0; i < categories.Count; i++)
                {
                    var v = s.Values[i] ?? 0;
                    s.Base[i] = v >= 0 ? up[i] : down[i];
                    if (v >= 0) up[i] += v; else down[i] += v;
                }
            }
        }

        // Column and bar slots side by side in a category: one per unstacked series, one per stack.
        var slots = 0;
        var stackSlots = new Dictionary<(NxChartSeriesType, Axis), int>();
        foreach (var s in built.Where(s => s.Visible && s.Type is NxChartSeriesType.Column or NxChartSeriesType.Bar))
        {
            if (s.Source.Stacked)
            {
                if (!stackSlots.TryGetValue((s.Type, s.Axis), out var slot))
                    stackSlots[(s.Type, s.Axis)] = slot = slots++;
                s.Slot = slot;
            }
            else
                s.Slot = slots++;
        }

        // Ranges.
        foreach (var a in axes)
        {
            var mine = built.Where(s => s.Visible && s.Axis == a).ToList();
            var lows = mine.SelectMany(s => Enumerable.Range(0, categories.Count).Where(i => s.Values[i] is not null)
                .SelectMany(i => new[] { s.Top(i), s.Base.Length > i ? s.Base[i] : s.Values[i]!.Value })).ToList();
            var (min, max, step) = ChartMath.Scale(lows.DefaultIfEmpty(0).Min(), lows.DefaultIfEmpty(1).Max(), targetTicks,
                a.Source?.NarrowRange == true, a.Source?.Min, a.Source?.Max);
            (a.Min, a.Max, a.Step) = (min, max, step);
        }

        // Labels.
        var given = categoryAxis?.Labels?.ToList();
        var format = categoryAxis?.LabelFormat ?? (isDate ? DateFormat(unit, categories) : null);
        var labels = categories.Select((c, i) => given is not null
            ? (i < given.Count ? given[i] ?? "" : "")
            : ChartMath.Format(c, format, culture)).ToList();

        return new ChartModel
        {
            AllSeries = built,
            Categories = categories,
            Labels = labels,
            Axes = axes,
            IsPie = false,
            IsHorizontal = built.Any(s => s.Type == NxChartSeriesType.Bar),
            IsDate = isDate,
            Slots = Math.Max(1, slots)
        };
    }

    private static ChartModel BuildPie(NxChartSeries pie, NxChartCategoryAxis? categoryAxis, ISet<int> hidden, CultureInfo culture)
    {
        var r = Read(pie);
        var given = categoryAxis?.Categories?.Cast<object?>().ToList();
        var categories = r.Values.Select((_, i) => pie.CategoryField is not null ? r.Categories[i] : given is not null && i < given.Count ? given[i] : (object)(i + 1)).ToList();
        var colors = r.Colors.Select((c, i) => c ?? pie.Color ?? Palette(i)).ToArray();
        var series = new Series
        {
            Source = pie,
            Index = 0,
            Color = colors.FirstOrDefault() ?? Palette(0),
            Values = r.Values.Select((v, i) => hidden.Contains(i) ? null : v).ToArray(),
            Items = r.Items.ToArray(),
            PointColors = colors,
            Visible = true,
            Axis = new Axis()
        };
        return new ChartModel
        {
            AllSeries = [series],
            Categories = categories,
            Labels = categories.Select(c => ChartMath.Format(c, categoryAxis?.LabelFormat, culture)).ToList(),
            Axes = [series.Axis],
            IsPie = true,
            IsHorizontal = false,
            IsDate = false
        };
    }

    // ── Reading items ─────────────────────────────────────────────────────────

    private sealed record RawSeries(NxChartSeries Source, List<double?> Values, List<object?> Categories, List<object?> Items, List<string?> Colors);

    private static RawSeries Read(NxChartSeries s)
    {
        var values = new List<double?>();
        var categories = new List<object?>();
        var items = new List<object?>();
        var colors = new List<string?>();
        foreach (var item in s.Data ?? Array.Empty<object>())
        {
            items.Add(item);
            values.Add(ToDouble(s.Field is null ? item : Get(item, s.Field)));
            categories.Add(s.CategoryField is null ? null : Get(item, s.CategoryField));
            colors.Add(s.ColorField is null ? null : Get(item, s.ColorField)?.ToString());
        }
        return new RawSeries(s, values, categories, items, colors);
    }

    private static object? Get(object? item, string property)
    {
        if (item is null)
            return null;
        var getter = Getters.GetOrAdd((item.GetType(), property), key =>
        {
            var info = key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance)
                       ?? throw new InvalidOperationException($"NxChart: {key.Item1.Name} has no public property '{key.Item2}'.");
            return o => info.GetValue(o);
        });
        return getter(item);
    }

    private static double? ToDouble(object? value) => value switch
    {
        null => null,
        double d => double.IsNaN(d) ? null : d,
        IConvertible c when value is not string and not DateTime and not bool => c.ToDouble(CultureInfo.InvariantCulture),
        _ => null
    };

    private static double Combine(List<double> values, NxChartAggregate aggregate) => aggregate switch
    {
        NxChartAggregate.Sum => values.Sum(),
        NxChartAggregate.Min => values.Count == 0 ? 0 : values.Min(),
        NxChartAggregate.Average => values.Count == 0 ? 0 : values.Average(),
        NxChartAggregate.Count => values.Count,
        NxChartAggregate.First => values.Count == 0 ? 0 : values[0],
        _ => values.Count == 0 ? 0 : values.Max()
    };

    private static bool SameCategory(object? a, object? b) =>
        Equals(a, b) || (a is not null && b is not null && a.ToString() == b.ToString());

    // ── Dates ─────────────────────────────────────────────────────────────────

    private static DateTime? AsDate(object? value) => value switch
    {
        DateTime d => d,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        DateTimeOffset d => d.DateTime,
        _ => null
    };

    private static int IndexOf(List<object?> categories, DateTime date) =>
        categories.FindIndex(c => c is DateTime d && d == date);

    private static NxChartBaseUnit AutoUnit(List<DateTime> dates)
    {
        if (dates.Count == 0)
            return NxChartBaseUnit.Days;
        if (dates.Any(d => d.TimeOfDay != TimeSpan.Zero))
            return (dates.Max() - dates.Min()).TotalDays <= 3 ? NxChartBaseUnit.Hours : NxChartBaseUnit.Days;
        return dates.All(d => d.Day == 1) && dates.Select(d => (d.Year, d.Month)).Distinct().Count() > 1
            ? NxChartBaseUnit.Months
            : NxChartBaseUnit.Days;
    }

    private static DateTime Floor(DateTime d, NxChartBaseUnit unit, CultureInfo culture) => unit switch
    {
        NxChartBaseUnit.Hours => d.Date.AddHours(d.Hour),
        NxChartBaseUnit.Weeks => d.Date.AddDays(-(((int)d.DayOfWeek - (int)culture.DateTimeFormat.FirstDayOfWeek + 7) % 7)),
        NxChartBaseUnit.Months => new DateTime(d.Year, d.Month, 1),
        NxChartBaseUnit.Years => new DateTime(d.Year, 1, 1),
        _ => d.Date
    };

    private static DateTime Next(DateTime d, NxChartBaseUnit unit) => unit switch
    {
        NxChartBaseUnit.Hours => d.AddHours(1),
        NxChartBaseUnit.Weeks => d.AddDays(7),
        NxChartBaseUnit.Months => d.AddMonths(1),
        NxChartBaseUnit.Years => d.AddYears(1),
        _ => d.AddDays(1)
    };

    private static string DateFormat(NxChartBaseUnit unit, List<object?> categories) => unit switch
    {
        NxChartBaseUnit.Hours => categories.Count > 0 && categories[0] is DateTime f && categories[^1] is DateTime l && f.Date != l.Date ? "M/d HH:mm" : "HH:mm",
        NxChartBaseUnit.Months => "MMM yy",
        NxChartBaseUnit.Years => "yyyy",
        _ => "M/d"
    };

    /// <summary>The default color of the <paramref name="index"/>th series or slice: the theme's chart palette, round and round.</summary>
    public static string Palette(int index) => $"var(--nx-chart-{index % 8 + 1})";
}
