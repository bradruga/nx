using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxTimeColumnsTests : BunitContext
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-picker.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Bound columns: ValueChanged writes back like @bind-Value.</summary>
    private IRenderedComponent<NxTimeColumns> RenderBound(List<TimeOnly> raised, TimeOnly value,
        Action<ComponentParameterCollectionBuilder<NxTimeColumns>>? configure = null)
    {
        IRenderedComponent<NxTimeColumns>? cut = null;
        cut = Render<NxTimeColumns>(p =>
        {
            p.Add(x => x.Value, value);
            p.Add(x => x.Culture, EnUs);
            p.Add(x => x.ValueChanged, (TimeOnly v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static AngleSharp.Dom.IElement Option(IRenderedComponent<NxTimeColumns> cut, string column, string text) =>
        cut.FindAll($".nx-timecolumns-{column} [role=option]").Single(o => o.TextContent.Trim() == text);

    private static string[] Texts(IRenderedComponent<NxTimeColumns> cut, string column) =>
        cut.FindAll($".nx-timecolumns-{column} [role=option]").Select(o => o.TextContent.Trim()).ToArray();

    [Test]
    public void Render_12Hour_ThreeLabelledListboxesOfButtons()
    {
        var cut = RenderBound([], new TimeOnly(14, 30), p => p.Add(x => x.Class, "mine").Add(x => x.AriaLabel, "Arrival"));

        var root = cut.Find(".nx-timecolumns.mine");
        Assert.That(root.GetAttribute("role"), Is.EqualTo("group"));
        Assert.That(root.GetAttribute("aria-label"), Is.EqualTo("Arrival"));
        Assert.That(cut.FindAll("[role=listbox]").Select(l => l.GetAttribute("aria-label")), Is.EqualTo(new[] { "Hour", "Minute", "AM or PM" }));
        Assert.That(Texts(cut, "hours"), Is.EqualTo(new[] { "12", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11" }));
        Assert.That(Texts(cut, "periods"), Is.EqualTo(new[] { "AM", "PM" }));
        Assert.That(cut.FindAll("[role=option]").All(o => o.NodeName == "BUTTON" && o.GetAttribute("type") == "button"), Is.True);
        Assert.That(cut.FindAll("[aria-selected=true]").Select(o => o.TextContent.Trim()), Is.EqualTo(new[] { "2", "30", "PM" }));
        Assert.That(module.Invocations.Count(i => i.Identifier == "initColumns"), Is.EqualTo(1));
    }

    [Test]
    public void Render_24Hour_TwoColumns_ZeroPaddedHours()
    {
        var cut = RenderBound([], new TimeOnly(7, 0), p => p.Add(x => x.Use24Hour, true));

        Assert.That(cut.FindAll("[role=listbox]"), Has.Count.EqualTo(2));
        Assert.That(Texts(cut, "hours"), Has.Length.EqualTo(24));
        Assert.That(Texts(cut, "hours")[..2], Is.EqualTo(new[] { "00", "01" }));
        Assert.That(cut.Find(".nx-timecolumns-hours [aria-selected=true]").TextContent.Trim(), Is.EqualTo("07"));
    }

    [Test]
    public void RovingTabStop_OnlyTheSelectedOptionOfEachColumn()
    {
        var cut = RenderBound([], new TimeOnly(14, 30));

        foreach (var column in cut.FindAll("[role=listbox]"))
        {
            var stops = column.QuerySelectorAll("[role=option][tabindex='0']");
            Assert.That(stops, Has.Length.EqualTo(1));
            Assert.That(stops[0].GetAttribute("aria-selected"), Is.EqualTo("true"));
        }
    }

    [Test]
    public async Task Pick_SetsOnePart_KeepsTheOthers()
    {
        var raised = new List<TimeOnly>();
        var cut = RenderBound(raised, new TimeOnly(14, 30));

        await Option(cut, "hours", "12").ClickAsync(new MouseEventArgs());
        await Option(cut, "minutes", "55").ClickAsync(new MouseEventArgs());
        await Option(cut, "periods", "AM").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { new TimeOnly(12, 30), new TimeOnly(12, 55), new TimeOnly(0, 55) }));
        Assert.That(cut.FindAll("[aria-selected=true]").Select(o => o.TextContent.Trim()), Is.EqualTo(new[] { "12", "55", "AM" }));
    }

    [Test]
    public async Task Pick_TheSelectedOption_RaisesNothing()
    {
        var raised = new List<TimeOnly>();
        var cut = RenderBound(raised, new TimeOnly(14, 30));

        await Option(cut, "minutes", "30").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.Empty);
    }

    [Test]
    public void MinuteStep_SetsTheRows_OffGridValueGetsItsOwn()
    {
        Assert.That(Texts(RenderBound([], new TimeOnly(9, 0), p => p.Add(x => x.MinuteStep, 15)), "minutes"), Is.EqualTo(new[] { "00", "15", "30", "45" }));
        Assert.That(Texts(RenderBound([], new TimeOnly(9, 0), p => p.Add(x => x.MinuteStep, 1)), "minutes"), Has.Length.EqualTo(60));
        Assert.That(Texts(RenderBound([], new TimeOnly(9, 0), p => p.Add(x => x.MinuteStep, 0)), "minutes"), Has.Length.EqualTo(60), "clamped to 1");
        Assert.That(Texts(RenderBound([], new TimeOnly(9, 7), p => p.Add(x => x.MinuteStep, 15)), "minutes"), Is.EqualTo(new[] { "00", "07", "15", "30", "45" }));
    }

    [Test]
    public async Task MinMax_DisableOutside_PickMovesToTheLimit_ThenReveals()
    {
        var raised = new List<TimeOnly>();
        var cut = RenderBound(raised, new TimeOnly(13, 15), p => p
            .Add(x => x.Use24Hour, true)
            .Add(x => x.Min, new TimeOnly(8, 30))
            .Add(x => x.Max, new TimeOnly(17, 0)));

        Assert.That(Option(cut, "hours", "07").HasAttribute("disabled"), Is.True);
        Assert.That(Option(cut, "hours", "08").HasAttribute("disabled"), Is.False);
        Assert.That(Option(cut, "hours", "17").HasAttribute("disabled"), Is.False);
        Assert.That(Option(cut, "hours", "18").HasAttribute("disabled"), Is.True);

        await Option(cut, "hours", "17").ClickAsync(new MouseEventArgs());

        Assert.That(raised.Single(), Is.EqualTo(new TimeOnly(17, 0)), "17:15 is after Max");
        Assert.That(Option(cut, "minutes", "05").HasAttribute("disabled"), Is.True);
        Assert.That(module.Invocations.Any(i => i.Identifier == "revealColumns"), Is.True);
    }

    [Test]
    public void Periods_UseTheCulturesDesignators_DisabledByLimits()
    {
        var morning = RenderBound([], new TimeOnly(9, 0), p => p.Add(x => x.Max, new TimeOnly(11, 30)));
        Assert.That(Option(morning, "periods", "PM").HasAttribute("disabled"), Is.True);
        Assert.That(Option(morning, "periods", "AM").HasAttribute("disabled"), Is.False);

        var greek = Render<NxTimeColumns>(p => p.Add(x => x.Value, new TimeOnly(9, 0)).Add(x => x.Culture, CultureInfo.GetCultureInfo("el-GR")));
        Assert.That(Texts(greek, "periods"), Is.EqualTo(new[]
        {
            CultureInfo.GetCultureInfo("el-GR").DateTimeFormat.AMDesignator,
            CultureInfo.GetCultureInfo("el-GR").DateTimeFormat.PMDesignator
        }));
    }
}
