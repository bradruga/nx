using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxSliderTests : BunitContext
{
    private IRenderedComponent<NxSlider<int>> RenderBound(List<int> raised, int initial = 3,
        Action<ComponentParameterCollectionBuilder<NxSlider<int>>>? configure = null)
    {
        IRenderedComponent<NxSlider<int>>? cut = null;
        cut = Render<NxSlider<int>>(p =>
        {
            p.Add(x => x.Id, "s");
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (int v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    [Test]
    public void Render_NativeRange_WithLimitsAndFill()
    {
        var cut = RenderBound([], 3, p => p.Add(x => x.Label, "Crew").Add(x => x.Max, 12));

        var input = cut.Find("input#s[type=range]");
        Assert.That(input.GetAttribute("min"), Is.EqualTo("0"));
        Assert.That(input.GetAttribute("max"), Is.EqualTo("12"));
        Assert.That(input.GetAttribute("step"), Is.EqualTo("1"));
        Assert.That(input.GetAttribute("value"), Is.EqualTo("3"));
        Assert.That(cut.Find("label[for=s]").TextContent, Is.EqualTo("Crew"));
        Assert.That(cut.Find(".nx-slider").GetAttribute("style"), Does.Contain("--nx-slider-percent: 25%"));
    }

    [Test]
    public async Task Input_RaisesWhileDragging()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised);

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "7" });
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "8" });

        Assert.That(raised, Is.EqualTo(new[] { 7, 8 }));
        Assert.That(cut.Find(".nx-slider").GetAttribute("style"), Does.Contain("--nx-slider-percent: 80%"));
    }

    [Test]
    public async Task NotImmediate_RaisesOnRelease()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, configure: p => p.Add(x => x.Immediate, false));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "6" });
        Assert.That(raised, Is.Empty);
        await cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = "6" });

        Assert.That(raised, Is.EqualTo(new[] { 6 }));
    }

    [Test]
    public async Task Buttons_StepAndStopAtTheEnds()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 9, p => p.Add(x => x.ShowButtons, true));

        var buttons = cut.FindAll(".nx-slider-button");
        Assert.That(buttons[0].GetAttribute("aria-label"), Is.EqualTo("Decrease"));
        Assert.That(buttons[1].GetAttribute("tabindex"), Is.EqualTo("-1"));
        await cut.FindAll(".nx-slider-button")[1].ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { 10 }));
        Assert.That(cut.FindAll(".nx-slider-button")[1].HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void Ticks_EveryLargeStep_Formatted()
    {
        var cut = Render<NxSlider<decimal>>(p => p
            .Add(x => x.Min, 0m).Add(x => x.Max, 0.1m).Add(x => x.LargeStep, 0.05m)
            .Add(x => x.Format, "P0").Add(x => x.Value, 0.05m).Add(x => x.ShowValue, true));

        var ticks = cut.FindAll(".nx-slider-tick");
        Assert.That(ticks.Select(t => t.GetAttribute("style")), Is.EqualTo(new[] { "--nx-slider-at: 0%", "--nx-slider-at: 50%", "--nx-slider-at: 100%" }));
        Assert.That(cut.Find(".nx-slider-value").TextContent, Is.EqualTo(0.05m.ToString("P0")));
        Assert.That(cut.Find("input").GetAttribute("aria-valuetext"), Is.EqualTo(0.05m.ToString("P0")));
    }

    [Test]
    public void Ticks_LabelledWithFormattedValues()
    {
        var cut = Render<NxSlider<decimal>>(p => p
            .Add(x => x.Min, 0m).Add(x => x.Max, 0.1m).Add(x => x.LargeStep, 0.05m).Add(x => x.Format, "P0"));

        Assert.That(cut.FindAll(".nx-slider-tick").Select(t => t.TextContent),
            Is.EqualTo(new[] { 0m.ToString("P0"), 0.05m.ToString("P0"), 0.1m.ToString("P0") }));
    }

    [Test]
    public void NoLargeStep_NoTicks()
    {
        var cut = RenderBound([]);

        Assert.That(cut.FindAll(".nx-slider-tick"), Is.Empty);
    }

    [Test]
    public async Task DecreaseButton_StepsBySmallStep_AndDisablesAtMin()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised, 4, p => p.Add(x => x.ShowButtons, true).Add(x => x.SmallStep, 2));

        await cut.FindAll(".nx-slider-button")[0].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".nx-slider-button")[0].ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { 2, 0 }));
        Assert.That(cut.FindAll(".nx-slider-button")[0].HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find("input").GetAttribute("step"), Is.EqualTo("2"));
    }

    [Test]
    public async Task Input_PastMax_RaisesMax()
    {
        var raised = new List<int>();
        var cut = RenderBound(raised);

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "15" });

        Assert.That(raised, Is.EqualTo(new[] { 10 }));
    }

    [Test]
    public async Task NotImmediate_ValueShownWhileDragging()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Immediate, false).Add(x => x.ShowValue, true));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "6" });

        Assert.That(cut.Find(".nx-slider-value").TextContent, Is.EqualTo("6"));
        Assert.That(cut.Find(".nx-slider").GetAttribute("style"), Does.Contain("--nx-slider-percent: 60%"));
    }

    [Test]
    public void AttributesToInput_ClassStyleToOuter()
    {
        var cut = RenderBound([], configure: p => p
            .Add(x => x.Class, "wide").Add(x => x.Style, "width: 300px").AddUnmatched("aria-label", "Crew"));

        Assert.That(cut.Find("input").GetAttribute("aria-label"), Is.EqualTo("Crew"));
        Assert.That(cut.Find(".nx-slider").ClassList, Does.Contain("wide"));
        Assert.That(cut.Find(".nx-slider").GetAttribute("style"), Does.EndWith("width: 300px"));
    }

    [Test]
    public void OutsideValue_Clamped_AndWins()
    {
        var cut = RenderBound([], 30);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("10"));

        cut.Render(p => p.Add(x => x.Value, 2));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("2"));
    }

    [Test]
    public void Disabled_DisablesInputAndButtons()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Disabled, true).Add(x => x.ShowButtons, true));
        Assert.That(cut.Find("input").HasAttribute("disabled"), Is.True);
        Assert.That(cut.FindAll(".nx-slider-button").All(b => b.HasAttribute("disabled")), Is.True);
        Assert.That(cut.Find(".nx-slider").ClassList, Does.Contain("nx-slider-disabled"));
    }

    private sealed class Mix
    {
        [System.ComponentModel.DataAnnotations.Range(3, 7)]
        public int Slump { get; set; } = 9;
    }

    [Test]
    public async Task Validation_InvalidState_AndNotifies()
    {
        var mix = new Mix();
        var context = new EditContext(mix);
        var store = new ValidationMessageStore(context);
        var changed = new List<string>();
        context.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier.FieldName);
        var cut = Render<NxSlider<int>>(p => p
            .AddCascadingValue(context)
            .Add(x => x.Value, mix.Slump)
            .Add(x => x.ValueChanged, v => mix.Slump = v)
            .Add(x => x.ValueExpression, () => mix.Slump));

        store.Add(context.Field(nameof(Mix.Slump)), "Too wet");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);
        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-slider").ClassList, Does.Contain("nx-slider-invalid"));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "5" });
        Assert.That(changed, Is.EqualTo(new[] { "Slump" }));
        Assert.That(mix.Slump, Is.EqualTo(5));
    }
}
