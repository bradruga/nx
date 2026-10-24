using Bunit;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxIconTests : BunitContext
{
    [Test]
    public void Render_StrokedSvg_DecorativeByDefault()
    {
        var cut = Render<NxIcon>(p => p.Add(x => x.Icon, NxIcons.Trash));

        var svg = cut.Find("svg");
        Assert.That(svg.ClassList, Is.EquivalentTo(new[] { "nx-icon", "nx-icon-md" }));
        Assert.That(svg.GetAttribute("viewBox"), Is.EqualTo("0 0 24 24"));
        Assert.That(svg.GetAttribute("fill"), Is.EqualTo("none"));
        Assert.That(svg.GetAttribute("stroke"), Is.EqualTo("currentColor"));
        Assert.That(svg.GetAttribute("data-icon"), Is.EqualTo("trash-2"));
        Assert.That(svg.GetAttribute("aria-hidden"), Is.EqualTo("true"));
        Assert.That(svg.GetAttribute("focusable"), Is.EqualTo("false"));
        Assert.That(svg.HasAttribute("role"), Is.False);
        Assert.That(svg.QuerySelectorAll("path"), Is.Not.Empty);
    }

    [Test]
    public void AriaLabel_MakesItAnImage_TitleRendersTitle()
    {
        var cut = Render<NxIcon>(p => p
            .Add(x => x.Icon, NxIcons.CheckCircle)
            .Add(x => x.AriaLabel, "Approved")
            .Add(x => x.Title, "Approved by QC"));

        var svg = cut.Find("svg");
        Assert.That(svg.GetAttribute("role"), Is.EqualTo("img"));
        Assert.That(svg.GetAttribute("aria-label"), Is.EqualTo("Approved"));
        Assert.That(svg.HasAttribute("aria-hidden"), Is.False);
        Assert.That(cut.Find("svg > title").TextContent, Is.EqualTo("Approved by QC"));
    }

    [TestCase(NxIconSize.XSmall, "nx-icon-xs")]
    [TestCase(NxIconSize.Small, "nx-icon-sm")]
    [TestCase(NxIconSize.Medium, "nx-icon-md")]
    [TestCase(NxIconSize.Large, "nx-icon-lg")]
    [TestCase(NxIconSize.XLarge, "nx-icon-xl")]
    [TestCase(NxIconSize.XXLarge, "nx-icon-2xl")]
    [TestCase(NxIconSize.XXXLarge, "nx-icon-3xl")]
    public void Size_IsAClass(NxIconSize size, string css)
    {
        var cut = Render<NxIcon>(p => p.Add(x => x.Icon, NxIcons.Plus).Add(x => x.Size, size));
        Assert.That(cut.Find("svg").ClassList, Does.Contain(css));
    }

    [TestCase(NxIconColor.Primary, "nx-icon-primary")]
    [TestCase(NxIconColor.Success, "nx-icon-success")]
    [TestCase(NxIconColor.Warning, "nx-icon-warning")]
    [TestCase(NxIconColor.Danger, "nx-icon-danger")]
    [TestCase(NxIconColor.Muted, "nx-icon-muted")]
    public void Color_IsAClass(NxIconColor color, string css)
    {
        var cut = Render<NxIcon>(p => p.Add(x => x.Icon, NxIcons.Plus).Add(x => x.Color, color));
        Assert.That(cut.Find("svg").ClassList, Does.Contain(css));
    }

    [Test]
    public void Inherit_AddsNoColorClass_ClassStyleAndAttributesPassThrough()
    {
        var cut = Render<NxIcon>(p => p
            .Add(x => x.Icon, NxIcons.Plus)
            .Add(x => x.Class, "mine")
            .Add(x => x.Style, "color: red")
            .AddUnmatched("id", "plus-icon"));

        var svg = cut.Find("svg");
        Assert.That(svg.ClassList, Is.EquivalentTo(new[] { "nx-icon", "nx-icon-md", "mine" }));
        Assert.That(svg.GetAttribute("style"), Is.EqualTo("color: red"));
        Assert.That(svg.Id, Is.EqualTo("plus-icon"));
    }

    [Test]
    public void FilledCustomIcon_UsesFillAndItsViewBox()
    {
        var diamond = new NxIconData("diamond", """<path d="M8 0 16 8 8 16 0 8z"/>""") { Filled = true, ViewBox = "0 0 16 16" };
        var cut = Render<NxIcon>(p => p.Add(x => x.Icon, diamond));

        var svg = cut.Find("svg");
        Assert.That(svg.GetAttribute("fill"), Is.EqualTo("currentColor"));
        Assert.That(svg.HasAttribute("stroke"), Is.False);
        Assert.That(svg.GetAttribute("viewBox"), Is.EqualTo("0 0 16 16"));
        Assert.That(cut.Find("path").GetAttribute("d"), Is.EqualTo("M8 0 16 8 8 16 0 8z"));
    }

    [Test]
    public void IconSet_EveryEntryHasDrawingAndMatchesItsProperty()
    {
        Assert.That(NxIcons.All, Has.Count.GreaterThanOrEqualTo(125));
        foreach (var (name, icon) in NxIcons.All)
        {
            Assert.That(icon.Content, Does.StartWith("<"), name);
            Assert.That(typeof(NxIcons).GetProperty(name)!.GetValue(null), Is.SameAs(icon), name);
        }
    }

    [Test]
    public void IconSet_CoversTheTelerikNamesErpUses()
    {
        string[] used = ["Plus", "Trash", "X", "Pencil", "Print", "CheckCircle", "ArrowLeft", "ArrowRight", "FileExcel", "Check",
                         "ExclamationCircle", "Download", "Undo", "Lock", "Redo", "Eye", "Save", "ClockArrowRotate", "Cancel", "XCircle"];
        Assert.That(used.Where(n => !NxIcons.All.ContainsKey(n)), Is.Empty);
    }
}
