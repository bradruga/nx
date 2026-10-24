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
        await page.GotoAsync(_baseUrl + "/multiselect/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        await page.Locator(".nx-multiselect:has(#overview-multi) .nx-combobox-field").TapAsync();
        await Expect(page.Locator(".nx-combobox-overlay .nx-combobox-overlay-done")).ToBeVisibleAsync();
    }
}
