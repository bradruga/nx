using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

/// <summary>
/// Draws an <see cref="NxChart"/>: lays the model out in the measured size and renders the SVG, legend, tooltip and
/// screen-reader table. Rendered by NxChart after its series; not for use on its own.
/// </summary>
public partial class NxChartSurface : IAsyncDisposable
{
    // ── JS interop ────────────────────────────────────────────────────────────
    // nx-chart.js reports the plot's size as it resizes, follows the pointer to the nearest point (only a change of
    // point crosses to .NET) and cancels the page scroll of the keys that move between points.

    private const string ModulePath = "./_content/Nx.Components/nx-chart.js";
    private const double Font = 12;
    private static readonly string[] PlotKeys = ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End"];

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly string id = $"nx-chart-{Guid.NewGuid():N}"[..17];
    private readonly HashSet<int> hidden = [];
    private IJSObjectReference? module;
    private DotNetObjectReference<NxChartSurface>? selfReference;
    private ElementReference plot;
    private double width, height;
    private ChartModel model = null!;
    private Drawing drawing = new();
    private (int Series, int Index)? active;

    /// <summary>The chart this surface draws.</summary>
    [Parameter, EditorRequired] public NxChart Chart { get; set; } = default!;

    private CultureInfo Culture => Chart.Culture ?? CultureInfo.CurrentCulture;
    private bool Measured => width > 0 && height > 0;
    private string Label => Chart.AriaLabel ?? Chart.Title ?? "Chart";

