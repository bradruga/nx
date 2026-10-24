using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class TimePickerTests : PageTest
{
    private readonly string _baseUrl;

    public TimePickerTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Input(string id) => Page.Locator($"#{id}");
    private ILocator Toggle(string id) => Page.Locator($".nx-timepicker:has(#{id}) .nx-timepicker-toggle");
    private ILocator Popup => Page.Locator(".nx-timepicker-popup");
    private ILocator Sheet => Page.Locator(".nx-sheet");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/timepicker/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Typed_LooseForm_FormatsInEachClock()
    {
        await GoToPage();

        await Input("time-12").FillAsync("930p");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Input("time-12")).ToHaveValueAsync("9:30 PM");
        await Expect(Page.Locator("#time-12-value")).ToHaveTextAsync("Value: 21:30");
        await Expect(Input("time-24")).ToHaveValueAsync("21:30");
    }

    [Test]
    public async Task Mask_24Hour()
    {
        await GoToPage();

        var input = Input("time-24");
        await input.ClickAsync();
        await input.FillAsync("");
        await input.PressSequentiallyAsync("0745");
        await Expect(input).ToHaveValueAsync("07:45");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#time-12-value")).ToHaveTextAsync("Value: 07:45");
    }

    [Test]
    public async Task List_StepAndLimits_ArrowsEnterPick()
    {
        await GoToPage();

        var input = Input("time-step");
        await input.ScrollIntoViewIfNeededAsync();
        await input.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Popup.Locator("[role=option]").First).ToHaveTextAsync("6:00 AM");
        await Expect(Popup.Locator("[role=option]").Last).ToHaveTextAsync("6:00 PM");
        await Expect(Popup.Locator("[role=option]")).ToHaveCountAsync(49);
        await Expect(Popup.Locator(".nx-timepicker-item-active")).ToHaveTextAsync("7:00 AM");

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#time-step-value")).ToHaveTextAsync("Value: 07:30");
    }

    [Test]
    public async Task List_ActiveScrollsIntoView()
    {
        await GoToPage();

        await Toggle("time-step").ScrollIntoViewIfNeededAsync();
        await Toggle("time-step").ClickAsync();
        await Page.Keyboard.PressAsync("PageDown");
        await Page.Keyboard.PressAsync("PageDown");
        await Page.Keyboard.PressAsync("PageDown");

        await Expect(Popup.Locator(".nx-timepicker-item-active")).ToBeInViewportAsync();
    }

    [Test]
    public async Task Click_Picks_OutsideClickCloses()
    {
        await GoToPage("overview");

        await Toggle("overview-time").ClickAsync();
        await Popup.GetByRole(AriaRole.Option, new() { Name = "8:45 AM", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#overview-time-value")).ToHaveTextAsync("Value: 08:45");
        await Expect(Popup).ToHaveCountAsync(0);

        await Toggle("overview-time").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        await Page.Locator("h1").ClickAsync();
        await Expect(Popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Types_TimeSpanAndDateTime()
    {
        await GoToPage();

        await Input("type-timespan").FillAsync("10:15 AM");
        await Input("type-datetime").FillAsync("7:45 AM");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator("#type-timespan-value")).ToHaveTextAsync("Value: 10:15");
        await Expect(Page.Locator("#type-datetime-value")).ToHaveTextAsync("Value: 2026-10-23 07:45");
    }

    [Test]
    public async Task Sheet_ScrolledToTime_TapPicks()
    {
        await GoToPage();

        await Toggle("time-sheet").ScrollIntoViewIfNeededAsync();
        await Toggle("time-sheet").ClickAsync();
        await Expect(Sheet).ToBeVisibleAsync();
        Assert.That(await Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('nx-scroll-locked')"), Is.True);
        var item = Sheet.GetByRole(AriaRole.Option, new() { Name = "1:30 PM", Exact = true });
        var box = (await item.BoundingBoxAsync())!;
        Assert.That(box.Height, Is.GreaterThanOrEqualTo(44));
        await item.ClickAsync();

        await Expect(Sheet).ToHaveCountAsync(0);
        await Expect(Page.Locator("#time-sheet-value")).ToHaveTextAsync("Value: 13:30");
    }

    [Test]
    public async Task Validation_CrossField()
    {
        await GoToPage();

        await Input("visit-start").FillAsync("10:00 AM");
        await Input("visit-end").FillAsync("9:00 AM");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator("#time-submit").ClickAsync();
        await Expect(Page.Locator("#time-form .validation-message")).ToHaveTextAsync("Leave after arriving.");

        await Input("visit-end").FillAsync("11:30 AM");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator("#time-submit").ClickAsync();
        await Expect(Page.Locator("#time-result")).ToHaveTextAsync("Booked 10:00–11:30");
    }

    [Test]
    public async Task Phone_AutoModeUsesSheet()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/timepicker/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        await page.Locator(".nx-timepicker:has(#overview-time) .nx-timepicker-toggle").TapAsync();

        await Expect(page.Locator(".nx-sheet")).ToBeVisibleAsync();
        await Expect(page.Locator(".nx-sheet .nx-timepicker-item-selected")).ToBeFocusedAsync();
    }
}
