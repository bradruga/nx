using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class MultiSelectTests : PageTest
{
    private readonly string _baseUrl;

    public MultiSelectTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Field(string id) => Page.Locator($"#{id}");
    private ILocator Popup => Page.Locator(".nx-combobox-popup");
    private ILocator Option(string name) => Popup.GetByRole(AriaRole.Option, new() { Name = name, Exact = true });

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/multiselect/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private async Task OpenAsync(string id)
    {
        await Field(id).ScrollIntoViewIfNeededAsync();
        await Field(id).FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
    }

    [Test]
    public async Task Toggle_ChipsAndKeepOpen()
    {
        await GoToPage("overview");

        await OpenAsync("overview-multi");
        await Option("Baldwin Paving").ClickAsync();

        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Page.Locator("#overview-multi-value")).ToHaveTextAsync("Values: [11, 5, 2]");
        await Expect(Page.Locator(".nx-multiselect:has(#overview-multi) .nx-combobox-chip-text")).ToHaveCountAsync(3);
    }

    [Test]
    public async Task SelectAll_FilteredOnly_ThenClears()
    {
        await GoToPage();

        await Field("all-multi").ClickAsync();
        await Field("all-multi").PressSequentiallyAsync("new");
        await Expect(Popup.Locator("[role=option]")).ToHaveCountAsync(4);
        await Option("Select all").ClickAsync();
        await Expect(Page.Locator("#all-value")).ToHaveTextAsync("Values: [New Hampshire, New Jersey, New York]");
        await Expect(Option("Select all")).ToHaveAttributeAsync("aria-selected", "true");

        await Option("Select all").ClickAsync();
        await Expect(Page.Locator("#all-value")).ToHaveTextAsync("Values: []");
    }

    [Test]
    public async Task Summary_AboveThreshold()
    {
        await GoToPage();

        await OpenAsync("summary-multi");
        await Option("Albany").ClickAsync();
        await Option("Bethlehem").ClickAsync();
        var field = Page.Locator(".nx-multiselect:has(#summary-multi)");
        await Expect(field.Locator(".nx-combobox-chip-text")).ToHaveCountAsync(2);
        await Option("Hartford").ClickAsync();

        await Expect(field.Locator(".nx-combobox-summary")).ToHaveTextAsync("3 selected");
        await Expect(field.Locator(".nx-combobox-chip-text")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".nx-multiselect:has(#summary-always) .nx-combobox-summary")).ToHaveTextAsync("2 shifts");
    }

    [Test]
    public async Task MaxSelections_GreysOutTheRest()
    {
        await GoToPage();

        await OpenAsync("max-multi");
        await Option("Dana Ortiz").ClickAsync();
        await Option("Marcus Bell").ClickAsync();
        await Option("Priya Raman").ClickAsync();
        await Expect(Page.Locator("#max-value")).ToHaveTextAsync("Values: [1, 2, 3]");
        await Expect(Option("Tom Kowalski")).ToHaveAttributeAsync("aria-disabled", "true");

        await Option("Tom Kowalski").ClickAsync(new() { Force = true });
        await Expect(Page.Locator("#max-value")).ToHaveTextAsync("Values: [1, 2, 3]");

        await Option("Marcus Bell").ClickAsync();
        await Expect(Option("Tom Kowalski")).Not.ToHaveAttributeAsync("aria-disabled", "true");
    }

    [Test]
    public async Task PickOnly_TypeAheadTogglesWithoutTyping()
    {
        await GoToPage();

        var field = Field("summary-always");
        await field.ScrollIntoViewIfNeededAsync();
        await Expect(field).ToHaveAttributeAsync("readonly", "");
        await field.FocusAsync();
        await Page.Keyboard.PressAsync("w");

        await Expect(Page.Locator(".nx-multiselect:has(#summary-always) .nx-combobox-summary")).ToHaveTextAsync("3 shifts");
    }

    [Test]
    public async Task Keyboard_EnterTogglesAfterArrow_BackspaceRemovesLast()
    {
        await GoToPage("overview");

        await OpenAsync("overview-multi");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-multi-value")).Not.ToHaveTextAsync("Values: [11, 5]");

        await Page.Keyboard.PressAsync("Escape");
        await Page.Keyboard.PressAsync("Backspace");
        await Page.Keyboard.PressAsync("Backspace");
        await Expect(Page.Locator("#overview-multi-value")).ToHaveTextAsync("Values: [11]");
    }

    [Test]
    public async Task Validation_MinTwo()
    {
        await GoToPage();

        await Page.Locator("#multi-submit").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#multi-submit").ClickAsync();
        await Expect(Page.Locator("#multi-form .validation-message")).ToHaveTextAsync("Pick at least two plants.");

        await OpenAsync("report-plants");
        await Option("Albany").ClickAsync();
        await Option("Hartford").ClickAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Page.Locator("#multi-submit").ClickAsync();
        await Expect(Page.Locator("#multi-result")).ToHaveTextAsync("Report for Albany, Hartford");
    }

    [Test]
    public async Task Sheet_StagesUntilDone_BackDiscards()
    {
        await GoToPage();

        var field = Page.Locator(".nx-multiselect:has(#sheet-multi) .nx-combobox-field");
        await field.ScrollIntoViewIfNeededAsync();
        await field.ClickAsync();
        var overlay = Page.Locator(".nx-combobox-overlay");
        await overlay.GetByRole(AriaRole.Option, new() { Name = "Albany", Exact = true }).ClickAsync();
        await overlay.Locator(".nx-combobox-overlay-back").ClickAsync();
        await Expect(Page.Locator("#sheet-value")).ToHaveTextAsync("Values: []");

        await field.ClickAsync();
        await overlay.GetByRole(AriaRole.Option, new() { Name = "Select all", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#sheet-value")).ToHaveTextAsync("Values: []");
        await overlay.Locator(".nx-combobox-overlay-done").ClickAsync();
        await Expect(Page.Locator("#sheet-value")).ToHaveTextAsync("Values: [Albany, Bethlehem, Hartford, Newburgh, Syracuse, Worcester]");
    }

    [Test]
    public async Task Phone_NoPageScroll_SheetOpens()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/multiselect/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        await page.Locator(".nx-multiselect:has(#sheet-auto) .nx-combobox-field").TapAsync();
        await Expect(page.Locator(".nx-combobox-overlay .nx-combobox-overlay-done")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Phone_DefaultIsThePopup()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/multiselect/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        await page.Locator(".nx-multiselect:has(#overview-multi) .nx-combobox-toggle").TapAsync();
        await Expect(page.Locator(".nx-combobox-popup")).ToBeVisibleAsync();
        await Expect(page.Locator(".nx-combobox-overlay")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ItemByValue_ChipsShowNamesBeforeTheListLoads()
    {
        await GoToPage();

        await Expect(Page.Locator(".nx-multiselect:has(#provider-multi) .nx-combobox-chip-text"))
            .ToHaveTextAsync(new[] { "Brookside Utilities", "Lakeshore Excavation" });
        await Expect(Page.Locator("#provider-value")).ToHaveTextAsync("Values: [3, 12]");
    }

    [Test]
    public async Task Columns_TypePageDownSpace_Toggles_SummaryAfterThreshold()
    {
        await GoToPage();
        var field = Field("virtual-multi");
        var host = Page.Locator(".nx-multiselect:has(#virtual-multi)");
        await field.ScrollIntoViewIfNeededAsync();
        await field.ClickAsync();
        await field.PressSequentiallyAsync("P-0001");
        await Expect(Popup.Locator("[role=option]")).ToHaveCountAsync(10);

        await Page.Keyboard.PressAsync("PageDown");
        await Expect(Popup.Locator(".nx-combobox-item-active")).ToContainTextAsync("P-00019");
        await Page.Keyboard.PressAsync(" ");
        await Expect(Option("P-00019").Or(Popup.Locator("[role=option]", new() { HasText = "P-00019" }))).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(host.Locator(".nx-combobox-chip-text")).ToHaveTextAsync(new[] { "P-00019" });
        await Expect(field).ToHaveValueAsync("P-0001");

        foreach (var expected in new[] { 2, 3 })
        {
            await Page.Keyboard.PressAsync("ArrowUp");
            await Page.Keyboard.PressAsync(" ");
            await Expect(host.Locator(".nx-combobox-chip-text")).ToHaveCountAsync(expected);
        }
        await Expect(host.Locator(".nx-combobox-summary")).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("ArrowUp");
        await Page.Keyboard.PressAsync(" ");
        await Expect(host.Locator(".nx-combobox-summary")).ToHaveTextAsync("4 selected");
        await Expect(host.Locator(".nx-combobox-chip-text")).ToHaveCountAsync(0);
        await Expect(Popup).ToBeVisibleAsync();
    }

    [Test]
    public async Task Columns_PopupWidthNarrowerThanTheField_FillsTheField()
    {
        await GoToPage();
        await OpenAsync("virtual-multi");
        await Expect(Popup.Locator(".nx-combobox-item-grid").First).ToBeVisibleAsync();

        var widths = await Popup.EvaluateAsync<double[]>(@"p => [
            p.querySelector('.nx-combobox-popup-body').getBoundingClientRect().width,
            p.getBoundingClientRect().width,
            document.querySelector('.nx-multiselect:has(#virtual-multi)').getBoundingClientRect().width]");

        // PopupWidth="420px" is narrower than the 560px field here; the popup is never narrower than its field.
        Assert.That(widths[2], Is.GreaterThan(420));
        Assert.That(widths[0], Is.EqualTo(widths[1]).Within(2.5), "the body fills the popup");
        Assert.That(widths[1], Is.EqualTo(widths[2]).Within(2.5), "the popup matches the field");
    }

    [Test]
    public async Task Chips_MoreThanMaxVisible_ShowsPlusNMore()
    {
        await GoToPage();
        var host = Page.Locator(".nx-multiselect:has(#group-multi)");
        await Expect(host.Locator(".nx-combobox-chip-text")).ToHaveCountAsync(2);
        await Expect(host.Locator(".nx-combobox-chip-more")).ToHaveCountAsync(0);

        await OpenAsync("group-multi");
        // The ItemTemplate adds each person's title to the option's name.
        ILocator Person(string name) => Popup.Locator("[role=option]", new() { HasText = name });
        await Person("Dana Ortiz").ClickAsync();
        await Expect(host.Locator(".nx-combobox-chip-text")).ToHaveCountAsync(3);
        await Expect(host.Locator(".nx-combobox-chip-more")).ToHaveCountAsync(0);

        await Person("Grace Liu").ClickAsync();
        await Person("Tom Kowalski").ClickAsync();
        await Expect(host.Locator(".nx-combobox-chip-more")).ToHaveTextAsync("+2 more");
        await Expect(host.Locator(".nx-combobox-chip-text")).ToHaveCountAsync(3);
    }

    [Test]
    public async Task Chips_Phone_NoHorizontalOverflow()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToPage();
        var host = Page.Locator(".nx-multiselect:has(#group-multi)");
        await host.ScrollIntoViewIfNeededAsync();
        // Select everyone so the chips are as wide as they get.
        await Field("group-multi").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        foreach (var name in new[] { "Dana Ortiz", "Priya Raman", "Hannah O'Neill", "Victor Moreau" })
            await Popup.Locator("[role=option]", new() { HasText = name }).ClickAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(host.Locator(".nx-combobox-chip-more")).ToHaveTextAsync("+3 more");

        var overflow = await Page.EvaluateAsync<double[]>(@"() => {
            const host = document.querySelector('.nx-multiselect:has(#group-multi)');
            const r = host.getBoundingClientRect();
            const chips = [...host.querySelectorAll('.nx-combobox-chip')].map(c => c.getBoundingClientRect().right);
            return [document.documentElement.scrollWidth - document.documentElement.clientWidth, Math.max(...chips) - r.right, r.right - window.innerWidth];
        }");
        Assert.That(overflow[0], Is.LessThanOrEqualTo(0), "page scrolls sideways");
        Assert.That(overflow[1], Is.LessThanOrEqualTo(0.5), "a chip spills out of the field");
        Assert.That(overflow[2], Is.LessThanOrEqualTo(0), "the field runs off screen");
    }
}
