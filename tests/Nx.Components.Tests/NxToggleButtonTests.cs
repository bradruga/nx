using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxToggleButtonTests : BunitContext
{
    private IRenderedComponent<NxToggleButton> RenderBound(List<bool> raised, bool initial = false,
        Action<ComponentParameterCollectionBuilder<NxToggleButton>>? configure = null)
    {
        IRenderedComponent<NxToggleButton>? cut = null;
        cut = Render<NxToggleButton>(p =>
        {
            p.Add(x => x.Selected, initial);
            p.Add(x => x.SelectedChanged, (bool v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Selected, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    [Test]
    public void Render_NxButtonWithAriaPressed()
    {
        var cut = RenderBound([], configure: p => p.AddChildContent("Show deleted").AddUnmatched("id", "deleted"));

        var button = cut.Find("button#deleted");
        Assert.That(button.ClassList, Does.Contain("nx-button").And.Contain("nx-button-secondary"));
        Assert.That(button.GetAttribute("aria-pressed"), Is.EqualTo("false"));
        Assert.That(button.TextContent.Trim(), Is.EqualTo("Show deleted"));
    }

    [Test]
    public async Task Click_Toggles_AndRaises()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised, configure: p => p.AddChildContent("x"));

        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("button").GetAttribute("aria-pressed"), Is.EqualTo("true"));
        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { true, false }));
        Assert.That(cut.Find("button").GetAttribute("aria-pressed"), Is.EqualTo("false"));
    }

    [Test]
    public void OutsideChange_Wins()
    {
        var cut = RenderBound([], configure: p => p.AddChildContent("x"));
        cut.Render(p => p.Add(x => x.Selected, true));
        Assert.That(cut.Find("button").GetAttribute("aria-pressed"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Disabled_DoesNotToggle()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised, configure: p => p.AddChildContent("x").Add(x => x.Disabled, true));

        Assert.That(cut.Find("button").HasAttribute("disabled"), Is.True);
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public void IconOnly_StaysIconOnly_WithItsLabel()
    {
        var cut = RenderBound([], configure: p => p
            .Add(x => x.Icon, (RenderFragment)(b => b.AddMarkupContent(0, "<svg class=\"i\"></svg>")))
            .Add(x => x.AriaLabel, "Activity feed")
            .Add(x => x.Title, "Feed"));

        var button = cut.Find("button");
        Assert.That(button.ClassList, Does.Contain("nx-button-icon-only"));
        Assert.That(button.GetAttribute("aria-label"), Is.EqualTo("Activity feed"));
        Assert.That(button.GetAttribute("title"), Is.EqualTo("Feed"));
    }

    [Test]
    public void VariantSizeClass_PassThrough_DefaultsLeftToTheGroup()
    {
        var own = RenderBound([], configure: p => p.AddChildContent("x").Add(x => x.Variant, NxButtonVariant.Ghost).Add(x => x.Size, NxSize.Small).Add(x => x.Class, "mine"));
        Assert.That(own.Find("button").ClassList, Does.Contain("nx-button-ghost").And.Contain("nx-button-sm").And.Contain("mine"));

        var grouped = Render<NxButtonGroup>(p => p
            .Add(x => x.Size, NxSize.Large)
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NxToggleButton>(0);
                b.AddComponentParameter(1, nameof(NxToggleButton.ChildContent), (RenderFragment)(t => t.AddContent(0, "Bold")));
                b.CloseComponent();
            })));
        Assert.That(grouped.Find("button").ClassList, Does.Contain("nx-button-lg"));
        Assert.That(grouped.Find("button").GetAttribute("aria-pressed"), Is.EqualTo("false"));
    }
}
