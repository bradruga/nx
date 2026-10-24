using Bunit;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxProgressBarTests : BunitContext
{
    [Test]
    public void Render_ProgressbarRole_ValuesAndPercentLabel()
    {
        var cut = Render<NxProgressBar>(p => p.Add(x => x.Value, 42).Add(x => x.AriaLabel, "Produced"));

        var bar = cut.Find(".nx-progressbar");
        Assert.That(bar.GetAttribute("role"), Is.EqualTo("progressbar"));
        Assert.That(bar.GetAttribute("aria-label"), Is.EqualTo("Produced"));
        Assert.That(bar.GetAttribute("aria-valuemin"), Is.EqualTo("0"));
        Assert.That(bar.GetAttribute("aria-valuemax"), Is.EqualTo("100"));
        Assert.That(bar.GetAttribute("aria-valuenow"), Is.EqualTo("42"));
        Assert.That(bar.GetAttribute("aria-valuetext"), Is.EqualTo("42%"));
        Assert.That(cut.Find(".nx-progressbar-fill").GetAttribute("style"), Is.EqualTo("width: 42%"));
        Assert.That(cut.FindAll(".nx-progressbar-label").Select(l => l.TextContent), Is.EqualTo(new[] { "42%", "42%" }));
        Assert.That(cut.FindAll(".nx-progressbar-label").All(l => l.GetAttribute("aria-hidden") == "true"), Is.True);
    }

    [Test]
    public void Max_ValueIsACount()
    {
        var cut = Render<NxProgressBar>(p => p.Add(x => x.Value, 3).Add(x => x.Max, 8));

        Assert.That(cut.Find(".nx-progressbar").GetAttribute("aria-valuemax"), Is.EqualTo("8"));
        Assert.That(cut.Find(".nx-progressbar-fill").GetAttribute("style"), Is.EqualTo("width: 37.5%"));
        Assert.That(cut.Find(".nx-progressbar-label").TextContent, Is.EqualTo("38%"));
        Assert.That(cut.Find(".nx-progressbar-label-on-fill").GetAttribute("style"), Is.EqualTo("clip-path: inset(0 62.5% 0 0)"));
    }

    [TestCase(-5, "0")]
    [TestCase(250, "100")]
    [TestCase(double.NaN, "0")]
    public void Value_ClampedToRange(double value, string now)
    {
        var cut = Render<NxProgressBar>(p => p.Add(x => x.Value, value));
        Assert.That(cut.Find(".nx-progressbar").GetAttribute("aria-valuenow"), Is.EqualTo(now));
    }

    [Test]
    public void ZeroMax_FallsBackTo100()
    {
        var cut = Render<NxProgressBar>(p => p.Add(x => x.Value, 50).Add(x => x.Max, 0));
        Assert.That(cut.Find(".nx-progressbar-label").TextContent, Is.EqualTo("50%"));
    }

    [Test]
    public void LabelTemplate_GetsValueMaxPercentAndComplete()
    {
        var cut = Render<NxProgressBar>(p => p
            .Add(x => x.Value, 8)
            .Add(x => x.Max, 8)
            .Add(x => x.LabelTemplate, c => b => b.AddContent(0, $"{c.Value}/{c.Max} {c.Percent}% {(c.IsComplete ? "done" : "busy")}")));

        Assert.That(cut.Find(".nx-progressbar-label").TextContent, Is.EqualTo("8/8 100% done"));
    }

    [Test]
    public void ShowLabelFalse_NoLabel()
    {
        var cut = Render<NxProgressBar>(p => p.Add(x => x.Value, 10).Add(x => x.ShowLabel, false));
        Assert.That(cut.FindAll(".nx-progressbar-label"), Is.Empty);
    }

    [Test]
    public void Indeterminate_NoValuesNoLabel()
    {
        var cut = Render<NxProgressBar>(p => p.Add(x => x.Indeterminate, true).Add(x => x.Value, 30));

        var bar = cut.Find(".nx-progressbar");
        Assert.That(bar.ClassList, Does.Contain("nx-progressbar-indeterminate"));
        Assert.That(bar.HasAttribute("aria-valuenow"), Is.False);
        Assert.That(bar.HasAttribute("aria-valuemax"), Is.False);
        Assert.That(cut.Find(".nx-progressbar-fill").HasAttribute("style"), Is.False);
        Assert.That(cut.FindAll(".nx-progressbar-label"), Is.Empty);
    }

    [Test]
    public void Thin_ClassStyleAttributes()
    {
        var cut = Render<NxProgressBar>(p => p
            .Add(x => x.Thin, true)
            .Add(x => x.Class, "mine")
            .Add(x => x.Style, "--nx-progressbar-fill: red")
            .AddUnmatched("id", "slides"));

        var bar = cut.Find("#slides");
        Assert.That(bar.ClassList, Does.Contain("nx-progressbar-thin").And.Contain("mine"));
        Assert.That(bar.GetAttribute("style"), Is.EqualTo("--nx-progressbar-fill: red"));
    }
}
