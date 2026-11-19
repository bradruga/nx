using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace Nx.Components.Tests;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxAutoCompleteTests : BunitContext
{
    private sealed record Place(string City, string County);

    private static readonly List<string> Cities = ["Albany", "Bangor", "Barre", "Burlington", "Portland", "Portsmouth", "Salem"];

    private static readonly List<Place> Places = [new("Albany", "Albany County"), new("Bangor", "Penobscot County"), new("Barre", "Washington County")];

    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Nx.Components/nx-combobox.js").Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<NxAutoComplete<string>> RenderBound(List<string?> raised, string? initial = null,
        Action<ComponentParameterCollectionBuilder<NxAutoComplete<string>>>? configure = null)
    {
        IRenderedComponent<NxAutoComplete<string>>? cut = null;
        cut = Render<NxAutoComplete<string>>(p =>
        {
            p.Add(x => x.Id, "ac");
            p.Add(x => x.Data, Cities);
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

    private static Task TypeAsync<T>(IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.Find("input").InputAsync(new ChangeEventArgs { Value = text });

    private static Task KeyAsync<T>(IRenderedComponent<T> cut, string key) where T : IComponent =>
        cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key });

    private static List<string> Options<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll(".nx-combobox-item").Select(i => i.TextContent.Trim()).ToList();

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Render_TextFieldWithoutArrow()
    {
        var cut = RenderBound([], "Salem");

        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain("nx-autocomplete").And.Contain("nx-combobox-suggest"));
        Assert.That(cut.FindAll(".nx-combobox-toggle"), Is.Empty);
        var input = cut.Find("input#ac");
        Assert.That(input.GetAttribute("value"), Is.EqualTo("Salem"));
        Assert.That(input.GetAttribute("aria-autocomplete"), Is.EqualTo("list"));
        Assert.That(input.HasAttribute("readonly"), Is.False);
    }

    [Test]
    public void OutsideValue_NotInTheList_IsShownAsIs()
    {
        var cut = RenderBound([], "Lowell");
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Lowell"));
    }

    [Test]
    public void Prefix_RendersBeforeTheText()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Prefix, (RenderFragment)(b => b.AddMarkupContent(0, "<i class=\"search\"></i>"))));
        Assert.That(cut.Find(".nx-combobox-field > .nx-combobox-prefix + .nx-combobox-control"), Is.Not.Null);
        Assert.That(cut.Find(".nx-combobox-prefix i.search"), Is.Not.Null);
    }

    // ── Suggesting ────────────────────────────────────────────────────────────

    [Test]
    public async Task Typing_OpensMatches_NothingHighlighted()
    {
        var cut = RenderBound([]);

        await TypeAsync(cut, "port");

        Assert.That(cut.Find("input").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(Options(cut), Is.EqualTo(new[] { "Portland", "Portsmouth" }));
        Assert.That(cut.FindAll(".nx-combobox-item-active"), Is.Empty);
        Assert.That(cut.Find("input").HasAttribute("aria-activedescendant"), Is.False);
    }

    [Test]
    public async Task MinLength_KeepsTheListShutUntilReached()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.MinLength, 3));

        await TypeAsync(cut, "ba");
        Assert.That(cut.FindAll(".nx-combobox-popup"), Is.Empty);
        await KeyAsync(cut, "ArrowDown");
        Assert.That(cut.FindAll(".nx-combobox-popup"), Is.Empty);

        await TypeAsync(cut, "bar");
        Assert.That(Options(cut), Is.EqualTo(new[] { "Barre" }));

        await TypeAsync(cut, "b");
        Assert.That(cut.FindAll(".nx-combobox-popup"), Is.Empty);
    }

    [Test]
    public async Task NoMatches_ClosesInsteadOfSayingSo()
    {
        var cut = RenderBound([]);

        await TypeAsync(cut, "zzz");

        Assert.That(cut.FindAll(".nx-combobox-popup"), Is.Empty);
        Assert.That(cut.Markup, Does.Not.Contain("No matches"));
    }

    [Test]
    public async Task StartsWithFilter()
    {
        var cut = RenderBound([], configure: p => p.Add(x => x.Filter, NxComboBoxFilter.StartsWith));
        await TypeAsync(cut, "an");
        Assert.That(cut.FindAll(".nx-combobox-popup"), Is.Empty);   // "Albany", "Bangor" contain it, none start with it

        await TypeAsync(cut, "b");
        Assert.That(Options(cut), Is.EqualTo(new[] { "Bangor", "Barre", "Burlington" }));
    }

    // ── Committing ────────────────────────────────────────────────────────────

    [Test]
    public async Task Enter_WithoutArrowing_CommitsTheTypedText()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised);

        await TypeAsync(cut, "Port");
        await KeyAsync(cut, "Enter");

        Assert.That(raised, Is.EqualTo(new[] { "Port" }));
        Assert.That(cut.FindAll(".nx-combobox-popup"), Is.Empty);
    }

    [Test]
    public async Task ArrowDownEnter_PicksASuggestion()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised);

        await TypeAsync(cut, "port");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "Enter");

        Assert.That(raised, Is.EqualTo(new[] { "Portsmouth" }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Portsmouth"));
    }

    [Test]
    public async Task Click_PicksASuggestion()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised);

        await TypeAsync(cut, "sal");
        await cut.Find(".nx-combobox-item").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { "Salem" }));
    }

    [Test]
    public async Task Blur_CommitsText_EvenAfterTheListClosedForNoMatches()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised);

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await TypeAsync(cut, "Lowell");
        await cut.Find("input").BlurAsync(new FocusEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { "Lowell" }));
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Lowell"));
    }

    [Test]
    public async Task Escape_ClosesAndKeepsTheText()
    {
        var cut = RenderBound([], "Salem");

        await TypeAsync(cut, "Port");
        await KeyAsync(cut, "Escape");

        Assert.That(cut.FindAll(".nx-combobox-popup"), Is.Empty);
        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Port"));
    }

    [Test]
    public async Task Clear_EmptiesTheValue()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised, "Salem", p => p.Add(x => x.Clearable, true));

        await cut.Find(".nx-combobox-clear").ClickAsync(new MouseEventArgs());

        Assert.That(raised, Is.EqualTo(new string?[] { null }));
    }

    // ── Objects, templates, provider ──────────────────────────────────────────

    [Test]
    public async Task Objects_ItemTextIsMatchedAndPutInTheField_TemplateShowsMore()
    {
        var raised = new List<string?>();
        var cut = Render<NxAutoComplete<Place>>(p => p
            .Add(x => x.Data, Places)
            .Add(x => x.ItemText, c => c.City)
            .Add(x => x.ValueChanged, (string? v) => raised.Add(v))
            .Add(x => x.ItemTemplate, c => b => b.AddMarkupContent(0, $"<b>{c.City}</b> <i>{c.County}</i>")));

        await TypeAsync(cut, "ba");
        Assert.That(cut.FindAll(".nx-combobox-item i").Select(i => i.TextContent), Is.EqualTo(new[] { "Albany County", "Penobscot County", "Washington County" }));

        await cut.FindAll(".nx-combobox-item")[1].ClickAsync(new MouseEventArgs());
        Assert.That(raised, Is.EqualTo(new[] { "Bangor" }));
    }

    [Test]
    public async Task DataProvider_GetsTheTypedText()
    {
        var filters = new List<string>();
        var cut = Render<NxAutoComplete<string>>(p => p
            .Add(x => x.DebounceDelay, 0)
            .Add(x => x.DataProvider, request =>
            {
                filters.Add(request.Filter);
                return Task.FromResult<IEnumerable<string>>(Cities.Where(c => c.StartsWith(request.Filter, StringComparison.OrdinalIgnoreCase)));
            }));

        await TypeAsync(cut, "sa");

        cut.WaitForState(() => cut.FindAll(".nx-combobox-item").Count == 1);
        Assert.That(filters, Is.EqualTo(new[] { "sa" }));
        Assert.That(Options(cut), Is.EqualTo(new[] { "Salem" }));
    }

    [Test]
    public void ReadOnlyAndDisabled()
    {
        var ro = RenderBound([], "Salem", p => p.Add(x => x.ReadOnly, true));
        Assert.That(ro.Find("input").HasAttribute("readonly"), Is.True);

        var off = RenderBound([], "Salem", p => p.Add(x => x.Disabled, true));
        Assert.That(off.Find("input").HasAttribute("disabled"), Is.True);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private sealed class Site
    {
        public string? City { get; set; }
    }

    [Test]
    public async Task Validation_InvalidWhileTheFieldHasMessages()
    {
        var site = new Site();
        var context = new EditContext(site);
        var store = new ValidationMessageStore(context);
        var cut = Render<NxAutoComplete<string>>(p => p
            .AddCascadingValue(context)
            .Add(x => x.Data, Cities)
            .Add(x => x.Value, site.City)
            .Add(x => x.ValueExpression, () => site.City));

        store.Add(context.Field(nameof(Site.City)), "Required");
        await cut.InvokeAsync(context.NotifyValidationStateChanged);

        Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));
        Assert.That(cut.Find(".nx-combobox").ClassList, Does.Contain("nx-combobox-invalid"));
    }

    [Test]
    public async Task Validation_CommitUpdatesModelAndMarksModified()
    {
        var site = new Site();
        var context = new EditContext(site);
        var cut = Render<NxAutoComplete<string>>(p => p
            .AddCascadingValue(context)
            .Add(x => x.Data, Cities)
            .Add(x => x.Value, site.City)
            .Add(x => x.ValueChanged, (string? v) => site.City = v)
            .Add(x => x.ValueExpression, () => site.City));

        await TypeAsync(cut, "Lowell");
        await KeyAsync(cut, "Enter");

        Assert.That(site.City, Is.EqualTo("Lowell"));
        Assert.That(context.IsModified(context.Field(nameof(Site.City))), Is.True);
    }

    // ── Hardening ─────────────────────────────────────────────────────────────

    [Test]
    public async Task CustomFilter_ReplacesFilter()
    {
        var cut = Render<NxAutoComplete<Place>>(p => p
            .Add(x => x.Data, Places)
            .Add(x => x.ItemText, c => c.City)
            .Add(x => x.CustomFilter, (c, text) => c.County.StartsWith(text, StringComparison.OrdinalIgnoreCase)));

        await TypeAsync(cut, "pen");

        Assert.That(Options(cut), Is.EqualTo(new[] { "Bangor" }));
    }

    [Test]
    public async Task Virtualize_RendersAWindow()
    {
        var cut = Render<NxAutoComplete<string>>(p => p
            .Add(x => x.Data, Enumerable.Range(1, 10_000).Select(i => $"Part {i}")));

        await TypeAsync(cut, "part");

        var options = cut.FindAll("[role=option]");
        Assert.That(options.Count, Is.GreaterThan(0).And.LessThan(200));
        Assert.That(options[0].GetAttribute("aria-setsize"), Is.EqualTo("10000"));
    }

    [Test]
    public async Task NarrowViewport_StillSuggestsInAPopup()
    {
        var cut = RenderBound([]);

        await cut.InvokeAsync(() => cut.FindComponent<NxComboBox<string, string>>().Instance.OnViewportChanged(true));
        await TypeAsync(cut, "port");

        Assert.That(cut.Find("input").HasAttribute("readonly"), Is.False);
        Assert.That(cut.FindAll(".nx-combobox-overlay"), Is.Empty);
        Assert.That(Options(cut), Is.EqualTo(new[] { "Portland", "Portsmouth" }));
    }

    [Test]
    public async Task ParentRerender_SameValue_KeepsTypedText()
    {
        var cut = RenderBound([], "Salem");

        await TypeAsync(cut, "Port");
        cut.Render(p => p.Add(x => x.Value, "Salem").Add(x => x.Placeholder, "City"));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Port"));
    }

    [Test]
    public void OutsideValueChange_ReplacesText()
    {
        var cut = RenderBound([], "Salem");

        cut.Render(p => p.Add(x => x.Value, "Albany"));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Albany"));
    }

    [Test]
    public async Task Blur_ExactTextInOtherCase_TakesTheItemText()
    {
        var raised = new List<string?>();
        var cut = RenderBound(raised);

        await TypeAsync(cut, "salem");
        await cut.Find("input").BlurAsync(new FocusEventArgs());

        Assert.That(raised, Is.EqualTo(new[] { "Salem" }));
    }
}
