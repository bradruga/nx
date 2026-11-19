using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxComboBoxTests : BunitContext
{
    private sealed record City(int Id, string Name, string State);

    private static readonly List<City> Cities =
    [
        new(1, "Albany",     "NY"),
        new(2, "Boston",     "MA"),
        new(3, "Burlington", "VT"),
        new(4, "Hartford",   "CT"),
        new(5, "Newark",     "NJ"),
        new(6, "New Haven",  "CT"),
        new(7, "Worcester",  "MA"),
    ];

    private class Model
    {
        public int? CityId { get; set; }
    }

    private BunitJSModuleInterop module = default!;

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule("./_content/Nx.Components/nx-combobox.js");
        module.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxComboBox<City, int?>> RenderCities(Action<ComponentParameterCollectionBuilder<NxComboBox<City, int?>>>? configure = null) =>
        Render<NxComboBox<City, int?>>(p =>
        {
            p.Add(x => x.Data, Cities);
            p.Add(x => x.ItemText, c => c.Name);
            p.Add(x => x.ItemValue, c => c.Id);
            configure?.Invoke(p);
        });

    /// <summary>
    /// Renders a combo whose callbacks write back into the parameters, like @bind-Value and @bind-SelectedItem.
    /// The lists record only what happens after the first render; the initial Value-to-SelectedItem sync is applied and dropped.
    /// </summary>
    private IRenderedComponent<NxComboBox<City, int?>> RenderBound(
        List<int?> values, List<City?> items,
        Action<ComponentParameterCollectionBuilder<NxComboBox<City, int?>>>? configure = null, int? initial = null)
    {
        IRenderedComponent<NxComboBox<City, int?>>? cut = null;
        cut = RenderCities(p =>
        {
            p.Add(x => x.Value, initial);
            p.Add(x => x.ValueChanged, (int? v) =>
            {
                values.Add(v);
                cut?.Render(q => q.Add(x => x.Value, v));
            });
            p.Add(x => x.SelectedItemChanged, (City? c) =>
            {
                items.Add(c);
                cut?.Render(q => q.Add(x => x.SelectedItem, c));
            });
            configure?.Invoke(p);
        });
        if (items.Count > 0)
            cut.Render(q => q.Add(x => x.SelectedItem, items[^1]));
        values.Clear();
        items.Clear();
        return cut;
    }

    private static Task KeyAsync<T1, T2>(IRenderedComponent<NxComboBox<T1, T2>> cut, string key, bool alt = false) =>
        cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key, AltKey = alt });

    private static Task TypeAsync<T1, T2>(IRenderedComponent<NxComboBox<T1, T2>> cut, string text) =>
        cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });

    private static Task BlurAsync<T1, T2>(IRenderedComponent<NxComboBox<T1, T2>> cut) =>
        cut.Find("input").BlurAsync(new FocusEventArgs());

    private static List<string> OptionTexts<T1, T2>(IRenderedComponent<NxComboBox<T1, T2>> cut) =>
        cut.FindAll("[role=option]").Select(o => o.TextContent.Trim()).ToList();

    private static string? ActiveText<T1, T2>(IRenderedComponent<NxComboBox<T1, T2>> cut)
    {
        var id = cut.Find("input").GetAttribute("aria-activedescendant");
        return id is null ? null : cut.Find($"#{id}").TextContent.Trim();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_Closed_InputIsCollapsedCombobox()
    {
        var cut = RenderCities(p => p.Add(x => x.Placeholder, "City"));

        var input = cut.Find("input");
        Assert.That(input.GetAttribute("role"), Is.EqualTo("combobox"));
        Assert.That(input.GetAttribute("aria-expanded"), Is.EqualTo("false"));
        Assert.That(input.GetAttribute("aria-autocomplete"), Is.EqualTo("list"));
        Assert.That(input.GetAttribute("aria-haspopup"), Is.EqualTo("listbox"));
        Assert.That(input.GetAttribute("autocomplete"), Is.EqualTo("off"));
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("City"));
        Assert.That(input.HasAttribute("aria-controls"), Is.False);
        Assert.That(cut.FindAll(".nx-combobox-popup"), Is.Empty);
        Assert.That(cut.FindAll(".nx-combobox-toggle"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Id_GeneratedWhenNotSet_OverriddenWhenSet()
    {
        Assert.That(RenderCities().Find("input").Id, Does.StartWith("nx-combobox-"));
        Assert.That(RenderCities(p => p.Add(x => x.Id, "city")).Find("input").Id, Is.EqualTo("city"));
    }

    [Test]
    public void ClassStyleAndAttributes_Applied()
    {
        var cut = RenderCities(p => p
            .Add(x => x.Class, "wide")
            .Add(x => x.Style, "width: 200px")
            .AddUnmatched("aria-label", "City")
            .AddUnmatched("name", "city"));

        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain("wide"));
        Assert.That(cut.Find(".nx-combobox").GetAttribute("style"), Is.EqualTo("width: 200px"));
        Assert.That(cut.Find("input").GetAttribute("aria-label"), Is.EqualTo("City"));
        Assert.That(cut.Find("input").GetAttribute("name"), Is.EqualTo("city"));
    }

    // ── Value and selected item ───────────────────────────────────────────────

    [Test]
    public void Value_ShowsMatchingItemText()
    {
        var cut = RenderCities(p => p.Add(x => x.Value, 4));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Hartford"));
    }

    [Test]
    public void SelectedItem_ShowsItemText()
    {
        var cut = RenderCities(p => p.Add(x => x.SelectedItem, Cities[6]));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Worcester"));
    }

    [Test]
    public void Value_Unknown_ShowsNothing()
    {
        var cut = RenderCities(p => p.Add(x => x.Value, 99));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));
    }

    [Test]
    public void Value_ChangedFromOutside_UpdatesText()
    {
        var cut = RenderCities(p => p.Add(x => x.Value, 1));

        cut.Render(p => p.Add(x => x.Value, 2));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Boston"));
    }

    [Test]
    public void Value_Initial_BringsBoundSelectedItemAlong()
    {
        var items = new List<City?>();
        RenderCities(p => p
            .Add(x => x.Value, 4)
            .Add(x => x.SelectedItemChanged, (City? c) => items.Add(c)));

        Assert.That(items, Is.EqualTo(new[] { Cities[3] }));
    }

    [Test]
    public void Value_SetFromOutside_BringsBoundSelectedItemAlong()
    {
        var items = new List<City?>();
        var cut = RenderCities(p => p
            .Add(x => x.Value, 1)
            .Add(x => x.SelectedItemChanged, (City? c) => items.Add(c)));

        cut.Render(p => p
            .Add(x => x.Value, 3)
            .Add(x => x.SelectedItem, Cities[0]));

        Assert.That(items, Is.EqualTo(new[] { Cities[0], Cities[2] }));
    }

    [Test]
    public void SelectedItem_SetFromOutside_BringsBoundValueAlong()
    {
        var values = new List<int?>();
        var cut = RenderCities(p => p
            .Add(x => x.ValueChanged, (int? v) => values.Add(v)));

        cut.Render(p => p.Add(x => x.SelectedItem, Cities[4]));

        Assert.That(values, Is.EqualTo(new int?[] { 5 }));
    }

    [Test]
    public void StringItems_NeedNoSelectors()
    {
        var cut = Render<NxComboBox<string, string>>(p => p
            .Add(x => x.Data, ["Red", "Green", "Blue"])
            .Add(x => x.Value, "Green"));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Green"));
    }

    [Test]
    public void MissingItemValue_ForDifferentTypes_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxComboBox<City, int>>(p => p
            .Add(x => x.Data, Cities)
            .Add(x => x.Value, 1)));
    }

    // ── Opening ───────────────────────────────────────────────────────────────

    [Test]
    public async Task ArrowDown_OpensFullListAsListbox()
    {
        var cut = RenderCities();

        await KeyAsync(cut, "ArrowDown");

        var input = cut.Find("input");
        Assert.That(input.GetAttribute("aria-expanded"), Is.EqualTo("true"));
        var list = cut.Find("[role=listbox]");
        Assert.That(input.GetAttribute("aria-controls"), Is.EqualTo(list.Id));
        Assert.That(OptionTexts(cut), Has.Count.EqualTo(7));
        Assert.That(cut.Instance.IsOpen, Is.True);
    }

    [Test]
    public async Task ToggleButton_OpensAndCloses()
    {
        var cut = RenderCities();

        await cut.Find(".nx-combobox-toggle").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Instance.IsOpen, Is.True);
        Assert.That(cut.Find(".nx-combobox-toggle").GetAttribute("aria-label"), Is.EqualTo("Hide options"));

        await cut.Find(".nx-combobox-toggle").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.Find(".nx-combobox-toggle").GetAttribute("aria-label"), Is.EqualTo("Show options"));
    }

    [Test]
    public async Task Typing_OpensFilteredList()
    {
        var cut = RenderCities();

        await TypeAsync(cut, "bur");

        Assert.That(cut.Instance.IsOpen, Is.True);
        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Burlington" }));
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task DisabledOrReadOnly_DoesNotOpen(bool disabled, bool readOnly)
    {
        var cut = RenderCities(p => p
            .Add(x => x.Disabled, disabled)
            .Add(x => x.ReadOnly, readOnly));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.Find("input").HasAttribute(disabled ? "disabled" : "readonly"), Is.True);
        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain(disabled ? "nx-combobox-disabled" : "nx-combobox-readonly"));
    }

    [Test]
    public async Task Open_PlacesPopupThroughJs_CloseReleasesIt()
    {
        var cut = RenderCities();

        await KeyAsync(cut, "ArrowDown");
        Assert.That(module.Invocations.Select(i => i.Identifier), Does.Contain("open"));

        await KeyAsync(cut, "Escape");
        Assert.That(module.Invocations.Select(i => i.Identifier), Does.Contain("close"));
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Filter_Contains_IgnoresCase()
    {
        var cut = RenderCities();

        await TypeAsync(cut, "OR");

        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Hartford", "Worcester" }));
    }

    [Test]
    public async Task Filter_StartsWith()
    {
        var cut = RenderCities(p => p.Add(x => x.Filter, NxComboBoxFilter.StartsWith));

        await TypeAsync(cut, "new");

        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Newark", "New Haven" }));
    }

    [Test]
    public async Task Filter_None_KeepsAllAndHighlightsFirstMatch()
    {
        var cut = RenderCities(p => p.Add(x => x.Filter, NxComboBoxFilter.None));

        await TypeAsync(cut, "har");

        Assert.That(OptionTexts(cut), Has.Count.EqualTo(7));
        Assert.That(ActiveText(cut), Is.EqualTo("Hartford"));
    }

    [Test]
    public async Task CustomFilter_ReplacesFilter()
    {
        var cut = RenderCities(p => p.Add(x => x.CustomFilter, (c, text) => c.State.Equals(text, StringComparison.OrdinalIgnoreCase)));

        await TypeAsync(cut, "ct");

        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Hartford", "New Haven" }));
    }

    [Test]
    public async Task Filter_HighlightsMatchedText()
    {
        var cut = RenderCities();

        await TypeAsync(cut, "ford");

        Assert.That(cut.Find("[role=option] mark.nx-combobox-match").TextContent, Is.EqualTo("ford"));
    }

    [Test]
    public async Task Filter_NoMatches_ShowsNoDataText()
    {
        var cut = RenderCities(p => p.Add(x => x.NoDataText, "Nothing here"));

        await TypeAsync(cut, "zzz");

        Assert.That(cut.FindAll("[role=option]"), Is.Empty);
        Assert.That(cut.Find(".nx-combobox-empty").TextContent.Trim(), Is.EqualTo("Nothing here"));
    }

    [Test]
    public async Task Typing_HighlightsFirstMatch()
    {
        var cut = RenderCities();

        await TypeAsync(cut, "o");

        Assert.That(ActiveText(cut), Is.EqualTo("Boston"));
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Keyboard_ArrowsMoveHighlightAndClamp()
    {
        var cut = RenderCities();

        await KeyAsync(cut, "ArrowDown");
        Assert.That(ActiveText(cut), Is.EqualTo("Albany"));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowDown");
        Assert.That(ActiveText(cut), Is.EqualTo("Burlington"));

        await KeyAsync(cut, "ArrowUp");
        Assert.That(ActiveText(cut), Is.EqualTo("Boston"));

        for (var i = 0; i < 5; i++)
            await KeyAsync(cut, "ArrowUp");
        Assert.That(ActiveText(cut), Is.EqualTo("Albany"));
    }

    [Test]
    public async Task Keyboard_PageDownAndUp_MoveByTen()
    {
        var cut = Render<NxComboBox<int, int>>(p => p.Add(x => x.Data, Enumerable.Range(1, 30)));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "PageDown");
        Assert.That(ActiveText(cut), Is.EqualTo("11"));

        await KeyAsync(cut, "PageUp");
        Assert.That(ActiveText(cut), Is.EqualTo("1"));
    }

    [Test]
    public async Task Keyboard_Enter_SelectsHighlightedAndCloses()
    {
        var values = new List<int?>();
        var items = new List<City?>();
        var cut = RenderBound(values, items);

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");

        Assert.That(values, Is.EqualTo(new int?[] { 2 }));
        Assert.That(items, Is.EqualTo(new[] { Cities[1] }));
        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Boston"));
    }

    [Test]
    public async Task Keyboard_Escape_ClosesAndRestoresSelection()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, [], initial: 1);

        await TypeAsync(cut, "Bos");
        await KeyAsync(cut, "Escape");

        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Albany"));
        Assert.That(values, Is.Empty);
    }

    [Test]
    public async Task Keyboard_EscapeWhileClosed_ClearsWhenClearable()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, [], p => p.Add(x => x.Clearable, true), initial: 1);

        await KeyAsync(cut, "Escape");

        Assert.That(values, Is.EqualTo(new int?[] { null }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));
    }

    [Test]
    public async Task Keyboard_AltArrowUp_Closes()
    {
        var cut = RenderCities();

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowUp", alt: true);

        Assert.That(cut.Instance.IsOpen, Is.False);
    }

    [Test]
    public async Task Keyboard_ReopenHighlightsSelectedItem()
    {
        var cut = RenderBound([], [], initial: 5);

        await KeyAsync(cut, "ArrowDown");

        Assert.That(ActiveText(cut), Is.EqualTo("Newark"));
    }

    [Test]
    public async Task Keyboard_ActiveDescendantPointsAtRenderedOption()
    {
        var cut = RenderCities();

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowDown");

        var id = cut.Find("input").GetAttribute("aria-activedescendant");
        var option = cut.Find($"#{id}");
        Assert.That(option.GetAttribute("role"), Is.EqualTo("option"));
        Assert.That(option.ClassList, Does.Contain("nx-combobox-item-active"));
    }

    // ── Mouse ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Click_Option_Selects()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, []);

        await KeyAsync(cut, "ArrowDown");
        await cut.FindAll("[role=option]")[3].ClickAsync(new MouseEventArgs());

        Assert.That(values, Is.EqualTo(new int?[] { 4 }));
        Assert.That(cut.Instance.IsOpen, Is.False);
    }

    [Test]
    public async Task SelectedOption_MarkedAriaSelectedWithCheck()
    {
        var cut = RenderBound([], [], initial: 2);

        await KeyAsync(cut, "ArrowDown");

        var selected = cut.FindAll("[role=option][aria-selected=true]");
        Assert.That(selected, Has.Count.EqualTo(1));
        Assert.That(selected[0].TextContent.Trim(), Is.EqualTo("Boston"));
        Assert.That(selected[0].QuerySelector(".nx-combobox-check"), Is.Not.Null);
        Assert.That(cut.FindAll("[role=option][aria-selected=false]"), Has.Count.EqualTo(6));
    }

    // ── Leaving the field ─────────────────────────────────────────────────────

    [Test]
    public async Task Blur_ExactText_SelectsThatItem()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, []);

        await TypeAsync(cut, "worcester");
        await BlurAsync(cut);

        Assert.That(values, Is.EqualTo(new int?[] { 7 }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Worcester"));
        Assert.That(cut.Instance.IsOpen, Is.False);
    }

    [Test]
    public async Task Blur_PartialText_SelectsHighlightedMatch()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, []);

        await TypeAsync(cut, "hart");
        await BlurAsync(cut);

        Assert.That(values, Is.EqualTo(new int?[] { 4 }));
    }

    [Test]
    public async Task Blur_NoMatch_RevertsToSelection()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, [], initial: 1);

        await TypeAsync(cut, "zzz");
        await BlurAsync(cut);

        Assert.That(values, Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Albany"));
    }

    [Test]
    public async Task Blur_EmptyText_Clears()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, [], initial: 1);

        await TypeAsync(cut, "");
        await BlurAsync(cut);

        Assert.That(values, Is.EqualTo(new int?[] { null }));
    }

    [Test]
    public async Task Blur_UntouchedField_RaisesNothing()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, [], initial: 1);

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await BlurAsync(cut);

        Assert.That(values, Is.Empty);
    }

    // ── Custom values ─────────────────────────────────────────────────────────

    [Test]
    public async Task AllowCustomValue_KeepsUnmatchedText()
    {
        var values = new List<string?>();
        IRenderedComponent<NxComboBox<string, string>>? cut = null;
        cut = Render<NxComboBox<string, string>>(p => p
            .Add(x => x.Data, ["Flatbed", "Rail"])
            .Add(x => x.AllowCustomValue, true)
            .Add(x => x.ValueChanged, (string? v) => { values.Add(v); cut!.Render(q => q.Add(x => x.Value, v)); }));

        await TypeAsync(cut, "Barge");
        await KeyAsync(cut, "Enter");

        Assert.That(values, Is.EqualTo(new[] { "Barge" }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Barge"));
    }

    [Test]
    public async Task AllowCustomValue_EnterAfterArrow_PicksItem()
    {
        var values = new List<string?>();
        var cut = Render<NxComboBox<string, string>>(p => p
            .Add(x => x.Data, ["Flatbed", "Rail"])
            .Add(x => x.AllowCustomValue, true)
            .Add(x => x.ValueChanged, (string? v) => values.Add(v)));

        await TypeAsync(cut, "a");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");

        Assert.That(values, Is.EqualTo(new[] { "Rail" }));
    }

    [Test]
    public void AllowCustomValue_InitialUnmatchedValueShown()
    {
        var cut = Render<NxComboBox<string, string>>(p => p
            .Add(x => x.Data, ["Flatbed", "Rail"])
            .Add(x => x.AllowCustomValue, true)
            .Add(x => x.Value, "Barge"));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Barge"));
    }

    [Test]
    public void AllowCustomValue_NonStringValue_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RenderCities(p => p.Add(x => x.AllowCustomValue, true)));
    }

    // ── Clear button ──────────────────────────────────────────────────────────

    [Test]
    public async Task Clear_VisibleWithSelection_ClickClears()
    {
        var values = new List<int?>();
        var items = new List<City?>();
        var cut = RenderBound(values, items, p => p.Add(x => x.Clearable, true), initial: 3);

        var clear = cut.Find(".nx-combobox-clear");
        Assert.That(clear.GetAttribute("tabindex"), Is.EqualTo("-1"));
        Assert.That(clear.GetAttribute("aria-label"), Is.EqualTo("Clear"));

        await clear.ClickAsync(new MouseEventArgs());

        Assert.That(values, Is.EqualTo(new int?[] { null }));
        Assert.That(items, Is.EqualTo(new City?[] { null }));
        Assert.That(cut.FindAll(".nx-combobox-clear"), Is.Empty);
    }

    [Test]
    public void Clear_HiddenWithoutSelectionOrWhenDisabled()
    {
        Assert.That(RenderCities(p => p.Add(x => x.Clearable, true)).FindAll(".nx-combobox-clear"), Is.Empty);
        Assert.That(RenderCities(p => p
            .Add(x => x.Clearable, true)
            .Add(x => x.Value, 1)
            .Add(x => x.Disabled, true)).FindAll(".nx-combobox-clear"), Is.Empty);
    }

    // ── Grouping ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Grouping_HeadersInFirstAppearanceOrder()
    {
        var cut = RenderCities(p => p.Add(x => x.ItemGroup, c => c.State));

        await KeyAsync(cut, "ArrowDown");

        var headers = cut.FindAll(".nx-combobox-group").Select(h => h.TextContent.Trim()).ToList();
        Assert.That(headers, Is.EqualTo(new[] { "NY", "MA", "VT", "CT", "NJ" }));
        Assert.That(cut.FindAll(".nx-combobox-group").All(h => h.GetAttribute("role") == "presentation"), Is.True);
        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Albany", "Boston", "Worcester", "Burlington", "Hartford", "New Haven", "Newark" }));
    }

    [Test]
    public async Task Grouping_ArrowKeysSkipHeaders()
    {
        var cut = RenderCities(p => p.Add(x => x.ItemGroup, c => c.State));

        await KeyAsync(cut, "ArrowDown");
        Assert.That(ActiveText(cut), Is.EqualTo("Albany"));

        await KeyAsync(cut, "ArrowDown");
        Assert.That(ActiveText(cut), Is.EqualTo("Boston"));
    }

    [Test]
    public async Task Grouping_PosInSetCountsItemsOnly()
    {
        var cut = RenderCities(p => p.Add(x => x.ItemGroup, c => c.State));

        await KeyAsync(cut, "ArrowDown");

        var options = cut.FindAll("[role=option]");
        Assert.That(options.Select(o => o.GetAttribute("aria-posinset")), Is.EqualTo(Enumerable.Range(1, 7).Select(i => i.ToString())));
        Assert.That(options.All(o => o.GetAttribute("aria-setsize") == "7"), Is.True);
    }

    [Test]
    public async Task GroupHeaderTemplate_RendersHeader()
    {
        var cut = RenderCities(p => p
            .Add(x => x.ItemGroup, c => c.State)
            .Add(x => x.GroupHeaderTemplate, state => b => b.AddMarkupContent(0, $"<b>State {state}</b>")));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.Find(".nx-combobox-group b").TextContent, Is.EqualTo("State NY"));
    }

    // ── Templates ─────────────────────────────────────────────────────────────

    [Test]
    public async Task ItemTemplate_RendersEachItem()
    {
        var cut = RenderCities(p => p.Add(x => x.ItemTemplate, c => b => b.AddMarkupContent(0, $"<i>{c.Name}, {c.State}</i>")));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.FindAll("[role=option] .nx-combobox-item-template i")[0].TextContent, Is.EqualTo("Albany, NY"));
    }

    [Test]
    public async Task SelectedItemTemplate_ShownUntilFocused()
    {
        var cut = RenderCities(p => p
            .Add(x => x.Value, 2)
            .Add(x => x.SelectedItemTemplate, c => b => b.AddMarkupContent(0, $"<em>{c.Name} ({c.State})</em>")));

        Assert.That(cut.Find(".nx-combobox-selected em").TextContent, Is.EqualTo("Boston (MA)"));
        Assert.That(cut.Find(".nx-combobox-selected").GetAttribute("aria-hidden"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain("nx-combobox-templated"));

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        Assert.That(cut.FindAll(".nx-combobox-selected"), Is.Empty);
    }

    // ── Data provider ─────────────────────────────────────────────────────────

    [Test]
    public async Task DataProvider_OpenLoadsUnfiltered()
    {
        var requests = new List<string>();
        var cut = Render<NxComboBox<City, int?>>(p => p
            .Add(x => x.DataProvider, r => { requests.Add(r.Filter); return Task.FromResult<IEnumerable<City>>(Cities); })
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(requests, Is.EqualTo(new[] { "" }));
        Assert.That(OptionTexts(cut), Has.Count.EqualTo(7));
    }

    // ── ItemByValue ───────────────────────────────────────────────────────────

    private IRenderedComponent<NxComboBox<City, int?>> RenderProvider(Action<ComponentParameterCollectionBuilder<NxComboBox<City, int?>>> configure) =>
        Render<NxComboBox<City, int?>>(p =>
        {
            p.Add(x => x.DataProvider, _ => Task.FromResult<IEnumerable<City>>(Cities));
            p.Add(x => x.ItemText, c => c.Name);
            p.Add(x => x.ItemValue, c => c.Id);
            configure(p);
        });

    [Test]
    public void ItemByValue_ShowsTheTextBeforeTheListLoads()
    {
        var asked = new List<int?>();
        var cut = RenderProvider(p => p
            .Add(x => x.Value, 2)
            .Add(x => x.ItemByValue, id => { asked.Add(id); return Task.FromResult(Cities.FirstOrDefault(c => c.Id == id)); }));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Boston"));
        Assert.That(asked, Is.EqualTo(new int?[] { 2 }));
    }

    [Test]
    public void ItemByValue_WithoutIt_TheFieldStaysEmpty()
    {
        var cut = RenderProvider(p => p.Add(x => x.Value, 2));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.Null.Or.Empty);
    }

    [Test]
    public async Task ItemByValue_WithoutIt_TheFieldFillsOnceTheListLoads()
    {
        var cut = RenderProvider(p => p.Add(x => x.Value, 2));

        await cut.Find(".nx-combobox-toggle").ClickAsync(new MouseEventArgs());
        cut.WaitForState(() => cut.Find("input").GetAttribute("value") == "Boston");
        await cut.Find(".nx-combobox-toggle").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Boston"));
    }

    [Test]
    public async Task ItemByValue_SlowAnswer_FillsTheFieldWhenItArrives()
    {
        var gate = new TaskCompletionSource<City?>();
        var cut = RenderProvider(p => p
            .Add(x => x.Value, 2)
            .Add(x => x.ItemByValue, _ => gate.Task));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.Null.Or.Empty);

        await cut.InvokeAsync(() => gate.SetResult(Cities[1]));

        cut.WaitForState(() => cut.Find("input").GetAttribute("value") == "Boston");
    }

    [Test]
    public void ItemByValue_CalledOncePerValue_AcrossRenders()
    {
        var asked = new List<int?>();
        var cut = RenderProvider(p => p
            .Add(x => x.Value, 99)
            .Add(x => x.ItemByValue, id => { asked.Add(id); return Task.FromResult<City?>(null); }));

        cut.Render(p => p.Add(x => x.Placeholder, "City"));
        cut.Render(p => p.Add(x => x.Placeholder, "Town"));

        Assert.That(asked, Is.EqualTo(new int?[] { 99 }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.Null.Or.Empty);
    }

    [Test]
    public void ItemByValue_ValueChangedFromOutside_LooksUpTheNewValue()
    {
        var cut = RenderProvider(p => p
            .Add(x => x.Value, 2)
            .Add(x => x.ItemByValue, id => Task.FromResult(Cities.FirstOrDefault(c => c.Id == id))));

        cut.Render(p => p.Add(x => x.Value, 5));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Newark"));
    }

    [Test]
    public async Task ItemByValue_LateAnswerForAnOldValue_IsDropped()
    {
        var gate = new TaskCompletionSource<City?>();
        var cut = RenderProvider(p => p
            .Add(x => x.Value, 2)
            .Add(x => x.ItemByValue, id => id == 2 ? gate.Task : Task.FromResult(Cities.FirstOrDefault(c => c.Id == id))));

        cut.Render(p => p.Add(x => x.Value, 5));
        await cut.InvokeAsync(() => gate.SetResult(Cities[1]));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Newark"));
    }

    [Test]
    public void ItemByValue_BringsABoundSelectedItemAlong()
    {
        var items = new List<City?>();
        var cut = RenderProvider(p => p
            .Add(x => x.Value, 2)
            .Add(x => x.SelectedItemChanged, (City? c) => items.Add(c))
            .Add(x => x.ItemByValue, id => Task.FromResult(Cities.FirstOrDefault(c => c.Id == id))));

        cut.WaitForState(() => items.Count == 1);
        Assert.That(items[0], Is.EqualTo(Cities[1]));
    }

    [Test]
    public void ItemByValue_NotCalledForData_OrWithoutAValue()
    {
        var asked = 0;
        RenderCities(p => p
            .Add(x => x.Value, 99)
            .Add(x => x.ItemByValue, _ => { asked++; return Task.FromResult<City?>(null); }));
        RenderProvider(p => p
            .Add(x => x.ItemByValue, _ => { asked++; return Task.FromResult<City?>(null); }));

        Assert.That(asked, Is.Zero);
    }

    [Test]
    public void ItemByValue_Multiple_ChipsShowTheItemsText()
    {
        var asked = new List<int>();
        var cut = Render<NxComboBox<City, int>>(p => p
            .Add(x => x.DataProvider, _ => Task.FromResult<IEnumerable<City>>(Cities))
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id)
            .Add(x => x.Multiple, true)
            .Add(x => x.Values, new[] { 2, 99, 5 })
            .Add(x => x.ItemByValue, id => { asked.Add(id); return Task.FromResult(Cities.FirstOrDefault(c => c.Id == id)); }));

        cut.Render(p => p.Add(x => x.Placeholder, "Cities"));

        Assert.That(cut.FindAll(".nx-combobox-chip-text").Select(c => c.TextContent.Trim()), Is.EqualTo(new[] { "Boston", "99", "Newark" }));
        Assert.That(asked, Is.EqualTo(new[] { 2, 99, 5 }));
    }

    [Test]
    public async Task DataProvider_TypingDebouncesAndCancelsOlderRequests()
    {
        var requests = new List<NxComboBoxRequest>();
        var cut = Render<NxComboBox<City, int?>>(p => p
            .Add(x => x.DataProvider, async r =>
            {
                requests.Add(r);
                await Task.Delay(50, CancellationToken.None);
                return Cities.Where(c => c.Name.StartsWith(r.Filter, StringComparison.OrdinalIgnoreCase));
            })
            .Add(x => x.DebounceDelay, 30)
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id));

        var first = cut.InvokeAsync(() => cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "b" }));
        await cut.InvokeAsync(() => { _ = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "bo" }); });
        await first;

        cut.WaitForState(() => cut.FindAll("[role=option]").Count == 1, TimeSpan.FromSeconds(3));

        Assert.That(requests.Select(r => r.Filter), Is.EqualTo(new[] { "bo" }), "the first keystroke was debounced away");
        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Boston" }));
    }

    [Test]
    public async Task DataProvider_NewerRequestCancelsSlowerOne()
    {
        var tokens = new List<CancellationToken>();
        var gate = new TaskCompletionSource<IEnumerable<City>>();
        var cut = Render<NxComboBox<City, int?>>(p => p
            .Add(x => x.DataProvider, r =>
            {
                tokens.Add(r.CancellationToken);
                return tokens.Count == 1 ? gate.Task : Task.FromResult<IEnumerable<City>>(Cities.Take(1));
            })
            .Add(x => x.DebounceDelay, 0)
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id));

        await cut.InvokeAsync(() => { _ = cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "a" }); });
        await TypeAsync(cut, "al");

        Assert.That(tokens, Has.Count.EqualTo(2));
        Assert.That(tokens[0].IsCancellationRequested, Is.True);
        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Albany" }));

        await cut.InvokeAsync(() => gate.SetResult(Cities));
        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Albany" }), "a cancelled response must not replace the newer one");
    }

    [Test]
    public async Task DataProvider_ShowsLoadingWhileRunning()
    {
        var gate = new TaskCompletionSource<IEnumerable<City>>();
        var cut = Render<NxComboBox<City, int?>>(p => p
            .Add(x => x.DataProvider, _ => gate.Task)
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id));

        await cut.InvokeAsync(() => { _ = cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }); });

        Assert.That(cut.Find("input").GetAttribute("aria-busy"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-combobox-empty").TextContent, Does.Contain("Loading"));
        Assert.That(cut.FindAll(".nx-combobox-field .nx-spinner"), Has.Count.EqualTo(1));

        await cut.InvokeAsync(() => gate.SetResult(Cities));

        Assert.That(cut.Find("input").HasAttribute("aria-busy"), Is.False);
        Assert.That(OptionTexts(cut), Has.Count.EqualTo(7));
    }

    [Test]
    public void Loading_Parameter_ShowsSpinner()
    {
        var cut = RenderCities(p => p.Add(x => x.Loading, true));

        Assert.That(cut.Find(".nx-combobox-spinner").GetAttribute("aria-hidden"), Is.EqualTo("true"));
        Assert.That(cut.Find("input").GetAttribute("aria-busy"), Is.EqualTo("true"));
    }

    // ── Virtualization ────────────────────────────────────────────────────────

    private int MeasureCalls => module.Invocations.Count(i => i.Identifier == "measureItemHeight");

    [Test]
    public async Task Virtualize_RendersAWindow_AndReportsFullSetSize()
    {
        module.Setup<float>("measureItemHeight", _ => true).SetResult(30f);
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 10_000)));

        await KeyAsync(cut, "ArrowDown");

        var options = cut.FindAll("[role=option]");
        Assert.That(options.Count, Is.GreaterThan(0).And.LessThan(200));
        Assert.That(options[0].GetAttribute("aria-setsize"), Is.EqualTo("10000"));
        Assert.That(options[0].GetAttribute("style"), Does.Contain("height: 30px"));
        var listbox = cut.Find("[role=listbox]");
        Assert.That(listbox.ClassList, Does.Contain("nx-combobox-list-virtual"));
        Assert.That(listbox.ClassList, Does.Not.Contain("nx-combobox-list-measuring"));
    }

    [Test]
    public async Task Virtualize_ScrollsActiveRowIntoView()
    {
        module.Setup<float>("measureItemHeight", _ => true).SetResult(30f);
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 10_000)));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "PageDown");

        var scroll = module.Invocations.Last(i => i.Identifier == "scrollToIndex");
        Assert.That(scroll.Arguments[1], Is.EqualTo(10));
        Assert.That(scroll.Arguments[2], Is.EqualTo(30f));
    }

    [Test]
    public async Task Virtualize_BelowThreshold_RendersEveryRowUnpinned()
    {
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 199)));

        await KeyAsync(cut, "ArrowDown");

        var options = cut.FindAll("[role=option]");
        Assert.That(options, Has.Count.EqualTo(199));
        Assert.That(options[0].GetAttribute("style"), Is.Null.Or.Empty);
        Assert.That(cut.Find("[role=listbox]").ClassList, Does.Not.Contain("nx-combobox-list-virtual"));
        Assert.That(MeasureCalls, Is.Zero);
        Assert.That(module.Invocations.Last(i => i.Identifier == "scrollToIndex").Arguments[2], Is.EqualTo(0f));
    }

    [Test]
    public async Task Virtualize_AtThreshold_Virtualizes()
    {
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 200)));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.Find("[role=listbox]").ClassList, Does.Contain("nx-combobox-list-virtual"));
        Assert.That(MeasureCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task Virtualize_ThresholdIsARowCount_FilteringBelowItRendersPlainly()
    {
        module.Setup<float>("measureItemHeight", _ => true).SetResult(30f);
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 10_000)));

        await TypeAsync(cut, "999");

        var options = cut.FindAll("[role=option]");
        Assert.That(options, Has.Count.EqualTo(19));
        Assert.That(options[0].GetAttribute("style"), Is.Null.Or.Empty);
        Assert.That(cut.Find("[role=listbox]").ClassList, Does.Not.Contain("nx-combobox-list-virtual"));
    }

    [Test]
    public async Task Virtualize_MeasuresOnce_ThenKeepsTheHeight()
    {
        module.Setup<float>("measureItemHeight", _ => true).SetResult(30f);
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 10_000)));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Escape");
        await KeyAsync(cut, "ArrowDown");

        Assert.That(MeasureCalls, Is.EqualTo(1));
        Assert.That(cut.Find("[role=option]").GetAttribute("style"), Does.Contain("height: 30px"));
    }

    [Test]
    public async Task Virtualize_ItemHeight_SkipsTheMeasurement()
    {
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 10_000))
            .Add(x => x.ItemHeight, 40f));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "PageDown");

        Assert.That(MeasureCalls, Is.Zero);
        Assert.That(cut.Find("[role=option]").GetAttribute("style"), Does.Contain("height: 40px"));
        Assert.That(module.Invocations.Last(i => i.Identifier == "scrollToIndex").Arguments[2], Is.EqualTo(40f));
    }

    [Test]
    public async Task Virtualize_NothingMeasured_RowsKeepTheirOwnHeight()
    {
        // Loose mode answers the measurement with 0.
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 10_000)));

        await KeyAsync(cut, "ArrowDown");

        var options = cut.FindAll("[role=option]");
        Assert.That(options.Count, Is.GreaterThan(0).And.LessThan(200));
        Assert.That(options[0].GetAttribute("style"), Is.Null.Or.Empty);
        Assert.That(MeasureCalls, Is.EqualTo(1));
        Assert.That(cut.Find("[role=listbox]").ClassList, Does.Not.Contain("nx-combobox-list-measuring"));
    }

    [Test]
    public async Task Virtualize_ThresholdMaxValue_NeverVirtualizes()
    {
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 500))
            .Add(x => x.VirtualizeThreshold, int.MaxValue));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.FindAll("[role=option]"), Has.Count.EqualTo(500));
    }

    [Test]
    public async Task Virtualize_GroupHeaders_PinnedToTheRowHeight()
    {
        module.Setup<float>("measureItemHeight", _ => true).SetResult(30f);
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 1000))
            .Add(x => x.ItemGroup, i => i <= 500 ? "Low" : "High"));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.Find(".nx-combobox-group").GetAttribute("style"), Does.Contain("height: 30px"));
    }

    [Test]
    public async Task Virtualize_Overlay_MeasuresItsOwnRows()
    {
        module.Setup<float>("measureItemHeight", _ => true).SetResult(30f);
        var cut = Render<NxComboBox<int, int>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 10_000)));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Escape");
        cut.Render(p => p.Add(x => x.MobileMode, NxMobileMode.Always));
        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-combobox-overlay [role=option]"), Is.Not.Empty);
        Assert.That(MeasureCalls, Is.EqualTo(2));
    }

    // ── EditForm validation ───────────────────────────────────────────────────

    private (IRenderedComponent<NxComboBox<City, int?>> Cut, EditContext Context) RenderInForm()
    {
        var model = new Model();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxComboBox<City, int?>>(c => c
                .Add(x => x.Data, Cities)
                .Add(x => x.ItemText, i => i.Name)
                .Add(x => x.ItemValue, i => i.Id)
                .Add(x => x.Value, model.CityId)
                .Add(x => x.ValueChanged, (int? v) => model.CityId = v)
                .Add(x => x.ValueExpression, () => model.CityId)));
        return (host.FindComponent<NxComboBox<City, int?>>(), context);
    }

    [Test]
    public async Task Validation_Selecting_MarksFieldModified()
    {
        var (cut, context) = RenderInForm();

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");

        Assert.That(context.IsModified(context.Field(nameof(Model.CityId))), Is.True);
        Assert.That(cut.Find("input").ClassList, Does.Contain("modified"));
    }

    [Test]
    public async Task Validation_Messages_MarkInvalid()
    {
        var (cut, context) = RenderInForm();
        var store = new ValidationMessageStore(context);

        store.Add(context.Field(nameof(Model.CityId)), "Pick a city.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain("nx-combobox-invalid"));
        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));

        store.Clear();
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("input").HasAttribute("aria-invalid"), Is.False);
    }

    // ── Accessibility ─────────────────────────────────────────────────────────

    [Test]
    public async Task Accessibility_OptionsHaveUniqueIdsAndButtonsAreOutOfTabOrder()
    {
        var cut = RenderCities(p => p.Add(x => x.Value, 1).Add(x => x.Clearable, true));

        await KeyAsync(cut, "ArrowDown");

        var ids = cut.FindAll("[role=option]").Select(o => o.Id).ToList();
        Assert.That(ids, Is.Unique);
        Assert.That(ids.All(id => !string.IsNullOrEmpty(id)), Is.True);
        Assert.That(cut.Find(".nx-combobox-toggle").GetAttribute("tabindex"), Is.EqualTo("-1"));
        Assert.That(cut.Find(".nx-combobox-clear").GetAttribute("tabindex"), Is.EqualTo("-1"));
        Assert.That(cut.Find(".nx-combobox-toggle svg").GetAttribute("aria-hidden"), Is.EqualTo("true"));
    }

    [Test]
    public void Accessibility_ClosedHasNoActiveDescendant()
    {
        var cut = RenderCities(p => p.Add(x => x.Value, 1));

        Assert.That(cut.Find("input").HasAttribute("aria-activedescendant"), Is.False);
    }

    [Test]
    public async Task FocusAsync_FocusesInput()
    {
        var cut = RenderCities();

        await cut.InvokeAsync(() => cut.Instance.FocusAsync().AsTask());

        JSInterop.VerifyFocusAsyncInvoke();
    }

    // ── Multi-column ──────────────────────────────────────────────────────────

    private static readonly List<NxComboBoxColumnDefinition<City>> CityColumns =
    [
        new() { Field = c => c.Name, Header = "City", Width = "1fr" },
        new() { Field = c => c.State, Header = "State", Width = "60px", Alignment = NxColumnAlignment.Center },
        new() { Field = c => c.Id, Header = "Id", Width = "50px", Alignment = NxColumnAlignment.End, Format = "000" },
    ];

    [Test]
    public async Task Columns_RenderHeaderAndAlignedCells()
    {
        var cut = RenderCities(p => p.Add(x => x.ColumnDefinitions, CityColumns));

        await KeyAsync(cut, "ArrowDown");

        var header = cut.Find(".nx-combobox-columns-header");
        Assert.That(header.GetAttribute("aria-hidden"), Is.EqualTo("true"));
        Assert.That(header.GetAttribute("style"), Does.Contain("grid-template-columns: 1fr 60px 50px"));
        Assert.That(header.Children.Take(3).Select(h => h.TextContent), Is.EqualTo(new[] { "City", "State", "Id" }));

        var first = cut.FindAll("[role=option]")[0];
        Assert.That(first.ClassList, Does.Contain("nx-combobox-item-grid"));
        var cells = first.QuerySelectorAll(".nx-combobox-cell");
        Assert.That(cells.Select(c => c.TextContent.Trim()), Is.EqualTo(new[] { "Albany", "NY", "001" }));
        Assert.That(cells[1].ClassList, Does.Contain("nx-combobox-cell-center"));
        Assert.That(cells[2].ClassList, Does.Contain("nx-combobox-cell-end"));
    }

    [Test]
    public async Task Columns_Declarative_RegisterInOrder()
    {
        var cut = Render<NxComboBox<City, int?>>(p => p
            .Add(x => x.Data, Cities)
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id)
            .Add(x => x.Columns, b =>
            {
                b.OpenComponent<NxComboBoxColumn<City>>(0);
                b.AddAttribute(1, nameof(NxComboBoxColumn<City>.Field), (Func<City, object?>)(c => c.State));
                b.AddAttribute(2, nameof(NxComboBoxColumn<City>.Header), "State");
                b.CloseComponent();
                b.OpenComponent<NxComboBoxColumn<City>>(3);
                b.AddAttribute(4, nameof(NxComboBoxColumn<City>.Field), (Func<City, object?>)(c => c.Name));
                b.AddAttribute(5, nameof(NxComboBoxColumn<City>.Header), "City");
                b.CloseComponent();
            }));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.Find(".nx-combobox-columns-header").Children.Take(2).Select(h => h.TextContent), Is.EqualTo(new[] { "State", "City" }));
        Assert.That(cut.FindAll("[role=option]")[0].QuerySelectorAll(".nx-combobox-cell").Select(c => c.TextContent.Trim()),
            Is.EqualTo(new[] { "NY", "Albany" }));
    }

    [Test]
    public async Task Columns_CellTemplate()
    {
        var columns = new List<NxComboBoxColumnDefinition<City>>
        {
            new() { Field = c => c.Name, Header = "City" },
            new() { Header = "Badge", Template = c => b => b.AddMarkupContent(0, $"<b>{c.State}</b>") },
        };
        var cut = RenderCities(p => p.Add(x => x.ColumnDefinitions, columns));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.FindAll("[role=option]")[1].QuerySelector(".nx-combobox-cell b")!.TextContent, Is.EqualTo("MA"));
    }

    [Test]
    public async Task Columns_FilterMatchesAnyColumn()
    {
        var cut = RenderCities(p => p.Add(x => x.ColumnDefinitions, CityColumns));

        await TypeAsync(cut, "ma");

        Assert.That(cut.FindAll("[role=option]").Select(o => o.QuerySelector(".nx-combobox-cell")!.TextContent.Trim()),
            Is.EqualTo(new[] { "Boston", "Worcester" }));
    }

    [Test]
    public async Task Columns_FilterAllColumnsOff_MatchesItemTextOnly()
    {
        var cut = RenderCities(p => p
            .Add(x => x.ColumnDefinitions, CityColumns)
            .Add(x => x.FilterAllColumns, false));

        await TypeAsync(cut, "ma");

        Assert.That(cut.FindAll("[role=option]"), Is.Empty);
    }

    [Test]
    public async Task Columns_KeyboardAndSelectionUnchanged_FieldShowsItemText()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, [], p => p.Add(x => x.ColumnDefinitions, CityColumns));

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");

        Assert.That(values, Is.EqualTo(new int?[] { 2 }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Boston"));
    }

    [Test]
    public async Task Columns_WithVirtualize_KeepHeaderAndGridRows()
    {
        var cut = RenderCities(p => p
            .Add(x => x.ColumnDefinitions, CityColumns)
            .Add(x => x.VirtualizeThreshold, 0)
            .Add(x => x.ItemHeight, 34f));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.FindAll(".nx-combobox-columns-header"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("[role=option].nx-combobox-item-grid"), Is.Not.Empty);
        Assert.That(cut.Find("[role=option]").GetAttribute("style"), Does.Contain("height: 34px"));
    }

    [Test]
    public async Task NoColumns_ListUnchanged()
    {
        var cut = RenderCities();

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.FindAll(".nx-combobox-columns-header"), Is.Empty);
        Assert.That(cut.FindAll(".nx-combobox-item-grid"), Is.Empty);
        Assert.That(cut.Find("[role=listbox]").HasAttribute("aria-multiselectable"), Is.False);
    }

    // ── Mobile overlay ────────────────────────────────────────────────────────

    [Test]
    public void Mobile_Always_FieldIsReadOnlyTapTarget()
    {
        var cut = RenderCities(p => p.Add(x => x.MobileMode, NxMobileMode.Always));

        var input = cut.Find("input");
        Assert.That(input.HasAttribute("readonly"), Is.True);
        Assert.That(input.GetAttribute("inputmode"), Is.EqualTo("none"));
        Assert.That(input.GetAttribute("aria-haspopup"), Is.EqualTo("dialog"));
        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain("nx-combobox-mobile"));
    }

    [Test]
    public async Task Mobile_TapOpensFullScreenOverlay_WithFocusedSearch()
    {
        var cut = RenderCities(p => p
            .Add(x => x.MobileMode, NxMobileMode.Always)
            .Add(x => x.Placeholder, "Pick a city"));

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());

        var overlay = cut.Find(".nx-combobox-overlay");
        Assert.That(overlay.GetAttribute("role"), Is.EqualTo("dialog"));
        Assert.That(overlay.GetAttribute("aria-modal"), Is.EqualTo("true"));
        Assert.That(overlay.GetAttribute("aria-label"), Is.EqualTo("Pick a city"));
        Assert.That(cut.FindAll(".nx-combobox-popup"), Is.Empty);
        Assert.That(cut.FindAll(".nx-combobox-overlay [role=option]"), Has.Count.EqualTo(7));
        Assert.That(module.Invocations.Select(i => i.Identifier), Does.Not.Contain("open"));
        Assert.That(module.Invocations.Any(i => i.Identifier == "lockScroll" && (bool)i.Arguments[0]!), Is.True);
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Test]
    public async Task Mobile_SearchFilters_PickSelectsAndCloses()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, [], p => p.Add(x => x.MobileMode, NxMobileMode.Always));

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-combobox-overlay-search").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "wor" });
        Assert.That(cut.FindAll(".nx-combobox-overlay [role=option]").Select(o => o.TextContent.Trim()), Is.EqualTo(new[] { "Worcester" }));

        await cut.Find(".nx-combobox-overlay [role=option]").ClickAsync(new MouseEventArgs());

        Assert.That(values, Is.EqualTo(new int?[] { 7 }));
        Assert.That(cut.FindAll(".nx-combobox-overlay"), Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Worcester"));
        Assert.That(module.Invocations.Any(i => i.Identifier == "lockScroll" && !(bool)i.Arguments[0]!), Is.True);
    }

    [Test]
    public async Task Mobile_EnterInSearch_SelectsHighlighted()
    {
        var values = new List<int?>();
        var cut = RenderBound(values, [], p => p.Add(x => x.MobileMode, NxMobileMode.Always));

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-combobox-overlay-search").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await cut.Find(".nx-combobox-overlay-search").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.That(values, Is.EqualTo(new int?[] { 2 }));
        Assert.That(cut.FindAll(".nx-combobox-overlay"), Is.Empty);
    }

    [TestCase("back")]
    [TestCase("escape")]
    public async Task Mobile_BackOrEscape_ClosesWithoutChange(string how)
    {
        var values = new List<int?>();
        var cut = RenderBound(values, [], p => p.Add(x => x.MobileMode, NxMobileMode.Always), initial: 1);

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-combobox-overlay-search").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        if (how == "back")
            await cut.Find(".nx-combobox-overlay-back").ClickAsync(new MouseEventArgs());
        else
            await cut.Find(".nx-combobox-overlay-search").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.That(cut.FindAll(".nx-combobox-overlay"), Is.Empty);
        Assert.That(values, Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Albany"));
    }

    [Test]
    public async Task Mobile_Default_IsThePopup_AndDoesNotWatchViewport()
    {
        var cut = RenderCities();

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(true));
        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());
        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.False);
        Assert.That(cut.FindAll(".nx-combobox-overlay"), Is.Empty);
        Assert.That(cut.FindAll(".nx-combobox-popup"), Has.Count.EqualTo(1));
        Assert.That(module.Invocations.Select(i => i.Identifier), Does.Not.Contain("watchViewport"));
    }

    [Test]
    public void Mobile_ChangedToAutoLater_StartsWatchingViewport()
    {
        var cut = RenderCities();

        cut.Render(p => p.Add(x => x.MobileMode, NxMobileMode.Auto));

        Assert.That(module.Invocations.Count(i => i.Identifier == "watchViewport"), Is.EqualTo(1));
    }

    [Test]
    public async Task Mobile_Auto_SwitchesWhenViewportNarrows()
    {
        var cut = RenderCities(p => p.Add(x => x.MobileMode, NxMobileMode.Auto));
        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.False);

        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(true));

        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.True);
        Assert.That(module.Invocations.Select(i => i.Identifier), Does.Contain("watchViewport"));
    }

    [Test]
    public void Mobile_Never_DoesNotWatchViewport()
    {
        RenderCities(p => p.Add(x => x.MobileMode, NxMobileMode.Never));

        Assert.That(module.Invocations.Select(i => i.Identifier), Does.Not.Contain("watchViewport"));
    }

    [Test]
    public async Task Mobile_WithColumns_OverlayShowsGrid()
    {
        var cut = RenderCities(p => p
            .Add(x => x.MobileMode, NxMobileMode.Always)
            .Add(x => x.ColumnDefinitions, CityColumns));

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-combobox-overlay .nx-combobox-columns-header"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll(".nx-combobox-overlay .nx-combobox-item-grid"), Has.Count.EqualTo(7));
    }

    // ── Multiple ──────────────────────────────────────────────────────────────

    /// <summary>A Multiple combo whose callback writes back, like @bind-Values.</summary>
    private IRenderedComponent<NxComboBox<City, int>> RenderMulti(
        List<List<int>> raised, Action<ComponentParameterCollectionBuilder<NxComboBox<City, int>>>? configure = null,
        IEnumerable<int>? initial = null)
    {
        IRenderedComponent<NxComboBox<City, int>>? cut = null;
        cut = Render<NxComboBox<City, int>>(p =>
        {
            p.Add(x => x.Data, Cities);
            p.Add(x => x.ItemText, c => c.Name);
            p.Add(x => x.ItemValue, c => c.Id);
            p.Add(x => x.Multiple, true);
            p.Add(x => x.Values, initial ?? []);
            p.Add(x => x.ValuesChanged, (IEnumerable<int> v) =>
            {
                raised.Add(v.ToList());
                cut?.Render(q => q.Add(x => x.Values, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static List<string> ChipTexts(IRenderedComponent<NxComboBox<City, int>> cut) =>
        cut.FindAll(".nx-combobox-chip:not(.nx-combobox-chip-more) .nx-combobox-chip-text").Select(c => c.TextContent.Trim()).ToList();

    [Test]
    public void Multiple_InitialValues_RenderChips()
    {
        var cut = RenderMulti([], initial: [2, 4]);

        Assert.That(ChipTexts(cut), Is.EqualTo(new[] { "Boston", "Hartford" }));
        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain("nx-combobox-multiple"));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));
        Assert.That(cut.Find("input").HasAttribute("placeholder"), Is.False);
    }

    [Test]
    public async Task Multiple_ClickTogglesWithoutClosing()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised);

        await KeyAsync(cut, "ArrowDown");
        Assert.That(cut.Find("[role=listbox]").GetAttribute("aria-multiselectable"), Is.EqualTo("true"));
        await cut.FindAll("[role=option]")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll("[role=option]")[3].ClickAsync(new MouseEventArgs());

        Assert.That(cut.Instance.IsOpen, Is.True);
        Assert.That(raised.Last(), Is.EqualTo(new[] { 2, 4 }));
        Assert.That(cut.FindAll("[role=option][aria-selected=true]"), Has.Count.EqualTo(2));
        Assert.That(cut.FindAll("[role=option][aria-selected=true] .nx-combobox-check"), Has.Count.EqualTo(2));

        await cut.FindAll("[role=option]")[1].ClickAsync(new MouseEventArgs());
        Assert.That(raised.Last(), Is.EqualTo(new[] { 4 }));
    }

    [Test]
    public async Task Multiple_EnterToggles()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised);

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");

        Assert.That(raised.Last(), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(cut.Instance.IsOpen, Is.True);
    }

    [Test]
    public async Task Multiple_SpaceTogglesOnlyAfterArrowing()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised);

        // The input is marked so nx-combobox.js can cancel Space's default once the user arrows.
        Assert.That(cut.Find("input").GetAttribute("data-nx-multiple"), Is.EqualTo("true"));

        await TypeAsync(cut, "new");
        await KeyAsync(cut, " ");
        Assert.That(raised, Is.Empty, "Space must type while filtering");

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, " ");

        Assert.That(raised.Last(), Is.EqualTo(new[] { 6 }));
    }

    [Test]
    public void SingleSelect_InputIsNotMarkedMultiple()
    {
        var cut = RenderCities();

        Assert.That(cut.Find("input").HasAttribute("data-nx-multiple"), Is.False);
    }

    [Test]
    public async Task Multiple_BackspaceInEmptyInput_RemovesLastChip()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised, initial: [1, 2, 3]);

        await KeyAsync(cut, "Backspace");

        Assert.That(raised.Last(), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(ChipTexts(cut), Is.EqualTo(new[] { "Albany", "Boston" }));
    }

    [Test]
    public async Task Multiple_BackspaceWithText_KeepsChips()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised, initial: [1]);

        await TypeAsync(cut, "b");
        await KeyAsync(cut, "Backspace");

        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task Multiple_ChipRemoveButton()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised, initial: [1, 2]);

        var remove = cut.FindAll(".nx-combobox-chip-remove")[0];
        Assert.That(remove.GetAttribute("aria-label"), Is.EqualTo("Remove Albany"));
        Assert.That(remove.GetAttribute("tabindex"), Is.EqualTo("-1"));
        await remove.ClickAsync(new MouseEventArgs());

        Assert.That(raised.Last(), Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public async Task Multiple_ClearAll()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised, p => p.Add(x => x.Clearable, true), initial: [1, 2, 3]);

        Assert.That(cut.Find(".nx-combobox-clear").GetAttribute("aria-label"), Is.EqualTo("Clear all"));
        await cut.Find(".nx-combobox-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised.Last(), Is.Empty);
        Assert.That(ChipTexts(cut), Is.Empty);
    }

    [Test]
    public void Multiple_MaxVisibleChips_ShowsMoreCount()
    {
        var cut = RenderMulti([], p => p.Add(x => x.MaxVisibleChips, 2), initial: [1, 2, 3, 4, 5]);

        Assert.That(ChipTexts(cut), Is.EqualTo(new[] { "Albany", "Boston" }));
        Assert.That(cut.Find(".nx-combobox-chip-more").TextContent.Trim(), Is.EqualTo("+3 more"));
    }

    [Test]
    public void Multiple_ChipTemplate()
    {
        var cut = RenderMulti([], p => p.Add(x => x.ChipTemplate, c => b => b.AddMarkupContent(0, $"<i>{c.State}</i>")), initial: [2]);

        Assert.That(cut.Find(".nx-combobox-chip i").TextContent, Is.EqualTo("MA"));
    }

    [Test]
    public async Task Multiple_BlurDiscardsFilterText()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised, initial: [1]);

        await TypeAsync(cut, "bos");
        await BlurAsync(cut);

        Assert.That(cut.Instance.IsOpen, Is.False);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));
        Assert.That(raised, Is.Empty);
    }

    [Test]
    public async Task Multiple_SelectedItemsBinding()
    {
        var items = new List<List<City>>();
        var cut = Render<NxComboBox<City, int>>(p => p
            .Add(x => x.Data, Cities)
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id)
            .Add(x => x.Multiple, true)
            .Add(x => x.SelectedItems, new[] { Cities[0] })
            .Add(x => x.SelectedItemsChanged, (IEnumerable<City> v) => items.Add(v.ToList())));

        Assert.That(ChipTexts(cut), Is.EqualTo(new[] { "Albany" }));
        await KeyAsync(cut, "ArrowDown");
        await cut.FindAll("[role=option]")[2].ClickAsync(new MouseEventArgs());

        Assert.That(items.Last(), Is.EqualTo(new[] { Cities[0], Cities[2] }));
    }

    [Test]
    public void Multiple_ValuesSetFromCode_BringSelectedItemsAlong()
    {
        var items = new List<List<City>>();
        var cut = Render<NxComboBox<City, int>>(p => p
            .Add(x => x.Data, Cities)
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id)
            .Add(x => x.Multiple, true)
            .Add(x => x.Values, new[] { 3 })
            .Add(x => x.SelectedItemsChanged, (IEnumerable<City> v) => items.Add(v.ToList())));

        Assert.That(items.Last(), Is.EqualTo(new[] { Cities[2] }));
        cut.Render(p => p.Add(x => x.Values, new[] { 3, 7 }));

        Assert.That(items.Last(), Is.EqualTo(new[] { Cities[2], Cities[6] }));
    }

    [Test]
    public void Multiple_UnknownValue_ChipShowsValueUntilDataArrives()
    {
        var cut = Render<NxComboBox<City, int>>(p => p
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id)
            .Add(x => x.Multiple, true)
            .Add(x => x.Values, new[] { 5 }));

        Assert.That(ChipTexts(cut), Is.EqualTo(new[] { "5" }));

        cut.Render(p => p.Add(x => x.Data, Cities));

        Assert.That(ChipTexts(cut), Is.EqualTo(new[] { "Newark" }));
    }

    private class MultiModel
    {
        public IEnumerable<int> CityIds { get; set; } = [];
    }

    [Test]
    public async Task Multiple_Validation_OnCollection()
    {
        var model = new MultiModel();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxComboBox<City, int>>(c => c
                .Add(x => x.Data, Cities)
                .Add(x => x.ItemText, i => i.Name)
                .Add(x => x.ItemValue, i => i.Id)
                .Add(x => x.Multiple, true)
                .Add(x => x.Values, model.CityIds)
                .Add(x => x.ValuesChanged, (IEnumerable<int> v) => model.CityIds = v)
                .Add(x => x.ValuesExpression, () => model.CityIds)));
        var cut = host.FindComponent<NxComboBox<City, int>>();

        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");

        Assert.That(context.IsModified(context.Field(nameof(MultiModel.CityIds))), Is.True);

        var store = new ValidationMessageStore(context);
        store.Add(context.Field(nameof(MultiModel.CityIds)), "Pick at least two.");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);
        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Multiple_Overlay_TogglesWaitForDone()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised, p => p.Add(x => x.MobileMode, NxMobileMode.Always), initial: [1]);

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".nx-combobox-overlay [role=option]")[1].ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.Empty, "a toggle in the overlay must wait for Done");
        Assert.That(cut.FindAll(".nx-combobox-overlay [role=option][aria-selected=true]"), Has.Count.EqualTo(2));

        await cut.Find(".nx-combobox-overlay-done").ClickAsync(new MouseEventArgs());

        Assert.That(raised.Last(), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(cut.FindAll(".nx-combobox-overlay"), Is.Empty);
    }

    [Test]
    public async Task Multiple_Overlay_BackDiscardsToggles()
    {
        var raised = new List<List<int>>();
        var cut = RenderMulti(raised, p => p.Add(x => x.MobileMode, NxMobileMode.Always), initial: [1]);

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".nx-combobox-overlay [role=option]")[0].ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-combobox-overlay-back").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.Empty);
        Assert.That(ChipTexts(cut), Is.EqualTo(new[] { "Albany" }));
    }

    [Test]
    public async Task SingleSelectOverlay_HasNoDoneButton()
    {
        var cut = RenderCities(p => p.Add(x => x.MobileMode, NxMobileMode.Always));

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());

        Assert.That(cut.FindAll(".nx-combobox-overlay"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll(".nx-combobox-overlay-done"), Is.Empty);
    }

    [Test]
    public async Task Multiple_WithColumns_HasCheckColumn()
    {
        var cut = Render<NxComboBox<City, int>>(p => p
            .Add(x => x.Data, Cities)
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id)
            .Add(x => x.Multiple, true)
            .Add(x => x.Values, new[] { 1 })
            .Add(x => x.ColumnDefinitions, CityColumns));

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.FindAll("[role=option] .nx-combobox-check-cell"), Has.Count.EqualTo(7));
        Assert.That(cut.FindAll("[role=option] .nx-combobox-check-cell .nx-combobox-check"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Multiple_WithAllowCustomValue_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<NxComboBox<string, string>>(p => p
            .Add(x => x.Data, ["a"])
            .Add(x => x.Multiple, true)
            .Add(x => x.AllowCustomValue, true)));
    }

    [Test]
    public void SingleSelect_Default_RendersNoChips()
    {
        var cut = RenderCities(p => p.Add(x => x.Value, 1));

        Assert.That(cut.FindAll(".nx-combobox-chip"), Is.Empty);
        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Not.Contain("nx-combobox-multiple"));
    }

    // ── Hardening: two-way rule, data arriving, overlay title ─────────────────

    [Test]
    public async Task ParentRerender_SameValue_KeepsTypedText()
    {
        var cut = RenderBound([], [], initial: 1);

        await TypeAsync(cut, "bos");
        cut.Render(p => p.Add(x => x.Value, 1).Add(x => x.Placeholder, "City"));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("bos"));
        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Boston" }));
    }

    [Test]
    public async Task OutsideValueChange_WhileOpen_ReplacesTypedText_ClosesTheList()
    {
        var cut = RenderBound([], [], initial: 1);

        await TypeAsync(cut, "bos");
        cut.Render(p => p.Add(x => x.Value, 4));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Hartford"));
        Assert.That(cut.Instance.IsOpen, Is.False);
    }

    [Test]
    public void Value_BeforeData_ShowsTextWhenDataArrives()
    {
        var cut = Render<NxComboBox<City, int?>>(p => p
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id)
            .Add(x => x.Value, 5));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));

        cut.Render(p => p.Add(x => x.Data, Cities));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Newark"));
    }

    [Test]
    public async Task Data_ReplacedWhileOpen_ListShowsNewItems()
    {
        var cut = RenderCities();

        await KeyAsync(cut, "ArrowDown");
        cut.Render(p => p.Add(x => x.Data, Cities.Take(2).ToList()));

        Assert.That(OptionTexts(cut), Is.EqualTo(new[] { "Albany", "Boston" }));
    }

    [Test]
    public async Task Mobile_MobileTitle_NamesOverlayAndSearch()
    {
        var cut = RenderCities(p => p
            .Add(x => x.MobileMode, NxMobileMode.Always)
            .Add(x => x.Placeholder, "City")
            .Add(x => x.MobileTitle, "Find a city"));

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());

        Assert.That(cut.Find(".nx-combobox-overlay").GetAttribute("aria-label"), Is.EqualTo("Find a city"));
        Assert.That(cut.Find(".nx-combobox-overlay-search").GetAttribute("placeholder"), Is.EqualTo("Find a city"));
    }
}
