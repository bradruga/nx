using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DropDownTests : PageTest
{
    private readonly string _baseUrl;

    public DropDownTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Field(string id) => Page.Locator($"#{id}");
    private ILocator Popup => Page.Locator(".nx-combobox-popup");
    private ILocator Active => Popup.Locator(".nx-combobox-item-active");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/dropdown/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Click_OpensAtSelection_ClickPicks()
    {
        await GoToPage("overview");

        await Field("overview-customer").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Active).ToHaveTextAsync("Kiewit Northeast");
        await Popup.GetByRole(AriaRole.Option, new() { Name = "Cascade Civil", Exact = true }).ClickAsync();

        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-customer-value")).ToHaveTextAsync("Value: 5");
        await Expect(Field("overview-customer")).ToHaveValueAsync("Cascade Civil");
    }

    [Test]
    public async Task NoTyping_FieldIsReadOnly_NoCaret()
    {
        await GoToPage("overview");

        await Expect(Field("overview-customer")).ToHaveAttributeAsync("readonly", "");
        await Field("overview-customer").FocusAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Page.Keyboard.TypeAsync("zz");
        await Expect(Field("overview-customer")).ToHaveValueAsync("Kiewit Northeast");
    }

    [Test]
    public async Task TypeAhead_Closed_CyclesSameLetter_Fast()
    {
        await GoToPage();

        await Field("typeahead-dropdown").FocusAsync();
        await Page.Keyboard.PressAsync("n");
        await Page.Keyboard.PressAsync("n");
        await Page.Keyboard.PressAsync("n");

        await Expect(Page.Locator("#typeahead-value")).ToHaveTextAsync("Value: New York");
        await Expect(Popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task TypeAhead_Open_PrefixThenSpacePicks()
    {
        await GoToPage();

        await Field("typeahead-dropdown").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Popup).ToBeVisibleAsync();
        await Page.Keyboard.TypeAsync("mas");
        await Expect(Active).ToHaveTextAsync("Massachusetts");
        await Page.Keyboard.PressAsync(" ");

        await Expect(Page.Locator("#typeahead-value")).ToHaveTextAsync("Value: Massachusetts");
        await Expect(Popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Keyboard_HomeEndEscape()
    {
        await GoToPage();

        await Field("typeahead-dropdown").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("End");
        await Expect(Active).ToHaveTextAsync("Virginia");
        await Page.Keyboard.PressAsync("Home");
        await Expect(Active).ToHaveTextAsync("Connecticut");
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#typeahead-value")).ToHaveTextAsync("Value: null");
    }

    [Test]
    public async Task DefaultItem_ClearsTheValue()
    {
        await GoToPage();

        await Expect(Field("default-dropdown")).ToHaveAttributeAsync("placeholder", "All departments");
        await Field("default-dropdown").ClickAsync();
        await Popup.GetByRole(AriaRole.Option, new() { Name = "Quality", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#default-value")).ToHaveTextAsync("Value: Quality");

        await Field("default-dropdown").ClickAsync();
        await Popup.GetByRole(AriaRole.Option, new() { Name = "All departments", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#default-value")).ToHaveTextAsync("Value: null");
    }

    [Test]
    public async Task Grouping_TemplateAndSelectedTemplate()
    {
        await GoToPage();

        await Field("group-dropdown").ClickAsync();
        await Expect(Popup.Locator(".nx-combobox-group")).ToHaveCountAsync(5);
        await Popup.Locator("[role=option]", new() { HasText = "Grace Liu" }).ClickAsync();

        await Expect(Page.Locator("#group-value")).ToHaveTextAsync("Value: 6");
        await Expect(Page.Locator(".nx-dropdown:has(#group-dropdown) .nx-combobox-selected")).ToContainTextAsync("Grace Liu");
    }

    [Test]
    public async Task Virtualized_TypeAheadAndScroll()
    {
        await GoToPage();

        await Field("virtual-dropdown").ScrollIntoViewIfNeededAsync();
        await Field("virtual-dropdown").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Popup).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("End");
        await Expect(Active).ToContainTextAsync("P-50000");
    }

    [Test]
    public async Task DataProvider_LoadsWhenOpened()
    {
        await GoToPage();

        await Field("provider-dropdown").ScrollIntoViewIfNeededAsync();
        await Field("provider-dropdown").ClickAsync();
        await Expect(Popup.GetByRole(AriaRole.Option, new() { Name = "Baldwin Paving", Exact = true })).ToBeVisibleAsync();
        await Popup.GetByRole(AriaRole.Option, new() { Name = "Baldwin Paving", Exact = true }).ClickAsync();

        await Expect(Page.Locator("#provider-value")).ToHaveTextAsync("Value: 2");
    }

    [Test]
    public async Task Validation_RequiredThenBooked()
    {
        await GoToPage();

        await Page.Locator("#dropdown-submit").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#dropdown-submit").ClickAsync();
        await Expect(Page.Locator("#dropdown-form .validation-message")).ToHaveTextAsync("Pick a carrier.");

        await Field("ticket-carrier").FocusAsync();
        await Page.Keyboard.PressAsync("r");
        await Page.Locator("#dropdown-submit").ClickAsync();
        await Expect(Page.Locator("#dropdown-result")).ToHaveTextAsync("Booked with Rail");
    }

    [Test]
    public async Task Sheet_NoSearchBox_ListTakesKeys()
    {
        await GoToPage();

        await Field("sheet-dropdown").ScrollIntoViewIfNeededAsync();
        await Field("sheet-dropdown").ClickAsync();
        var overlay = Page.Locator(".nx-combobox-overlay");
        await Expect(overlay).ToBeVisibleAsync();
        await Expect(overlay.Locator(".nx-combobox-overlay-search")).ToHaveCountAsync(0);
        await Expect(overlay.Locator(".nx-combobox-overlay-list")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(overlay).ToHaveCountAsync(0);
        await Expect(Page.Locator("#sheet-value")).ToHaveTextAsync("Value: Bethlehem");
    }

    [Test]
    public async Task Phone_AutoSheet_TapPicks()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/dropdown/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        await page.Locator(".nx-dropdown:has(#overview-ship) .nx-combobox-field").TapAsync();
        var overlay = page.Locator(".nx-combobox-overlay");
        await Expect(overlay).ToBeVisibleAsync();
        var row = overlay.GetByRole(AriaRole.Option, new() { Name = "Flatbed", Exact = true });
        Assert.That((await row.BoundingBoxAsync())!.Height, Is.GreaterThanOrEqualTo(44));
        await row.TapAsync();

        await Expect(page.Locator("#overview-ship-value")).ToHaveTextAsync("Value: Flatbed");
    }
}
