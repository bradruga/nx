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
        await Page.Keyboard.PressAsync("Alt+ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        foreach (var key in new[] { "ArrowRight", "ArrowRight", "ArrowDown", "PageDown", "ArrowLeft" })
            await Page.Keyboard.PressAsync(key);
        await Expect(Input("overview-date")).ToHaveAttributeAsync("aria-activedescendant", "overview-date-calendar-d20261130");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#overview-date-value")).ToHaveTextAsync("Value: 2026-11-30");
        await Expect(Input("overview-date")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Keyboard_UpDown_StepThePartUnderTheCaret_CalendarStaysClosed()
    {
        await GoToPage("overview");
        var input = Input("overview-date");

        await input.FocusAsync();
        await Expect(input).ToHaveValueAsync("10/23/2026");
        // Programmatic focus leaves the caret wherever the browser puts it; put it in the day.
        await input.EvaluateAsync("e => e.setSelectionRange(4, 4)");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Page.Locator("#overview-date-value")).ToHaveTextAsync("Value: 2026-10-24");
        Assert.That(await input.EvaluateAsync<int[]>("e => [e.selectionStart, e.selectionEnd]"), Is.EqualTo(new[] { 3, 5 }), "the day stays selected");

        await input.EvaluateAsync("e => e.setSelectionRange(0, 0)");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator("#overview-date-value")).ToHaveTextAsync("Value: 2026-09-24");
        await Expect(Popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Keyboard_CtrlUpZoomsToYear_EscapeCloses()
    {
        await GoToPage("overview");

        await Input("overview-date").FocusAsync();
        await Page.Keyboard.PressAsync("Alt+ArrowDown");
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
    public async Task Typing_NothingInserted_ReadOnCommit()
    {
        await GoToPage();

        var us = Input("mask-en-US");
        await us.ScrollIntoViewIfNeededAsync();
        await us.ClickAsync();
        await us.FillAsync("");
        await us.PressSequentiallyAsync("12252026");
        await Expect(us).ToHaveValueAsync("12252026");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#mask-value")).ToHaveTextAsync("Value: 2026-12-25");
        await Expect(us).ToHaveValueAsync("12/25/2026");

        await us.ClickAsync();
        await us.FillAsync("");
        await us.PressSequentiallyAsync("1/5/2026");
        await Expect(us).ToHaveValueAsync("1/5/2026");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#mask-value")).ToHaveTextAsync("Value: 2026-01-05");
        await Expect(Input("mask-de-DE")).ToHaveValueAsync("05.01.2026");
        await Expect(Input("mask-en-GB")).ToHaveValueAsync("05/01/2026");
    }

    [Test]
    public async Task Typing_MonthAndDay_GetThisYear()
    {
        await GoToPage();

        var us = Input("mask-en-US");
        await us.ScrollIntoViewIfNeededAsync();
        await us.ClickAsync();
        await us.FillAsync("");
        await us.PressSequentiallyAsync("12/5");
        await Expect(us).ToHaveValueAsync("12/5");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#mask-value")).ToHaveTextAsync($"Value: {DateTime.Today.Year}-12-05");
        await Expect(us).ToHaveValueAsync($"12/05/{DateTime.Today.Year}");
    }

    [Test]
    public async Task FastTyping_LosesNothing()
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
    public async Task InlineCalendar_KeysMoveTheDay_PageDoesNotScroll()
    {
        await GoToPage();

        var grid = Page.Locator("#inline-calendar");
        // Centered, so the page could scroll either way.
        await grid.EvaluateAsync("g => g.scrollIntoView({ block: 'center' })");
        // nx-input.js cancels the keys' scrolling one round trip after the page turns interactive.
        await grid.EvaluateAsync("""
            g => new Promise(done => { const probe = () => { const e = new KeyboardEvent('keydown', { key: 'Home', cancelable: true });
                 g.dispatchEvent(e); e.defaultPrevented ? done() : setTimeout(probe, 50); }; probe(); })
            """);
        await grid.FocusAsync();
        var scroll = await Page.EvaluateAsync<double>("() => window.scrollY");
        Assert.That(scroll, Is.GreaterThan(0));

        // From Fri Oct 23 2026: a week on, a month on, back a week, then the end of that week.
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(grid).ToHaveAttributeAsync("aria-activedescendant", new System.Text.RegularExpressions.Regex("-d20261030$"));
        await Page.Keyboard.PressAsync("PageDown");
        await Expect(grid).ToHaveAttributeAsync("aria-activedescendant", new System.Text.RegularExpressions.Regex("-d20261130$"));
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(grid).ToHaveAttributeAsync("aria-activedescendant", new System.Text.RegularExpressions.Regex("-d20261123$"));
        await Page.Keyboard.PressAsync("End");
        await Expect(grid).ToHaveAttributeAsync("aria-activedescendant", new System.Text.RegularExpressions.Regex("-d20261128$"));
        await Page.Keyboard.PressAsync("Space");

        await Expect(Page.Locator("#inline-value")).ToHaveTextAsync("Value: 2026-11-28");
        await Expect(grid).ToBeFocusedAsync();
        Assert.That(await Page.EvaluateAsync<double>("() => window.scrollY"), Is.EqualTo(scroll), "arrows, PageDown, End and Space do not scroll the page");
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
        var area = await FixedArea.SizeAsync(Page);
        Assert.That(box.Width, Is.EqualTo(area.Width).Within(1));
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
        // Focus moves into the sheet a moment after it shows; keys pressed before that go to the page.
        await Expect(Sheet.Locator(".nx-calendar-grids")).ToBeFocusedAsync();
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

    // ── Edit text on focus ────────────────────────────────────────────────────

    [Test]
    public async Task Focus_TypedRightAway_OnASlowConnection_NothingLostOrOverwritten()
    {
        await GoToPage();
        var input = Input("format-long");
        await input.ScrollIntoViewIfNeededAsync();
        await Expect(input).ToHaveValueAsync("Friday, October 23, 2026");
        var cdp = await Page.Context.NewCDPSessionAsync(Page);
        await cdp.SendAsync("Network.enable");
        await cdp.SendAsync("Network.emulateNetworkConditions", new() { ["offline"] = false, ["latency"] = 250, ["downloadThroughput"] = -1, ["uploadThroughput"] = -1 });

        // The edit text is there, selected, before any round trip: what is typed next replaces it.
        await input.FocusAsync();
        Assert.That(await input.InputValueAsync(), Is.EqualTo("10/23/2026"));
        Assert.That(await input.EvaluateAsync<bool>("el => el.selectionStart === 0 && el.selectionEnd === el.value.length"), Is.True);
        await Page.Keyboard.TypeAsync("12/5/2026");
        Assert.That(await input.InputValueAsync(), Is.EqualTo("12/5/2026"));

        // The focus round trip lands after the typing. It used to write the edit text of the old date over it.
        await Page.WaitForTimeoutAsync(1500);
        Assert.That(await input.InputValueAsync(), Is.EqualTo("12/5/2026"));

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Input("format-dmy")).ToHaveValueAsync("05-12-2026");
        await Expect(input).ToHaveValueAsync("12/05/2026");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(input).ToHaveValueAsync("Saturday, December 5, 2026");
    }

    [Test]
    public async Task Focus_ByClick_SwapsAtOnce_PutsTheCaret_TypingAddsToTheText()
    {
        await GoToPage();
        var input = Input("format-long");
        await input.ScrollIntoViewIfNeededAsync();
        var cdp = await Page.Context.NewCDPSessionAsync(Page);
        await cdp.SendAsync("Network.enable");
        await cdp.SendAsync("Network.emulateNetworkConditions", new() { ["offline"] = false, ["latency"] = 250, ["downloadThroughput"] = -1, ["uploadThroughput"] = -1 });

        await input.ClickAsync();
        Assert.That(await input.InputValueAsync(), Is.EqualTo("10/23/2026"));
        Assert.That(await input.EvaluateAsync<bool>("el => el.selectionStart === el.selectionEnd"), Is.True, "a click puts the caret; it does not select");

        await Page.Keyboard.PressAsync("End");
        await Page.Keyboard.PressAsync("Backspace");
        await Page.Keyboard.TypeAsync("7");
        await Page.WaitForTimeoutAsync(1500);
        Assert.That(await input.InputValueAsync(), Is.EqualTo("10/23/2027"));

        await Page.Keyboard.PressAsync("Tab");
        await Expect(input).ToHaveValueAsync("Saturday, October 23, 2027");
    }

    // ── DateTimeOffset ────────────────────────────────────────────────────────

    [Test]
    public async Task DateTimeOffset_KeepsItsTimeAndOffset()
    {
        await GoToPage();

        await Expect(Input("offset-kept")).ToHaveValueAsync("10/23/2026");
        await Input("offset-kept").FillAsync("1/15/2027");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#offset-kept-value")).ToHaveTextAsync("Value: 2027-01-15 18:00 +09:00");
    }

    [TestCase("America/Chicago", "7/15/2026", "2026-07-15 00:00 -05:00")]
    [TestCase("America/Chicago", "1/15/2026", "2026-01-15 00:00 -06:00")]
    [TestCase("Asia/Kolkata", "7/15/2026", "2026-07-15 00:00 +05:30")]
    public async Task DateTimeOffset_Empty_TakesTheBrowsersOffsetOnThatDate(string timeZone, string typed, string expected)
    {
        await using var context = await Browser.NewContextAsync(new() { TimezoneId = timeZone });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/datepicker/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        await page.Locator("#offset-empty").FillAsync(typed);
        await page.Keyboard.PressAsync("Enter");

        await Expect(page.Locator("#offset-empty-value")).ToHaveTextAsync($"Value: {expected}");
    }

    [Test]
    public async Task DateTimeOffset_Empty_CalendarPick_ThenKeepsThatOffset()
    {
        await using var context = await Browser.NewContextAsync(new() { TimezoneId = "Europe/Berlin" });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/datepicker/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
        var input = page.Locator("#offset-empty");

        await input.FillAsync("7/1/2026");
        await page.Keyboard.PressAsync("Enter");
        await Expect(page.Locator("#offset-empty-value")).ToHaveTextAsync("Value: 2026-07-01 00:00 +02:00");

        await page.Locator(".nx-datepicker:has(#offset-empty) .nx-datepicker-toggle").ClickAsync();
        await page.Locator(".nx-datepicker-popup td[data-date='2026-07-20']").ClickAsync();
        await Expect(page.Locator("#offset-empty-value")).ToHaveTextAsync("Value: 2026-07-20 00:00 +02:00");
    }

    [Test]
    public async Task Playground_DateTimeOffset()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-type").SelectOptionAsync("DateTimeOffset?");
        // The switch replaces the picker a round trip later; text filled into the old one would be lost.
        await Expect(Page.Locator("#playground-value")).ToHaveAttributeAsync("data-type", "DateTimeOffset?");
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: null");
        await Input("playground-datepicker").FillAsync("11/1/2026");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^Value: 2026-11-01 00:00 [+-]\d\d:\d\d$"));
    }
}
