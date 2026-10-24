using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxMultiSelectTests : BunitContext
{
    private static readonly string[] States = ["Maine", "Maryland", "Massachusetts", "New Hampshire", "New Jersey", "New York", "Vermont"];

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-combobox.js").Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Bound like @bind-Values; <paramref name="log"/> collects each raised selection.</summary>
    private IRenderedComponent<NxMultiSelect<string, string>> RenderBound(List<string[]> log, IEnumerable<string>? initial = null,
        Action<ComponentParameterCollectionBuilder<NxMultiSelect<string, string>>>? configure = null)
    {
        IRenderedComponent<NxMultiSelect<string, string>>? cut = null;
        cut = Render<NxMultiSelect<string, string>>(p =>
        {
            p.Add(x => x.Id, "ms");
            p.Add(x => x.Data, States);
            p.Add(x => x.Values, initial ?? []);
            p.Add(x => x.ValuesChanged, (IEnumerable<string> v) =>
            {
                log.Add(v.ToArray());
                cut?.Render(q => q.Add(x => x.Values, v));
            });
            configure?.Invoke(p);
        });
        return cut;
    }

    private static Task KeyAsync<T>(IRenderedComponent<T> cut, string key) where T : IComponent =>
        cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key });

    private static async Task TypeAsync<T>(IRenderedComponent<T> cut, string text) where T : IComponent =>
        await cut.Find("input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });

    private static IReadOnlyList<string> Options<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll("[role=option]").Select(o => o.TextContent.Trim()).ToList();

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_MultipleComboboxWithChips()
    {
        var cut = RenderBound([], ["Maine", "Vermont"]);

        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain("nx-multiselect").And.Contain("nx-combobox-multiple"));
        Assert.That(cut.FindAll(".nx-combobox-chip-text").Select(c => c.TextContent.Trim()), Is.EqualTo(new[] { "Maine", "Vermont" }));
        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.False);
    }

    [Test]
    public async Task Listbox_IsMultiselectable()
    {
        var cut = RenderBound([]);

        await KeyAsync(cut, "ArrowDown");

        Assert.That(cut.Find("[role=listbox]").GetAttribute("aria-multiselectable"), Is.EqualTo("true"));
    }

    // ── Select all ────────────────────────────────────────────────────────────

    [Test]
    public async Task SelectAll_SelectsEveryItemThenClears()
    {
        var log = new List<string[]>();
        var cut = RenderBound(log, configure: p => p.Add(x => x.SelectAll, true));

        await KeyAsync(cut, "ArrowDown");
        var all = cut.FindAll("[role=option]")[0];
        Assert.That(all.TextContent.Trim(), Is.EqualTo("Select all"));
        Assert.That(all.GetAttribute("aria-selected"), Is.EqualTo("false"));

        await all.ClickAsync(new MouseEventArgs());
        Assert.That(log.Last(), Is.EqualTo(States));
        Assert.That(cut.FindAll("[role=option]")[0].GetAttribute("aria-selected"), Is.EqualTo("true"));

        await cut.FindAll("[role=option]")[0].ClickAsync(new MouseEventArgs());
        Assert.That(log.Last(), Is.Empty);
    }

    [Test]
    public async Task SelectAll_OnlyTheFilteredItems()
    {
        var log = new List<string[]>();
        var cut = RenderBound(log, ["Vermont"], p => p.Add(x => x.SelectAll, true));

        await TypeAsync(cut, "new");
        Assert.That(Options(cut), Is.EqualTo(new[] { "Select all", "New Hampshire", "New Jersey", "New York" }));
        await cut.FindAll("[role=option]")[0].ClickAsync(new MouseEventArgs());

        Assert.That(log.Last(), Is.EqualTo(new[] { "Vermont", "New Hampshire", "New Jersey", "New York" }));
    }

    [Test]
    public async Task SelectAll_Enter()
    {
        var log = new List<string[]>();
        var cut = RenderBound(log, configure: p => p.Add(x => x.SelectAll, true));

        await KeyAsync(cut, "ArrowDown");      // opens on the first row: Select all
        await KeyAsync(cut, "Enter");

        Assert.That(log.Last(), Has.Length.EqualTo(States.Length));
    }

    [Test]
    public async Task NoSelectAllRow_WhenNothingMatches()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.SelectAll, true));

        await TypeAsync(cut, "zzz");

        Assert.That(cut.FindAll("[role=option]"), Is.Empty);
    }

    private sealed record Crew(int Id, string Name);

    [Test]
    public async Task SelectAll_WithRecords_OpensAtTheSelection()
    {
        // Regression: the select-all row has no item, and opening looked for the selected row through it.
        Crew[] crews = [new(1, "North"), new(2, "South"), new(3, "Night")];
        var cut = Render<NxMultiSelect<Crew, int>>(p => p
            .Add(x => x.Data, crews)
            .Add(x => x.ItemText, c => c.Name)
            .Add(x => x.ItemValue, c => c.Id)
            .Add(x => x.Values, new[] { 2 })
            .Add(x => x.SelectAll, true));

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(cut.Find(".nx-combobox-item-active").TextContent.Trim(), Is.EqualTo("South"));
    }

    // ── Max selections ────────────────────────────────────────────────────────

    [Test]
    public async Task MaxSelections_BlocksMore_AndGreysOutTheRest()
    {
        var log = new List<string[]>();
        var cut = RenderBound(log, ["Maine", "Maryland"], p => p.Add(x => x.MaxSelections, 2));

        await KeyAsync(cut, "ArrowDown");
        var vermont = cut.FindAll("[role=option]").Single(o => o.TextContent.Trim() == "Vermont");
        Assert.That(vermont.GetAttribute("aria-disabled"), Is.EqualTo("true"));
        Assert.That(vermont.ClassList, Does.Contain("nx-combobox-item-disabled"));
        await vermont.ClickAsync(new MouseEventArgs());
        Assert.That(log, Is.Empty);

        // Removing one frees a place.
        await cut.FindAll("[role=option]").Single(o => o.TextContent.Trim() == "Maine").ClickAsync(new MouseEventArgs());
        Assert.That(cut.FindAll("[aria-disabled=true]"), Is.Empty);
    }

    [Test]
    public async Task MaxSelections_SelectAllStopsAtTheCap()
    {
        var log = new List<string[]>();
        var cut = RenderBound(log, configure: p => p.Add(x => x.SelectAll, true).Add(x => x.MaxSelections, 3));

        await KeyAsync(cut, "ArrowDown");
        await cut.FindAll("[role=option]")[0].ClickAsync(new MouseEventArgs());

        Assert.That(log.Last(), Is.EqualTo(new[] { "Maine", "Maryland", "Massachusetts" }));
    }

    // ── Summary ───────────────────────────────────────────────────────────────

    [Test]
    public void Summary_AboveThreshold_ReplacesChips()
    {
        var cut = RenderBound([], ["Maine", "Vermont"], p => p.Add(x => x.SummaryThreshold, 1));

        Assert.That(cut.Find(".nx-combobox-summary").TextContent, Is.EqualTo("2 selected"));
        Assert.That(cut.FindAll(".nx-combobox-chip-text"), Is.Empty);
    }

    [Test]
    public void Summary_AtOrBelowThreshold_ShowsChips()
    {
        var cut = RenderBound([], ["Maine", "Vermont"], p => p.Add(x => x.SummaryThreshold, 2));

        Assert.That(cut.FindAll(".nx-combobox-summary"), Is.Empty);
        Assert.That(cut.FindAll(".nx-combobox-chip-text"), Has.Count.EqualTo(2));
    }

    [Test]
    public void Summary_ZeroAlways_CustomFormat_NoneWhenEmpty()
    {
        var some = RenderBound([], ["Maine"], p => p.Add(x => x.SummaryThreshold, 0).Add(x => x.SummaryFormat, "{0} states"));
        Assert.That(some.Find(".nx-combobox-summary").TextContent, Is.EqualTo("1 states"));

        var none = RenderBound([], [], p => p.Add(x => x.SummaryThreshold, 0).Add(x => x.Placeholder, "Any state"));
        Assert.That(none.FindAll(".nx-combobox-summary"), Is.Empty);
        Assert.That(none.Find("input").GetAttribute("placeholder"), Is.EqualTo("Any state"));
    }

    // ── Filtering and pick-only ───────────────────────────────────────────────

    [Test]
    public async Task Filterable_TypingFilters()
    {
        var cut = RenderBound([]);

        await TypeAsync(cut, "ma");

        Assert.That(Options(cut), Is.EqualTo(new[] { "Maine", "Maryland", "Massachusetts" }));
    }

    [Test]
    public async Task NotFilterable_PickOnly_LettersToggleWhenClosed_SpaceTogglesWhenOpen()
    {
        var log = new List<string[]>();
        var cut = RenderBound(log, configure: p => p.Add(x => x.Filterable, false));

        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.True);
        await KeyAsync(cut, "v");
        Assert.That(log.Last(), Is.EqualTo(new[] { "Vermont" }));

        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "m");
        await KeyAsync(cut, " ");
        Assert.That(log.Last(), Is.EqualTo(new[] { "Vermont", "Maine" }));
        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("true"));
    }

    // ── Engine features ───────────────────────────────────────────────────────

    [Test]
    public async Task Toggling_KeepsListOpen_BackspaceRemovesLastChip()
    {
        var log = new List<string[]>();
        var cut = RenderBound(log, ["Maine"]);

        await KeyAsync(cut, "ArrowDown");
        await cut.FindAll("[role=option]").Single(o => o.TextContent.Trim() == "Vermont").ClickAsync(new MouseEventArgs());
        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        await KeyAsync(cut, "Escape");
        await KeyAsync(cut, "Backspace");

        Assert.That(log, Is.EqualTo(new[] { new[] { "Maine", "Vermont" }, new[] { "Maine" } }));
    }

    [Test]
    public async Task Overlay_StagesUntilDone_WithSelectAll()
    {
        var log = new List<string[]>();
        var cut = RenderBound(log, configure: p => p.Add(x => x.MobileMode, NxComboBoxMobileMode.Always).Add(x => x.SelectAll, true));

        await cut.Find(".nx-combobox-field").ClickAsync(new MouseEventArgs());
        await cut.Find(".nx-combobox-overlay [role=option]").ClickAsync(new MouseEventArgs());
        Assert.That(log, Is.Empty);

        await cut.Find(".nx-combobox-overlay-done").ClickAsync(new MouseEventArgs());
        Assert.That(log.Last(), Has.Length.EqualTo(States.Length));
    }

    [Test]
    public async Task Validation_ThroughTheEngine()
    {
        var model = new Holder();
        var context = new EditContext(model);
        var host = Render<CascadingValue<EditContext>>(p => p
            .Add(x => x.Value, context)
            .AddChildContent<NxMultiSelect<string, string>>(c => c
                .Add(x => x.Data, States)
                .Add(x => x.Values, model.States)
                .Add(x => x.ValuesChanged, (IEnumerable<string> v) => model.States = v)
                .Add(x => x.ValuesExpression, () => model.States)));
        var cut = host.FindComponent<NxMultiSelect<string, string>>();

        await KeyAsync(cut, "ArrowDown");
        await cut.FindAll("[role=option]")[0].ClickAsync(new MouseEventArgs());

        Assert.That(model.States, Is.EqualTo(new[] { "Maine" }));
        Assert.That(context.IsModified(context.Field(nameof(Holder.States))), Is.True);
    }

    private class Holder
    {
        public IEnumerable<string>? States { get; set; } = [];
    }
}
