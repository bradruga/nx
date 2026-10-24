using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DatePickerTests : PageTest
{
    private readonly string _baseUrl;

    public DatePickerTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Input(string id) => Page.Locator($"#{id}");
    private ILocator Toggle(string id) => Page.Locator($".nx-datepicker:has(#{id}) .nx-datepicker-toggle");
    private ILocator Popup => Page.Locator(".nx-datepicker-popup");
    private ILocator Sheet => Page.Locator(".nx-sheet");
    private ILocator Day(string date) => Page.Locator($"td[data-date='{date}']");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/datepicker/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Toggle_OpensCalendar_ClickPicksAndCloses()
    {
        await GoToPage("overview");

        await Toggle("overview-date").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Input("overview-date")).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Input("overview-date")).ToBeFocusedAsync();
        await Popup.Locator("td[data-date='2026-10-14']").ClickAsync();

        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-date-value")).ToHaveTextAsync("Value: 2026-10-14");
        await Expect(Input("overview-date")).ToHaveValueAsync("10/14/2026");
    }

    [Test]
    public async Task Popup_SitsUnderTheField()
    {
        await GoToPage();
        // Room below for the calendar: the field near the top of the viewport, page content under it.
        var field = Page.Locator(".nx-datepicker:has(#limits) .nx-field-box");
        await field.EvaluateAsync("el => window.scrollBy(0, el.getBoundingClientRect().top - 150)");
        Assert.That((await field.BoundingBoxAsync())!.Y, Is.EqualTo(150).Within(2), "test setup: the page cannot scroll the field into place");

        await Toggle("limits").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();

        var box = (await field.BoundingBoxAsync())!;
        var popup = (await Popup.BoundingBoxAsync())!;
        Assert.That(popup.Y, Is.EqualTo(box.Y + box.Height + 4).Within(2));
        Assert.That(popup.X, Is.EqualTo(box.X).Within(2));
    }

    [Test]
    public async Task Keyboard_FastArrowsThenEnter_PickWithoutLeavingTheField()
    {
        await GoToPage("overview");

        await Input("overview-date").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        foreach (var key in new[] { "ArrowRight", "ArrowRight", "ArrowDown", "PageDown", "ArrowLeft" })
            await Page.Keyboard.PressAsync(key);
        await Expect(Input("overview-date")).ToHaveAttributeAsync("aria-activedescendant", "overview-date-calendar-d20261130");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#overview-date-value")).ToHaveTextAsync("Value: 2026-11-30");
        await Expect(Input("overview-date")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Keyboard_CtrlUpZoomsToYear_EscapeCloses()
    {
        await GoToPage("overview");

        await Input("overview-date").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Control+ArrowUp");
        await Expect(Popup.Locator(".nx-calendar-title")).ToHaveTextAsync("2026");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Popup.Locator(".nx-calendar-title")).ToHaveTextAsync("November 2026");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-date-value")).ToHaveTextAsync("Value: 2026-10-23");
    }

    [Test]
    public async Task Mask_InsertsSeparatorsAndPads()
    {
        await GoToPage();

        var us = Input("mask-en-US");
        await us.ScrollIntoViewIfNeededAsync();
        await us.ClickAsync();
        await us.FillAsync("");
        await us.PressSequentiallyAsync("12252026");
        await Expect(us).ToHaveValueAsync("12/25/2026");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#mask-value")).ToHaveTextAsync("Value: 2026-12-25");

        await us.ClickAsync();
        await us.FillAsync("");
        await us.PressSequentiallyAsync("1/5/2026");
        await Expect(us).ToHaveValueAsync("01/05/2026");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#mask-value")).ToHaveTextAsync("Value: 2026-01-05");
        await Expect(Input("mask-de-DE")).ToHaveValueAsync("05.01.2026");
        await Expect(Input("mask-en-GB")).ToHaveValueAsync("05/01/2026");
    }

    [Test]
    public async Task Mask_FastTyping_LosesNothing()
    {
        await GoToPage();

        var de = Input("mask-de-DE");
        await de.ScrollIntoViewIfNeededAsync();
        await de.ClickAsync();
        await de.FillAsync("");
        await de.PressSequentiallyAsync("24122026", new() { Delay = 0 });
        await Page.Keyboard.PressAsync("Enter");

        await Expect(de).ToHaveValueAsync("24.12.2026");
        await Expect(Page.Locator("#mask-value")).ToHaveTextAsync("Value: 2026-12-24");
    }

    [Test]
    public async Task Format_ShowsWhileNotFocused_EditsInShortDate()
    {
        await GoToPage();

        var input = Input("format-long");
        await Expect(input).ToHaveValueAsync("Friday, October 23, 2026");
        await input.FocusAsync();
        await Expect(input).ToHaveValueAsync("10/23/2026");
    }

    [Test]
    public async Task Limits_DisabledDaysNotPickable()
    {
        await GoToPage();

        await Toggle("limits").ScrollIntoViewIfNeededAsync();
        await Toggle("limits").ClickAsync();
        await Expect(Popup.Locator("td[data-date='2026-10-24']")).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(Popup.Locator("td[data-date='2026-10-04']")).ToHaveAttributeAsync("aria-disabled", "true");
        await Popup.Locator("td[data-date='2026-10-24']").ClickAsync(new() { Force = true });
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Page.Locator("#limits-value")).ToHaveTextAsync("Value: 2026-10-23");

        await Popup.Locator("td[data-date='2026-10-26']").ClickAsync();
        await Expect(Page.Locator("#limits-value")).ToHaveTextAsync("Value: 2026-10-26");
    }

    [Test]
    public async Task WeekNumbers_Shown()
    {
        await GoToPage();

        await Toggle("weeks-de").ScrollIntoViewIfNeededAsync();
        await Toggle("weeks-de").ClickAsync();

        await Expect(Popup.Locator("th.nx-calendar-week").First).ToHaveTextAsync("1");
        await Expect(Popup.Locator("th.nx-calendar-weekday").Nth(1)).ToHaveAttributeAsync("abbr", "Montag");
    }

    [Test]
    public async Task InlineCalendar_TakesFocusAndKeys()
    {
        await GoToPage();

        var grid = Page.Locator("#inline-calendar");
        await grid.ScrollIntoViewIfNeededAsync();
        await grid.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#inline-value")).ToHaveTextAsync("Value: 2026-10-24");
        await Expect(grid).ToBeFocusedAsync();
    }

    [Test]
    public async Task Clearable_Clears()
    {
        await GoToPage("overview");

        await Page.Locator(".nx-datepicker:has(#overview-date) .nx-datepicker-clear").ClickAsync();

        await Expect(Page.Locator("#overview-date-value")).ToHaveTextAsync("Value: null");
        await Expect(Input("overview-date")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Validation_RequiredAndCrossField()
    {
        await GoToPage();

        await Page.Locator("#order-submit").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#order-submit").ClickAsync();
        await Expect(Page.Locator("#date-form .validation-message").First).ToHaveTextAsync("Enter the order date.");
        await Expect(Input("order-date")).ToHaveAttributeAsync("aria-invalid", "true");

        await Input("order-date").FillAsync("10/23/2026");
        await Input("order-needed").FillAsync("10/20/2026");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator("#order-submit").ClickAsync();
        await Expect(Page.Locator("#date-form .validation-message")).ToHaveTextAsync("Needed by cannot be before the order date.");

        await Input("order-needed").FillAsync("10/30/2026");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator("#order-submit").ClickAsync();
        await Expect(Page.Locator("#order-result")).ToHaveTextAsync("Placed 2026-10-23, needed by 2026-10-30");
    }

    [Test]
    public async Task Sheet_Always_OpensFullScreen_PickCloses()
    {
        await GoToPage();

        await Toggle("sheet-always").ScrollIntoViewIfNeededAsync();
        await Toggle("sheet-always").ClickAsync();

        await Expect(Sheet).ToBeVisibleAsync();
        var box = (await Sheet.BoundingBoxAsync())!;
        var clientWidth = await Page.EvaluateAsync<int>("() => document.documentElement.clientWidth");
        Assert.That(box.Width, Is.EqualTo(clientWidth).Within(1));
        await Expect(Sheet.Locator(".nx-calendar-grids")).ToBeFocusedAsync();
        Assert.That(await Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('nx-scroll-locked')"), Is.True);

        await Sheet.Locator("td[data-date]:not(.nx-calendar-other)").Nth(9).ClickAsync();

        await Expect(Sheet).ToHaveCountAsync(0);
        await Expect(Page.Locator("#sheet-value")).Not.ToHaveTextAsync("Value: null");
        Assert.That(await Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('nx-scroll-locked')"), Is.False);
        await Expect(Toggle("sheet-always")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Sheet_EscapeClosesWithoutChange()
    {
        await GoToPage();

        await Toggle("sheet-always").ScrollIntoViewIfNeededAsync();
        await Toggle("sheet-always").ClickAsync();
        await Expect(Sheet).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Sheet).ToHaveCountAsync(0);
        await Expect(Page.Locator("#sheet-value")).ToHaveTextAsync("Value: null");
    }

    [Test]
    public async Task Phone_AutoModeOpensSheet_WithTouchSizedDays()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/datepicker/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
        await Expect(page.Locator("#overview-date")).ToHaveAttributeAsync("aria-haspopup", "dialog");

        await page.Locator(".nx-datepicker:has(#overview-date) .nx-datepicker-toggle").TapAsync();

        var sheet = page.Locator(".nx-sheet");
        await Expect(sheet).ToBeVisibleAsync();
        var day = (await sheet.Locator("td[data-date='2026-10-14']").BoundingBoxAsync())!;
        Assert.That(day.Width, Is.GreaterThanOrEqualTo(40));
        Assert.That(day.Height, Is.GreaterThanOrEqualTo(40));
        await sheet.Locator("td[data-date='2026-10-14']").TapAsync();
        await Expect(page.Locator("#overview-date-value")).ToHaveTextAsync("Value: 2026-10-14");
        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
    }

    [Test]
    public async Task Toggle_KeepsFocusInTheInput()
    {
        await GoToPage("overview");

        await Toggle("overview-date").ClickAsync();
        await Expect(Toggle("overview-date")).Not.ToBeFocusedAsync();
        await Expect(Input("overview-date")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Playground_TypeAndCultureSwitch()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-culture").SelectOptionAsync("de-DE");
        await Expect(Input("playground-datepicker")).ToHaveValueAsync("23.10.2026");

        await Page.Locator("#pg-type").SelectOptionAsync("DateTime");
        // The TValue switch remounts the picker: wait for the new one before typing into it.
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: 2026-10-23 09:00");
        await Expect(Input("playground-datepicker")).ToHaveValueAsync("23.10.2026");
        await Input("playground-datepicker").FillAsync("01.11.2026");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: 2026-11-01 09:00");
    }
}
