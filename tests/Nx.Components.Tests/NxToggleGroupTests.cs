using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxToggleGroupTests : BunitContext
{
    public enum View { List, Board, Calendar, Map }

    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-input.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxToggleGroup<View>> RenderSingle(List<View> raised, View initial,
        Action<ComponentParameterCollectionBuilder<NxToggleGroup<View>>>? configure = null)
    {
        IRenderedComponent<NxToggleGroup<View>>? cut = null;
        cut = Render<NxToggleGroup<View>>(p =>
        {
            p.Add(x => x.Data, Enum.GetValues<View>());
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (View v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private IRenderedComponent<NxToggleGroup<View>> RenderMulti(List<List<View>> raised, IEnumerable<View> initial,
        Action<ComponentParameterCollectionBuilder<NxToggleGroup<View>>>? configure = null)
    {
        IRenderedComponent<NxToggleGroup<View>>? cut = null;
        cut = Render<NxToggleGroup<View>>(p =>
        {
            p.Add(x => x.Data, Enum.GetValues<View>());
            p.Add(x => x.Multiple, true);
            p.Add(x => x.Values, initial);
            p.Add(x => x.ValuesChanged, (IEnumerable<View> v) =>
            {
                raised.Add(v.ToList());
                cut?.Render(q => q.Add(x => x.Values, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static Task ClickAsync(IRenderedComponent<NxToggleGroup<View>> cut, int index) =>
        cut.FindAll(".nx-toggle")[index].ClickAsync(new MouseEventArgs());

    /// <summary>What the browser reports when nx-input.js (or Tab, or a click) moves focus to an item.</summary>
    private static Task FocusItemAsync(IRenderedComponent<NxToggleGroup<View>> cut, int index) =>
        cut.FindAll(".nx-toggle")[index].FocusAsync(new FocusEventArgs());

    private static List<string?> TabIndexes(IRenderedComponent<NxToggleGroup<View>> cut) =>
        cut.FindAll(".nx-toggle").Select(t => t.GetAttribute("tabindex")).ToList();

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Single_RadiogroupOfRadios()
    {
        var cut = RenderSingle([], View.Board, p => p.Add(x => x.AriaLabel, "View"));

        var group = cut.Find(".nx-togglegroup");
        Assert.That(group.GetAttribute("role"), Is.EqualTo("radiogroup"));
        Assert.That(group.GetAttribute("aria-label"), Is.EqualTo("View"));
        var items = cut.FindAll(".nx-toggle");
        Assert.That(items.Select(i => i.TextContent.Trim()), Is.EqualTo(new[] { "List", "Board", "Calendar", "Map" }));
        Assert.That(items.All(i => i.GetAttribute("role") == "radio"), Is.True);
        Assert.That(items.Select(i => i.GetAttribute("aria-checked")), Is.EqualTo(new[] { "false", "true", "false", "false" }));
        Assert.That(items[1].ClassList, Does.Contain("nx-toggle-selected"));
    }

    [Test]
    public void Multiple_GroupOfPressedButtons()
    {
        var cut = RenderMulti([], [View.List, View.Map]);

        Assert.That(cut.Find(".nx-togglegroup").GetAttribute("role"), Is.EqualTo("group"));
        var items = cut.FindAll(".nx-toggle");
        Assert.That(items.All(i => !i.HasAttribute("role")), Is.True);
        Assert.That(items.Select(i => i.GetAttribute("aria-pressed")), Is.EqualTo(new[] { "true", "false", "false", "true" }));
    }

    [Test]
    public void Declarative_ItemsWithIcons_IconOnlyNeedsAriaLabel()
    {
        var cut = Render<NxToggleGroup<string>>(p => p
            .Add(x => x.Value, "b")
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxToggleGroupItem<string>>(0);
                b.AddAttribute(1, "Value", "a");
                b.AddAttribute(2, "Label", "Bold");
                b.AddAttribute(3, "Icon", (RenderFragment)(i => i.AddMarkupContent(0, "<svg class='b'></svg>")));
                b.CloseComponent();
                b.OpenComponent<NxToggleGroupItem<string>>(4);
                b.AddAttribute(5, "Value", "b");
                b.AddAttribute(6, "Icon", (RenderFragment)(i => i.AddMarkupContent(0, "<svg class='i'></svg>")));
                b.AddAttribute(7, "AriaLabel", "Italic");
                b.CloseComponent();
            }));

        var items = cut.FindAll(".nx-toggle");
        Assert.That(items[0].QuerySelector(".nx-toggle-icon svg.b"), Is.Not.Null);
        Assert.That(items[0].QuerySelector(".nx-toggle-icon")!.GetAttribute("aria-hidden"), Is.EqualTo("true"));
        Assert.That(items[1].ClassList, Does.Contain("nx-toggle-icon-only"));
        Assert.That(items[1].GetAttribute("aria-label"), Is.EqualTo("Italic"));
        Assert.That(items[1].GetAttribute("aria-checked"), Is.EqualTo("true"));
    }

    [Test]
    public void ItemTextAndTemplate()
    {
        var cut = RenderSingle([], View.List, p => p.Add(x => x.ItemText, v => v.ToString().ToUpperInvariant()));
        Assert.That(cut.FindAll(".nx-toggle")[0].TextContent.Trim(), Is.EqualTo("LIST"));

        var templated = RenderSingle([], View.List, p => p.Add(x => x.ItemTemplate, v => b => b.AddMarkupContent(0, $"<em>{v}</em>")));
        Assert.That(templated.FindAll(".nx-toggle em")[2].TextContent, Is.EqualTo("Calendar"));
    }

    [TestCase(NxSize.Small, "nx-togglegroup-sm")]
    [TestCase(NxSize.Large, "nx-togglegroup-lg")]
    public void Size_AppliesClass(NxSize size, string expected)
    {
        var cut = RenderSingle([], View.List, p => p.Add(x => x.Size, size));

        Assert.That(cut.Find(".nx-togglegroup").ClassList, Does.Contain(expected));
    }

    [Test]
    public void FullWidth_AppliesClass()
    {
        var cut = RenderSingle([], View.List, p => p.Add(x => x.FullWidth, true));

        Assert.That(cut.Find(".nx-togglegroup").ClassList, Does.Contain("nx-togglegroup-full-width"));
    }

    [Test]
    public void Item_OutsideGroup_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxToggleGroupItem<string>>(p => p.Add(x => x.Value, "a")));
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Single_ClickSelects_ClickingSelectedKeepsIt()
    {
        var raised = new List<View>();
        var cut = RenderSingle(raised, View.List);

        await ClickAsync(cut, 2);
        await ClickAsync(cut, 2);

        Assert.That(raised, Is.EqualTo(new[] { View.Calendar }));
        Assert.That(cut.FindAll(".nx-toggle")[2].GetAttribute("aria-checked"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Single_AllowDeselect_ClearsSelection()
    {
        var raised = new List<View?>();
        IRenderedComponent<NxToggleGroup<View?>>? cut = null;
        cut = Render<NxToggleGroup<View?>>(p => p
            .Add(x => x.Data, new View?[] { View.List, View.Map })
            .Add(x => x.Value, View.Map)
            .Add(x => x.AllowDeselect, true)
            .Add(x => x.ValueChanged, (View? v) => { raised.Add(v); cut?.Render(q => q.Add(x => x.Value, v)); }));

        await cut.FindAll(".nx-toggle")[1].ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new View?[] { null }));
        Assert.That(cut.FindAll(".nx-toggle-selected"), Is.Empty);
    }

    [Test]
    public async Task Multiple_ClickToggles()
    {
        var raised = new List<List<View>>();
        var cut = RenderMulti(raised, [View.List]);

        await ClickAsync(cut, 2);
        await ClickAsync(cut, 0);

        Assert.That(raised[0], Is.EqualTo(new[] { View.List, View.Calendar }));
        Assert.That(raised[1], Is.EqualTo(new[] { View.Calendar }));
    }

    [Test]
    public async Task DisabledItem_InertAndNeverTheTabStop()
    {
        var raised = new List<View>();
        var cut = RenderSingle(raised, View.Board, p => p.Add(x => x.ItemDisabled, v => v == View.Board));

        Assert.That(cut.FindAll(".nx-toggle")[1].HasAttribute("disabled"), Is.True);
        await ClickAsync(cut, 1);

        Assert.That(raised, Is.Empty);
        Assert.That(TabIndexes(cut), Is.EqualTo(new[] { "0", "-1", "-1", "-1" }), "a disabled selection must not hold the tab stop");
    }

    [Test]
    public void GroupDisabled_DisablesEveryItem()
    {
        var cut = RenderSingle([], View.List, p => p.Add(x => x.Disabled, true));

        Assert.That(cut.FindAll(".nx-toggle").All(t => t.HasAttribute("disabled")), Is.True);
        Assert.That(cut.Find(".nx-togglegroup").GetAttribute("aria-disabled"), Is.EqualTo("true"));
    }

    // ── Keyboard: roving tabindex ─────────────────────────────────────────────

    [Test]
    public void TabStop_IsTheSelectedItem()
    {
        var cut = RenderSingle([], View.Calendar);

        Assert.That(TabIndexes(cut), Is.EqualTo(new[] { "-1", "-1", "0", "-1" }));
    }

    [Test]
    public void TabStop_WithNoSelection_IsFirstEnabled()
    {
        var cut = RenderMulti([], [], p => p.Add(x => x.ItemDisabled, v => v == View.List));

        Assert.That(TabIndexes(cut), Is.EqualTo(new[] { "-1", "0", "-1", "-1" }));
    }

    [Test]
    public async Task TabStop_FollowsFocus_WithoutChangingSelection()
    {
        var raised = new List<View>();
        var cut = RenderSingle(raised, View.List);

        await FocusItemAsync(cut, 2);

        Assert.That(TabIndexes(cut), Is.EqualTo(new[] { "-1", "-1", "0", "-1" }));
        Assert.That(raised, Is.Empty, "focus alone does not select; the arrow keys select by clicking (nx-input.js)");
    }

    [Test]
    public async Task Multiple_FocusMovesTabStop_ValuesUnchanged()
    {
        var raised = new List<List<View>>();
        var cut = RenderMulti(raised, [View.List]);

        await FocusItemAsync(cut, 3);

        Assert.That(TabIndexes(cut), Is.EqualTo(new[] { "-1", "-1", "-1", "0" }));
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task Single_ClickAfterArrow_SelectsAndKeepsTabStopThere()
    {
        var raised = new List<View>();
        var cut = RenderSingle(raised, View.List);

        // nx-input.js on ArrowRight: focus the next item, then click it.
        await FocusItemAsync(cut, 1);
        await ClickAsync(cut, 1);

        Assert.That(raised, Is.EqualTo(new[] { View.Board }));
        Assert.That(TabIndexes(cut), Is.EqualTo(new[] { "-1", "0", "-1", "-1" }));
    }

    [Test]
    public void Keyboard_RovingFocusRunsInTheBrowser()
    {
        RenderSingle([], View.List);

        var call = module.Invocations.Single(i => i.Identifier == "roving");
        Assert.That(call.Arguments[1], Is.EqualTo(".nx-toggle"));
    }

    [Test]
    public void Keyboard_ContainerRole_TellsJsWhetherArrowsSelect()
    {
        Assert.That(RenderSingle([], View.List).Find(".nx-togglegroup").GetAttribute("role"), Is.EqualTo("radiogroup"));
        Assert.That(RenderMulti([], []).Find(".nx-togglegroup").GetAttribute("role"), Is.EqualTo("group"));
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private class Model
    {
        public View? View { get; set; }
        public IEnumerable<View> Views { get; set; } = new List<View>();
    }

    [Test]
    public async Task Validation_Single()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxToggleGroup<View?>>(c => c
                .Add(x => x.Data, new View?[] { View.List, View.Map })
                .Add(x => x.Value, model.View)
                .Add(x => x.ValueChanged, (View? v) => model.View = v)
                .Add(x => x.ValueExpression, () => model.View)));
        var cut = host.FindComponent<NxToggleGroup<View?>>();

        await cut.FindAll(".nx-toggle")[0].ClickAsync(new MouseEventArgs());
        Assert.That(context.IsModified(context.Field(nameof(Model.View))), Is.True);

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(Model.View)), "Pick a view.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);
        Assert.That(cut.Find(".nx-togglegroup").GetAttribute("aria-invalid"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Validation_Multiple_OnCollection()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxToggleGroup<View>>(c => c
                .Add(x => x.Data, Enum.GetValues<View>())
                .Add(x => x.Multiple, true)
                .Add(x => x.Values, model.Views)
                .Add(x => x.ValuesChanged, (IEnumerable<View> v) => model.Views = v)
                .Add(x => x.ValuesExpression, () => model.Views)));
        var cut = host.FindComponent<NxToggleGroup<View>>();

        await cut.FindAll(".nx-toggle")[1].ClickAsync(new MouseEventArgs());

        Assert.That(context.IsModified(context.Field(nameof(Model.Views))), Is.True);
        Assert.That(model.Views, Is.EqualTo(new[] { View.Board }));
    }
}
