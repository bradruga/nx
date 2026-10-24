using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxColorGradientTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-colorgradient.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxColorGradient> RenderBound(List<string?> raised, string? initial = "#ff0000",
        Action<ComponentParameterCollectionBuilder<NxColorGradient>>? configure = null)
    {
        IRenderedComponent<NxColorGradient>? cut = null;
        cut = Render<NxColorGradient>(p =>
        {
            p.Add(x => x.Id, "g");
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (string? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static string? Style(IRenderedComponent<NxColorGradient> cut) => cut.Find(".nx-colorgradient").GetAttribute("style");

    [Test]
    public void Render_AreaSlidersInputs_FromTheValue()
    {
        var cut = RenderBound([], "#2e6db4");

        Assert.That(cut.Find(".nx-colorgradient").GetAttribute("role"), Is.EqualTo("group"));
        var thumb = cut.Find(".nx-colorgradient-thumb");
        Assert.That(thumb.GetAttribute("role"), Is.EqualTo("slider"));
        Assert.That(thumb.GetAttribute("aria-valuetext"), Is.EqualTo("Saturation 74%, brightness 71%"));
        Assert.That(cut.Find(".nx-colorgradient-hue").GetAttribute("value"), Is.EqualTo("212"));
        Assert.That(cut.Find("#g-hex").GetAttribute("value"), Is.EqualTo("#2e6db4"));
        Assert.That(cut.FindAll(".nx-colorgradient-alpha"), Has.Count.EqualTo(1));
        Assert.That(Style(cut), Does.Contain("--nx-colorgradient-hue: 212").And.Contain("--nx-colorgradient-opaque: #2e6db4"));
        Assert.That(module.Invocations.Single(i => i.Identifier == "init").Arguments[2], Does.Contain("ArrowUp"));
    }

    [Test]
    public async Task AreaPick_RaisesHex()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#ff0000");

        await cut.Instance.OnAreaPick(1, 0.5);

        Assert.That(raised, Is.EqualTo(new[] { "#800000" }));
        Assert.That(Style(cut), Does.Contain("--nx-colorgradient-y: 50%"));
    }

    [Test]
    public async Task AreaKeys_StepSaturationAndBrightness()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#ff0000");

        await cut.Find(".nx-colorgradient-thumb").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });
        await cut.Find(".nx-colorgradient-thumb").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });

        Assert.That(raised, Is.EqualTo(new[] { "#e60000", "#e6e6e6" }));
    }

    [Test]
    public async Task Hue_KeptAtGrey()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#ff0000");

        await cut.Find(".nx-colorgradient-thumb").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        await cut.Find(".nx-colorgradient-thumb").KeyDownAsync(new KeyboardEventArgs { Key = "End" });

        Assert.That(raised[^1], Is.EqualTo("#ff0000"), "saturating again returns to red, not to hue 0 by chance");
        Assert.That(Style(cut), Does.Contain("--nx-colorgradient-hue: 0"));
    }

    [Test]
    public async Task HueAndAlphaSliders()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#ff0000");

        await cut.Find(".nx-colorgradient-hue").InputAsync(new ChangeEventArgs { Value = "120" });
        await cut.Find(".nx-colorgradient-alpha").InputAsync(new ChangeEventArgs { Value = "50" });

        Assert.That(raised, Is.EqualTo(new[] { "#00ff00", "#00ff0080" }));
    }

    [Test]
    public async Task HexInput_WithOrWithoutHash_BadTextReverts()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#ff0000", p => p.Add(x => x.ShowOpacityEditor, false));

        await cut.Find("#g-hex").ChangeAsync(new ChangeEventArgs { Value = "00f" });
        await cut.Find("#g-hex").ChangeAsync(new ChangeEventArgs { Value = "not a color" });

        Assert.That(raised, Is.EqualTo(new[] { "#0000ff" }));
        Assert.That(cut.Find("#g-hex").GetAttribute("value"), Is.EqualTo("#0000ff"));
        Assert.That(cut.FindAll(".nx-colorgradient-alpha"), Is.Empty);
    }

    [Test]
    public async Task RgbView_PartsAndRgbValue()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "rgb(10, 20, 30)", p => p.Add(x => x.Format, NxColorFormat.Rgb).Add(x => x.ValueFormat, NxColorFormat.Rgb));

        var parts = cut.FindAll(".nx-colorgradient-input");
        Assert.That(parts.Select(i => i.GetAttribute("value")), Is.EqualTo(new[] { "10", "20", "30", "100" }));
        Assert.That(parts.Select(i => i.GetAttribute("aria-label")), Is.EqualTo(new[] { "Red", "Green", "Blue", "Opacity percent" }));
        await parts[0].ChangeAsync(new ChangeEventArgs { Value = "200" });
        await cut.FindAll(".nx-colorgradient-input")[3].ChangeAsync(new ChangeEventArgs { Value = "25" });

        Assert.That(raised, Is.EqualTo(new[] { "rgb(200, 20, 30)", "rgba(200, 20, 30, 0.25)" }));
    }

    [Test]
    public async Task ViewButton_SwitchesInputs()
    {
        var cut = RenderBound([]);
        Assert.That(cut.FindAll("#g-hex"), Has.Count.EqualTo(1));
        await cut.Find(".nx-colorgradient-view").ClickAsync(new MouseEventArgs());
        Assert.That(cut.FindAll("#g-hex"), Is.Empty);
        Assert.That(cut.FindAll(".nx-colorgradient-input"), Has.Count.EqualTo(4));
    }

    [Test]
    public void OutsideValue_Replaces()
    {
        var cut = RenderBound([], "#ff0000");
        cut.Render(p => p.Add(x => x.Value, "#0000ff"));
        Assert.That(cut.Find("#g-hex").GetAttribute("value"), Is.EqualTo("#0000ff"));
    }

    [Test]
    public async Task Disabled_IgnoresChanges()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, configure: p => p.Add(x => x.Disabled, true));

        await cut.Instance.OnAreaPick(0, 0);

        Assert.That(raised, Is.Empty);
        Assert.That(cut.Find(".nx-colorgradient-thumb").GetAttribute("tabindex"), Is.EqualTo("-1"));
        Assert.That(cut.Find(".nx-colorgradient-hue").HasAttribute("disabled"), Is.True);
    }

    // ── ColorMath ─────────────────────────────────────────────────────────────

    [TestCase("#f00", "#ff0000")]
    [TestCase("#FF000080", "#ff000080")]
    [TestCase("#f008", "#ff000088")]
    [TestCase("rgb(46, 109, 180)", "#2e6db4")]
    [TestCase("rgba(46,109,180,0.5)", "#2e6db480")]
    [TestCase("rgb(46 109 180 / 50%)", "#2e6db480")]
    public void Math_ParseToHex(string color, string hex) =>
        Assert.That(ColorMath.ToHex(ColorMath.Parse(color)!.Value), Is.EqualTo(hex));

    [TestCase("red")]
    [TestCase("#12345")]
    [TestCase("")]
    public void Math_Unreadable(string color) => Assert.That(ColorMath.Parse(color), Is.Null);

    [Test]
    public void Math_HsvRoundTrip()
    {
        foreach (var hex in new[] { "#2e6db4", "#ffffff", "#000000", "#7a3e9d", "#c0392b" })
        {
            var rgba = ColorMath.Parse(hex)!.Value;
            Assert.That(ColorMath.ToHex(ColorMath.ToRgba(ColorMath.ToHsv(rgba))), Is.EqualTo(hex));
        }
        Assert.That(ColorMath.ToRgb(new Rgba(1, 2, 3, 0.5)), Is.EqualTo("rgba(1, 2, 3, 0.5)"));
    }
}
