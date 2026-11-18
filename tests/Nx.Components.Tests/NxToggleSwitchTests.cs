using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxToggleSwitchTests : BunitContext
{
    [SetUp]
    public void SetUp() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<NxToggleSwitch> RenderBound(List<bool> raised, bool initial,
        Action<ComponentParameterCollectionBuilder<NxToggleSwitch>>? configure = null)
    {
        IRenderedComponent<NxToggleSwitch>? cut = null;
        cut = Render<NxToggleSwitch>(p =>
        {
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (bool v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static Task ClickAsync(IRenderedComponent<NxToggleSwitch> cut) =>
        cut.Find("button").ClickAsync(new MouseEventArgs());

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_SwitchRole_LabelForIt()
    {
        var cut = Render<NxToggleSwitch>(p => p.Add(x => x.Label, "Notify me"));

        var track = cut.Find("button.nx-switch-track");
        Assert.That(track.GetAttribute("role"), Is.EqualTo("switch"));
        Assert.That(track.GetAttribute("aria-checked"), Is.EqualTo("false"));
        Assert.That(cut.Find("label.nx-switch-label").GetAttribute("for"), Is.EqualTo(track.Id));
        Assert.That(cut.Find(".nx-switch-thumb").GetAttribute("aria-hidden"), Is.EqualTo("true"));
    }

    [Test]
    public void On_SetsAriaCheckedAndClass()
    {
        var cut = Render<NxToggleSwitch>(p => p.Add(x => x.Value, true));

        Assert.That(cut.Find("button").GetAttribute("aria-checked"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-switch").ClassList, Does.Contain("nx-switch-on"));
    }

    [TestCase(NxSize.Small, "nx-switch-sm")]
    [TestCase(NxSize.Medium, "nx-switch-md")]
    [TestCase(NxSize.Large, "nx-switch-lg")]
    public void Size_AppliesClass(NxSize size, string expected)
    {
        var cut = Render<NxToggleSwitch>(p => p.Add(x => x.Size, size));

        Assert.That(cut.Find(".nx-switch").ClassList, Does.Contain(expected));
    }

    [Test]
    public void StateText_Inside_ShowsCurrentTextInTrack()
    {
        var cut = Render<NxToggleSwitch>(p => p.Add(x => x.Value, true).Add(x => x.OnText, "On").Add(x => x.OffText, "Off"));

        var state = cut.Find(".nx-switch-track .nx-switch-state-inside");
        Assert.That(state.TextContent, Is.EqualTo("On"));
        Assert.That(state.GetAttribute("aria-hidden"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-switch").ClassList, Does.Contain("nx-switch-text-inside"));
    }

    [Test]
    public async Task StateText_Beside_FollowsValue()
    {
        var cut = RenderBound([], false, p => p
            .Add(x => x.OnText, "Yes").Add(x => x.OffText, "No")
            .Add(x => x.StateTextPosition, NxToggleTextPosition.Beside));

        Assert.That(cut.FindAll(".nx-switch-track .nx-switch-state"), Is.Empty);
        Assert.That(cut.Find(".nx-switch-state-beside").TextContent, Is.EqualTo("No"));

        await ClickAsync(cut);
        Assert.That(cut.Find(".nx-switch-state-beside").TextContent, Is.EqualTo("Yes"));
    }

    [Test]
    public async Task StateText_Inside_FollowsValue()
    {
        var cut = RenderBound([], true, p => p.Add(x => x.OnText, "On").Add(x => x.OffText, "Off"));

        await ClickAsync(cut);

        Assert.That(cut.Find(".nx-switch-state-inside").TextContent, Is.EqualTo("Off"));
    }

    [Test]
    public void ChildContent_RichLabel()
    {
        var cut = Render<NxToggleSwitch>(p => p
            .Add(x => x.Label, "Publish ")
            .Add(x => x.ChildContent, b => b.AddMarkupContent(0, "<a href=\"#\">to portal</a>")));

        Assert.That(cut.Find("label.nx-switch-label").TextContent, Is.EqualTo("Publish to portal"));
    }

    [Test]
    public void NoStateText_RendersNone()
    {
        var cut = Render<NxToggleSwitch>();

        Assert.That(cut.FindAll(".nx-switch-state"), Is.Empty);
    }

    [Test]
    public void LabelPosition_Start_AddsClass()
    {
        var cut = Render<NxToggleSwitch>(p => p.Add(x => x.Label, "x").Add(x => x.LabelPosition, NxLabelPosition.Start));

        Assert.That(cut.Find(".nx-switch").ClassList, Does.Contain("nx-switch-label-start"));
    }

    [Test]
    public void AdditionalAttributes_GoToSwitch()
    {
        var cut = Render<NxToggleSwitch>(p => p.AddUnmatched("aria-label", "Dark mode").Add(x => x.Id, "dark"));

        Assert.That(cut.Find("button").GetAttribute("aria-label"), Is.EqualTo("Dark mode"));
        Assert.That(cut.Find("button").Id, Is.EqualTo("dark"));
    }

    // ── Toggling ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Click_Toggles()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised, false);

        await ClickAsync(cut);
        await ClickAsync(cut);

        Assert.That(raised, Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public async Task Disabled_NoToggle()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised, false, p => p.Add(x => x.Disabled, true));

        await ClickAsync(cut);

        Assert.That(cut.Find("button").HasAttribute("disabled"), Is.True);
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task Loading_SpinnerBusy_StaysFocusable_NoToggle()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised, true, p => p.Add(x => x.Loading, true));

        await ClickAsync(cut);

        var track = cut.Find("button");
        Assert.That(cut.FindAll(".nx-switch-thumb .nx-spinner"), Has.Count.EqualTo(1));
        Assert.That(track.GetAttribute("aria-busy"), Is.EqualTo("true"));
        Assert.That(track.GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(track.HasAttribute("disabled"), Is.False);
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public void Keyboard_IsANativeButton()
    {
        var cut = Render<NxToggleSwitch>();

        Assert.That(cut.Find(".nx-switch-track").TagName, Is.EqualTo("BUTTON"));
        Assert.That(cut.Find(".nx-switch-track").GetAttribute("type"), Is.EqualTo("button"));
    }

    [Test]
    public async Task FocusAsync_Focuses()
    {
        var cut = Render<NxToggleSwitch>();

        await cut.InvokeAsync(() => cut.Instance.FocusAsync().AsTask());

        JSInterop.VerifyFocusAsyncInvoke();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private class Model
    {
        public bool Enabled { get; set; }
    }

    [Test]
    public async Task Validation_ToggleMarksModified_MessagesMarkInvalid()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxToggleSwitch>(c => c
                .Add(x => x.Value, model.Enabled)
                .Add(x => x.ValueChanged, (bool v) => model.Enabled = v)
                .Add(x => x.ValueExpression, () => model.Enabled)));
        var cut = host.FindComponent<NxToggleSwitch>();

        await ClickAsync(cut);
        Assert.That(context.IsModified(context.Field(nameof(Model.Enabled))), Is.True);

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.Enabled)), "Must be on.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("button").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-switch").ClassList, Does.Contain("nx-switch-invalid"));
    }
}
