using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxColorPickerTests : BunitContext
{
    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-colorpicker.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxColorPicker> RenderBound(List<string?> raised, string? initial = null,
        Action<ComponentParameterCollectionBuilder<NxColorPicker>>? configure = null)
    {
        IRenderedComponent<NxColorPicker>? cut = null;
        cut = Render<NxColorPicker>(p =>
        {
            p.Add(x => x.Id, "cp");
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

    private static Task OpenAsync(IRenderedComponent<NxColorPicker> cut) => cut.Find("#cp").ClickAsync(new MouseEventArgs());

    [Test]
    public void Render_ButtonWithSwatchAndName()
    {
        var cut = RenderBound([], "#4371c4", p => p.Add(x => x.AriaLabel, "Crew color"));

        var button = cut.Find("button#cp");
        Assert.That(button.GetAttribute("aria-haspopup"), Is.EqualTo("listbox"));
        Assert.That(button.GetAttribute("aria-expanded"), Is.EqualTo("false"));
        Assert.That(button.GetAttribute("aria-label"), Is.EqualTo("Crew color: #4371c4"));
        Assert.That(cut.Find(".nx-colorpicker-swatch").GetAttribute("style"), Is.EqualTo("background-color: #4371c4"));
        Assert.That(cut.FindAll(".nx-colorpicker-popup"), Is.Empty);
    }

    [Test]
    public void NoValue_StruckOutSwatch()
    {
        var cut = RenderBound([]);
        Assert.That(cut.Find(".nx-colorpicker-swatch").ClassList, Does.Contain("nx-colorpicker-swatch-empty"));
        Assert.That(cut.Find("#cp").GetAttribute("aria-label"), Is.EqualTo("Color: none"));
    }

    [Test]
    public async Task Open_OfficePaletteByDefault_SelectedMarked_JsPlacesIt()
    {
        var cut = RenderBound([], "#ED7E32");
        await OpenAsync(cut);

        Assert.That(cut.Find("#cp").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        var cells = cut.FindAll(".nx-colorpicker-cell");
        Assert.That(cells, Has.Count.EqualTo(70));
        Assert.That(cut.Find(".nx-colorpicker-palette").GetAttribute("role"), Is.EqualTo("listbox"));
        Assert.That(cut.Find(".nx-colorpicker-palette").GetAttribute("style"), Is.EqualTo("--nx-colorpicker-columns: 10"));
        var selected = cut.Find(".nx-colorpicker-cell[aria-selected=true]");
        Assert.That(selected.GetAttribute("aria-label"), Is.EqualTo("#ed7e32"));
        Assert.That(selected.GetAttribute("tabindex"), Is.EqualTo("0"));
        Assert.That(cells.Count(c => c.GetAttribute("tabindex") == "0"), Is.EqualTo(1));
        Assert.That(module.Invocations.Any(i => i.Identifier == "open"), Is.True);
    }

    [Test]
    public async Task Pick_RaisesValueChanged_Closes()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#ffffff");
        await OpenAsync(cut);

        await cut.Find(".nx-colorpicker-cell[aria-label='#4371c4']").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { "#4371c4" }));
        Assert.That(cut.FindAll(".nx-colorpicker-popup"), Is.Empty);
        Assert.That(module.Invocations.Any(i => i.Identifier == "close"), Is.True);
    }

    [Test]
    public async Task PickSameColor_NoEvent()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#4371c4");
        await OpenAsync(cut);
        await cut.Find(".nx-colorpicker-cell[aria-label='#4371c4']").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task OwnColorsAndColumns()
    {
        var cut = RenderBound([], "rgb(46, 109, 180)", p => p.Add(x => x.Colors, ["#c0392b", "#2e6db4", "#3e9e4f"]).Add(x => x.Columns, 3));
        await OpenAsync(cut);

        Assert.That(cut.FindAll(".nx-colorpicker-cell").Select(c => c.GetAttribute("aria-label")), Is.EqualTo(new[] { "#c0392b", "#2e6db4", "#3e9e4f" }));
        Assert.That(cut.Find(".nx-colorpicker-palette").GetAttribute("style"), Is.EqualTo("--nx-colorpicker-columns: 3"));
        // An rgb() value matches its hex swatch.
        Assert.That(cut.Find(".nx-colorpicker-cell[aria-selected=true]").GetAttribute("aria-label"), Is.EqualTo("#2e6db4"));
    }

    [Test]
    public async Task Clearable_NoColorSetsNull()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#ffff00", p => p.Add(x => x.Clearable, true));
        await OpenAsync(cut);

        await cut.Find(".nx-colorpicker-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new string?[] { null }));
        await OpenAsync(cut);
        Assert.That(cut.Find(".nx-colorpicker-clear").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task ViewGradient_NoPalette_ApplyRaisesDraft()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#4371c4", p => p.Add(x => x.View, NxColorPickerView.Gradient));
        Assert.That(cut.Find("#cp").GetAttribute("aria-haspopup"), Is.EqualTo("dialog"));
        await OpenAsync(cut);

        Assert.That(cut.FindAll(".nx-colorpicker-palette"), Is.Empty);
        Assert.That(cut.FindAll(".nx-colorpicker-tabs"), Is.Empty);
        Assert.That(cut.Find("#cp").HasAttribute("aria-controls"), Is.False);

        // Dragging raises nothing; only Apply does.
        await cut.InvokeAsync(() => cut.FindComponent<NxColorGradient>().Instance.OnAreaPick(1, 1));
        Assert.That(raised, Is.Empty);

        await cut.Find("#cp-apply").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Has.Count.EqualTo(1));
        Assert.That(raised[0], Does.Match("^#[0-9a-f]{6}$").And.Not.EqualTo("#4371c4"));
        Assert.That(cut.FindAll(".nx-colorpicker-popup"), Is.Empty);
    }

    [Test]
    public async Task ViewBoth_TabsSwitch_SwatchValueOpensOnPalette()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#4371c4", p => p.Add(x => x.View, NxColorPickerView.Both));
        await OpenAsync(cut);

        Assert.That(cut.Find("#cp-tab-palette").GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(cut.FindAll(".nx-colorpicker-palette"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll(".nx-colorgradient"), Is.Empty);

        var opens = module.Invocations.Count(i => i.Identifier == "open");
        await cut.Find("#cp-tab-gradient").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("#cp-tab-gradient").GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(cut.FindAll(".nx-colorpicker-palette"), Is.Empty);
        Assert.That(cut.FindAll(".nx-colorgradient"), Has.Count.EqualTo(1));
        // The popup changed size, so it is placed again.
        Assert.That(module.Invocations.Count(i => i.Identifier == "open"), Is.EqualTo(opens + 1));

        await cut.Find("#cp-tab-palette").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-colorpicker-cell[aria-label='#ed7e32']").ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new[] { "#ed7e32" }));
    }

    [Test]
    public async Task ViewBoth_ColorOutsidePalette_OpensOnGradient()
    {
        var cut = RenderBound([], "#123456", p => p.Add(x => x.View, NxColorPickerView.Both));
        await OpenAsync(cut);
        Assert.That(cut.Find("#cp-tab-gradient").GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(cut.FindAll(".nx-colorgradient"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Escape_Closes_ToggleClickCloses()
    {
        var cut = RenderBound([], "#ffffff");
        await OpenAsync(cut);
        await cut.Find(".nx-colorpicker-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.That(cut.FindAll(".nx-colorpicker-popup"), Is.Empty);

        await OpenAsync(cut);
        await OpenAsync(cut);
        Assert.That(cut.FindAll(".nx-colorpicker-popup"), Is.Empty);
    }

    [Test]
    public async Task ViewGradient_EscapeDropsTheDraft_ReopenShowsTheValue()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "#4371c4", p => p.Add(x => x.View, NxColorPickerView.Gradient));
        await OpenAsync(cut);

        await cut.InvokeAsync(() => cut.FindComponent<NxColorGradient>().Instance.OnAreaPick(1, 1));
        Assert.That(cut.Find("#cp-gradient-hex").GetAttribute("value"), Is.Not.EqualTo("#4371c4"));
        await cut.Find(".nx-colorpicker-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        await OpenAsync(cut);

        Assert.That(raised, Is.Empty);
        Assert.That(cut.Find("#cp-gradient-hex").GetAttribute("value"), Is.EqualTo("#4371c4"));
    }

    [Test]
    public async Task ArrowUp_OnButton_Opens()
    {
        var cut = RenderBound([]);
        await cut.Find("#cp").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.That(cut.FindAll(".nx-colorpicker-popup"), Has.Count.EqualTo(1));
    }

    [Test]
    public void ExtraAttributes_GoToTheButton()
    {
        var cut = RenderBound([], null, p => p.AddUnmatched("name", "crew-color"));
        Assert.That(cut.Find("button#cp").GetAttribute("name"), Is.EqualTo("crew-color"));
    }

    [Test]
    public async Task ArrowDown_OnButton_Opens()
    {
        var cut = RenderBound([]);
        await cut.Find("#cp").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.That(cut.FindAll(".nx-colorpicker-popup"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OutsidePress_Closes()
    {
        var cut = RenderBound([]);
        await OpenAsync(cut);
        await cut.InvokeAsync(() => cut.Instance.OnOutsidePress());
        Assert.That(cut.FindAll(".nx-colorpicker-popup"), Is.Empty);
    }

    [Test]
    public async Task Disabled_DoesNotOpen()
    {
        var cut = RenderBound([], "#000000", p => p.Add(x => x.Disabled, true));
        Assert.That(cut.Find("#cp").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find(".nx-colorpicker").ClassList, Does.Contain("nx-colorpicker-disabled"));
        await cut.InvokeAsync(() => cut.Find("#cp").Click());
        Assert.That(cut.FindAll(".nx-colorpicker-popup"), Is.Empty);
    }

    [Test]
    public void ShowValue_PrintsColorOrPlaceholder()
    {
        var cut = RenderBound([], "#4371c4", p => p.Add(x => x.ShowValue, true));
        Assert.That(cut.Find(".nx-colorpicker-value").TextContent, Is.EqualTo("#4371c4"));

        var empty = RenderBound([], null, p => p.Add(x => x.ShowValue, true).Add(x => x.Placeholder, "Unset"));
        Assert.That(empty.Find(".nx-colorpicker-value").TextContent, Is.EqualTo("Unset"));
    }

    [TestCase("#FFF", "#ffffff")]
    [TestCase("#4371C4", "#4371c4")]
    [TestCase("#4371c4ff", "#4371c4")]
    [TestCase("rgb(67, 113, 196)", "#4371c4")]
    [TestCase("rgba(67,113,196,0.5)", "#4371c4")]
    [TestCase("red", null)]
    public void Normalize_HexAndRgb(string color, string? expected) =>
        Assert.That(NxColorPicker.Normalize(color), Is.EqualTo(expected));

    private sealed class Crew
    {
        public string? Color { get; set; }
    }

    [Test]
    public async Task Validation_InvalidState_AndNotifiesOnPick()
    {
        var crew = new Crew();
        var context = new EditContext(crew);
        var store = new ValidationMessageStore(context);
        var changed = new List<string>();
        context.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier.FieldName);
        var cut = Render<NxColorPicker>(p => p
            .AddCascadingValue(context)
            .Add(x => x.Id, "cp")
            .Add(x => x.Value, crew.Color)
            .Add(x => x.ValueChanged, v => crew.Color = v)
            .Add(x => x.ValueExpression, () => crew.Color));

        store.Add(context.Field(nameof(Crew.Color)), "Required");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);
        Assert.That(cut.Find("#cp").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-colorpicker").ClassList, Does.Contain("nx-colorpicker-invalid"));

        await OpenAsync(cut);
        await cut.Find(".nx-colorpicker-cell").ClickAsync(new MouseEventArgs());
        Assert.That(changed, Is.EqualTo(new[] { "Color" }));
    }
}
