using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxRadioGroupTests : BunitContext
{
    public enum Shift { Day, Swing, Night }

    [SetUp]
    public void SetUp() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<NxRadioGroup<Shift>> RenderData(List<Shift> raised, Shift initial,
        Action<ComponentParameterCollectionBuilder<NxRadioGroup<Shift>>>? configure = null)
    {
        IRenderedComponent<NxRadioGroup<Shift>>? cut = null;
        cut = Render<NxRadioGroup<Shift>>(p =>
        {
            p.Add(x => x.Data, Enum.GetValues<Shift>());
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (Shift v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static RenderFragment Declared(params (string Value, string Label, bool Disabled)[] items) => b =>
    {
        var seq = 0;
        foreach (var (value, label, disabled) in items)
        {
            b.OpenComponent<NxRadioButton<string>>(seq++);
            b.AddAttribute(seq++, nameof(NxRadioButton<string>.Value), value);
            b.AddAttribute(seq++, nameof(NxRadioButton<string>.Label), label);
            b.AddAttribute(seq++, nameof(NxRadioButton<string>.Disabled), disabled);
            b.CloseComponent();
        }
    };

    private static List<bool> CheckedStates<T>(IRenderedComponent<NxRadioGroup<T>> cut) =>
        cut.FindAll("input[type=radio]").Select(i => i.HasAttribute("checked")).ToList();

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Data_RendersOneNativeRadioPerItem_SharingAName()
    {
        var cut = RenderData([], Shift.Swing);

        var radios = cut.FindAll("input[type=radio]");
        Assert.That(radios, Has.Count.EqualTo(3));
        Assert.That(radios.Select(r => r.GetAttribute("name")).Distinct().Count(), Is.EqualTo(1));
        Assert.That(cut.FindAll(".nx-radio-label").Select(l => l.TextContent), Is.EqualTo(new[] { "Day", "Swing", "Night" }));
        Assert.That(CheckedStates(cut), Is.EqualTo(new[] { false, true, false }));
    }

    [Test]
    public void Labels_PointAtTheirInputs()
    {
        var cut = RenderData([], Shift.Day);

        foreach (var radio in cut.FindAll(".nx-radio"))
            Assert.That(radio.QuerySelector("label")!.GetAttribute("for"), Is.EqualTo(radio.QuerySelector("input")!.Id));
    }

    [Test]
    public void ItemText_AndItemTemplate()
    {
        var cut = RenderData([], Shift.Day, p => p.Add(x => x.ItemText, s => s + " shift"));
        Assert.That(cut.FindAll(".nx-radio-label")[2].TextContent, Is.EqualTo("Night shift"));

        var templated = RenderData([], Shift.Day, p => p.Add(x => x.ItemTemplate, s => b => b.AddMarkupContent(0, $"<b>{s}</b>")));
        Assert.That(templated.FindAll(".nx-radio-label b")[1].TextContent, Is.EqualTo("Swing"));
    }

    [Test]
    public void Declarative_Items()
    {
        var cut = Render<NxRadioGroup<string>>(p => p
            .Add(x => x.Value, "b")
            .Add(x => x.ChildContent, Declared(("a", "Alpha", false), ("b", "Beta", false))));

        Assert.That(cut.FindAll(".nx-radio-label").Select(l => l.TextContent), Is.EqualTo(new[] { "Alpha", "Beta" }));
        Assert.That(CheckedStates(cut), Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public void Group_HasRadiogroupRole_LabelledByCaption()
    {
        var cut = RenderData([], Shift.Day, p => p.Add(x => x.Label, "Shift"));

        var group = cut.Find("[role=radiogroup]");
        var caption = cut.Find(".nx-radiogroup-label");
        Assert.That(caption.TextContent, Is.EqualTo("Shift"));
        Assert.That(group.GetAttribute("aria-labelledby"), Is.EqualTo(caption.Id));
        Assert.That(group.GetAttribute("aria-orientation"), Is.EqualTo("vertical"));
    }

    [Test]
    public void Orientation_Horizontal()
    {
        var cut = RenderData([], Shift.Day, p => p.Add(x => x.Orientation, NxOrientation.Horizontal));

        Assert.That(cut.Find(".nx-radiogroup").ClassList, Does.Contain("nx-radiogroup-horizontal"));
        Assert.That(cut.Find("[role=radiogroup]").GetAttribute("aria-orientation"), Is.EqualTo("horizontal"));
    }

    [Test]
    public void Name_Overrides()
    {
        var cut = RenderData([], Shift.Day, p => p.Add(x => x.Name, "shift"));

        Assert.That(cut.FindAll("input").All(i => i.GetAttribute("name") == "shift"), Is.True);
    }

    [Test]
    public void RadioButton_OutsideGroup_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxRadioButton<string>>(p => p.Add(x => x.Value, "a")));
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Change_ChecksItem_AndUpdatesEveryRadio()
    {
        var raised = new List<Shift>();
        var cut = RenderData(raised, Shift.Day);

        await cut.FindAll("input")[2].ChangeAsync(new ChangeEventArgs { Value = "on" });

        Assert.That(raised, Is.EqualTo(new[] { Shift.Night }));
        Assert.That(CheckedStates(cut), Is.EqualTo(new[] { false, false, true }));
        Assert.That(cut.FindAll(".nx-radio")[2].ClassList, Does.Contain("nx-radio-checked"));
    }

    [Test]
    public void ExternalValueChange_MovesTheCheck_EvenForDeclaredItems()
    {
        var cut = Render<NxRadioGroup<string>>(p => p
            .Add(x => x.Value, "a")
            .Add(x => x.ChildContent, Declared(("a", "Alpha", false), ("b", "Beta", false))));

        cut.Render(p => p.Add(x => x.Value, "b"));

        Assert.That(CheckedStates(cut), Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public void DisabledItems_FromDataAndDeclared()
    {
        var cut = RenderData([], Shift.Day, p => p.Add(x => x.ItemDisabled, s => s == Shift.Night));
        Assert.That(cut.FindAll("input")[2].HasAttribute("disabled"), Is.True);
        Assert.That(cut.FindAll(".nx-radio")[2].ClassList, Does.Contain("nx-radio-disabled"));

        var declared = Render<NxRadioGroup<string>>(p => p
            .Add(x => x.ChildContent, Declared(("a", "Alpha", false), ("b", "Beta", true))));
        Assert.That(declared.FindAll("input").Select(i => i.HasAttribute("disabled")), Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public async Task GroupDisabled_DisablesAll_IgnoresChange()
    {
        var raised = new List<Shift>();
        var cut = RenderData(raised, Shift.Day, p => p.Add(x => x.Disabled, true));

        Assert.That(cut.FindAll("input").All(i => i.HasAttribute("disabled")), Is.True);
        await cut.FindAll("input")[1].ChangeAsync(new ChangeEventArgs { Value = "on" });
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public void Keyboard_NativeRadios_GiveTheRovingTabStop()
    {
        // Tab enters at the checked radio and arrows move-and-check: browser behavior for one name group,
        // covered end to end in Nx.Playwright. Here: they are real radios with no tabindex of their own.
        var cut = RenderData([], Shift.Swing);

        Assert.That(cut.FindAll("input").All(i => i.GetAttribute("type") == "radio" && !i.HasAttribute("tabindex")), Is.True);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private class Model
    {
        public Shift? Shift { get; set; }
    }

    [Test]
    public async Task Validation_ChangeMarksModified_MessagesMarkInvalid()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxRadioGroup<Shift?>>(c => c
                .Add(x => x.Data, new Shift?[] { Shift.Day, Shift.Night })
                .Add(x => x.Value, model.Shift)
                .Add(x => x.ValueChanged, (Shift? v) => model.Shift = v)
                .Add(x => x.ValueExpression, () => model.Shift)));
        var cut = host.FindComponent<NxRadioGroup<Shift?>>();

        await cut.FindAll("input")[0].ChangeAsync(new ChangeEventArgs { Value = "on" });
        Assert.That(context.IsModified(context.Field(nameof(Model.Shift))), Is.True);

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.Shift)), "Pick a shift.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("[role=radiogroup]").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.FindAll(".nx-radio-invalid"), Has.Count.EqualTo(2));
    }
}