    private NxChartLegendPosition LegendPosition =>
        Chart.LegendPosition == NxChartLegendPosition.Right && Measured && width < 360 ? NxChartLegendPosition.Bottom : Chart.LegendPosition;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        if (module is null)
            return;
        selfReference = DotNetObjectReference.Create(this);
        var size = await module.InvokeAsync<double[]>("observe", plot, PlotKeys, selfReference);
        if (size is [var w, var h])
            Resize(w, h);
    }

    internal async Task RefreshAsync()
    {
        if (module is null)
            return;
        var size = await module.InvokeAsync<double[]>("measure", plot);
        if (size is [var w, var h])
            Resize(w, h);
    }

    /// <summary>Called by nx-chart.js when the plot area's size changes. Not for application use.</summary>
    [JSInvokable]
    public Task OnResize(double w, double h) => InvokeAsync(() => Resize(w, h));

    private void Resize(double w, double h)
    {
        if (Math.Abs(w - width) < 0.5 && Math.Abs(h - height) < 0.5)
            return;
        (width, height) = (w, h);
        StateHasChanged();
    }

    /// <summary>
    /// Builds the model and layout. Called as the surface renders, not when its parameters are set: NxChart sets
    /// them while it diffs, before its series take their new parameters, which happens just ahead of this render.
    /// </summary>
    private void Rebuild()
    {
        var ticks = Measured ? (int)Math.Clamp((height - 60) / 48, 2, 10) : 5;
        model = ChartModel.Build(Chart.Series, Chart.CategoryAxis, Chart.ValueAxes, hidden, Culture, ticks);
        if (active is { } a && (a.Series >= model.AllSeries.Count || a.Index >= Count(model.AllSeries[a.Series])))
            active = null;
        drawing = Measured ? Layout() : new Drawing();
    }

    private int Count(ChartModel.Series s) => model.IsPie ? s.Values.Length : model.Categories.Count;

    // ── Layout ────────────────────────────────────────────────────────────────

    /// <summary>Everything drawn, in SVG coordinates.</summary>
    private sealed class Drawing
    {
        public double X0, Y0, X1, Y1;
        public List<(double X1, double Y1, double X2, double Y2)> Grid { get; } = [];
        public List<(double X, double Y, string Text, string Anchor)> Labels { get; } = [];
        public List<(double X, double Y, string Text, bool Vertical)> Titles { get; } = [];
        public List<Shape> Shapes { get; } = [];
        public List<Shape> Markers { get; } = [];
        public List<(double X, double Y, string Text, string Anchor)> ValueLabels { get; } = [];
        public Dictionary<(int, int), (double X, double Y)> Anchors { get; } = [];
        public double Band;
    }

    /// <summary>One path, rectangle or circle; <see cref="Key"/> ties it to a point for hover and click.</summary>
    private sealed record Shape(string Kind, string? D, double X, double Y, double W, double H, string Color, string? Stroke,
        double StrokeWidth, string? Dash, (int Series, int Index)? Key, string CssClass);

    private Drawing Layout() => model.IsPie ? LayoutPie() : LayoutCartesian();

    private Drawing LayoutCartesian()
    {
        var d = new Drawing();
        var cat = Chart.CategoryAxis;
        var showCats = cat?.Visible ?? true;
        var horizontal = model.IsHorizontal;
        var n = Math.Max(1, model.Categories.Count);
        var axes = model.Axes.Where(a => a.Side >= 0).ToList();
        string TickText(ChartModel.Axis a, double v) => ChartMath.Format(v, a.Format, Culture);
        double AxisWidth(ChartModel.Axis a) =>
            a.Ticks().Select(t => ChartMath.TextWidth(TickText(a, t))).DefaultIfEmpty(0).Max() + 8 + (a.Title is null ? 0 : Font + 8);
        var catLabelWidth = model.Labels.Select(l => ChartMath.TextWidth(l)).DefaultIfEmpty(0).Max();

        double left = 8, right = 12, top = 10, bottom = 8;
        if (horizontal)
        {
            left += showCats ? catLabelWidth + 8 + (cat?.Title is null ? 0 : Font + 8) : 0;
            bottom += axes.Any(a => a.Side == 0) ? Font + 8 + (axes.First(a => a.Side == 0).Title is null ? 0 : Font + 8) : 0;
            top += axes.Any(a => a.Side == 1) ? Font + 8 : 0;
            right += catLabelWidth > 0 ? 8 : 0;
        }
        else
        {
            foreach (var a in axes)
            {
                if (a.Side == 0) left += AxisWidth(a);
                else right += AxisWidth(a);
            }
            bottom += showCats ? Font + 10 + (cat?.Title is null ? 0 : Font + 8) : 0;
        }
        (d.X0, d.Y0, d.X1, d.Y1) = (left, top, Math.Max(left + 10, width - right), Math.Max(top + 10, height - bottom));

        var length = horizontal ? d.Y1 - d.Y0 : d.X1 - d.X0;
        d.Band = length / n;
        double Center(int i) => horizontal ? d.Y0 + d.Band * (i + 0.5) : d.X0 + d.Band * (i + 0.5);
        double ValuePos(ChartModel.Axis a, double v) => horizontal
            ? d.X0 + a.Ratio(v) * (d.X1 - d.X0)
            : d.Y1 - a.Ratio(v) * (d.Y1 - d.Y0);

        // Grid and value labels, from the first axis drawn; the second only labels its side.
        foreach (var a in axes)
        {
            foreach (var t in a.Ticks())
            {
                var p = ValuePos(a, t);
                if (a.Side == 0)
                    d.Grid.Add(horizontal ? (p, d.Y0, p, d.Y1) : (d.X0, p, d.X1, p));
                var text = TickText(a, t);
                if (horizontal)
                    d.Labels.Add((p, a.Side == 0 ? d.Y1 + Font + 4 : d.Y0 - 6, text, "middle"));
                else
                    d.Labels.Add((a.Side == 0 ? d.X0 - 6 : d.X1 + 6, p + Font / 3, text, a.Side == 0 ? "end" : "start"));
            }
            if (a.Title is { } title)
            {
                if (horizontal)
                    d.Titles.Add(((d.X0 + d.X1) / 2, height - 6, title, false));
                else
                    d.Titles.Add((a.Side == 0 ? Font : width - Font / 2, (d.Y0 + d.Y1) / 2, title, true));
            }
        }

        // Category labels: every step-th, enough apart not to overlap.
        if (showCats)
        {
            var room = horizontal ? Font + 4 : catLabelWidth + 10;
            var step = Math.Max(1, cat?.LabelStep ?? (int)Math.Ceiling(room / Math.Max(1, d.Band)));
            for (var i = cat?.LabelSkip ?? 0; i < model.Categories.Count; i += step)
            {
                if (string.IsNullOrEmpty(model.Labels[i]))
                    continue;
                if (horizontal)
                    d.Labels.Add((d.X0 - 6, Center(i) + Font / 3, model.Labels[i], "end"));
                else
                    d.Labels.Add((Center(i), d.Y1 + Font + 4, model.Labels[i], "middle"));
            }
            if (cat?.Title is { } title)
            {
                if (horizontal)
                    d.Titles.Add((Font, (d.Y0 + d.Y1) / 2, title, true));
                else
                    d.Titles.Add(((d.X0 + d.X1) / 2, height - 6, title, false));
            }
        }

        // Columns and bars: slots side by side within each category's band.
        var bars = model.Shown.Where(s => s.Type is NxChartSeriesType.Column or NxChartSeriesType.Bar).ToList();
        if (bars.Count > 0)
        {
            var gap = bars[0].Source.Gap;
            var spacing = bars[0].Source.Spacing;
            var slots = model.Slots;
            var size = d.Band / (slots + (slots - 1) * spacing + gap);
            foreach (var s in bars)
            {
                for (var i = 0; i < model.Categories.Count; i++)
                {
                    if (s.Values[i] is not double v)
                        continue;
                    var start = Center(i) - (slots * size + (slots - 1) * spacing * size) / 2 + s.Slot * size * (1 + spacing);
                    var from = ValuePos(s.Axis, s.Base.Length > i ? s.Base[i] : Math.Max(s.Axis.Min, Math.Min(0, s.Axis.Max)));
                    var to = ValuePos(s.Axis, s.Top(i));
                    var color = s.PointColors[i] ?? s.Color;
                    var shape = horizontal
                        ? new Shape("rect", null, Math.Min(from, to), start, Math.Abs(to - from), size, color, null, 0, null, (s.Index, i), "nx-chart-bar")
                        : new Shape("rect", null, start, Math.Min(from, to), size, Math.Abs(to - from), color, null, 0, null, (s.Index, i), "nx-chart-bar");
                    d.Shapes.Add(shape);
                    d.Anchors[(s.Index, i)] = horizontal ? (to, start + size / 2) : (start + size / 2, to);
                    if (s.Source.ShowLabels)
                    {
                        var text = ChartMath.Format(v, s.Source.LabelFormat ?? s.Axis.Format, Culture);
                        d.ValueLabels.Add(horizontal
                            ? (to + (v >= 0 ? 4 : -4), start + size / 2 + Font / 3, text, v >= 0 ? "start" : "end")
                            : (start + size / 2, to + (v >= 0 ? -4 : Font + 2), text, "middle"));
                    }
                }
            }
        }

        // Every area, then every line and outline, then the markers on top.
        var strokes = new List<Shape>();
        foreach (var s in model.Shown.Where(s => s.Type is NxChartSeriesType.Area or NxChartSeriesType.Line && !horizontal))
        {
            var points = new List<(int I, double X, double Y, double B)>();
            for (var i = 0; i < model.Categories.Count; i++)
            {
                var stacked = s.Base.Length > i;
                if (s.Values[i] is null && !stacked)
                {
                    points.Add((-1, 0, 0, 0));   // a gap
                    continue;
                }
                var baseValue = stacked ? s.Base[i] : Math.Max(s.Axis.Min, Math.Min(0, s.Axis.Max));
                points.Add((i, Center(i), ValuePos(s.Axis, s.Top(i)), ValuePos(s.Axis, baseValue)));
            }
            var runs = Runs(points);
            var lineWidth = s.Source.LineWidth ?? 2;
            if (s.Type == NxChartSeriesType.Area)
            {
                foreach (var run in runs)
                {
                    var fill = "M" + string.Join(" L", run.Select(p => $"{ChartMath.N(p.X)} {ChartMath.N(p.Y)}"))
                             + " L" + string.Join(" L", run.AsEnumerable().Reverse().Select(p => $"{ChartMath.N(p.X)} {ChartMath.N(p.B)}")) + " Z";
                    d.Shapes.Add(new Shape("area", fill, 0, 0, 0, 0, s.Color, null, 0, null, null, "nx-chart-area"));
                }
            }
            foreach (var run in runs)
            {
                var line = "M" + string.Join(" L", run.Select(p => $"{ChartMath.N(p.X)} {ChartMath.N(p.Y)}"));
                var dash = s.Source.DashType switch { NxChartDashType.Dash => "8 5", NxChartDashType.Dot => "2 4", _ => null };
                strokes.Add(new Shape("line", line, 0, 0, 0, 0, "none", s.Source.LineColor ?? s.Color, lineWidth, dash, null, "nx-chart-line"));
            }
            foreach (var p in points.Where(p => p.I >= 0 && s.Values[p.I] is not null))
            {
                d.Anchors[(s.Index, p.I)] = (p.X, p.Y);
                d.Markers.Add(new Shape("circle", null, p.X, p.Y, s.HasMarkers ? (d.Band < 14 ? 2.5 : 3.5) : 0, 0, s.Color, s.Color, 2, null, (s.Index, p.I), "nx-chart-marker"));
            }
        }
        d.Shapes.AddRange(strokes);
        return d;
    }

    /// <summary>The unbroken stretches of a line, split where a value is missing.</summary>
    private static List<List<(int I, double X, double Y, double B)>> Runs(List<(int I, double X, double Y, double B)> points)
    {
        var runs = new List<List<(int, double, double, double)>>();
        List<(int, double, double, double)>? run = null;
        foreach (var p in points)
        {
            if (p.I < 0)
            {
                run = null;
                continue;
            }
            if (run is null)
                runs.Add(run = []);
            run.Add(p);
        }
        return runs;
    }

    private Drawing LayoutPie()
    {
        var d = new Drawing { X0 = 0, Y0 = 0, X1 = width, Y1 = height };
        var s = model.AllSeries[0];
        var total = s.Values.Sum(v => Math.Max(0, v ?? 0));
        var cx = width / 2;
        var cy = height / 2;
        var r = Math.Max(10, Math.Min(width, height) / 2 - 10);
        var angle = -Math.PI / 2;
        for (var i = 0; i < s.Values.Length; i++)
        {
            if (s.Values[i] is not double v || v <= 0 || total <= 0)
                continue;
            var sweep = v / total * Math.PI * 2;
            var mid = angle + sweep / 2;
            string path;
            if (sweep >= Math.PI * 2 - 1e-9)
                path = $"M{ChartMath.N(cx - r)} {ChartMath.N(cy)} A{ChartMath.N(r)} {ChartMath.N(r)} 0 1 1 {ChartMath.N(cx + r)} {ChartMath.N(cy)} A{ChartMath.N(r)} {ChartMath.N(r)} 0 1 1 {ChartMath.N(cx - r)} {ChartMath.N(cy)} Z";
            else
            {
                var (x1, y1) = (cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
                var (x2, y2) = (cx + r * Math.Cos(angle + sweep), cy + r * Math.Sin(angle + sweep));
                path = $"M{ChartMath.N(cx)} {ChartMath.N(cy)} L{ChartMath.N(x1)} {ChartMath.N(y1)} A{ChartMath.N(r)} {ChartMath.N(r)} 0 {(sweep > Math.PI ? 1 : 0)} 1 {ChartMath.N(x2)} {ChartMath.N(y2)} Z";
            }
            var color = s.PointColors[i] ?? ChartModel.Palette(i);
            d.Shapes.Add(new Shape("slice", path, Math.Cos(mid), Math.Sin(mid), 0, 0, color, null, 0, null, (0, i), "nx-chart-slice"));
            d.Anchors[(0, i)] = (cx + r * 0.6 * Math.Cos(mid), cy + r * 0.6 * Math.Sin(mid));
            if (s.Source.ShowLabels)
                d.ValueLabels.Add((cx + r * 0.65 * Math.Cos(mid), cy + r * 0.65 * Math.Sin(mid) + Font / 3,
                    ChartMath.Format(v, s.Source.LabelFormat, Culture), "middle"));
            angle += sweep;
        }
        return d;
    }

    // ── Points, tooltips, legend ──────────────────────────────────────────────

    private NxChartPoint PointAt(int seriesIndex, int index)
    {
        var s = model.AllSeries[seriesIndex];
        var value = s.Values[index];
        double? share = null;
        if (model.IsPie)
        {
            var total = s.Values.Sum(v => Math.Max(0, v ?? 0));
            share = total > 0 && value is double v ? v / total : null;
        }
        var name = model.IsPie ? model.Labels[index] : s.Source.Name;
        return new NxChartPoint(name, s.Items[index], model.Categories[index], value,
            ChartMath.Format(value, model.IsPie ? s.Source.LabelFormat : s.Axis.Format, Culture), share);
    }

    private RenderFragment? TooltipFor(int seriesIndex, int index)
    {
        var point = PointAt(seriesIndex, index);
        var template = model.AllSeries[seriesIndex].Source.TooltipTemplate ?? Chart.TooltipTemplate;
        if (template is not null)
            return template(point);
        var category = model.IsPie ? null : model.Labels[index];
        var share = point.Percentage is double p ? $" ({p.ToString("P1", Culture)})" : "";
        return b =>
        {
            if (!string.IsNullOrEmpty(category))
            {
                b.OpenElement(0, "div");
                b.AddAttribute(1, "class", "nx-chart-tooltip-category");
                b.AddContent(2, category);
                b.CloseElement();
            }
            b.OpenElement(3, "div");
            if (point.SeriesName is not null)
            {
                b.AddContent(4, point.SeriesName + ": ");
            }
            b.OpenElement(5, "strong");
            b.AddContent(6, point.FormattedValue + share);
            b.CloseElement();
            b.CloseElement();
        };
    }

    private string TooltipStyle
    {
        get
        {
            if (active is not { } a || !drawing.Anchors.TryGetValue(a, out var p))
                return "";
            // Above the point, centered; kept inside the plot at the edges, below it near the top.
            var shiftX = p.X < 90 ? "0%" : p.X > width - 90 ? "-100%" : "-50%";
            var shiftY = p.Y < 70 ? "12px" : "calc(-100% - 12px)";
            return string.Create(CultureInfo.InvariantCulture, $"left: {p.X:0.#}px; top: {p.Y:0.#}px; transform: translate({shiftX}, {shiftY});");
        }
    }

    /// <summary>An SVG <c>&lt;text&gt;</c>: built here because Razor reads a bare <c>&lt;text&gt;</c> in a loop as its own tag.</summary>
    private static RenderFragment SvgText(double x, double y, string text, string anchor, string? cssClass, string? transform) => b =>
    {
        b.OpenElement(0, "text");
        b.AddAttribute(1, "class", cssClass);
        b.AddAttribute(2, "x", ChartMath.N(x));
        b.AddAttribute(3, "y", ChartMath.N(y));
        b.AddAttribute(4, "text-anchor", anchor);
        b.AddAttribute(5, "transform", transform);
        b.AddContent(6, text);
        b.CloseElement();
    };

    /// <summary>A line or area point's category, marked with a vertical line while it is shown.</summary>
    private double? GuideX =>
        active is { } a && !model.IsPie && !model.IsHorizontal && a.Series < model.AllSeries.Count
        && model.AllSeries[a.Series].Type is NxChartSeriesType.Line or NxChartSeriesType.Area
        && drawing.Anchors.TryGetValue(a, out var p) ? p.X : null;

    private sealed record LegendItem(int Index, string Text, string Color, bool Shown, bool IsLine);

    private IEnumerable<LegendItem> LegendItems()
    {
        if (model.IsPie)
        {
            var s = model.AllSeries[0];
            return model.Labels.Select((label, i) => new LegendItem(i, label, s.PointColors[i] ?? ChartModel.Palette(i), !hidden.Contains(i), false));
        }
        return model.AllSeries.Where(s => s.Source.VisibleInLegend)
            .Select(s => new LegendItem(s.Index, s.Source.Name ?? $"Series {s.Index + 1}", s.Color, s.Visible, s.Type == NxChartSeriesType.Line));
    }

    private void ToggleLegend(int index)
    {
        if (!hidden.Remove(index))
            hidden.Add(index);
        active = null;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Called by nx-chart.js when the pointer moves to another point, or off them. Not for application use.</summary>
    [JSInvokable]
    public Task OnHover(int seriesIndex, int index) => InvokeAsync(() =>
    {
        var next = seriesIndex < 0 ? ((int, int)?)null : (seriesIndex, index);
        if (next == active)
            return;
        active = next;
        StateHasChanged();
    });

    /// <summary>Called by nx-chart.js when a point is clicked or tapped. Not for application use.</summary>
    [JSInvokable]
    public Task OnPointClick(int seriesIndex, int index) => InvokeAsync(async () =>
    {
        if (seriesIndex < 0 || seriesIndex >= model.AllSeries.Count || index >= Count(model.AllSeries[seriesIndex]))
            return;
        active = (seriesIndex, index);
        StateHasChanged();
        await Chart.OnSeriesClick.InvokeAsync(PointAt(seriesIndex, index));
    });

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        var keys = drawing.Anchors.Keys.OrderBy(k => k.Item2).ThenBy(k => k.Item1).ToList();
        if (keys.Count == 0)
            return;
        var current = active is { } a && keys.Contains(a) ? a : ((int, int)?)null;
        (int, int)? next = e.Key switch
        {
            "Escape" => null,
            "Home" => keys[0],
            "End" => keys[^1],
            "ArrowRight" or "ArrowLeft" when current is null => keys[0],
            "ArrowRight" => NextAlong(keys, current!.Value, +1),
            "ArrowLeft" => NextAlong(keys, current!.Value, -1),
            "ArrowDown" or "ArrowUp" when current is null => keys[0],
            "ArrowDown" => NextAcross(keys, current!.Value, +1),
            "ArrowUp" => NextAcross(keys, current!.Value, -1),
            "Enter" or " " => current,
            _ => current
        };
        if (e.Key is "Enter" or " " && current is { } hit)
        {
            await Chart.OnSeriesClick.InvokeAsync(PointAt(hit.Item1, hit.Item2));
            return;
        }
        active = next;
    }

    /// <summary>The same series' point in the next or previous category (the next slice for a pie).</summary>
    private static (int, int) NextAlong(List<(int, int)> keys, (int S, int I) from, int direction)
    {
        var same = keys.Where(k => k.Item1 == from.S).OrderBy(k => k.Item2).ToList();
        var at = same.IndexOf(from);
        return same[Math.Clamp(at + direction, 0, same.Count - 1)];
    }

    /// <summary>Another series' point in the same category.</summary>
    private static (int, int) NextAcross(List<(int, int)> keys, (int S, int I) from, int direction)
    {
        var same = keys.Where(k => k.Item2 == from.I).OrderBy(k => k.Item1).ToList();
        var at = same.IndexOf(from);
        return same[Math.Clamp(at + direction, 0, same.Count - 1)];
    }

    private void HandleBlur() => active = null;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", plot);
                await module.DisposeAsync();
            }
            catch (Exception gone) when (gone is JSDisconnectedException or OperationCanceledException)
            {
                // The circuit is gone; the browser cleaned up with it.
            }
        }
        selfReference?.Dispose();
    }
}
