using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxChartTests : BunitContext
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-chart.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<double[]>("observe", _ => true).SetResult([800, 300]);
    }

    private sealed record Day(DateTime Date, int Loads, double? Hours);
    private sealed record Slice(string Name, double Count, string? Color = null);

    private static readonly Day[] Days =
    [
        new(new DateTime(2026, 10, 1), 10, 2.5), new(new DateTime(2026, 10, 2), 20, 4), new(new DateTime(2026, 10, 4), 15, null)
    ];

    /// <summary>A child component with these parameters, for a chart's content.</summary>
    private static RenderFragment Child<T>(params (string Name, object? Value)[] parameters) where T : IComponent => b =>
    {
        b.OpenComponent<T>(0);
        var seq = 1;
        foreach (var (name, value) in parameters)
            b.AddComponentParameter(seq++, name, value);
        b.CloseComponent();
    };

    private static RenderFragment All(params RenderFragment[] children) => b =>
    {
        for (var i = 0; i < children.Length; i++)
            b.AddContent(i, children[i]);
    };

    private IRenderedComponent<NxChart> RenderChart(RenderFragment content, Action<ComponentParameterCollectionBuilder<NxChart>>? configure = null) =>
        Render<NxChart>(p =>
        {
            p.Add(x => x.Id, "c");
            p.Add(x => x.Culture, Us);
            p.Add(x => x.ChildContent, content);
            configure?.Invoke(p);
        });

    private static RenderFragment LoadsLine(params (string, object?)[] more) =>
        Child<NxChartSeries>([("Name", "Loads"), ("Data", Days), ("Field", "Loads"), ("CategoryField", "Date"), .. more]);

    private static List<string> Texts(IRenderedComponent<NxChart> cut, string selector) =>
        cut.FindAll(selector).Select(e => e.TextContent).ToList();

    // ── Line ──────────────────────────────────────────────────────────────────

    [Test]
    public void Line_DateAxis_FillsMissingDays_PathMarkersLabels()
    {
        var cut = RenderChart(LoadsLine(), p => p.Add(x => x.Title, "Loads"));

        Assert.That(cut.Find(".nx-chart-title").TextContent, Is.EqualTo("Loads"));
        Assert.That(cut.Find("svg").GetAttribute("viewBox"), Is.EqualTo("0 0 800 300"));
        Assert.That(Texts(cut, ".nx-chart-labels text").Where(t => t.Contains('/')), Is.EqualTo(new[] { "10/1", "10/2", "10/3", "10/4" }));
        var markers = cut.FindAll("circle.nx-chart-marker");
        Assert.That(markers.Select(m => m.GetAttribute("data-p")), Is.EqualTo(new[] { "0-0", "0-1", "0-3" }), "10/3 has no point");
        Assert.That(markers[0].GetAttribute("r"), Is.EqualTo("3.5"));
        Assert.That(cut.FindAll("path.nx-chart-line"), Has.Count.EqualTo(2), "the line breaks at the missing day");
        Assert.That(Texts(cut, ".nx-chart-labels text").Where(t => !t.Contains('/')), Does.Contain("0").And.Contain("20"));
    }

    [Test]
    public void Line_YFollowsTheValue()
    {
        var cut = RenderChart(LoadsLine());
        var y = cut.FindAll("circle.nx-chart-marker").Select(m => double.Parse(m.GetAttribute("cy")!, CultureInfo.InvariantCulture)).ToList();
        Assert.That(y[1], Is.LessThan(y[2]).And.LessThan(y[0]), "20 is drawn higher than 15 and 10");
        Assert.That((y[0] - y[2]) / (y[0] - y[1]), Is.EqualTo(0.5).Within(0.01), "evenly scaled");
    }

    [Test]
    public void Line_DashMarkersOff_ColorAndWidth()
    {
        var cut = RenderChart(LoadsLine(("DashType", NxChartDashType.Dot), ("Markers", false), ("Color", "red"), ("LineWidth", 3d)));
        var line = cut.Find("path.nx-chart-line");
        Assert.That(line.GetAttribute("stroke-dasharray"), Is.EqualTo("2 4"));
        Assert.That(line.GetAttribute("style"), Is.EqualTo("stroke: red; stroke-width: 3px"));
        Assert.That(cut.FindAll("circle.nx-chart-marker").All(m => m.GetAttribute("r") == "0"), Is.True, "kept for hit-testing, not drawn");
    }

    [Test]
    public void ValueAxis_FormatTitleNarrowAndLimits()
    {
        var cut = RenderChart(All(LoadsLine(), Child<NxChartValueAxis>(("Title", "Loads"), ("LabelFormat", "{0:C0}"))));
        Assert.That(Texts(cut, ".nx-chart-labels text"), Does.Contain("$20").And.Contain("Loads"));

        var narrow = RenderChart(All(LoadsLine(), Child<NxChartValueAxis>(("NarrowRange", true))));
        Assert.That(Texts(narrow, ".nx-chart-labels text"), Does.Not.Contain("0"));

        var fixedRange = RenderChart(All(LoadsLine(), Child<NxChartValueAxis>(("Min", 5d), ("Max", 50d))));
        var ticks = Texts(fixedRange, ".nx-chart-labels text").Where(t => !t.Contains('/')).ToList();
        Assert.That(ticks.First(), Is.EqualTo("5"));
        Assert.That(ticks.Last(), Is.EqualTo("45"), "ticks run from Min by the step");
    }

    [Test]
    public void TwoValueAxes_SecondOnTheRight()
    {
        var cut = RenderChart(All(
            LoadsLine(("Axis", "count")),
            Child<NxChartSeries>(("Name", "Hours"), ("Type", NxChartSeriesType.Area), ("Data", Days), ("Field", "Hours"), ("CategoryField", "Date"), ("Axis", "hours")),
            Child<NxChartValueAxis>(("Name", "count")),
            Child<NxChartValueAxis>(("Name", "hours"), ("LabelFormat", "0.0"))));

        var right = cut.FindAll(".nx-chart-labels text").Where(t => t.GetAttribute("text-anchor") == "start").Select(t => t.TextContent).ToList();
        Assert.That(right, Does.Contain("4.0"));
        Assert.That(cut.FindAll("path.nx-chart-area"), Has.Count.EqualTo(1), "the area stops before the missing values");
    }

    [Test]
    public void CategoryAxis_CategoriesLabelsStepAndTitle()
    {
        var cut = RenderChart(All(
            Child<NxChartSeries>(("Data", new[] { 1, 2, 3, 4 })),
            Child<NxChartCategoryAxis>(("Categories", new[] { "Mon", "Tue", "Wed", "Thu" }), ("LabelStep", 2), ("Title", "Day"))));
        Assert.That(Texts(cut, ".nx-chart-labels text"), Does.Contain("Mon").And.Contain("Wed").And.Not.Contain("Tue").And.Contain("Day"));

        var labels = RenderChart(All(
            Child<NxChartSeries>(("Data", new[] { 1, 2, 3 })),
            Child<NxChartCategoryAxis>(("Categories", new[] { "a", "b", "c" }), ("Labels", new string?[] { "Week 1", "", null }))));
        Assert.That(Texts(labels, ".nx-chart-labels text").Where(t => !char.IsDigit(t[0])), Is.EqualTo(new[] { "Week 1" }));
    }

    [Test]
    public void DateAxis_Months_SumsEachMonth()
    {
        var items = new[] { new Day(new DateTime(2026, 5, 3), 4, null), new Day(new DateTime(2026, 5, 20), 6, null), new Day(new DateTime(2026, 7, 2), 5, null) };
        var cut = RenderChart(All(
            Child<NxChartSeries>(("Type", NxChartSeriesType.Column), ("Data", items), ("Field", "Loads"), ("CategoryField", "Date"), ("Aggregate", NxChartAggregate.Sum)),
            Child<NxChartCategoryAxis>(("BaseUnit", NxChartBaseUnit.Months))));

        Assert.That(Texts(cut, ".nx-chart-labels text"), Does.Contain("May 26").And.Contain("Jun 26").And.Contain("Jul 26"));
        Assert.That(cut.FindAll("rect.nx-chart-bar"), Has.Count.EqualTo(2));
        Assert.That(cut.FindAll(".nx-chart-sr tbody tr").Select(r => r.QuerySelectorAll("td")[0].TextContent), Is.EqualTo(new[] { "10", "", "5" }));
    }

    // ── Columns, bars, pie ────────────────────────────────────────────────────

    [Test]
    public void Columns_SideBySide_ThenStacked()
    {
        var a = new[] { 10, 20 };
        var b = new[] { 5, 5 };
        var cats = Child<NxChartCategoryAxis>(("Categories", new[] { "x", "y" }));
        var side = RenderChart(All(
            Child<NxChartSeries>(("Type", NxChartSeriesType.Column), ("Data", a)),
            Child<NxChartSeries>(("Type", NxChartSeriesType.Column), ("Data", b)), cats));
        var rects = side.FindAll("rect.nx-chart-bar").Select(r => (X: Num(r, "x"), Y: Num(r, "y"), H: Num(r, "height"))).ToList();
        Assert.That(rects[2].X, Is.GreaterThan(rects[0].X), "the second series sits beside the first");

        var stacked = RenderChart(All(
            Child<NxChartSeries>(("Type", NxChartSeriesType.Column), ("Data", a), ("Stacked", true)),
            Child<NxChartSeries>(("Type", NxChartSeriesType.Column), ("Data", b), ("Stacked", true)), cats));
        var s = stacked.FindAll("rect.nx-chart-bar").Select(r => (X: Num(r, "x"), Y: Num(r, "y"), H: Num(r, "height"))).ToList();
        Assert.That(s[2].X, Is.EqualTo(s[0].X).Within(0.01), "stacked: same slot");
        Assert.That(s[2].Y + s[2].H, Is.EqualTo(s[0].Y).Within(0.01), "the second sits on top of the first");
    }

    private static double Num(AngleSharp.Dom.IElement e, string attribute) => double.Parse(e.GetAttribute(attribute)!, CultureInfo.InvariantCulture);

    [Test]
    public void Bars_Horizontal_WidthByValue_Labels()
    {
        var data = new[] { new Slice("Rebar", 1200), new Slice("Strand", 600) };
        var cut = RenderChart(Child<NxChartSeries>(("Type", NxChartSeriesType.Bar), ("Data", data), ("Field", "Count"), ("CategoryField", "Name"),
            ("ShowLabels", true), ("LabelFormat", "N0")));

        var bars = cut.FindAll("rect.nx-chart-bar");
        Assert.That(Num(bars[0], "width") / Num(bars[1], "width"), Is.EqualTo(2).Within(0.01));
        Assert.That(Num(bars[0], "y"), Is.LessThan(Num(bars[1], "y")), "the first category at the top");
        Assert.That(Texts(cut, ".nx-chart-value-labels text"), Is.EqualTo(new[] { "1,200", "600" }));
        Assert.That(cut.FindAll(".nx-chart-labels text").First(t => t.TextContent == "Rebar").GetAttribute("text-anchor"), Is.EqualTo("end"));
    }

    [Test]
    public async Task Pie_SlicesLegendPercentage()
    {
        var data = new[] { new Slice("North", 300), new Slice("South", 100, "#123456") };
        var cut = RenderChart(Child<NxChartSeries>(("Type", NxChartSeriesType.Pie), ("Data", data), ("Field", "Count"), ("CategoryField", "Name"), ("ColorField", "Color")),
            p => p.Add(x => x.LegendPosition, NxChartLegendPosition.Right));

        var slices = cut.FindAll("path.nx-chart-slice");
        Assert.That(slices, Has.Count.EqualTo(2));
        Assert.That(slices[1].GetAttribute("style"), Does.StartWith("fill: #123456"));
        Assert.That(Texts(cut, ".nx-chart-legend-text"), Is.EqualTo(new[] { "North", "South" }));
        Assert.That(cut.Find(".nx-chart-body").ClassList, Does.Contain("nx-chart-legend-right"));
        Assert.That(cut.FindAll(".nx-chart-labels"), Has.Count.EqualTo(1));

        await cut.FindComponent<NxChartSurface>().Instance.OnHover(0, 0);
        Assert.That(cut.Find(".nx-chart-tooltip").TextContent, Is.EqualTo("North: 300 (75.0%)"));
    }

    // ── Legend, tooltips, clicks, keys ────────────────────────────────────────

    [Test]
    public async Task Legend_HidesAndShowsASeries()
    {
        var cut = RenderChart(All(LoadsLine(), Child<NxChartSeries>(("Name", "Other"), ("Data", Days), ("Field", "Loads"), ("CategoryField", "Date"))));
        var items = cut.FindAll(".nx-chart-legend-item");
        Assert.That(items.Select(i => i.GetAttribute("aria-pressed")), Is.EqualTo(new[] { "true", "true" }));

        await items[0].ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-chart-legend-item")[0].GetAttribute("aria-pressed"), Is.EqualTo("false"));
        Assert.That(cut.FindAll("circle.nx-chart-marker").Select(m => m.GetAttribute("data-p")).Distinct().All(k => k!.StartsWith("1-")), Is.True);
        Assert.That(cut.FindAll(".nx-chart-sr thead th"), Has.Count.EqualTo(2), "the table drops the hidden series");
    }

    [Test]
    public async Task Tooltip_DefaultAndTemplate_ClickRaisesWithTheItem()
    {
        NxChartPoint? clicked = null;
        RenderFragment<NxChartPoint> template = p => b => b.AddContent(0, $"{((Day)p.DataItem!).Date:MMM d}: {p.Value}");
        var cut = RenderChart(LoadsLine(), p => p.Add(x => x.OnSeriesClick, e => clicked = e));
        var surface = cut.FindComponent<NxChartSurface>().Instance;

        await surface.OnHover(0, 1);
        Assert.That(cut.Find(".nx-chart-tooltip").TextContent, Is.EqualTo("10/2Loads: 20"));
        Assert.That(cut.Find(".nx-chart-guide"), Is.Not.Null);
        Assert.That(cut.Find("circle.nx-chart-active").GetAttribute("r"), Is.EqualTo("5"));

        await surface.OnPointClick(0, 1);
        Assert.That(clicked!.DataItem, Is.EqualTo(Days[1]));
        Assert.That(clicked.SeriesName, Is.EqualTo("Loads"));
        Assert.That(clicked.FormattedValue, Is.EqualTo("20"));

        var custom = RenderChart(LoadsLine(("TooltipTemplate", template)));
        await custom.FindComponent<NxChartSurface>().Instance.OnHover(0, 0);
        Assert.That(custom.Find(".nx-chart-tooltip").TextContent, Is.EqualTo("Oct 1: 10"));

        await surface.OnHover(-1, -1);
        Assert.That(cut.FindAll(".nx-chart-tooltip"), Is.Empty);
    }

    [Test]
    public async Task Keys_MoveAlongAndAcross_EnterClicks()
    {
        NxChartPoint? clicked = null;
        var cut = RenderChart(All(LoadsLine(), Child<NxChartSeries>(("Name", "Other"), ("Data", Days), ("Field", "Loads"), ("CategoryField", "Date"))),
            p => p.Add(x => x.OnSeriesClick, e => clicked = e));
        var plot = () => cut.Find(".nx-chart-plot");

        await plot().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.That(cut.Find("circle.nx-chart-active").GetAttribute("data-p"), Is.EqualTo("0-0"));
        await plot().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        await plot().KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.That(cut.Find("circle.nx-chart-active").GetAttribute("data-p"), Is.EqualTo("1-1"));
        await plot().KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        Assert.That(cut.Find("circle.nx-chart-active").GetAttribute("data-p"), Is.EqualTo("1-3"));
        await plot().KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.That(clicked!.SeriesName, Is.EqualTo("Other"));
        await plot().KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.That(cut.FindAll(".nx-chart-tooltip"), Is.Empty);
        Assert.That(module.Invocations.Single(i => i.Identifier == "observe").Arguments[1], Is.EqualTo(new[] { "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End" }));
    }

    [Test]
    public void Accessibility_GroupNamedAndDescribedByATable()
    {
        var cut = RenderChart(LoadsLine(), p => p.Add(x => x.Title, "Loads shipped"));
        var plot = cut.Find(".nx-chart-plot");
        Assert.That(plot.GetAttribute("aria-label"), Is.EqualTo("Loads shipped"));
        Assert.That(plot.GetAttribute("aria-roledescription"), Is.EqualTo("chart"));
        Assert.That(plot.GetAttribute("tabindex"), Is.EqualTo("0"));
        var table = cut.Find("#" + plot.GetAttribute("aria-describedby"));
        Assert.That(table.QuerySelectorAll("tbody tr"), Has.Length.EqualTo(4));
        Assert.That(cut.Find("svg").GetAttribute("aria-hidden"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Resize_Redraws_UnmeasuredDrawsNothing()
    {
        module.Setup<double[]>("observe", _ => true).SetResult([0, 0]);
        var cut = RenderChart(LoadsLine());
        Assert.That(cut.FindAll("svg"), Is.Empty);

        await cut.FindComponent<NxChartSurface>().Instance.OnResize(400, 200);
        Assert.That(cut.Find("svg").GetAttribute("viewBox"), Is.EqualTo("0 0 400 200"));
    }

    [Test]
    public void LegendRight_FallsToBottomWhenNarrow()
    {
        module.Setup<double[]>("observe", _ => true).SetResult([300, 200]);
        var cut = RenderChart(LoadsLine(), p => p.Add(x => x.LegendPosition, NxChartLegendPosition.Right));
        Assert.That(cut.Find(".nx-chart-body").ClassList, Does.Contain("nx-chart-legend-bottom"));
    }

    [Test]
    public void NoLegend_AndRootSize()
    {
        var cut = RenderChart(LoadsLine(), p => p.Add(x => x.LegendPosition, NxChartLegendPosition.None).Add(x => x.Height, "250px"));
        Assert.That(cut.FindAll(".nx-chart-legend"), Is.Empty);
        Assert.That(cut.Find(".nx-chart").GetAttribute("style"), Is.EqualTo("height: 250px; width: 100%;"));
    }

    [Test]
    public void UnknownField_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RenderChart(Child<NxChartSeries>(("Data", Days), ("Field", "Nope"))));
        Assert.That(ex!.Message, Does.Contain("Nope"));
    }

    [Test]
    public void SeriesOutsideAChart_Throws() => Assert.Throws<InvalidOperationException>(() => Render<NxChartSeries>());

    // ── ChartMath ─────────────────────────────────────────────────────────────

    [TestCase(0.8, 1)]
    [TestCase(2.6, 2)]
    [TestCase(37, 50)]
    [TestCase(120, 100)]
    public void Math_NiceStep(double raw, double nice) => Assert.That(ChartMath.NiceStep(raw), Is.EqualTo(nice));

    [Test]
    public void Math_Scale()
    {
        Assert.That(ChartMath.Scale(3, 47, 5, false, null, null), Is.EqualTo((0d, 50d, 10d)));
        Assert.That(ChartMath.Scale(-12, 30, 4, false, null, null), Is.EqualTo((-20d, 30d, 10d)));
        var (min, max, _) = ChartMath.Scale(4, 5, 5, true, null, null);
        Assert.That(min, Is.GreaterThan(3).And.LessThanOrEqualTo(4));
        Assert.That(max, Is.GreaterThanOrEqualTo(5).And.LessThan(6));
    }

    [Test]
    public void Math_Format()
    {
        Assert.That(ChartMath.Format(1234.6, "{0:C0}", Us), Is.EqualTo("$1,235"));
        Assert.That(ChartMath.Format(1234.5, "N1", Us), Is.EqualTo("1,234.5"));
        Assert.That(ChartMath.Format(1234.567, null, Us), Is.EqualTo("1,234.57"));
        Assert.That(ChartMath.Format(new DateTime(2026, 5, 1), "MMM yy", Us), Is.EqualTo("May 26"));
    }
}
