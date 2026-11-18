using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxButtonGroupTests : BunitContext
{
    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-input.js").Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Buttons as markup would declare them; each entry's parameters are set on one NxButton.</summary>
    private static RenderFragment Buttons(params Action<RenderTreeBuilderButton>[] buttons) => builder =>
    {
        var seq = 0;
        foreach (var configure in buttons)
        {
            builder.OpenComponent<NxButton>(seq++);
            var b = new RenderTreeBuilderButton(builder, () => seq++);
            configure(b);
            builder.CloseComponent();
        }
    };

    private sealed class RenderTreeBuilderButton(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, Func<int> seq)
    {
        public RenderTreeBuilderButton Set(string name, object? value)
        {
            builder.AddComponentParameter(seq(), name, value);
            return this;
        }

        public RenderTreeBuilderButton Text(string text) => Set(nameof(NxButton.ChildContent), (RenderFragment)(t => t.AddContent(0, text)));
    }

    [Test]
    public void Render_GroupRoleAndLabel_ButtonsInside()
    {
        var cut = Render<NxButtonGroup>(p => p
            .Add(x => x.AriaLabel, "Week")
            .Add(x => x.Class, "mine")
            .Add(x => x.ChildContent, Buttons(b => b.Text("Today"), b => b.Text("Next")))
            .AddUnmatched("id", "week"));

        var group = cut.Find("#week");
        Assert.That(group.GetAttribute("role"), Is.EqualTo("group"));
        Assert.That(group.GetAttribute("aria-label"), Is.EqualTo("Week"));
        Assert.That(group.ClassList, Is.EquivalentTo(new[] { "nx-buttongroup", "mine" }));
        Assert.That(cut.FindAll("#week > button.nx-button").Select(b => b.TextContent.Trim()), Is.EqualTo(new[] { "Today", "Next" }));
    }

    [Test]
    public void VariantAndSize_ApplyToButtonsThatDoNotSetTheirOwn()
    {
        var cut = Render<NxButtonGroup>(p => p
            .Add(x => x.Variant, NxButtonVariant.Ghost)
            .Add(x => x.Size, NxButtonSize.Small)
            .Add(x => x.ChildContent, Buttons(
                b => b.Text("Plain"),
                b => b.Text("Own").Set(nameof(NxButton.Variant), NxButtonVariant.Primary).Set(nameof(NxButton.Size), NxButtonSize.Large),
                b => b.Text("Secondary").Set(nameof(NxButton.Variant), NxButtonVariant.Secondary))));

        var buttons = cut.FindAll("button");
        Assert.That(buttons[0].ClassList, Does.Contain("nx-button-ghost").And.Contain("nx-button-sm"));
        Assert.That(buttons[1].ClassList, Does.Contain("nx-button-primary").And.Contain("nx-button-lg"));
        // A Variant passed as the default value is still the button's own.
        Assert.That(buttons[2].ClassList, Does.Contain("nx-button-secondary").And.Contain("nx-button-sm"));
    }

    [Test]
    public void NoGroupValues_ButtonsKeepTheirDefaults()
    {
        var cut = Render<NxButtonGroup>(p => p.Add(x => x.ChildContent, Buttons(b => b.Text("A"))));
        Assert.That(cut.Find("button").ClassList, Does.Contain("nx-button-secondary").And.Contain("nx-button-md"));
    }

    [Test]
    public async Task Disabled_DisablesEveryButton_AndBlocksClicks()
    {
        var clicks = 0;
        var cut = Render<NxButtonGroup>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.ChildContent, Buttons(b => b.Text("A").Set(nameof(NxButton.OnClick), EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++)), b => b.Text("B"))));

        Assert.That(cut.FindAll("button").All(b => b.HasAttribute("disabled")), Is.True);
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.That(clicks, Is.EqualTo(0));

        cut.Render(p => p.Add(x => x.Disabled, false));
        Assert.That(cut.FindAll("button").Any(b => b.HasAttribute("disabled")), Is.False);
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.That(clicks, Is.EqualTo(1));
    }

    [Test]
    public void GroupChange_ReachesButtons()
    {
        var cut = Render<NxButtonGroup>(p => p.Add(x => x.Size, NxButtonSize.Small).Add(x => x.ChildContent, Buttons(b => b.Text("A"))));
        cut.Render(p => p.Add(x => x.Size, NxButtonSize.Large));
        Assert.That(cut.Find("button").ClassList, Does.Contain("nx-button-lg"));
    }

    [Test]
    public void FullWidth_AddsClass()
    {
        var cut = Render<NxButtonGroup>(p => p.Add(x => x.FullWidth, true).Add(x => x.ChildContent, Buttons(b => b.Text("A"))));
        Assert.That(cut.Find(".nx-buttongroup").ClassList, Does.Contain("nx-buttongroup-full-width"));
    }

    private static RenderFragment MenuButton(params (string Name, object Value)[] parameters) => b =>
    {
        b.OpenComponent<NxDropDownButton>(0);
        b.AddComponentParameter(1, nameof(NxDropDownButton.Items), new List<NxMenuItem> { new("Hold") });
        b.AddComponentParameter(2, nameof(NxDropDownButton.ChildContent), (RenderFragment)(t => t.AddContent(0, "More")));
        var seq = 3;
        foreach (var (name, value) in parameters)
            b.AddComponentParameter(seq++, name, value);
        b.CloseComponent();
    };

    [Test]
    public void Disabled_DisablesAMenuButtonInTheGroup()
    {
        var cut = Render<NxButtonGroup>(p => p.Add(x => x.Disabled, true).Add(x => x.ChildContent, MenuButton()));

        Assert.That(cut.Find(".nx-dropdownbutton button").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void VariantAndSize_DoNotReachAMenuButton_ItKeepsItsOwn()
    {
        // QUESTIONS #114: menu buttons pass their own Variant and Size; only Disabled comes from the group.
        var cut = Render<NxButtonGroup>(p => p.Add(x => x.Variant, NxButtonVariant.Ghost).Add(x => x.Size, NxButtonSize.Small).Add(x => x.ChildContent, MenuButton()));

        Assert.That(cut.Find(".nx-dropdownbutton button").ClassList, Does.Contain("nx-button-secondary").And.Contain("nx-button-md"));
    }

    [Test]
    public void ButtonOutsideAGroup_Unchanged()
    {
        var cut = Render<NxButton>(p => p.AddChildContent("Solo"));
        Assert.That(cut.Find("button").ClassList, Does.Contain("nx-button-secondary").And.Contain("nx-button-md"));
        Assert.That(cut.Find("button").HasAttribute("disabled"), Is.False);
    }
}
