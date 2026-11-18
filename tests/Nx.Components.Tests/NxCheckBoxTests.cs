using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxCheckBoxTests : BunitContext
{
    [SetUp]
    public void SetUp() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<NxCheckBox<T>> RenderBound<T>(List<T?> raised, T? initial,
        Action<ComponentParameterCollectionBuilder<NxCheckBox<T>>>? configure = null)
    {
        IRenderedComponent<NxCheckBox<T>>? cut = null;
        cut = Render<NxCheckBox<T>>(p =>
        {
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (T? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static Task ClickAsync<T>(IRenderedComponent<NxCheckBox<T>> cut) =>
        cut.Find("button").ClickAsync(new MouseEventArgs());

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_ButtonWithCheckboxRole_AndLabelForIt()
    {
        var cut = Render<NxCheckBox<bool>>(p => p.Add(x => x.Label, "Rush order"));

        var box = cut.Find("button.nx-checkbox-box");
        Assert.That(box.GetAttribute("role"), Is.EqualTo("checkbox"));
        Assert.That(box.GetAttribute("type"), Is.EqualTo("button"));
        Assert.That(box.GetAttribute("aria-checked"), Is.EqualTo("false"));
        var label = cut.Find("label.nx-checkbox-label");
        Assert.That(label.TextContent, Is.EqualTo("Rush order"));
        Assert.That(label.GetAttribute("for"), Is.EqualTo(box.Id));
    }

    [Test]
    public void Checked_ShowsMarkAndAriaChecked()
    {
        var cut = Render<NxCheckBox<bool>>(p => p.Add(x => x.Value, true));

        Assert.That(cut.Find("button").GetAttribute("aria-checked"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-checkbox").ClassList, Does.Contain("nx-checkbox-checked"));
        Assert.That(cut.Find(".nx-checkbox-mark").GetAttribute("aria-hidden"), Is.EqualTo("true"));
    }

    [Test]
    public void NullableNull_IsIndeterminate()
    {
        var cut = Render<NxCheckBox<bool?>>(p => p.Add(x => x.Value, (bool?)null));

        Assert.That(cut.Find("button").GetAttribute("aria-checked"), Is.EqualTo("mixed"));
        Assert.That(cut.Find(".nx-checkbox").ClassList, Does.Contain("nx-checkbox-indeterminate"));
    }

    [Test]
    public void ChildContent_RichLabel()
    {
        var cut = Render<NxCheckBox<bool>>(p => p
            .Add(x => x.Label, "Accept ")
            .Add(x => x.ChildContent, b => b.AddMarkupContent(0, "<a href=\"#\">terms</a>")));

        Assert.That(cut.Find("label a").TextContent, Is.EqualTo("terms"));
        Assert.That(cut.Find("label").TextContent, Is.EqualTo("Accept terms"));
    }

    [Test]
    public void NoLabel_RendersNoLabelElement()
    {
        var cut = Render<NxCheckBox<bool>>(p => p.AddUnmatched("aria-label", "Select row"));

        Assert.That(cut.FindAll("label"), Is.Empty);
        Assert.That(cut.Find("button").GetAttribute("aria-label"), Is.EqualTo("Select row"));
    }

    [Test]
    public void LabelPosition_Start_AddsClass()
    {
        var cut = Render<NxCheckBox<bool>>(p => p.Add(x => x.Label, "x").Add(x => x.LabelPosition, NxLabelPosition.Start));

        Assert.That(cut.Find(".nx-checkbox").ClassList, Does.Contain("nx-checkbox-label-start"));
    }

    [Test]
    public void Id_ClassStyle()
    {
        var cut = Render<NxCheckBox<bool>>(p => p.Add(x => x.Id, "rush").Add(x => x.Class, "c").Add(x => x.Style, "margin: 2px"));

        Assert.That(cut.Find("button").Id, Is.EqualTo("rush"));
        Assert.That(cut.Find(".nx-checkbox").ClassList, Does.Contain("c"));
        Assert.That(cut.Find(".nx-checkbox").GetAttribute("style"), Is.EqualTo("margin: 2px"));
    }

    [Test]
    public void UnsupportedType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxCheckBox<int>>());
    }

    [Test]
    public void ThreeState_OnNonNullable_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxCheckBox<bool>>(p => p.Add(x => x.ThreeState, true)));
    }

    // ── Toggling ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Click_TogglesBool()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised, false);

        await ClickAsync(cut);
        await ClickAsync(cut);

        Assert.That(raised, Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public async Task Nullable_TwoState_IndeterminateGoesToChecked_ThenToggles()
    {
        var raised = new List<bool?>();
        var cut = RenderBound<bool?>(raised, null);

        await ClickAsync(cut);
        await ClickAsync(cut);
        await ClickAsync(cut);

        Assert.That(raised, Is.EqualTo(new bool?[] { true, false, true }));
    }

    [Test]
    public async Task Nullable_ThreeState_CyclesThroughIndeterminate()
    {
        var raised = new List<bool?>();
        var cut = RenderBound<bool?>(raised, false, p => p.Add(x => x.ThreeState, true));

        await ClickAsync(cut);
        await ClickAsync(cut);
        await ClickAsync(cut);

        Assert.That(raised, Is.EqualTo(new bool?[] { true, null, false }));
    }

    [Test]
    public async Task Disabled_NativeDisabled_NoToggle()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised, false, p => p.Add(x => x.Disabled, true));

        await ClickAsync(cut);

        Assert.That(cut.Find("button").HasAttribute("disabled"), Is.True);
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task ReadOnly_FocusableAriaReadonly_NoToggle()
    {
        var raised = new List<bool>();
        var cut = RenderBound(raised, true, p => p.Add(x => x.ReadOnly, true));

        await ClickAsync(cut);

        var box = cut.Find("button");
        Assert.That(box.HasAttribute("disabled"), Is.False);
        Assert.That(box.GetAttribute("aria-readonly"), Is.EqualTo("true"));
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public void Keyboard_IsANativeButton_SpaceActivatesInTheBrowser()
    {
        // Space (and Enter) activation of a <button> is browser behavior; covered end to end in Nx.Playwright.
        var cut = Render<NxCheckBox<bool>>();

        Assert.That(cut.Find(".nx-checkbox-box").TagName, Is.EqualTo("BUTTON"));
        Assert.That(cut.Find(".nx-checkbox-box").HasAttribute("tabindex"), Is.False);
    }

    [Test]
    public async Task FocusAsync_FocusesBox()
    {
        var cut = Render<NxCheckBox<bool>>();

        await cut.InvokeAsync(() => cut.Instance.FocusAsync().AsTask());

        JSInterop.VerifyFocusAsyncInvoke();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private class Model
    {
        public bool Accepted { get; set; }
    }

    [Test]
    public async Task Validation_ToggleMarksModified_MessagesMarkInvalid()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxCheckBox<bool>>(c => c
                .Add(x => x.Value, model.Accepted)
                .Add(x => x.ValueChanged, (bool v) => model.Accepted = v)
                .Add(x => x.ValueExpression, () => model.Accepted)));
        var cut = host.FindComponent<NxCheckBox<bool>>();

        await ClickAsync(cut);
        Assert.That(context.IsModified(context.Field(nameof(Model.Accepted))), Is.True);

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.Accepted)), "Accept the terms.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("button").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-checkbox").ClassList, Does.Contain("nx-checkbox-invalid"));
    }

    [Test]
    public async Task Validation_MessagesCleared_NoLongerInvalid()
    {
        var model = new Model();
        var context = new EditContext(model);
        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.Accepted)), "Accept the terms.");
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxCheckBox<bool>>(c => c
                .Add(x => x.Value, model.Accepted)
                .Add(x => x.ValueExpression, () => model.Accepted)));
        var cut = host.FindComponent<NxCheckBox<bool>>();
        Assert.That(cut.Find("button").GetAttribute("aria-invalid"), Is.EqualTo("true"));

        store.Clear();
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("button").HasAttribute("aria-invalid"), Is.False);
        Assert.That(cut.Find(".nx-checkbox").ClassList, Does.Not.Contain("nx-checkbox-invalid"));
    }

    [Test]
    public void ExternalValueChange_UpdatesTheBox()
    {
        var cut = Render<NxCheckBox<bool?>>(p => p.Add(x => x.Value, (bool?)true));

        cut.Render(p => p.Add(x => x.Value, (bool?)null));

        Assert.That(cut.Find("button").GetAttribute("aria-checked"), Is.EqualTo("mixed"));
    }
}
