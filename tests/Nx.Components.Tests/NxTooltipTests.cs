using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxTooltipTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-tooltip.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private static RenderFragment Markup(string html) => b => b.AddMarkupContent(0, html);

    private static string? Option(JSRuntimeInvocation call, string name)
    {
        var options = call.Arguments[2]!;
        var value = options.GetType().GetProperty(name)!.GetValue(options);
        return value is string[] list ? string.Join(",", list) : value?.ToString();
    }

    [Test]
    public void Wrap_RendersContentsWrapperAndHiddenTooltip()
    {
        var cut = Render<NxTooltip>(p => p
            .Add(x => x.Id, "tip")
            .Add(x => x.Text, "Saves the pour")
            .Add(x => x.ChildContent, Markup("<button id=\"save\">Save</button>")));

        Assert.That(cut.Find(".nx-tooltip-anchor > button#save"), Is.Not.Null);
        var tip = cut.Find("#tip");
        Assert.That(tip.GetAttribute("role"), Is.EqualTo("tooltip"));
        Assert.That(tip.HasAttribute("hidden"), Is.True);
        Assert.That(tip.HasAttribute("style"), Is.False);
        Assert.That(tip.TextContent.Trim(), Is.EqualTo("Saves the pour"));
    }

    [Test]
    public void Target_RendersNoWrapper()
    {
        var cut = Render<NxTooltip>(p => p.Add(x => x.Target, "#status").Add(x => x.Text, "Held"));

        Assert.That(cut.FindAll(".nx-tooltip-anchor"), Is.Empty);
        var init = module.Invocations.Single(i => i.Identifier == "init");
        Assert.That(Option(init, "target"), Is.EqualTo("#status"));
        Assert.That(Option(init, "wrapper"), Is.Null);
    }

    [Test]
    public void Content_ReplacesText()
    {
        var cut = Render<NxTooltip>(p => p
            .Add(x => x.Text, "plain")
            .Add(x => x.Content, Markup("<strong>Rich</strong>"))
            .Add(x => x.ChildContent, Markup("<span>a</span>")));

        Assert.That(cut.Find(".nx-tooltip-body").InnerHtml, Does.Contain("<strong>Rich</strong>"));
        Assert.That(cut.Find(".nx-tooltip-body").TextContent, Does.Not.Contain("plain"));
    }

    [Test]
    public void Init_PassesDefaults()
    {
        Render<NxTooltip>(p => p.Add(x => x.Id, "tip").Add(x => x.Text, "t").Add(x => x.ChildContent, Markup("<span>a</span>")));

        var init = module.Invocations.Single(i => i.Identifier == "init");
        Assert.That(init.Arguments[0], Is.EqualTo("tip"));
        Assert.That(Option(init, "triggers"), Is.EqualTo("hover,focus"));
        Assert.That(Option(init, "showDelay"), Is.EqualTo("400"));
        Assert.That(Option(init, "hideDelay"), Is.EqualTo("100"));
        Assert.That(Option(init, "placement"), Is.EqualTo("top"));
        Assert.That(Option(init, "disabled"), Is.EqualTo("False"));
    }

    [Test]
    public void Init_PassesTriggersPlacementDelays()
    {
        Render<NxTooltip>(p => p
            .Add(x => x.Text, "t")
            .Add(x => x.Trigger, NxTooltipTrigger.Click | NxTooltipTrigger.Focus)
            .Add(x => x.Placement, NxTooltipPlacement.Left)
            .Add(x => x.ShowDelay, 0)
            .Add(x => x.HideDelay, 50)
            .Add(x => x.ChildContent, Markup("<span>a</span>")));

        var init = module.Invocations.Single(i => i.Identifier == "init");
        Assert.That(Option(init, "triggers"), Is.EqualTo("focus,click"));
        Assert.That(Option(init, "placement"), Is.EqualTo("left"));
        Assert.That(Option(init, "showDelay"), Is.EqualTo("0"));
        Assert.That(Option(init, "hideDelay"), Is.EqualTo("50"));
    }

    [Test]
    public void ChangedParameters_ReInit_SameParameters_Refresh()
    {
        var cut = Render<NxTooltip>(p => p.Add(x => x.Text, "t").Add(x => x.ChildContent, Markup("<span>a</span>")));

        cut.Render(p => p.Add(x => x.Text, "changed text"));
        Assert.That(module.Invocations.Count(i => i.Identifier == "init"), Is.EqualTo(1));
        Assert.That(module.Invocations.Count(i => i.Identifier == "refresh"), Is.EqualTo(1));

        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.That(module.Invocations.Count(i => i.Identifier == "init"), Is.EqualTo(2));
        Assert.That(Option(module.Invocations.Last(i => i.Identifier == "init"), "disabled"), Is.EqualTo("True"));
    }

    [Test]
    public async Task ShowHide_CallJs()
    {
        var cut = Render<NxTooltip>(p => p.Add(x => x.Id, "tip").Add(x => x.Text, "t").Add(x => x.ChildContent, Markup("<span>a</span>")));

        await cut.InvokeAsync(cut.Instance.ShowAsync);
        await cut.InvokeAsync(cut.Instance.HideAsync);

        Assert.That(module.Invocations.Any(i => i.Identifier == "showNow" && (string?)i.Arguments[0] == "tip"), Is.True);
        Assert.That(module.Invocations.Any(i => i.Identifier == "hideNow"), Is.True);
    }

    [Test]
    public async Task Dispose_DisposesInJs()
    {
        var cut = Render<NxTooltip>(p => p.Add(x => x.Id, "tip").Add(x => x.Text, "t").Add(x => x.ChildContent, Markup("<span>a</span>")));

        await cut.Instance.DisposeAsync();

        Assert.That(module.Invocations.Any(i => i.Identifier == "dispose" && (string?)i.Arguments[0] == "tip"), Is.True);
    }

    [Test]
    public void GeneratedId_Unique()
    {
        var a = Render<NxTooltip>(p => p.Add(x => x.Text, "a"));
        var b = Render<NxTooltip>(p => p.Add(x => x.Text, "b"));

        Assert.That(a.Find("[role=tooltip]").Id, Is.Not.EqualTo(b.Find("[role=tooltip]").Id));
    }
}
