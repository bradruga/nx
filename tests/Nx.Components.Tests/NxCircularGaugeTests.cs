using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxCircularGaugeTests : BunitContext
{
    [Test]
    public void Render_MeterAndArc()
    {
        var cut = Render<NxCircularGauge>(p => p.Add(x => x.Value, 25).Add(x => x.AriaLabel, "Reconciled"));

        var gauge = cut.Find(".nx-gauge");
        Assert.That(gauge.GetAttribute("role"), Is.EqualTo("meter"));
        Assert.That(gauge.GetAttribute("aria-label"), Is.EqualTo("Reconciled"));
        Assert.That(gauge.GetAttribute("aria-valuemin"), Is.EqualTo("0"));
        Assert.That(gauge.GetAttribute("aria-valuemax"), Is.EqualTo("100"));
        Assert.That(gauge.GetAttribute("aria-valuenow"), Is.EqualTo("25"));
        Assert.That(gauge.GetAttribute("style"), Is.EqualTo("width: 200px; height: 200px"));
        Assert.That(cut.Find(".nx-gauge-pointer").GetAttribute("stroke-dashoffset"), Is.EqualTo("75"));
        Assert.That(cut.Find("svg").GetAttribute("aria-hidden"), Is.EqualTo("true"));
        Assert.That(cut.FindAll(".nx-gauge-center"), Is.Empty);
    }

    [TestCase(-10, "0", "100")]
    [TestCase(150, "100", "0")]
    public void Value_Clamped(double value, string now, string offset)
    {
        var cut = Render<NxCircularGauge>(p => p.Add(x => x.Value, value));
        Assert.That(cut.Find(".nx-gauge").GetAttribute("aria-valuenow"), Is.EqualTo(now));
        Assert.That(cut.Find(".nx-gauge-pointer").GetAttribute("stroke-dashoffset"), Is.EqualTo(offset));
    }

    [Test]
    public void Range_ScalesTheArc()
    {
        var cut = Render<NxCircularGauge>(p => p.Add(x => x.Value, 180).Add(x => x.Min, 0).Add(x => x.Max, 240));
        Assert.That(cut.Find(".nx-gauge-pointer").GetAttribute("stroke-dashoffset"), Is.EqualTo("25"));
        Assert.That(cut.Find(".nx-gauge").GetAttribute("aria-valuemax"), Is.EqualTo("240"));
    }

    [Test]
    public void Center_GetsTheClampedValue()
    {
        var cut = Render<NxCircularGauge>(p => p
            .Add(x => x.Value, 130)
            .Add(x => x.CenterTemplate, (RenderFragment<double>)(v => b => b.AddContent(0, $"{v}%"))));
        Assert.That(cut.Find(".nx-gauge-center").TextContent, Is.EqualTo("100%"));
    }

    [Test]
    public void Color_Size_Transitions_Class()
    {
        var cut = Render<NxCircularGauge>(p => p
            .Add(x => x.Color, "red")
            .Add(x => x.Size, "90px")
            .Add(x => x.Transitions, false)
            .Add(x => x.Class, "mine")
            .Add(x => x.Style, "margin: 4px"));

        Assert.That(cut.Find(".nx-gauge-pointer").GetAttribute("style"), Is.EqualTo("stroke: red"));
        var gauge = cut.Find(".nx-gauge");
        Assert.That(gauge.ClassList, Does.Contain("nx-gauge-static").And.Contain("mine"));
        Assert.That(gauge.GetAttribute("style"), Is.EqualTo("width: 90px; height: 90px; margin: 4px"));
    }

    [Test]
    public void EmptyRange_ShowsNothingFilled()
    {
        var cut = Render<NxCircularGauge>(p => p.Add(x => x.Value, 5).Add(x => x.Min, 10).Add(x => x.Max, 10));
        Assert.That(cut.Find(".nx-gauge-pointer").GetAttribute("stroke-dashoffset"), Is.EqualTo("100"));
    }
}
