using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxDropDownTests : BunitContext
{
    private sealed record Plant(int Id, string Name, string Region);

    private static readonly List<Plant> Plants =
    [
        new(1, "Albany", "North"),
        new(2, "Bethlehem", "South"),
        new(3, "Hartford", "North"),
        new(4, "Newburgh", "North"),
        new(5, "New Haven", "South"),
        new(6, "Norwich", "South"),
    ];

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-combobox.js").Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxDropDown<Plant, int?>> RenderBound(List<int?> raised, int? initial = null,
        Action<ComponentParameterCollectionBuilder<NxDropDown<Plant, int?>>>? configure = null)
    {
        IRenderedComponent<NxDropDown<Plant, int?>>? cut = null;
        cut = Render<NxDropDown<Plant, int?>>(p =>
        {
            p.Add(x => x.Id, "dd");
            p.Add(x => x.Data, Plants);
            p.Add(x => x.ItemText, i => i.Name);
            p.Add(x => x.ItemValue, i => i.Id);
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (int? v) =>
            {
                raised.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static Task KeyAsync<T>(IRenderedComponent<T> cut, string key, bool alt = false) where T : IComponent =>
        cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key, AltKey = alt });

    private static string? Active<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll(".nx-combobox-item-active").SingleOrDefault()?.TextContent.Trim();

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_ReadOnlyComboboxWithoutAutocomplete()
    {
        var cut = RenderBound([], 3);

        var input = cut.Find("input#dd");
        Assert.That(input.HasAttribute("readonly"), Is.True);
        Assert.That(input.GetAttribute("inputmode"), Is.EqualTo("none"));
        Assert.That(input.HasAttribute("aria-autocomplete"), Is.False);
        Assert.That(input.GetAttribute("role"), Is.EqualTo("combobox"));
        Assert.That(input.GetAttribute("value"), Is.EqualTo("Hartford"));
        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain("nx-dropdown").And.Contain("nx-combobox-pick"));
    }

    [Test]
    public void DefaultText_IsThePlaceholder()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.DefaultText, "Any plant"));

        Assert.That(cut.Find("input").GetAttribute("placeholder"), Is.EqualTo("Any plant"));
    }

    // ── Opening and picking ───────────────────────────────────────────────────

    [Test]
    public async Task FieldClick_TogglesTheList()
    {
        var cut = RenderBound([]);

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("false"));
    }

    [TestCase("Enter")]
    [TestCase(" ")]
    [TestCase("ArrowDown")]
    public async Task Keys_OpenAtTheSelection(string key)
    {
        var cut = RenderBound([], 4);

        await KeyAsync(cut, key);

        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(Active(cut), Is.EqualTo("Newburgh"));
    }

    [Test]
    public async Task OpenList_SpaceAndEnterPick()
    {
        var raised = new List<int?>();
        var cut = RenderBound(raised);

        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, " ");
        Assert.That(raised, Is.EqualTo(new int?[] { 2 }));
        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("false"));

        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "End");
        await KeyAsync(cut, "Enter");
        Assert.That(raised, Is.EqualTo(new int?[] { 2, 6 }));
    }

    [Test]
    public async Task HomeEnd_JumpWhenOpen()
    {
        var cut = RenderBound([], 3);

        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "End");
        Assert.That(Active(cut), Is.EqualTo("Norwich"));
        await KeyAsync(cut, "Home");
        Assert.That(Active(cut), Is.EqualTo("Albany"));
    }

    [Test]
    public async Task Escape_ClosesWithoutChange()
    {
        var raised = new List<int?>();
        var cut = RenderBound(raised, 1);

        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Escape");

        Assert.That(raised, Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Albany"));
    }

    // ── Type-ahead ────────────────────────────────────────────────────────────

    [Test]
    public async Task TypeAhead_Closed_SelectsAndCyclesSameLetter()
    {
        var raised = new List<int?>();
        var cut = RenderBound(raised);

        await KeyAsync(cut, "n");
        await KeyAsync(cut, "n");
        await KeyAsync(cut, "n");
        await KeyAsync(cut, "n");

        Assert.That(raised, Is.EqualTo(new int?[] { 4, 5, 6, 4 }));
        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("false"));
    }

    [Test]
    public async Task TypeAhead_Open_PrefixMovesHighlight_SpacePicks()
    {
        var raised = new List<int?>();
        var cut = RenderBound(raised);

        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "n");
        await KeyAsync(cut, "e");
        await KeyAsync(cut, "w");
        Assert.That(Active(cut), Is.EqualTo("Newburgh"));
        await KeyAsync(cut, " ");

        Assert.That(raised, Is.EqualTo(new int?[] { 4 }));
        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("false"));
    }

    [Test]
    public async Task TypeAhead_Open_NoMatchKeepsHighlight()
    {
        var cut = RenderBound([]);

        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "n");
        await KeyAsync(cut, "e");
        await KeyAsync(cut, "w");
        await KeyAsync(cut, "h");      // "newh" matches nothing

        Assert.That(Active(cut), Is.EqualTo("Newburgh"));
    }

    [Test]
    public async Task TypeAhead_EnterResetsThePrefix()
    {
        var cut = RenderBound([]);

        await KeyAsync(cut, "n");          // closed: Newburgh
        await KeyAsync(cut, "Enter");      // opens, and ends the prefix
        await KeyAsync(cut, "a");

        Assert.That(Active(cut), Is.EqualTo("Albany"));
    }

    // ── Default item ──────────────────────────────────────────────────────────

    [Test]
    public async Task DefaultItem_FirstRow_SelectedWhenNothingIs_PickingItClears()
    {
        var raised = new List<int?>();
        var cut = RenderBound(raised, 2, p => p.Add(x => x.DefaultText, "Any plant"));

        await KeyAsync(cut, "Enter");
        var first = cut.FindAll("[role=option]")[0];
        Assert.That(first.TextContent.Trim(), Is.EqualTo("Any plant"));
        Assert.That(first.GetAttribute("aria-selected"), Is.EqualTo("false"));
        Assert.That(first.ClassList, Does.Contain("nx-combobox-item-special"));

        await first.ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new int?[] { null }));
        await KeyAsync(cut, "Enter");
        Assert.That(cut.FindAll("[role=option]")[0].GetAttribute("aria-selected"), Is.EqualTo("true"));
        Assert.That(Active(cut), Is.EqualTo("Any plant"));
    }

    [Test]
    public async Task DefaultItem_SkippedByTypeAhead()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.DefaultText, "Any plant"));

        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "a");

        Assert.That(Active(cut), Is.EqualTo("Albany"));
    }

    // ── Engine features ───────────────────────────────────────────────────────

    [Test]
    public async Task Grouping_And_ItemTemplate()
    {
        var cut = RenderBound([], configure: p => p
            .Add(x => x.ItemGroup, i => i.Region)
            .Add(x => x.ItemTemplate, i => b => b.AddMarkupContent(0, $"<b>{i.Name}</b>")));

        await KeyAsync(cut, "Enter");

        Assert.That(cut.FindAll(".nx-combobox-group").Select(g => g.TextContent.Trim()), Is.EqualTo(new[] { "North", "South" }));
        Assert.That(cut.FindAll("[role=option] b"), Has.Count.EqualTo(6));
    }

    [Test]
    public async Task DataProvider_LoadsOnOpen_WithEmptyFilter()
    {
        var filters = new List<string>();
        var cut = Render<NxDropDown<Plant, int?>>(p => p
            .Add(x => x.DataProvider, r => { filters.Add(r.Filter); return Task.FromResult<IEnumerable<Plant>>(Plants); })
            .Add(x => x.ItemText, i => i.Name)
            .Add(x => x.ItemValue, i => i.Id));

        await KeyAsync(cut, "Enter");

        cut.WaitForState(() => cut.FindAll("[role=option]").Count == 6);
        Assert.That(filters, Is.EqualTo(new[] { "" }));
    }

    [Test]
    public async Task ReadOnly_DoesNotOpen()
    {
        var cut = RenderBound([], 1, p => p.Add(x => x.ReadOnly, true));

        await KeyAsync(cut, "Enter");
        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("false"));
    }

    [Test]
    public void SelectedItem_BindsToo()
    {
        var picked = new List<Plant?>();
        var cut = Render<NxDropDown<Plant, int?>>(p => p
            .Add(x => x.Data, Plants)
            .Add(x => x.ItemText, i => i.Name)
            .Add(x => x.ItemValue, i => i.Id)
            .Add(x => x.SelectedItem, Plants[4])
            .Add(x => x.SelectedItemChanged, (Plant? v) => picked.Add(v)));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("New Haven"));
    }

    [Test]
    public async Task Validation_ThroughTheEngine()
    {
        var model = new Holder();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxDropDown<Plant, int?>>(c => c
                .Add(x => x.Data, Plants)
                .Add(x => x.ItemText, i => i.Name)
                .Add(x => x.ItemValue, i => i.Id)
                .Add(x => x.Value, model.PlantId)
                .Add(x => x.ValueChanged, (int? v) => model.PlantId = v)
                .Add(x => x.ValueExpression, () => model.PlantId)));
        var cut = host.FindComponent<NxDropDown<Plant, int?>>();

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "b" });

        Assert.That(model.PlantId, Is.EqualTo(2));
        Assert.That(context.IsModified(context.Field(nameof(Holder.PlantId))), Is.True);
    }

    [Test]
    public void PlainComboBox_HasNoModes()
    {
        var cut = Render<NxComboBox<Plant, int?>>(p => p
            .Add(x => x.Data, Plants)
            .Add(x => x.ItemText, i => i.Name)
            .Add(x => x.ItemValue, i => i.Id));

        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.False);
        Assert.That(cut.Find("input").GetAttribute("aria-autocomplete"), Is.EqualTo("list"));
        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Not.Contain("nx-combobox-pick"));
    }

    private class Holder
    {
        public int? PlantId { get; set; }
    }

    public enum Shift { Day, Swing, Night }

    [Test]
    public void ValueTypeItems_ValueWinsOverTheUnsetSelectedItem()
    {
        // An unset SelectedItem of an enum is Day, its default; it used to select Day over Value.
        var cut = Render<NxDropDown<Shift, Shift>>(p => p.Add(x => x.Data, Enum.GetValues<Shift>()).Add(x => x.Value, Shift.Swing));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Swing"));
    }

    // ── Hardening: forwarded engine features ──────────────────────────────────

    [Test]
    public async Task Clearable_ClearButtonClears()
    {
        var raised = new List<int?>();
        var cut = RenderBound(raised, 3, p => p.Add(x => x.Clearable, true));

        await cut.Find(".nx-combobox-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new int?[] { null }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));
    }

    [Test]
    public async Task Disabled_DoesNotOpen()
    {
        var cut = RenderBound([], 1, p => p.Add(x => x.Disabled, true));

        await KeyAsync(cut, "Enter");
        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Find("input").HasAttribute("disabled"), Is.True);
        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("false"));
    }

    [Test]
    public async Task Columns_RenderGrid_PickShowsItemText()
    {
        var raised = new List<int?>();
        var cut = RenderBound(raised, configure: p => p.Add(x => x.ColumnDefinitions, new List<NxComboBoxColumnDefinition<Plant>>
        {
            new() { Field = i => i.Name, Header = "Plant" },
            new() { Field = i => i.Region, Header = "Region" },
        }));

        await KeyAsync(cut, "Enter");
        Assert.That(cut.Find(".nx-combobox-columns-header").Children.Take(2).Select(h => h.TextContent), Is.EqualTo(new[] { "Plant", "Region" }));
        Assert.That(cut.FindAll("[role=option]")[1].QuerySelectorAll(".nx-combobox-cell").Select(c => c.TextContent.Trim()),
            Is.EqualTo(new[] { "Bethlehem", "South" }));

        await cut.FindAll("[role=option]")[1].ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new int?[] { 2 }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Bethlehem"));
    }

    [Test]
    public async Task Virtualize_RendersAWindow_TypeAheadStillFinds()
    {
        var raised = new List<int?>();
        IRenderedComponent<NxDropDown<int, int?>>? cut = null;
        cut = Render<NxDropDown<int, int?>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 10_000))
            .Add(x => x.ItemText, i => $"P{i:00000}")
            .Add(x => x.ItemValue, i => i)
            .Add(x => x.ValueChanged, (int? v) => { raised.Add(v); cut!.Render(q => q.Add(x => x.Value, v)); }));

        await KeyAsync(cut, "Enter");
        var options = cut.FindAll("[role=option]");
        Assert.That(options.Count, Is.GreaterThan(0).And.LessThan(200));
        Assert.That(options[0].GetAttribute("aria-setsize"), Is.EqualTo("10000"));

        await KeyAsync(cut, "Escape");
        await KeyAsync(cut, "p");
        Assert.That(raised, Is.EqualTo(new int?[] { 1 }));
    }

    [Test]
    public async Task VirtualizeThreshold_AndItemHeight_ReachTheList()
    {
        var cut = RenderBound([], configure: p => p
            .Add(x => x.VirtualizeThreshold, 3)
            .Add(x => x.ItemHeight, 40f));

        await KeyAsync(cut, "Enter");

        Assert.That(cut.Find("[role=listbox]").ClassList, Does.Contain("nx-combobox-list-virtual"));
        Assert.That(cut.Find("[role=option]").GetAttribute("style"), Does.Contain("height: 40px"));
    }

    [Test]
    public void ItemByValue_ShowsTheTextBeforeTheListLoads()
    {
        var cut = Render<NxDropDown<Plant, int?>>(p => p
            .Add(x => x.DataProvider, _ => Task.FromResult<IEnumerable<Plant>>(Plants))
            .Add(x => x.ItemByValue, id => Task.FromResult(Plants.FirstOrDefault(i => i.Id == id)))
            .Add(x => x.ItemText, i => i.Name)
            .Add(x => x.ItemValue, i => i.Id)
            .Add(x => x.Value, 3));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Hartford"));
    }

    [Test]
    public async Task PhoneSheet_IsOptIn()
    {
        var cut = RenderBound([]);

        await cut.InvokeAsync(() => cut.FindComponent<NxComboBox<Plant, int?>>().Instance.OnViewportChanged(true));
        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-combobox-overlay"), Is.Empty);
        Assert.That(cut.FindAll(".nx-combobox-popup"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PhoneSheet_NoSearchBox_ListTakesKeys()
    {
        var raised = new List<int?>();
        var cut = RenderBound(raised, configure: p => p
            .Add(x => x.MobileMode, NxMobileMode.Always)
            .Add(x => x.MobileTitle, "Plant")
            .Add(x => x.DefaultText, "No plant"));

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-combobox-overlay-search"), Is.Empty);
        Assert.That(cut.Find(".nx-combobox-overlay-title").TextContent, Is.EqualTo("Plant"));
        var list = cut.Find(".nx-combobox-overlay-list");
        Assert.That(list.GetAttribute("aria-label"), Is.EqualTo("Plant"));
        Assert.That(cut.FindAll(".nx-combobox-overlay [role=option]")[0].TextContent.Trim(), Is.EqualTo("No plant"));

        await list.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await cut.Find(".nx-combobox-overlay-list").KeyDownAsync(new KeyboardEventArgs { Key = " " });

        Assert.That(raised, Is.EqualTo(new int?[] { 1 }));
        Assert.That(cut.FindAll(".nx-combobox-overlay"), Is.Empty);
    }

    [Test]
    public async Task EmptyList_SaysNoItems()
    {
        var cut = Render<NxDropDown<Plant, int?>>(p => p
            .Add(x => x.Data, new List<Plant>())
            .Add(x => x.ItemText, i => i.Name)
            .Add(x => x.ItemValue, i => i.Id));

        await KeyAsync(cut, "Enter");

        Assert.That(cut.Find(".nx-combobox-empty").TextContent.Trim(), Is.EqualTo("No items"));
    }
}
